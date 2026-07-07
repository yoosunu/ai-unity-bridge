import socket
import json
import cv2
from collections import defaultdict, deque, Counter

from ultralytics import YOLO
from config import CONFIG

if CONFIG.tracking.use_deepsort:
    from deep_sort_realtime.deepsort_tracker import DeepSort


model = YOLO(CONFIG.model.model_path)
model.to(CONFIG.model.device)

deepsort_tracker = None
if CONFIG.tracking.use_deepsort:
    deepsort_tracker = DeepSort(
        max_age=CONFIG.tracking.max_age,
        n_init=CONFIG.tracking.n_init,
    )

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.bind((CONFIG.network.host, CONFIG.network.port))
server.listen(1)
print(
    f"[{'DeepSort' if CONFIG.tracking.use_deepsort else 'ByteTrack'}] Waiting for Unity client..."
)
conn, addr = server.accept()
print("Connected:", addr)

cap = cv2.VideoCapture(CONFIG.video.video_path)
frame_width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
cropped_frame_height = CONFIG.video.crop_bottom - CONFIG.video.crop_top

conf_history = defaultdict(lambda: deque(maxlen=CONFIG.tracking.history_len))
label_history = defaultdict(lambda: deque(maxlen=CONFIG.tracking.history_len))

stats = {
    "raw_yolo": 0,
    "no_track_id": 0,  # ByteTrack 전용: id 못 받은 것
    "not_confirmed": 0,  # DeepSort 전용: 아직 확정 안 된 것
    "filtered_conf": 0,
    "filtered_dist": 0,
    "passed": 0,
}


def estimate_z_from_bottom_y(y2, image_height):
    normalized = max(0.0, min(1.0, y2 / image_height))
    z_near = CONFIG.coordinate.z_near
    z_far = CONFIG.coordinate.z_far
    z = z_far - normalized * (z_far - z_near)
    return round(max(z_near, z), 2)


def get_conf_threshold(label):
    return CONFIG.decision.class_conf_threshold.get(
        label, CONFIG.decision.default_conf_threshold
    )


def build_tracked_items_bytetrack(cropped):
    """ByteTrack 경로: (track_id, label, conf, x1, y1, x2, y2) 리스트 반환"""
    results = model.track(
        cropped,
        persist=True,
        tracker="my_bytetrack.yaml",
        imgsz=CONFIG.model.infer_size,
        verbose=False,
    )
    result = results[0]

    items = []
    if result.boxes is not None:
        stats["raw_yolo"] += len(result.boxes)
        has_track_ids = result.boxes.id is not None
        track_ids = result.boxes.id.tolist() if has_track_ids else None

        for i, box in enumerate(result.boxes):
            if not has_track_ids:
                stats["no_track_id"] += 1
                continue

            track_id = int(track_ids[i])
            cls_id = int(box.cls[0])
            label = result.names[cls_id]
            conf = float(box.conf[0])
            x1, y1, x2, y2 = box.xyxy[0].tolist()
            items.append((track_id, label, conf, x1, y1, x2, y2))

    return items


def build_tracked_items_deepsort(cropped):
    """DeepSort 경로: (track_id, label, conf, x1, y1, x2, y2) 리스트 반환"""
    results = model.predict(cropped, imgsz=CONFIG.model.infer_size, verbose=False)
    result = results[0]

    raw_detections = []
    if result.boxes is not None:
        stats["raw_yolo"] += len(result.boxes)
        for box in result.boxes:
            cls_id = int(box.cls[0])
            label = result.names[cls_id]
            conf = float(box.conf[0])
            x1, y1, x2, y2 = box.xyxy[0].tolist()
            raw_detections.append(([x1, y1, x2 - x1, y2 - y1], conf, label))

    tracks = deepsort_tracker.update_tracks(raw_detections, frame=cropped)

    items = []
    for track in tracks:
        if not track.is_confirmed():
            stats["not_confirmed"] += 1
            continue

        track_id_raw = track.track_id
        track_id = (
            int(track_id_raw)
            if str(track_id_raw).isdigit()
            else hash(track_id_raw) % 100000
        )
        label = track.get_det_class()
        conf = float(track.get_det_conf() or 0.0)
        x1, y1, x2, y2 = track.to_ltrb()
        items.append((track_id, label, conf, x1, y1, x2, y2))

    return items


frame_idx = 0

while cap.isOpened():
    ret, frame = cap.read()
    if not ret:
        break

    cropped = frame[CONFIG.video.crop_top : CONFIG.video.crop_bottom, :]

    if CONFIG.tracking.use_deepsort:
        tracked_items = build_tracked_items_deepsort(cropped)
    else:
        tracked_items = build_tracked_items_bytetrack(cropped)

    detections = []

    for track_id, label, conf, x1, y1, x2, y2 in tracked_items:
        conf_history[track_id].append(conf)
        label_history[track_id].append(label)

        threshold = get_conf_threshold(label)
        hits = sum(1 for c in conf_history[track_id] if c >= threshold)

        if hits < CONFIG.decision.min_hit_count:
            stats["filtered_conf"] += 1
            continue

        representative_conf = max(conf_history[track_id])
        stable_label = Counter(label_history[track_id]).most_common(1)[0][0]

        center_x = (x1 + x2) / 2
        box_width = x2 - x1
        box_height = y2 - y1

        unity_x = round(
            (center_x / frame_width - 0.5) * CONFIG.coordinate.unity_x_range, 2
        )
        unity_z = estimate_z_from_bottom_y(y2, cropped_frame_height)

        if unity_z > CONFIG.decision.max_relevant_z:
            stats["filtered_dist"] += 1
            continue

        stats["passed"] += 1
        detections.append(
            {
                "id": track_id,
                "label": stable_label,
                "confidence": round(representative_conf, 3),
                "x": unity_x,
                "z": unity_z,
                "box_width": round(box_width, 1),
                "box_height": round(box_height, 1),
            }
        )

    message = json.dumps({"objects": detections}) + "\n"
    conn.sendall(message.encode("utf-8"))

    frame_idx += 1
    if frame_idx % 100 == 0:
        print(f"[frame {frame_idx}] {stats}")

cap.release()
server.close()
print(f"\n최종 통계: {stats}")

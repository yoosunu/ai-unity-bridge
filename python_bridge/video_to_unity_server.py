import os

os.environ["HF_HUB_OFFLINE"] = "1"

import json
import cv2
import numpy as np
import math
from collections import deque, Counter
from ultralytics import YOLO
from transformers import pipeline
from PIL import Image
from config import CONFIG
import asyncio
import threading
import queue
import websockets

if CONFIG.tracking.use_deepsort:
    from deep_sort_realtime.deepsort_tracker import DeepSort

DEBUG_VISUALIZE = True
DEBUG_VISUALIZE_INTERVAL = 1

device = CONFIG.model.device

stock_model = YOLO("yolo26n.pt")
stock_model.to(device)

custom_model = YOLO(CONFIG.model.model_path)
custom_model.to(device)

depth_pipe = pipeline(
    task="depth-estimation",
    model="depth-anything/Depth-Anything-V2-Metric-Outdoor-Small-hf",
    device=device,
)

STOCK_LABEL_MAP = {
    "person": "Person",
    "bicycle": "Bike",
    "car": "Car",
    "motorcycle": "Bike",
    "bus": "Car",
    "truck": "Car",
    "traffic light": "TrafficLight",
    "stop sign": "Sign",
    "dog": "Animal",
    "bench": "Obstacle",
    "fire hydrant": "Pillar",
}

CUSTOM_MODEL_FRAME_INTERVAL = 5
DEPTH_FRAME_INTERVAL = 5

stock_deepsort = (
    DeepSort(max_age=CONFIG.tracking.max_age, n_init=CONFIG.tracking.n_init)
    if CONFIG.tracking.use_deepsort
    else None
)
custom_deepsort = (
    DeepSort(max_age=CONFIG.tracking.max_age, n_init=CONFIG.tracking.n_init)
    if CONFIG.tracking.use_deepsort
    else None
)

message_queue = queue.Queue()
incoming_queue = queue.Queue()
connected_clients = set()

async def _handler(websocket):
    connected_clients.add(websocket)
    print("Unity client connected via WebSocket")
    try:
        async for message in websocket:
            incoming_queue.put(message)
    finally:
        connected_clients.discard(websocket)

async def _broadcaster():
    while True:
        try:
            msg = message_queue.get_nowait()
        except queue.Empty:
            await asyncio.sleep(0.005)
            continue
        for ws in list(connected_clients):
            try:
                await ws.send(msg)
            except Exception:
                connected_clients.discard(ws)

async def _server_main():
    async with websockets.serve(_handler, "0.0.0.0", CONFIG.network.port):
        await _broadcaster()

def start_websocket_server():
    threading.Thread(target=lambda: asyncio.run(_server_main()), daemon=True).start()

start_websocket_server()
print(f"WebSocket 서버 시작됨 (포트 {CONFIG.network.port}). Unity 접속 대기 중...")

print("Unity에서 Space를 눌러 시작 신호를 보낼 때까지 대기 중...")
while True:
    try:
        msg = incoming_queue.get(timeout=0.5)
        if msg.strip() == "START":
            break
    except queue.Empty:
        continue
print("시작 신호 수신, 영상 처리를 시작합니다.")

cap = cv2.VideoCapture(CONFIG.video.video_path)
cap.set(cv2.CAP_PROP_POS_MSEC, CONFIG.video.start_msec)

frame_width = None
frame_height = None

conf_history = {}
label_history = {}

cached_depth_map = None


def get_history(track_id, is_static):
    if track_id not in conf_history:
        window = (
            CONFIG.decision.static_hit_window
            if is_static
            else CONFIG.decision.dynamic_hit_window
        )
        conf_history[track_id] = deque(maxlen=window)
        label_history[track_id] = deque(maxlen=window)
    return conf_history[track_id], label_history[track_id]


def get_conf_threshold(label):
    return CONFIG.decision.class_conf_threshold.get(
        label, CONFIG.decision.default_conf_threshold
    )


def get_depth_for_bbox(x1, y1, x2, y2, depth_map):
    h, w = depth_map.shape
    xi1 = int(np.clip(x1, 0, w - 1))
    xi2 = int(np.clip(x2, 0, w - 1))
    yi1 = int(np.clip(y1, 0, h - 1))
    yi2 = int(np.clip(y2, 0, h - 1))
    if xi2 <= xi1 or yi2 <= yi1:
        return float(depth_map[yi1, xi1])
    region = depth_map[yi1:yi2, xi1:xi2]
    return float(np.percentile(region, 10))

"""main"""
def calibrate_p10_depth(raw_depth: float) -> float:
    if not CONFIG.depth_calibration.enabled:
        return raw_depth

    if raw_depth < CONFIG.depth_calibration.near_far_boundary:
        corrected = (
            CONFIG.depth_calibration.near_slope * raw_depth
            + CONFIG.depth_calibration.near_intercept
        )
    else:
        corrected = (
            CONFIG.depth_calibration.far_slope * raw_depth
            + CONFIG.depth_calibration.far_intercept
        )

    return max(0.0, corrected)


def pixel_x_to_world_x(pixel_x, image_width, forward_depth, horizontal_fov_deg):
    normalized_x = (pixel_x - image_width / 2) / (image_width / 2)
    half_fov_rad = math.radians(horizontal_fov_deg / 2)
    theta = math.atan(normalized_x * math.tan(half_fov_rad))
    return forward_depth * math.tan(theta)


def compute_depth_map(frame_bgr):
    frame_rgb = cv2.cvtColor(frame_bgr, cv2.COLOR_BGR2RGB)
    pil_image = Image.fromarray(frame_rgb)
    result = depth_pipe(pil_image)
    depth = result["predicted_depth"].squeeze().cpu().numpy()
    if depth.shape != frame_bgr.shape[:2]:
        depth = cv2.resize(depth, (frame_bgr.shape[1], frame_bgr.shape[0]))
    return depth


def extract_items_bytetrack(result, label_map=None, offset=0):
    items = []
    if result.boxes is not None and result.boxes.id is not None:
        track_ids = result.boxes.id.tolist()
        for i, box in enumerate(result.boxes):
            cls_id = int(box.cls[0])
            raw_label = result.names[cls_id]
            label = label_map.get(raw_label) if label_map else raw_label
            if label is None:
                continue
            conf = float(box.conf[0])
            x1, y1, x2, y2 = box.xyxy[0].tolist()
            items.append((int(track_ids[i]) + offset, label, conf, x1, y1, x2, y2))
    return items


def extract_items_deepsort(frame, model, deepsort_tracker, label_map=None, offset=0):
    results = model.predict(frame, imgsz=CONFIG.model.infer_size, verbose=False)
    result = results[0]

    raw_detections = []
    if result.boxes is not None:
        for box in result.boxes:
            cls_id = int(box.cls[0])
            raw_label = result.names[cls_id]
            label = label_map.get(raw_label) if label_map else raw_label
            if label is None:
                continue
            conf = float(box.conf[0])
            x1, y1, x2, y2 = box.xyxy[0].tolist()
            raw_detections.append(([x1, y1, x2 - x1, y2 - y1], conf, label))

    tracks = deepsort_tracker.update_tracks(raw_detections, frame=frame)

    items = []
    for track in tracks:
        if not track.is_confirmed():
            continue
        track_id_raw = track.track_id
        track_id = (
            int(track_id_raw)
            if str(track_id_raw).isdigit()
            else hash(track_id_raw) % 100000
        ) + offset
        label = track.get_det_class()
        conf = float(track.get_det_conf() or 0.0)
        x1, y1, x2, y2 = track.to_ltrb()
        items.append((track_id, label, conf, x1, y1, x2, y2))
    return items


FRAME_SKIP = 1

frame_idx = 0

while cap.isOpened():
    ret, frame = cap.read()
    if not ret:
        break

    frame_idx += 1
    if frame_idx % FRAME_SKIP != 0:
        continue  # 추론 자체를 안 함 - 이게 진짜 속도를 줄여줌

    if CONFIG.video.rotate:
        frame = cv2.rotate(frame, cv2.ROTATE_90_CLOCKWISE)

    cropped = frame[CONFIG.video.crop_top : CONFIG.video.crop_bottom, :]

    if frame_width is None:
        frame_height, frame_width = cropped.shape[:2]

    if frame_idx % DEPTH_FRAME_INTERVAL == 0 or cached_depth_map is None:
        cached_depth_map = compute_depth_map(cropped)

    debug_frame = cropped.copy() if DEBUG_VISUALIZE else None

    tracked_items = []

    if CONFIG.tracking.use_deepsort:
        stock_items = extract_items_deepsort(
            cropped, stock_model, stock_deepsort, label_map=STOCK_LABEL_MAP
        )
    else:
        stock_results = stock_model.track(
            cropped,
            persist=True,
            tracker="python_bridge/my_bytetrack.yaml",
            imgsz=CONFIG.model.infer_size,
            verbose=False,
        )
        stock_items = extract_items_bytetrack(
            stock_results[0], label_map=STOCK_LABEL_MAP
        )
    tracked_items += [
        (tid, lbl, c, x1, y1, x2, y2, "stock")
        for tid, lbl, c, x1, y1, x2, y2 in stock_items
    ]

    if frame_idx % CUSTOM_MODEL_FRAME_INTERVAL == 0:
        if CONFIG.tracking.use_deepsort:
            custom_items = extract_items_deepsort(
                cropped, custom_model, custom_deepsort, offset=100000
            )
        else:
            custom_results = custom_model.track(
                cropped,
                persist=True,
                tracker="python_bridge/my_bytetrack.yaml",
                imgsz=CONFIG.model.infer_size,
                verbose=False,
            )
            custom_items = extract_items_bytetrack(custom_results[0], offset=100000)
        tracked_items += [
            (tid, lbl, c, x1, y1, x2, y2, "custom")
            for tid, lbl, c, x1, y1, x2, y2 in custom_items
        ]

    detections = []

    for track_id, label, conf, x1, y1, x2, y2, source in tracked_items:
        is_static = label in CONFIG.decision.static_labels
        history, lbl_history = get_history(track_id, is_static)
        history.append(conf)
        lbl_history.append(label)

        threshold = get_conf_threshold(label)
        hits = sum(1 for c in history if c >= threshold)
        min_hits = (
            CONFIG.decision.static_min_hit_count
            if is_static
            else CONFIG.decision.dynamic_min_hit_count
        )
        passed = hits >= min_hits

        raw_depth = get_depth_for_bbox(x1, y1, x2, y2, cached_depth_map)

        if raw_depth >= CONFIG.depth_calibration.raw_saturation_threshold:
            # 포화 구간 - 신뢰 불가로 이 프레임의 이 detection은 건너뜀
            if DEBUG_VISUALIZE:
                cv2.rectangle(
                    debug_frame,
                    (int(x1), int(y1)),
                    (int(x2), int(y2)),
                    (128, 128, 128),
                    1,
                )
            continue

        corrected_depth = calibrate_p10_depth(raw_depth)
        unity_z = round(corrected_depth, 2)

        center_x = (x1 + x2) / 2
        unity_x = round(
            pixel_x_to_world_x(
                pixel_x=center_x,
                image_width=frame_width,
                forward_depth=corrected_depth,
                horizontal_fov_deg=CONFIG.coordinate.horizontal_fov_deg,
            ),
            2,
        )

        if DEBUG_VISUALIZE:
            color = (
                (0, 0, 255)
                if not passed
                else ((255, 128, 0) if source == "stock" else (0, 255, 0))
            )
            cv2.rectangle(debug_frame, (int(x1), int(y1)), (int(x2), int(y2)), color, 2)
            text = f"[{source}] {label} conf={conf:.2f} z={unity_z}m"
            cv2.putText(
                debug_frame,
                text,
                (int(x1), max(int(y1) - 8, 15)),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.5,
                color,
                2,
            )

        if not passed:
            continue
        if unity_z > CONFIG.decision.max_relevant_z:
            continue

        representative_conf = max(history)
        stable_label = Counter(lbl_history).most_common(1)[0][0]

        detections.append(
            {
                "id": track_id,
                "label": stable_label,
                "confidence": round(representative_conf, 3),
                "x": unity_x,
                "z": unity_z,
                "box_width": round(x2 - x1, 1),
                "box_height": round(y2 - y1, 1),
                "is_static": is_static,
            }
        )

    if DEBUG_VISUALIZE and frame_idx % DEBUG_VISUALIZE_INTERVAL == 0:
        cv2.imshow(
            "Debug: Stock(orange) + Custom(green) + Filtered(red) + Depth-Z",
            debug_frame,
        )
        if cv2.waitKey(1) & 0xFF == ord("q"):
            break

    message = json.dumps({"objects": detections}) + "\n"
    message_queue.put(message)

    frame_idx += 1
    if frame_idx % 50 == 0:
        print(
            f"[frame {frame_idx}] tracked={len(tracked_items)}, sent={len(detections)}"
        )

cap.release()
if DEBUG_VISUALIZE:
    cv2.destroyAllWindows()
print("완료")

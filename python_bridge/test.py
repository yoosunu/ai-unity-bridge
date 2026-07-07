import time
import cv2
from ultralytics import YOLO
from deep_sort_realtime.deepsort_tracker import DeepSort

MODEL_PATH = "models/yolo26n.pt"
VIDEO_PATH = "test_videos/test.mp4"
CROP_TOP, CROP_BOTTOM = 200, 1900
IMGSZ = 1280

model = YOLO(MODEL_PATH)
model.to("mps")

tracker = DeepSort(
    max_age=30,
    n_init=3,
    embedder="mobilenet",  # 기본값보다 가벼운 옵션 (라이브러리 버전에 따라 옵션명 다를 수 있음)
    half=True,  # FP16으로 연산 (MPS에서 지원되면 속도 향상)
)

cap = cv2.VideoCapture(VIDEO_PATH)

frame_idx = 0

while cap.isOpened():
    ret, frame = cap.read()
    if not ret:
        break

    cropped = frame[CROP_TOP:CROP_BOTTOM, :]

    t0 = time.time()
    results = model.predict(cropped, imgsz=IMGSZ, verbose=False)
    t1 = time.time()

    result = results[0]
    raw_detections = []
    if result.boxes is not None:
        for box in result.boxes:
            cls_id = int(box.cls[0])
            label = result.names[cls_id]
            conf = float(box.conf[0])
            x1, y1, x2, y2 = box.xyxy[0].tolist()
            raw_detections.append(([x1, y1, x2 - x1, y2 - y1], conf, label))

    t2 = time.time()
    tracks = tracker.update_tracks(raw_detections, frame=cropped)
    t3 = time.time()

    yolo_time = t1 - t0
    deepsort_time = t3 - t2
    total_time = t3 - t0

    if frame_idx % 10 == 0:
        print(
            f"frame {frame_idx}: yolo={yolo_time:.3f}s, deepsort={deepsort_time:.3f}s, total={total_time:.3f}s, n_dets={len(raw_detections)}"
        )

    frame_idx += 1

cap.release()

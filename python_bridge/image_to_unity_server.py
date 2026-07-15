import socket
import json
import cv2
import numpy as np
import torch
from ultralytics import YOLO
from transformers import pipeline
from PIL import Image
from config import CONFIG

DEBUG_VISUALIZE = True
IMAGE_PATH = "python_bridge/test_images/test5.jpg"

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

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server.bind((CONFIG.network.host, CONFIG.network.port))
server.listen(1)
print("Waiting for Unity client...")
conn, addr = server.accept()
print("Connected:", addr)

image = cv2.imread(IMAGE_PATH)
if image is None:
    raise FileNotFoundError(f"이미지를 못 읽었어요: {IMAGE_PATH}")

image_height, image_width = image.shape[:2]
print(f"[DEBUG] 이미지 크기: {image_width}x{image_height}, 경로: {IMAGE_PATH}")

image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
pil_image = Image.fromarray(image_rgb)

depth_result = depth_pipe(pil_image)
depth_map = depth_result["predicted_depth"].squeeze().cpu().numpy()
if depth_map.shape != (image_height, image_width):
    depth_map = cv2.resize(depth_map, (image_width, image_height))


def get_depth_at(x, y, depth_map):
    xi = int(np.clip(x, 0, depth_map.shape[1] - 1))
    yi = int(np.clip(y, 0, depth_map.shape[0] - 1))
    return float(depth_map[yi, xi])


def get_conf_threshold(label):
    return CONFIG.decision.class_conf_threshold.get(
        label, CONFIG.decision.default_conf_threshold
    )


def get_depth_for_bbox(x1, y1, x2, y2, depth_map):
    """
    bbox 영역 내에서 depth map의 최솟값(가장 가까운 지점)을 사용.
    얇고 가는 물체(볼라드 등)에서 배경 depth가 섞여 들어오는 문제를 완화한다.
    """
    h, w = depth_map.shape
    xi1 = int(np.clip(x1, 0, w - 1))
    xi2 = int(np.clip(x2, 0, w - 1))
    yi1 = int(np.clip(y1, 0, h - 1))
    yi2 = int(np.clip(y2, 0, h - 1))

    if xi2 <= xi1 or yi2 <= yi1:
        return float(depth_map[yi1, xi1])  # 영역이 너무 작으면 그냥 한 점

    region = depth_map[yi1:yi2, xi1:xi2]
    # 하위 10% percentile 정도를 써서, 노이즈 픽셀 한두 개에 흔들리지 않게
    return float(np.percentile(region, 10))


def extract_items(result, label_map=None):
    items = []
    if result.boxes is not None:
        for box in result.boxes:
            cls_id = int(box.cls[0])
            raw_label = result.names[cls_id]
            label = label_map.get(raw_label) if label_map else raw_label
            if label is None:
                continue
            conf = float(box.conf[0])
            x1, y1, x2, y2 = box.xyxy[0].tolist()
            items.append((label, conf, x1, y1, x2, y2))
    return items


debug_frame = image.copy() if DEBUG_VISUALIZE else None

all_items = []
stock_results = stock_model.predict(image, imgsz=CONFIG.model.infer_size, verbose=False)
stock_items = extract_items(stock_results[0], label_map=STOCK_LABEL_MAP)
all_items += [
    (lbl, c, x1, y1, x2, y2, "stock") for lbl, c, x1, y1, x2, y2 in stock_items
]

custom_results = custom_model.predict(
    image, imgsz=CONFIG.model.infer_size, verbose=False
)
custom_items = extract_items(custom_results[0])
all_items += [
    (lbl, c, x1, y1, x2, y2, "custom") for lbl, c, x1, y1, x2, y2 in custom_items
]

# --- 디버그: 필터링 전 raw 탐지 전부 출력 ---
print(f"\n[DEBUG] stock 모델 raw 탐지: {len(stock_items)}개")
for lbl, c, *_ in stock_items:
    print(f"    {lbl}: conf={c:.3f}")

print(f"[DEBUG] custom 모델 raw 탐지: {len(custom_items)}개")
for lbl, c, *_ in custom_items:
    print(f"    {lbl}: conf={c:.3f}")

print(f"[DEBUG] 전체 합계: {len(all_items)}개\n")

detections = []

for i, (label, conf, x1, y1, x2, y2, source) in enumerate(all_items):
    threshold = get_conf_threshold(label)
    passed = conf >= threshold

    center_x = (x1 + x2) / 2
    bottom_y = y2

    unity_z = round(get_depth_for_bbox(x1, y1, x2, y2, depth_map), 2)
    unity_x = round((center_x / image_width - 0.5) * CONFIG.coordinate.unity_x_range, 2)

    # --- 디버그: 이 항목이 왜 통과/필터링됐는지 ---
    print(
        f"[DEBUG] {label} (source={source}): conf={conf:.3f} (threshold={threshold}), "
        f"passed={passed}, z={unity_z}m (max_relevant_z={CONFIG.decision.max_relevant_z})"
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
        print(
            f"    -> 거리 필터에 걸림 (z={unity_z} > max_relevant_z={CONFIG.decision.max_relevant_z})"
        )
        continue

    detections.append(
        {
            "id": i + 1,
            "label": label,
            "confidence": round(conf, 3),
            "x": unity_x,
            "z": unity_z,
            "box_width": round(x2 - x1, 1),
            "box_height": round(y2 - y1, 1),
            "is_static": label in CONFIG.decision.static_labels,
        }
    )

if DEBUG_VISUALIZE:
    cv2.imshow("Debug: Depth-based Z", debug_frame)
    cv2.waitKey(1)

print(f"\n=== 최종 통과: {len(detections)}개 ===")
for d in detections:
    print(f"{d['label']}: conf={d['confidence']}, x={d['x']}, z={d['z']}m")

message = json.dumps({"objects": detections}) + "\n"
conn.sendall(message.encode("utf-8"))
print(f"\n{len(detections)}개 객체 전송 완료")

input("Enter를 누르면 종료...")
if DEBUG_VISUALIZE:
    cv2.destroyAllWindows()
conn.close()
server.close()

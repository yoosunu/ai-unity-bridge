import socket
import json
import time
from ultralytics import YOLO

HOST = "127.0.0.1"
PORT = 5002

MODEL_PATH = "models/yolo26n.pt"
IMAGE_PATH = "test_images/test.jpg"

IMAGE_WIDTH = 640
UNITY_X_RANGE = 8.0

model = YOLO(MODEL_PATH)

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.bind((HOST, PORT))
server.listen(1)

print("Waiting for Unity client...")

conn, addr = server.accept()
print("Connected:", addr)

while True:
    results = model(IMAGE_PATH, verbose=False)
    result = results[0]

    detections = []

    for i, box in enumerate(result.boxes):
        cls_id = int(box.cls[0])
        label = result.names[cls_id]
        conf = float(box.conf[0])

        x1, y1, x2, y2 = box.xyxy[0].tolist()

        center_x = (x1 + x2) / 2
        box_height = y2 - y1

        unity_x = round((center_x / IMAGE_WIDTH - 0.5) * UNITY_X_RANGE, 2)

        # 임시 거리 추정: bbox가 클수록 가까운 것으로 가정
        unity_z = round(max(1.0, 8.0 - (box_height / 320.0) * 7.0), 2)

        detections.append(
            {
                "id": i + 1,
                "label": label,
                "confidence": round(conf, 3),
                "x": unity_x,
                "z": unity_z,
            }
        )

    message = json.dumps({"objects": detections}) + "\n"
    conn.sendall(message.encode("utf-8"))

    print("Sent:", message.strip())

    time.sleep(0.5)

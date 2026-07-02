from ultralytics import YOLO

MODEL_PATH = "models/yolo26n.pt"
IMAGE_PATH = "test_images/test.jpg"

model = YOLO(MODEL_PATH)

results = model(IMAGE_PATH)

result = results[0]

print("class names:", result.names)

for i, box in enumerate(result.boxes):
    cls_id = int(box.cls[0])
    label = result.names[cls_id]
    conf = float(box.conf[0])

    x1, y1, x2, y2 = box.xyxy[0].tolist()

    print({
        "id": i + 1,
        "label": label,
        "confidence": round(conf, 3),
        "bbox": [round(x1, 1), round(y1, 1), round(x2, 1), round(y2, 1)]
    })
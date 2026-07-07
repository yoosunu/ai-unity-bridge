from ultralytics import YOLO

MODEL_PATH = "models/yolo26n.pt"
IMAGE_PATH = "test_images/test.jpg"

model = YOLO(MODEL_PATH)

print("model.task:", model.task)
print("model.names:", model.names)

results = model.track(IMAGE_PATH, persist=True, tracker="bytetrack.yaml", verbose=True)
result = results[0]

print("boxes.id:", result.boxes.id)
print("boxes.is_track:", result.boxes.is_track)
print("boxes.data.shape:", result.boxes.data.shape)
print("boxes.data:", result.boxes.data)

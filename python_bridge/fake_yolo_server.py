import socket
import json
import math
import time

HOST = "127.0.0.1"
PORT = 5000

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.bind((HOST, PORT))
server.listen(1)

print("Waiting for Unity client...")

conn, addr = server.accept()
print("Connected:", addr)

t = 0

while True:
    detections = [
        {
            "id": 1,
            "label": "car",
            "x": round(math.sin(t) * 3, 2),
            "z": round(math.cos(t) * 3, 2)
        },
        {
            "id": 2,
            "label": "car",
            "x": round(math.sin(t + 2) * 4, 2),
            "z": round(math.cos(t + 2) * 4, 2)
        }
    ]

    message = json.dumps({"objects": detections}) + "\n"
    conn.sendall(message.encode("utf-8"))

    print("Sent:", message.strip())

    t += 0.1
    time.sleep(0.1)
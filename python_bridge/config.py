"""
WalkGuide ai-unity-bridge 전역 설정.
"""

from dataclasses import dataclass, field
from typing import Dict

# ────────────────────────────────
PLATFORM = "mac"
# PLATFORM = "windows"
# ────────────────────────────────


@dataclass(frozen=True)
class NetworkConfig:
    host: str = "127.0.0.1"
    port: int = 5002


@dataclass(frozen=True)
class ModelConfig:
    model_path: str = "python_bridge/models/yolo26n_custom.pt"
    device: str = "mps" if PLATFORM == "mac" else "cuda"
    infer_size: int = 1280


# test1
# @dataclass(frozen=True)
# class VideoConfig:
#     video_path: str = "test_videos/test.mp4"
#     crop_top: int = 200
#     crop_bottom: int = 1900
#     start_msec: int = 60000
#     rotate: bool = False


# test2
# @dataclass(frozen=True)
# class VideoConfig:
#     video_path: str = "test_videos/test2.mp4"
#     crop_top: int = 0
#     crop_bottom: int = 1080
#     start_msec: int = 0
#     rotate: bool = False  # test2.mp4는 회전 불필요


# test3
@dataclass(frozen=True)
class VideoConfig:
    video_path: str = "python_bridge/test_videos/test3.mov"
    crop_top: int = 0
    crop_bottom: int = 1920  # 회전 후 기준 세로 길이
    start_msec: int = 20000
    rotate: bool = True  # 회전 필요 여부 - 새로 추가


@dataclass(frozen=True)
class TrackingConfig:
    # Mac: DeepSort의 GPU 가속이 CUDA 전용이라 CPU로 fallback되어 너무 느림 -> ByteTrack 사용
    # Windows(CUDA): DeepSort 정상 가속되므로 사용
    use_deepsort: bool = PLATFORM == "windows"
    max_age: int = 30
    n_init: int = 3
    history_len: int = 5


@dataclass(frozen=True)
class CoordinateConfig:
    unity_x_range: float = 8.0
    z_near: float = 1.0
    z_far: float = 20.0  # depth 모델의 실제 스케일에 맞춰 상향


@dataclass(frozen=True)
class DecisionEngineConfig:
    class_conf_threshold: Dict[str, float] = field(
        default_factory=lambda: {
            "Person": 0.3,
            "Car": 0.35,
            "Bollard": 0.4,
            "Curb": 0.4,
            "Kickboard": 0.3,
            "Pillar": 0.25,
            "Bike": 0.25,
            "Stairs": 0.3,
            "TrafficLight": 0.3,
            "Sign": 0.3,
            "Animal": 0.35,
            "Obstacle": 0.3,
        }
    )
    default_conf_threshold: float = 0.3
    max_relevant_z: float = 40.0

    static_labels: tuple = (
        "Bollard",
        "Curb",
        "Pillar",
        "Stairs",
        "Hole",
        "TrafficLight",
        "Sign",
        "Obstacle",
    )
    # Animal은 넣지 않음 - 개는 움직이는 존재라 동적으로 취급

    dynamic_hit_window: int = 5
    dynamic_min_hit_count: int = 2
    static_hit_window: int = 3
    static_min_hit_count: int = 1


@dataclass(frozen=True)
class AppConfig:
    network: NetworkConfig = field(default_factory=NetworkConfig)
    model: ModelConfig = field(default_factory=ModelConfig)
    video: VideoConfig = field(default_factory=VideoConfig)
    tracking: TrackingConfig = field(default_factory=TrackingConfig)
    coordinate: CoordinateConfig = field(default_factory=CoordinateConfig)
    decision: DecisionEngineConfig = field(default_factory=DecisionEngineConfig)


CONFIG = AppConfig()

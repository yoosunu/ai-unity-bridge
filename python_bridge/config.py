"""
Simcheong_ai-unity-bridge 전역 설정.
"""

from dataclasses import dataclass, field
from typing import Dict

# ────────────────────────────────
# PLATFORM = "mac"
PLATFORM = "windows"
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
#     video_path: str = "python_bridge/test_videos/test.mp4"
#     crop_top: int = 200
#     crop_bottom: int = 1900
#     start_msec: int = 60000
#     rotate: bool = False


# test2
# @dataclass(frozen=True)
# class VideoConfig:
#     video_path: str = "python_bridge/test_videos/test2.mp4"
#     crop_top: int = 0
#     crop_bottom: int = 1080
#     start_msec: int = 0
#     rotate: bool = False  # test2.mp4는 회전 불필요


# test5
@dataclass(frozen=True)
class VideoConfig:
    video_path: str = (
        "python_bridge/test_videos/test5.mp4"  # 실제 저장 경로/파일명 확인
    )
    crop_top: int = 0
    crop_bottom: int = 1920  # 원본 높이 그대로, 크롭 불필요
    start_msec: int = (
        5000  # 처음부터 (천천히 일직선으로 걷는 영상이니 처음부터 보는 게 좋을 것 같아요)
    )
    rotate: bool = False  # 회전 메타데이터 문제 없음, 그대로 사용


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
    z_far: float = 8.0
    horizontal_fov_deg: float = 70.0


@dataclass(frozen=True)
class DecisionEngineConfig:
    class_conf_threshold: Dict[str, float] = field(
        default_factory=lambda: {
            "Person": 0.5,
            "Car": 0.5,
            "Bollard": 0.5,
            "Curb": 0.5,
            "Kickboard": 0.5,
            "Pillar": 0.5,
            "Bike": 0.5,
            "Stairs": 0.5,
            "TrafficLight": 0.5,
            "Sign": 0.5,
            "Animal": 0.5,
            "Obstacle": 0.2,
        }
    )
    default_conf_threshold: float = 0.5
    max_relevant_z: float = 12.0  # 30 -> 8

    static_labels: tuple = (
        "Bollard",
        "Curb",
        "Pillar",
        "Stairs",
        "Hole",
        "TrafficLight",
        "Sign",
        "Obstacle",
        "Car",
        "Person",
    )

    dynamic_hit_window: int = 5
    dynamic_min_hit_count: int = 2
    static_hit_window: int = 3
    static_min_hit_count: int = 1


@dataclass(frozen=True)
class DepthCalibrationConfig:
    enabled: bool = True
    near_slope: float = 0.5117074911
    near_intercept: float = -0.8900003473
    near_far_boundary: float = 20.0
    far_slope: float = 0.7633
    far_intercept: float = -0.51  # 여기, -11.7896이 아니라 -0.51로
    raw_saturation_threshold: float = 65.0


@dataclass(frozen=True)
class AppConfig:
    network: NetworkConfig = field(default_factory=NetworkConfig)
    model: ModelConfig = field(default_factory=ModelConfig)
    video: VideoConfig = field(default_factory=VideoConfig)
    tracking: TrackingConfig = field(default_factory=TrackingConfig)
    coordinate: CoordinateConfig = field(default_factory=CoordinateConfig)
    decision: DecisionEngineConfig = field(default_factory=DecisionEngineConfig)
    depth_calibration: DepthCalibrationConfig = field(
        default_factory=DepthCalibrationConfig
    )


CONFIG = AppConfig()

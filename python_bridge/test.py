from __future__ import annotations

from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np

# ============================================================
# 설정
# ============================================================

OUTPUT_DIR = Path("python_bridge/test_outputs")
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

CSV_OUTPUT_PATH = OUTPUT_DIR / "dav2_calibration_results.csv"
GRAPH_OUTPUT_PATH = OUTPUT_DIR / "dav2_calibration_graph.png"


# ============================================================
# 실험 데이터
#
# 순서:
# 1m_left, 1m_center, 1m_right,
# 2m_left, 2m_center, 2m_right,
# ...
# 5m_left, 5m_center, 5m_right
# ============================================================

FILE_NAMES = [
    "1m_left.png",
    "1m_center.png",
    "1m_right.png",
    "2m_left.png",
    "2m_center.png",
    "2m_right.png",
    "3m_left.png",
    "3m_center.png",
    "3m_right.png",
    "4m_left.png",
    "4m_center.png",
    "4m_right.png",
    "5m_left.png",
    "5m_center.png",
    "5m_right.png",
]

ACTUAL_DISTANCES = np.array(
    [
        1.0,
        1.0,
        1.0,
        2.0,
        2.0,
        2.0,
        3.0,
        3.0,
        3.0,
        4.0,
        4.0,
        4.0,
        5.0,
        5.0,
        5.0,
    ],
    dtype=float,
)

CENTER_DEPTHS = np.array(
    [
        4.06,
        4.20,
        3.88,
        5.88,
        5.82,
        5.51,
        7.53,
        7.89,
        8.09,
        8.48,
        9.45,
        9.34,
        11.19,
        12.14,
        12.75,
    ],
    dtype=float,
)

MEDIAN_DEPTHS = np.array(
    [
        4.15,
        4.29,
        3.94,
        6.37,
        5.87,
        5.56,
        7.89,
        8.06,
        8.66,
        8.82,
        9.63,
        10.47,
        11.92,
        12.39,
        13.34,
    ],
    dtype=float,
)

MEAN_DEPTHS = np.array(
    [
        4.95,
        4.87,
        4.66,
        7.21,
        6.84,
        6.37,
        9.03,
        9.20,
        9.83,
        10.07,
        10.74,
        11.45,
        13.18,
        13.78,
        14.73,
    ],
    dtype=float,
)

P10_DEPTHS = np.array(
    [
        3.88,
        4.14,
        3.79,
        5.80,
        5.73,
        5.40,
        7.50,
        7.84,
        8.04,
        8.27,
        9.21,
        9.22,
        10.87,
        11.81,
        12.53,
    ],
    dtype=float,
)


# ============================================================
# 분석 함수
# ============================================================


def fit_linear_calibration(
    raw_depths: np.ndarray,
    actual_distances: np.ndarray,
) -> dict[str, object]:
    """
    실제 거리 = slope * DAV2 원본 거리 + intercept
    형태의 선형 회귀식을 계산한다.
    """

    slope, intercept = np.polyfit(
        raw_depths,
        actual_distances,
        1,
    )

    calibrated = slope * raw_depths + intercept

    residuals = actual_distances - calibrated

    ss_res = np.sum(residuals**2)
    ss_tot = np.sum((actual_distances - np.mean(actual_distances)) ** 2)

    r_squared = 1.0 - (ss_res / ss_tot)
    mae = np.mean(np.abs(residuals))
    rmse = np.sqrt(np.mean(residuals**2))

    return {
        "slope": float(slope),
        "intercept": float(intercept),
        "calibrated": calibrated,
        "residuals": residuals,
        "r_squared": float(r_squared),
        "mae": float(mae),
        "rmse": float(rmse),
    }


def print_calibration_summary(
    name: str,
    raw_depths: np.ndarray,
    result: dict[str, object],
) -> None:
    slope = float(result["slope"])
    intercept = float(result["intercept"])
    calibrated = np.asarray(result["calibrated"])

    print("\n" + "=" * 78)
    print(f"{name} 기준 선형 보정")
    print("=" * 78)

    print(f"보정식: actual_distance = " f"{slope:.6f} × raw_depth " f"{intercept:+.6f}")

    print(f"R²   : {result['r_squared']:.6f}")
    print(f"MAE  : {result['mae']:.4f} m")
    print(f"RMSE : {result['rmse']:.4f} m")

    print("\n샘플별 결과")
    print(f"{'파일':18}" f"{'실제':>8}" f"{'원본':>10}" f"{'보정':>10}" f"{'오차':>10}")

    for file_name, actual, raw, corrected in zip(
        FILE_NAMES,
        ACTUAL_DISTANCES,
        raw_depths,
        calibrated,
    ):
        error = corrected - actual

        print(
            f"{file_name:18}"
            f"{actual:8.2f}"
            f"{raw:10.2f}"
            f"{corrected:10.2f}"
            f"{error:+10.2f}"
        )


def save_csv(
    results: dict[str, dict[str, object]],
) -> None:
    header = [
        "file",
        "actual_distance",
        "center_raw",
        "center_corrected",
        "median_raw",
        "median_corrected",
        "mean_raw",
        "mean_corrected",
        "p10_raw",
        "p10_corrected",
    ]

    rows = []

    for index, file_name in enumerate(FILE_NAMES):
        row = [
            file_name,
            ACTUAL_DISTANCES[index],
            CENTER_DEPTHS[index],
            np.asarray(results["Center"]["calibrated"])[index],
            MEDIAN_DEPTHS[index],
            np.asarray(results["Median"]["calibrated"])[index],
            MEAN_DEPTHS[index],
            np.asarray(results["Mean"]["calibrated"])[index],
            P10_DEPTHS[index],
            np.asarray(results["P10"]["calibrated"])[index],
        ]

        rows.append(row)

    with CSV_OUTPUT_PATH.open(
        "w",
        encoding="utf-8",
    ) as file:
        file.write(",".join(header) + "\n")

        for row in rows:
            file.write(",".join(str(value) for value in row) + "\n")

    print(f"\nCSV 저장 완료: {CSV_OUTPUT_PATH}")


def save_graph(
    results: dict[str, dict[str, object]],
) -> None:
    plt.figure(figsize=(10, 7))

    datasets = {
        "Center": CENTER_DEPTHS,
        "Median": MEDIAN_DEPTHS,
        "Mean": MEAN_DEPTHS,
        "P10": P10_DEPTHS,
    }

    for name, raw_depths in datasets.items():
        result = results[name]

        slope = float(result["slope"])
        intercept = float(result["intercept"])

        plt.scatter(
            raw_depths,
            ACTUAL_DISTANCES,
            label=f"{name} samples",
            alpha=0.75,
        )

        x_line = np.linspace(
            raw_depths.min(),
            raw_depths.max(),
            200,
        )

        y_line = slope * x_line + intercept

        plt.plot(
            x_line,
            y_line,
            label=(
                f"{name}: "
                f"y={slope:.3f}x{intercept:+.3f}, "
                f"R²={result['r_squared']:.3f}"
            ),
        )

    plt.xlabel("DAV2 raw depth output")
    plt.ylabel("Approximate actual distance (m)")
    plt.title("Depth Anything V2 Metric Outdoor Small\n" "Camera Calibration: 1m to 5m")
    plt.grid(True)
    plt.legend()
    plt.tight_layout()

    plt.savefig(
        GRAPH_OUTPUT_PATH,
        dpi=180,
    )

    print(f"그래프 저장 완료: {GRAPH_OUTPUT_PATH}")

    plt.show()


def print_best_result(
    results: dict[str, dict[str, object]],
) -> None:
    best_name = min(
        results,
        key=lambda key: float(results[key]["mae"]),
    )

    best = results[best_name]

    print("\n" + "=" * 78)
    print("가장 낮은 MAE를 보인 기준")
    print("=" * 78)

    print(f"기준      : {best_name}")
    print(f"R²        : {best['r_squared']:.6f}")
    print(f"MAE       : {best['mae']:.4f} m")
    print(f"RMSE      : {best['rmse']:.4f} m")
    print(
        "보정식    : actual_distance = "
        f"{best['slope']:.6f} × raw_depth "
        f"{best['intercept']:+.6f}"
    )


# ============================================================
# 서버 코드에서 사용할 수 있는 임시 보정 함수
# ============================================================


def calibrate_center_depth(raw_depth: float) -> float:
    """
    현재 1~5m 볼라드 실험에서 얻은 Center 기준 임시 보정식.
    동일 카메라와 비슷한 촬영 조건에서만 참고해야 한다.
    """
    corrected = 0.5008856992 * raw_depth - 0.8805284733
    return max(0.0, corrected)


def calibrate_median_depth(raw_depth: float) -> float:
    """
    Median 기준 임시 보정식.
    """
    corrected = 0.4702207745 * raw_depth - 0.8043995460
    return max(0.0, corrected)


def calibrate_mean_depth(raw_depth: float) -> float:
    """
    Mean 기준 임시 보정식.
    이번 데이터에서는 MAE가 가장 낮았지만,
    bbox 배경이 섞일 경우 불안정할 수 있다.
    """
    corrected = 0.4391077811 * raw_depth - 1.0078830870
    return max(0.0, corrected)


def calibrate_p10_depth(raw_depth: float) -> float:
    """
    현재 서버의 bbox 10 percentile 방식과 대응하는 임시 보정식.
    """
    corrected = 0.5117074911 * raw_depth - 0.8900003473
    return max(0.0, corrected)


# ============================================================
# 실행
# ============================================================


def main() -> None:
    datasets = {
        "Center": CENTER_DEPTHS,
        "Median": MEDIAN_DEPTHS,
        "Mean": MEAN_DEPTHS,
        "P10": P10_DEPTHS,
    }

    results: dict[str, dict[str, object]] = {}

    print("=" * 78)
    print("DAV2 Metric Outdoor Small 거리 캘리브레이션 분석")
    print("=" * 78)

    for name, raw_depths in datasets.items():
        result = fit_linear_calibration(
            raw_depths=raw_depths,
            actual_distances=ACTUAL_DISTANCES,
        )

        results[name] = result

        print_calibration_summary(
            name=name,
            raw_depths=raw_depths,
            result=result,
        )

    print_best_result(results)

    save_csv(results)
    save_graph(results)

    print("\n" + "=" * 78)
    print("현재 서버 코드에 임시 적용할 경우")
    print("=" * 78)

    print(
        "현재 get_depth_for_bbox()가 P10을 사용하므로:\n"
        "corrected_depth = "
        "0.5117074911 * raw_depth - 0.8900003473"
    )

    print("\n예시")

    sample_raw_depths = [4.0, 6.0, 8.0, 10.0, 12.0]

    for raw_depth in sample_raw_depths:
        corrected = calibrate_p10_depth(raw_depth)

        print(f"raw={raw_depth:5.2f}m " f"→ corrected={corrected:5.2f}m")

    print(
        "\n주의: 이 식은 현재 스마트폰, 현재 촬영 조건, "
        "1~5m 볼라드 데이터에 맞춘 임시 보정식입니다."
    )


if __name__ == "__main__":
    main()

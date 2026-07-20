#!/usr/bin/env python3
"""Convert an authoritative MoAD map into a deterministic Unreal scene contract."""

from __future__ import annotations

import argparse
import json
import math
import shutil
import sys
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any


SCENE_FORMAT = "moad-unreal-scene"
SCENE_VERSION = 1
DEFAULT_DEPTH_SPACING_CM = 120.0


class ContractError(ValueError):
    pass


@dataclass(frozen=True)
class LoadedMap:
    runtime: dict[str, Any]
    source_path: Path
    archive: zipfile.ZipFile | None = None

    def close(self) -> None:
        if self.archive is not None:
            self.archive.close()


def load_map(path: Path) -> LoadedMap:
    if path.suffix.lower() == ".moadmap":
        archive = zipfile.ZipFile(path)
        try:
            runtime = json.loads(archive.read("level.runtime.json"))
        except Exception:
            archive.close()
            raise
        return LoadedMap(runtime=runtime, source_path=path, archive=archive)
    return LoadedMap(runtime=json.loads(path.read_text(encoding="utf-8")), source_path=path)


def require_pair(value: Any, label: str) -> tuple[float, float]:
    if not isinstance(value, list) or len(value) != 2:
        raise ContractError(f"{label} must contain exactly two numbers")
    return float(value[0]), float(value[1])


def require_points(value: Any, label: str, minimum: int = 2) -> list[tuple[float, float]]:
    if not isinstance(value, list) or len(value) < minimum:
        raise ContractError(f"{label} needs at least {minimum} points")
    return [require_pair(point, f"{label}[{index}]") for index, point in enumerate(value)]


def unique_ids(items: list[dict[str, Any]], key: str, label: str) -> None:
    seen: set[str] = set()
    for item in items:
        item_id = str(item.get(key, "")).strip()
        if not item_id:
            raise ContractError(f"{label} contains an item without {key}")
        if item_id in seen:
            raise ContractError(f"duplicate {label} id: {item_id}")
        seen.add(item_id)


def visual_point(
    point: tuple[float, float],
    image_height: float,
    units_per_pixel: float,
    depth_y_cm: float,
    physical_height_m: float | None = None,
) -> dict[str, float]:
    x, screen_y = point
    result = {
        "x_cm": round(x * units_per_pixel, 4),
        "y_cm": round(depth_y_cm, 4),
        "z_cm": round((image_height - screen_y) * units_per_pixel, 4),
        "screen_x": x,
        "screen_y": screen_y,
    }
    if physical_height_m is not None:
        result["physical_height_cm"] = round(physical_height_m * 100.0, 4)
    return result


def slope_degrees(a: tuple[float, float], b: tuple[float, float], ppm: float) -> float:
    horizontal_m = abs(b[0] - a[0]) / ppm
    vertical_m = abs(b[1] - a[1])
    if horizontal_m < 0.001:
        return 90.0 if vertical_m > 0.001 else 0.0
    return math.degrees(math.atan2(vertical_m, horizontal_m))


def convert(runtime: dict[str, Any], depth_spacing_cm: float = DEFAULT_DEPTH_SPACING_CM) -> tuple[dict[str, Any], list[str]]:
    warnings: list[str] = []
    width, height = require_pair(runtime.get("runtime_size"), "runtime_size")
    metrics = runtime.get("world_metrics")
    if not isinstance(metrics, dict):
        raise ContractError("world_metrics is missing")
    ppm = float(metrics.get("pixels_per_meter", 0))
    if ppm <= 0:
        raise ContractError("world_metrics.pixels_per_meter must be positive")
    units_per_pixel = 100.0 / ppm

    raw_lanes = runtime.get("lanes")
    if not isinstance(raw_lanes, list) or not raw_lanes:
        raise ContractError("lanes must be a non-empty list")
    unique_ids(raw_lanes, "id", "lane")
    lane_by_id: dict[str, dict[str, Any]] = {}
    lanes: list[dict[str, Any]] = []
    for raw_lane in raw_lanes:
        lane_id = str(raw_lane["id"])
        lane_index = int(raw_lane["lane"])
        lane = {
            "id": lane_id,
            "index": lane_index,
            "depth_y_cm": round(lane_index * depth_spacing_cm, 4),
            "scale": float(raw_lane["scale"]),
            "z_order": int(raw_lane.get("z_index", 0)),
        }
        lane_by_id[lane_id] = lane
        lanes.append(lane)

    raw_supports = runtime.get("collision_surfaces", [])
    if not isinstance(raw_supports, list):
        raise ContractError("collision_surfaces must be a list")
    unique_ids(raw_supports, "id", "support")
    supports: list[dict[str, Any]] = []
    support_ids: set[str] = set()
    for raw_support in raw_supports:
        support_id = str(raw_support["id"])
        lane_id = str(raw_support.get("lane", ""))
        if lane_id not in lane_by_id:
            raise ContractError(f"support {support_id} references unknown lane {lane_id}")
        raw_points = require_points(raw_support.get("points"), f"support {support_id}")
        raw_heights = raw_support.get("height_points_m")
        if raw_heights is None:
            fallback = float(raw_support.get("height_m", raw_support.get("elevation_rank", 0)))
            heights = [fallback] * len(raw_points)
        else:
            heights = [float(value) for value in raw_heights]
            if len(heights) != len(raw_points):
                raise ContractError(f"support {support_id} height_points_m count does not match points")
        for index in range(1, len(raw_points)):
            angle = slope_degrees(
                (raw_points[index - 1][0], heights[index - 1]),
                (raw_points[index][0], heights[index]),
                ppm,
            )
            if 55.001 < angle < 89.999:
                raise ContractError(
                    f"support {support_id} segment {index} is {angle:.1f} degrees; "
                    "use a typed ladder or cliff transition"
                )
        lane = lane_by_id[lane_id]
        supports.append({
            "id": support_id,
            "lane_id": lane_id,
            "lane_index": lane["index"],
            "traversal_mode": str(raw_support.get("traversal_mode", "stable")),
            "speed_multiplier": float(raw_support.get("speed_multiplier", 1.0)),
            "elevation_rank": int(raw_support.get("elevation_rank", 0)),
            "points": [
                visual_point(point, height, units_per_pixel, lane["depth_y_cm"], heights[index])
                for index, point in enumerate(raw_points)
            ],
        })
        support_ids.add(support_id)

    raw_transitions = runtime.get("transitions", [])
    if not isinstance(raw_transitions, list):
        raise ContractError("transitions must be a list")
    unique_ids(raw_transitions, "id", "transition")
    transitions: list[dict[str, Any]] = []
    for raw_transition in raw_transitions:
        transition_id = str(raw_transition["id"])
        from_lane_id = str(raw_transition.get("from_lane", ""))
        to_lane_id = str(raw_transition.get("to_lane", ""))
        if from_lane_id not in lane_by_id or to_lane_id not in lane_by_id:
            raise ContractError(f"transition {transition_id} references an unknown lane")
        from_support_id = raw_transition.get("from_support_id")
        to_support_id = raw_transition.get("to_support_id")
        for endpoint, endpoint_name in ((from_support_id, "from"), (to_support_id, "to")):
            if endpoint and str(endpoint) not in support_ids:
                raise ContractError(f"transition {transition_id} {endpoint_name} support {endpoint} does not exist")
        if not from_support_id or not to_support_id:
            warnings.append(f"transition {transition_id} is not explicitly bound to both support tracks")
        path = require_points(raw_transition.get("path_points"), f"transition {transition_id}")
        from_lane = lane_by_id[from_lane_id]
        to_lane = lane_by_id[to_lane_id]
        count = max(len(path) - 1, 1)
        points: list[dict[str, float]] = []
        for index, point in enumerate(path):
            t = index / count
            depth_y = from_lane["depth_y_cm"] + (to_lane["depth_y_cm"] - from_lane["depth_y_cm"]) * t
            points.append(visual_point(point, height, units_per_pixel, depth_y))
        transitions.append({
            "id": transition_id,
            "from_lane_id": from_lane_id,
            "to_lane_id": to_lane_id,
            "from_support_id": from_support_id,
            "to_support_id": to_support_id,
            "type": str(raw_transition.get("transition_type", "depth_walk")),
            "animation_clip": str(raw_transition.get("animation_clip", "")),
            "duration_seconds": float(raw_transition.get("duration", 1.0)),
            "lane_handoff_t": float(raw_transition.get("lane_handoff_t", 0.5)),
            "points": points,
        })

    spawn_raw = runtime.get("spawn")
    if not isinstance(spawn_raw, dict):
        raise ContractError("spawn is missing")
    spawn_lane_id = str(spawn_raw.get("lane", ""))
    if spawn_lane_id not in lane_by_id:
        raise ContractError(f"spawn references unknown lane {spawn_lane_id}")
    spawn_lane = lane_by_id[spawn_lane_id]
    spawn = visual_point(
        require_pair(spawn_raw.get("position"), "spawn.position"),
        height,
        units_per_pixel,
        spawn_lane["depth_y_cm"],
        float(spawn_raw.get("height_m", 0.0)),
    )
    spawn.update({"lane_id": spawn_lane_id, "scale": spawn_lane["scale"]})

    scene = {
        "format": SCENE_FORMAT,
        "version": SCENE_VERSION,
        "source_level_id": str(runtime.get("id", "unnamed_level")),
        "projection": {
            "mode": "orthographic_image_space",
            "axes": {"horizontal": "X", "depth": "Y", "vertical": "Z"},
            "pixels_per_meter": ppm,
            "unreal_units_per_pixel": units_per_pixel,
            "depth_spacing_cm": depth_spacing_cm,
            "image_origin": "top_left",
            "world_origin": "bottom_left",
        },
        "background": {
            "file": str(runtime.get("background_file", "")),
            "width_px": width,
            "height_px": height,
            "world_width_cm": round(width * units_per_pixel, 4),
            "world_height_cm": round(height * units_per_pixel, 4),
        },
        "lanes": sorted(lanes, key=lambda item: item["index"]),
        "supports": supports,
        "transitions": transitions,
        "spawn": spawn,
    }
    return scene, warnings


def extract_background(loaded: LoadedMap, output_dir: Path, background_file: str) -> str | None:
    if loaded.archive is None or not background_file:
        return None
    archive_name = background_file.replace("\\", "/")
    if archive_name not in loaded.archive.namelist():
        candidates = [name for name in loaded.archive.namelist() if name.lower().endswith((".png", ".jpg", ".jpeg"))]
        if len(candidates) != 1:
            raise ContractError(f"background {archive_name} is missing from archive")
        archive_name = candidates[0]
    target = output_dir / Path(archive_name).name
    with loaded.archive.open(archive_name) as source, target.open("wb") as destination:
        shutil.copyfileobj(source, destination)
    return target.name


def write_output(input_path: Path, output_dir: Path, depth_spacing_cm: float) -> dict[str, Any]:
    loaded = load_map(input_path)
    try:
        scene, warnings = convert(loaded.runtime, depth_spacing_cm)
        output_dir.mkdir(parents=True, exist_ok=True)
        extracted = extract_background(loaded, output_dir, scene["background"]["file"])
        if extracted:
            scene["background"]["extracted_file"] = extracted
        scene_path = output_dir / "moad.unreal-scene.json"
        scene_path.write_text(json.dumps(scene, indent=2) + "\n", encoding="utf-8")
        report = {
            "status": "PASS" if not warnings else "PASS_WITH_WARNINGS",
            "input": str(input_path.resolve()),
            "scene": str(scene_path.resolve()),
            "background": extracted,
            "counts": {
                "lanes": len(scene["lanes"]),
                "supports": len(scene["supports"]),
                "transitions": len(scene["transitions"]),
            },
            "warnings": warnings,
        }
        (output_dir / "import-report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        return report
    finally:
        loaded.close()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="A .moadmap archive or runtime level JSON")
    parser.add_argument("output", type=Path, help="Output directory")
    parser.add_argument("--depth-spacing-cm", type=float, default=DEFAULT_DEPTH_SPACING_CM)
    args = parser.parse_args()
    try:
        report = write_output(args.input, args.output, args.depth_spacing_cm)
    except (ContractError, KeyError, OSError, ValueError, zipfile.BadZipFile) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

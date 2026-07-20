from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MODULE_PATH = ROOT / "integrations" / "unreal" / "tools" / "moadmap_to_unreal.py"
SPEC = importlib.util.spec_from_file_location("moadmap_to_unreal", MODULE_PATH)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class MoadMapToUnrealTests(unittest.TestCase):
    def test_real_editor_package_converts_without_manual_path_rebuild(self) -> None:
        source = ROOT / "apps" / "level-editor" / "public" / "semi-abandoned-tomb.moadmap"
        with tempfile.TemporaryDirectory() as directory:
            report = MODULE.write_output(source, Path(directory), 120.0)
            scene = json.loads((Path(directory) / "moad.unreal-scene.json").read_text(encoding="utf-8"))

            self.assertIn(report["status"], {"PASS", "PASS_WITH_WARNINGS"})
            self.assertEqual("moad-unreal-scene", scene["format"])
            self.assertEqual(3, len(scene["lanes"]))
            self.assertEqual(10, len(scene["supports"]))
            self.assertEqual(6, len(scene["transitions"]))
            self.assertTrue((Path(directory) / scene["background"]["extracted_file"]).is_file())

    def test_visual_coordinates_preserve_authored_support_line(self) -> None:
        runtime = {
            "id": "coordinate_proof",
            "runtime_size": [800, 600],
            "world_metrics": {"pixels_per_meter": 80},
            "background_file": "proof.png",
            "lanes": [{"id": "near", "lane": 0, "scale": 1.0, "z_index": 2}],
            "spawn": {"lane": "near", "position": [80, 520]},
            "collision_surfaces": [{
                "id": "floor",
                "lane": "near",
                "elevation_rank": 0,
                "points": [[80, 520], [240, 440]],
                "height_points_m": [0, 1],
            }],
            "transitions": [],
        }
        scene, warnings = MODULE.convert(runtime)
        points = scene["supports"][0]["points"]

        self.assertEqual([], warnings)
        self.assertEqual(100.0, points[0]["x_cm"])
        self.assertEqual(100.0, points[0]["z_cm"])
        self.assertEqual(300.0, points[1]["x_cm"])
        self.assertEqual(200.0, points[1]["z_cm"])
        self.assertEqual(100.0, points[1]["physical_height_cm"])

    def test_depth_changes_y_and_authored_lane_scale_only(self) -> None:
        runtime = {
            "id": "depth_proof",
            "runtime_size": [800, 600],
            "world_metrics": {"pixels_per_meter": 80},
            "background_file": "proof.png",
            "lanes": [
                {"id": "near", "lane": 0, "scale": 1.0, "z_index": 2},
                {"id": "far", "lane": 2, "scale": 0.76, "z_index": -2},
            ],
            "spawn": {"lane": "near", "position": [80, 520]},
            "collision_surfaces": [
                {"id": "near_floor", "lane": "near", "elevation_rank": 0, "points": [[0, 520], [400, 520]]},
                {"id": "far_floor", "lane": "far", "elevation_rank": 0, "points": [[0, 300], [400, 300]]},
            ],
            "transitions": [{
                "id": "walk_in",
                "from_lane": "near",
                "to_lane": "far",
                "from_support_id": "near_floor",
                "to_support_id": "far_floor",
                "transition_type": "depth_walk",
                "path_points": [[100, 520], [200, 410], [300, 300]],
            }],
        }
        scene, warnings = MODULE.convert(runtime)
        transition = scene["transitions"][0]

        self.assertEqual([], warnings)
        self.assertEqual(0.0, transition["points"][0]["y_cm"])
        self.assertEqual(240.0, transition["points"][-1]["y_cm"])
        self.assertEqual(1.0, scene["lanes"][0]["scale"])
        self.assertEqual(0.76, scene["lanes"][1]["scale"])

    def test_unknown_lane_fails_closed(self) -> None:
        runtime = {
            "id": "broken",
            "runtime_size": [800, 600],
            "world_metrics": {"pixels_per_meter": 80},
            "lanes": [{"id": "near", "lane": 0, "scale": 1, "z_index": 0}],
            "spawn": {"lane": "missing", "position": [0, 0]},
            "collision_surfaces": [],
            "transitions": [],
        }
        with self.assertRaisesRegex(MODULE.ContractError, "spawn references unknown lane"):
            MODULE.convert(runtime)


if __name__ == "__main__":
    unittest.main()

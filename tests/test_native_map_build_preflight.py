"""Synthetic regression of pinned native inspector build gates and staging.

Exercises the build workflow with fabricated DLL bytes and a mocked compiler.
No game assets or actual native compilation are used by these tests.
"""
import hashlib
import io
import json
import sys
from contextlib import redirect_stdout
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import native_map_build_preflight as preflight
from make_inspector_pck import verify_pack


class NativeMapBuildPreflightTests(unittest.TestCase):
    @staticmethod
    def synthetic_game(root: Path, *, os_dir="data_sts2_windows_x86_64"):
        root.mkdir(parents=True, exist_ok=True)
        data = root / os_dir
        data.mkdir(parents=True, exist_ok=True)
        for name in ("sts2.dll", "0Harmony.dll", "GodotSharp.dll"):
            (data / name).write_bytes(("fake-" + name).encode())
        return data

    def test_pin_verification_accepts_only_explicit_matching_game(self):
        with tempfile.TemporaryDirectory() as tmp:
            game = Path(tmp) / "game"
            stage = Path(tmp) / "stage"
            data = self.synthetic_game(game)
            expected = hashlib.sha256(b"fake-sts2.dll").hexdigest()
            selected, destination, report = preflight.check_installation(
                game, stage, expected_sha=expected)
            self.assertEqual(data.resolve(), selected)
            self.assertEqual(stage.resolve(), destination)
            self.assertTrue(report["dependencies_present"])
            self.assertEqual("NOT_TESTED", report["native_fidelity_status"])
            with self.assertRaisesRegex(ValueError, "SHA-256 differs"):
                preflight.check_installation(game, stage)
            selected, _, _ = preflight.check_installation(
                data, stage, expected_sha=expected)
            self.assertEqual(selected, data.resolve())

    def test_staging_under_original_game_is_forbidden(self):
        with tempfile.TemporaryDirectory() as tmp:
            game = Path(tmp) / "game"
            data = self.synthetic_game(game, os_dir="data_sts2_linuxbsd_x86_64")
            expected = hashlib.sha256(b"fake-sts2.dll").hexdigest()
            for stage in (game, game / "mods" / "Sts2ReferenceMod", data / "tmp"):
                with self.assertRaisesRegex(ValueError, "inside"):
                    preflight.check_installation(
                        game, stage, expected_sha=expected)

    def test_missing_dependencies_block_before_compilation(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            game = root / "game"
            data = self.synthetic_game(game)
            (data / "0Harmony.dll").unlink()
            expected = hashlib.sha256(b"fake-sts2.dll").hexdigest()
            with self.assertRaisesRegex(ValueError, "0Harmony.dll"):
                preflight.check_installation(
                    game, root / "stage", expected_sha=expected)

    def test_metadata_probe_accepts_only_pinned_matching_inspected_assembly(self):
        with tempfile.TemporaryDirectory() as tmp:
            data = self.synthetic_game(Path(tmp) / "game")
            digest = preflight.file_sha256(data / "sts2.dll")
            observed = {
                "schema": "sts2-native-metadata-contract-v1",
                "status": "NATIVE_METADATA_MEMBERS_PRESENT",
                "managed_assembly_sha256": digest,
                "metadata_only": True, "native_game_executed": False,
                "reflection_runtime_parity": "NOT_TESTED",
                "checks": [
                    {"type": "MegaCrit.Sts2.Core.Models.ActModel",
                     "kind": "field", "member": "_rooms", "present": True},
                    *({"type": t, "kind": k, "member": m, "present": True}
                      for t, k, m in sorted(
                          preflight.TURN_CHECKPOINT_METADATA_MEMBERS)),
                ],
            }
            class Success:
                returncode = 0
                stdout = json.dumps(observed)
                stderr = ""
            with patch.object(preflight.subprocess, "run",
                              return_value=Success()) as run:
                self.assertEqual(
                    "NATIVE_METADATA_MEMBERS_PRESENT",
                    preflight.probe_native_metadata(data)["status"])
                self.assertIn(str(data / "sts2.dll"), run.call_args.args[0])
                self.assertIn("--project", run.call_args.args[0])
            observed["managed_assembly_sha256"] = "0" * 64
            Success.stdout = json.dumps(observed)
            with patch.object(preflight.subprocess, "run",
                              return_value=Success()):
                with self.assertRaisesRegex(RuntimeError, "mismatched contract"):
                    preflight.probe_native_metadata(data)
            observed["managed_assembly_sha256"] = digest
            # A stale metadata probe could return success but omit the new
            # source turn-boundary contract: this must fail closed.
            missing = observed["checks"].pop()
            Success.stdout = json.dumps(observed)
            with patch.object(preflight.subprocess, "run",
                              return_value=Success()):
                with self.assertRaisesRegex(RuntimeError, "mismatched contract"):
                    preflight.probe_native_metadata(data)
            observed["checks"].append(missing)
            observed["checks"][0]["present"] = False
            Success.stdout = json.dumps(observed)
            with patch.object(preflight.subprocess, "run",
                              return_value=Success()):
                with self.assertRaisesRegex(RuntimeError, "mismatched contract"):
                    preflight.probe_native_metadata(data)

    def test_missing_native_metadata_blocks_build_without_staging(self):
        with tempfile.TemporaryDirectory() as tmp:
            data = self.synthetic_game(Path(tmp) / "game")
            stage = Path(tmp) / "stage"
            class Missing:
                returncode = 2
                stdout = '{"status": "NATIVE_METADATA_MEMBERS_MISSING"}'
                stderr = ""
            with patch.object(preflight.subprocess, "run",
                              return_value=Missing()) as run:
                with self.assertRaisesRegex(RuntimeError, "metadata contract failed"):
                    preflight.build_and_stage(data, stage, {})
                self.assertEqual(1, run.call_count)
            self.assertFalse(stage.exists())

    def test_metadata_only_cli_stages_nothing_and_never_compiles_mod(self):
        with tempfile.TemporaryDirectory() as tmp:
            game = Path(tmp) / "game"
            data = self.synthetic_game(game)
            stage = Path(tmp) / "stage"
            digest = preflight.file_sha256(data / "sts2.dll")
            report = {"status": "NATIVE_METADATA_MEMBERS_PRESENT"}
            argv = [
                "native_map_build_preflight.py", "--game-root", str(game),
                "--stage", str(stage), "--expected-sha256", digest,
                "--metadata-only",
            ]
            output = io.StringIO()
            with (patch.object(sys, "argv", argv),
                  patch.object(preflight, "probe_native_metadata",
                               return_value=report) as probe,
                  patch.object(preflight, "build_and_stage",
                               side_effect=AssertionError("native build should not run")),
                  redirect_stdout(output)):
                self.assertEqual(0, preflight.main())
            self.assertEqual(1, probe.call_count)
            self.assertEqual(data.resolve(), probe.call_args.args[0])
            self.assertEqual("PINNED_NATIVE_METADATA_VERIFIED",
                             json.loads(output.getvalue())["status"])
            self.assertFalse(stage.exists())

    def test_source_compiler_failure_leaves_no_staged_mod(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            game = root / "game"
            self.synthetic_game(game)
            stage = root / "stage"
            report = {"managed_assemblies": {"sts2.dll": {"sha256": "abc"}}}
            class Failure:
                returncode = 1
            with (patch.object(preflight, "probe_native_metadata",
                               return_value={"status": "NATIVE_METADATA_MEMBERS_PRESENT"}),
                  patch.object(preflight.subprocess, "run", return_value=Failure())):
                with self.assertRaisesRegex(RuntimeError, "compilation failed"):
                    preflight.build_and_stage(game, stage, report)
            self.assertFalse((stage / "mods").exists())

    def test_mocked_native_build_stages_manifest_and_binary_separately(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            data = self.synthetic_game(root / "game")
            stage = root / "stage"
            mock_project = root / "source"
            mock_project.mkdir()
            (mock_project / "mod_manifest.json").write_text(
                json.dumps({"pck_name": "Sts2ReferenceMod"}))
            (mock_project / "Sts2ReferenceMod.json").write_text(
                '{"id":"Sts2ReferenceMod"}')
            binary = root / "Sts2ReferenceMod.dll"
            binary.write_bytes(b"synthetic-compiled-inspector")
            run_args = []
            def fake_dotnet(args, **kwargs):
                run_args.extend(args)
                class Result:
                    returncode = 0
                return Result()
            pin_report = {"managed_assemblies": {
                "sts2.dll": {"sha256": hashlib.sha256(b"fake-sts2.dll").hexdigest()}}}
            with (patch.object(preflight, "MOD_ROOT", mock_project),
                  patch.object(preflight, "DLL_PATH", binary),
                  patch.object(preflight, "probe_native_metadata",
                               return_value={"status": "NATIVE_METADATA_MEMBERS_PRESENT"}),
                  patch.object(preflight.subprocess, "run", side_effect=fake_dotnet)):
                result = preflight.build_and_stage(data, stage, pin_report)
            artifacts = stage / "mods" / "Sts2ReferenceMod"
            self.assertEqual("NATIVE_SOURCE_COMPILED_STAGED_NOT_INSTALLED",
                             result["status"])
            self.assertEqual("NOT_TESTED", result["whole_run_game_parity"])
            self.assertEqual("NATIVE_METADATA_MEMBERS_PRESENT",
                             result["native_metadata_contract"]["status"])
            self.assertIn("-p:Sts2DataDir=" + str(data), run_args)
            self.assertEqual(b"synthetic-compiled-inspector",
                             (artifacts / "Sts2ReferenceMod.dll").read_bytes())
            packed = (artifacts / "Sts2ReferenceMod.pck").read_bytes()
            verify_pack(packed, (mock_project / "mod_manifest.json").read_bytes())
            self.assertFalse((artifacts / "mod_manifest.json").exists())
            self.assertEqual(result["status"], json.loads(
                (stage / "native-map-build-preflight.json").read_text())["status"])


if __name__ == "__main__":
    unittest.main()

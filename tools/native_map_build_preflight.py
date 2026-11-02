#!/usr/bin/env python3
"""Offline native-map inspector build gate for the pinned original v111 DLL.

Verifies the actual game assembly fingerprint, compiles STS2_NATIVE against
that installation, and stages the mod DLL/PCK outside the game. It NEVER
installs into Steam, starts the game, or modifies installed game files.
Source-level private-field reflection and native parity still require a
controlled original-game acceptance run.
"""
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

from make_inspector_pck import create_pack, verify_pack
from native_game_preflight import audit, file_sha256

SOURCE_DLL_SHA256 = (
    "0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9"
)
REPO_ROOT = Path(__file__).resolve().parents[1]
MOD_ROOT = REPO_ROOT / "native" / "Sts2ReferenceMod"
NATIVE_PROJECT = MOD_ROOT / "Sts2ReferenceMod.csproj"
METADATA_PROJECT = REPO_ROOT / "tools" / "NativeV111MetadataProbe" / "NativeV111MetadataProbe.csproj"
DLL_PATH = MOD_ROOT / "bin" / "Release" / "net9.0" / "Sts2ReferenceMod.dll"

# Fail closed if a stale or partial metadata probe accidentally reports
# success without checking the turn-boundary APIs. These are static names,
# not a claim that the native callback occurs at a settled phase boundary.
TURN_CHECKPOINT_METADATA_MEMBERS = frozenset({
    ("MegaCrit.Sts2.Core.Combat.CombatManager", "property", "IsInProgress"),
    ("MegaCrit.Sts2.Core.Combat.CombatManager", "method", "DebugOnlyGetState"),
    ("MegaCrit.Sts2.Core.Combat.CombatManager", "event", "AboutToSwitchToEnemyTurn"),
    ("MegaCrit.Sts2.Core.Combat.CombatManager", "event", "TurnEnded"),
    ("MegaCrit.Sts2.Core.Combat.CombatManager", "event", "TurnStarted"),
    ("MegaCrit.Sts2.Core.Combat.CombatManager", "method", "IsExecutingCardOrPotionEffect"),
    ("MegaCrit.Sts2.Core.Combat.CombatManager", "property", "EndingPlayerTurnPhaseOne"),
    ("MegaCrit.Sts2.Core.Combat.CombatManager", "property", "EndingPlayerTurnPhaseTwo"),
    ("MegaCrit.Sts2.Core.Combat.CombatManager", "property", "IsEnemyTurnStarted"),
    ("MegaCrit.Sts2.Core.Combat.CombatState", "property", "CurrentSide"),
    ("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "event",
     "BeforeActionExecuted"),
    ("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "event",
     "AfterActionExecuted"),
    ("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "property", "IsRunning"),
    ("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "property", "IsPaused"),
    ("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "property", "CurrentlyRunningAction"),
    ("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "method", "FinishedExecutingActions"),
    ("MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSet", "property", "IsEmpty"),
    ("MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSet", "method", "BecameEmpty"),
    ("MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSynchronizer",
     "property", "CombatState"),
    ("MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSynchronizer",
     "field", "_requestedActionsWaitingForPlayerTurn"),
    ("MegaCrit.Sts2.Core.GameActions.ReadyToBeginEnemyTurnAction", "type", ""),
    ("MegaCrit.Sts2.Core.GameActions.EndPlayerTurnAction", "type", ""),
})


def find_data_dir(game_root: Path) -> Path:
    """Accept either the game root or its exact managed data directory."""
    root = game_root.expanduser().resolve()
    choices = (
        root,
        root / "data_sts2_windows_x86_64",
        root / "data_sts2_linuxbsd_x86_64",
    )
    for candidate in choices:
        if (candidate / "sts2.dll").is_file():
            return candidate
    raise ValueError(
        "No sts2.dll in game root or known Windows/Linux game data directory"
    )


def require_separate_stage(game_root: Path, data_dir: Path, stage: Path) -> Path:
    """Reject staging inside the installed game, including symlink aliases."""
    destination = stage.expanduser().resolve()
    for forbidden in (game_root.expanduser().resolve(), data_dir.resolve()):
        if destination == forbidden or forbidden in destination.parents:
            raise ValueError(
                "Stage directory resolves inside the original game installation"
            )
    if (destination == REPO_ROOT
        or destination in REPO_ROOT.parents
        or REPO_ROOT in destination.parents):
        raise ValueError("Stage directory resolves to the repository root or parent")
    return destination


def check_installation(
    game_root: Path, stage: Path, *, expected_sha: str = SOURCE_DLL_SHA256
) -> tuple[Path, Path, dict]:
    """Provenance check with no .NET or game process; fully unit-testable."""
    data = find_data_dir(game_root)
    destination = require_separate_stage(game_root, data, stage)
    report = audit(data)
    if not report["dependencies_present"]:
        missing = [name for name, v in report["managed_assemblies"].items()
                   if not v["present"]]
        raise ValueError("Missing native build dependencies: " + ", ".join(missing))
    actual = report["managed_assemblies"]["sts2.dll"]["sha256"]
    if actual != expected_sha.lower():
        raise ValueError(
            f"Installed sts2.dll SHA-256 differs from pinned v111: "
            f"actual={actual}, expected={expected_sha.lower()}"
        )
    return data, destination, report


def probe_native_metadata(data: Path) -> dict:
    """Inspect the pinned installed PE metadata without launching the game."""
    command = [
        "dotnet", "run", "--project", str(METADATA_PROJECT),
        "-c", "Release", "--verbosity", "quiet",
        "--", str(data / "sts2.dll"),
    ]
    completed = subprocess.run(
        command, cwd=REPO_ROOT, check=False, capture_output=True, text=True)
    if completed.returncode:
        raise RuntimeError(
            "Pinned native assembly metadata contract failed: "
            + ((completed.stdout or "") + "\\n" + (completed.stderr or ""))[:2400])
    try:
        result = json.loads(completed.stdout)
    except (ValueError, TypeError) as error:
        raise RuntimeError(
            "Native metadata probe did not produce valid JSON") from error
    if (result.get("schema") != "sts2-native-metadata-contract-v1"
        or result.get("status") != "NATIVE_METADATA_MEMBERS_PRESENT"
        or result.get("metadata_only") is not True
        or result.get("native_game_executed") is not False
        or result.get("reflection_runtime_parity") != "NOT_TESTED"
        or result.get("managed_assembly_sha256") !=
            file_sha256(data / "sts2.dll")
        or not isinstance(result.get("checks"), list)
        or not result["checks"]
        or not all(isinstance(c, dict) and c.get("present") is True
                   for c in result["checks"])
        or not TURN_CHECKPOINT_METADATA_MEMBERS.issubset({
            (c.get("type"), c.get("kind"), c.get("member"))
            for c in result["checks"] if isinstance(c, dict)
            and c.get("present") is True
        })):
        raise RuntimeError("Native metadata probe returned an invalid or mismatched contract")
    return result


def build_and_stage(data: Path, destination: Path, report: dict) -> dict:
    """Compile against the installed native API, then stage only mod artifacts."""
    # Private reflection names, event declarations and public queue
    # properties must exist in the *installed* managed assembly. This is
    # read-only metadata inspection, not proof of runtime hook correctness.
    metadata = probe_native_metadata(data)
    # No -p:DefineConstants shortcut: Sts2DataDir must resolve the real
    # three managed game DLLs and enable STS2_NATIVE in the existing csproj.
    command = [
        "dotnet", "build", str(NATIVE_PROJECT), "-c", "Release",
        "-p:Sts2DataDir=" + str(data), "--nologo",
    ]
    print("Checking native source API via dotnet build", flush=True)
    completed = subprocess.run(command, cwd=REPO_ROOT, check=False)
    if completed.returncode:
        raise RuntimeError(
            f"Native inspector compilation failed (exit {completed.returncode}); "
            "the installed game was not modified"
        )
    if not DLL_PATH.is_file():
        raise RuntimeError("Native build reported success but produced no inspector DLL")
    manifest = (MOD_ROOT / "mod_manifest.json").read_bytes()
    mod_manifest = json.loads(manifest)
    native_manifest = (MOD_ROOT / "Sts2ReferenceMod.json").read_bytes()
    packed = create_pack(manifest)
    verify_pack(packed, manifest)
    mod_dir = destination / "mods" / "Sts2ReferenceMod"
    mod_dir.mkdir(parents=True, exist_ok=True)
    # The PCK must be named according to mod_manifest.json. Never copy a
    # loose mod_manifest.json, which the native loader misinterprets.
    name = mod_manifest["pck_name"]
    shutil.copy2(DLL_PATH, mod_dir / "Sts2ReferenceMod.dll")
    (mod_dir / (name + ".pck")).write_bytes(packed)
    (mod_dir / "Sts2ReferenceMod.json").write_bytes(native_manifest)

    result = {
        "status": "NATIVE_SOURCE_COMPILED_STAGED_NOT_INSTALLED",
        "original_game": report,
        "native_metadata_contract": metadata,
        "game_executed": False,
        "native_source_build_compiled": True,
        "native_map_private_rng_field_runtime_verified": False,
        "native_map_source_capture": "NOT_TESTED",
        "native_map_rng_alignment": "NOT_TESTED",
        "whole_run_game_parity": "NOT_TESTED",
        "staging_directory": str(mod_dir),
        "artifacts": {
            "mod_dll": {
                "name": "Sts2ReferenceMod.dll",
                "sha256": file_sha256(mod_dir / "Sts2ReferenceMod.dll"),
            },
            "mod_pck": {
                "name": name + ".pck",
                "sha256": file_sha256(mod_dir / (name + ".pck")),
            },
            "mod_manifest": "Sts2ReferenceMod.json",
        },
    }
    report_path = destination / "native-map-build-preflight.json"
    report_path.write_text(json.dumps(result, indent=2) + "\n",
                           encoding="utf-8")
    print("Native inspector staged at " + str(mod_dir))
    print("Preflight report: " + str(report_path))
    print("Original game unmodified; native capture and parity NOT_TESTED")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-root", type=Path, required=True,
                        help="Installed STS2 root, or managed data directory")
    parser.add_argument("--stage", type=Path, required=True,
                        help="Isolated staging directory OUTSIDE the game")
    parser.add_argument("--expected-sha256", default=SOURCE_DLL_SHA256,
                        help="Explicit pinned v111 sts2.dll fingerprint")
    parser.add_argument("--check-only", action="store_true",
                        help="Verify pin/dependencies without compilation")
    parser.add_argument("--metadata-only", action="store_true",
                        help="Verify pin and installed native metadata without compiling or launching game")
    args = parser.parse_args()
    try:
        data, stage, report = check_installation(
            args.game_root, args.stage, expected_sha=args.expected_sha256)
        if args.check_only and args.metadata_only:
            raise ValueError("Select either --check-only or --metadata-only")
        if args.metadata_only:
            metadata = probe_native_metadata(data)
            print(json.dumps({
                "status": "PINNED_NATIVE_METADATA_VERIFIED",
                "native_metadata_contract": metadata,
                "game_executed": False,
                "native_mod_compilation": "NOT_TESTED",
                "game_installation_modified": False,
            }, indent=2))
            return 0
        if args.check_only:
            print(json.dumps({
                "status": "NATIVE_INSTALLATION_PIN_VERIFIED",
                "game_data_directory": str(data),
                "installed_sts2_sha256":
                    report["managed_assemblies"]["sts2.dll"]["sha256"],
                "native_source_compilation": "NOT_TESTED",
                "game_executed": False,
            }, indent=2))
            return 0
        build_and_stage(data, stage, report)
        return 0
    except (ValueError, RuntimeError, OSError, json.JSONDecodeError) as error:
        print("NATIVE_MAP_PREFLIGHT_FAILED: " + str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

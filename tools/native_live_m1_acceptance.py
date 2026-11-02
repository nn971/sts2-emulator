#!/usr/bin/env python3
"""M1 offline acceptance: pair original native v111 source and run coordinator.

Selection uses the native combat run ID, build, seed and ascension captured
by the Inspector, never matching timestamps or importing a post-native state.
Requires an already built local emulator CLI. Does NOT launch the original
game, inject game actions, or declare native settlement certified.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

from native_live_offline_replay import load_live

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = "sts2-native-live-m1-offline-acceptance-v1"


class AcceptanceError(ValueError):
    def __init__(self, status, detail):
        super().__init__(detail)
        self.status = status


def _one(rows, kind: str, *, nested: bool = False):
    selected = [r for r in rows if r.get("type") == kind]
    if len(selected) != 1:
        raise AcceptanceError("BLOCKED_SOURCE_PAIRING",
                              "Expected exactly one " + kind + " source event")
    value = selected[0].get("data") if nested else selected[0]
    if not isinstance(value, dict):
        raise AcceptanceError("BLOCKED_SOURCE_PAIRING",
                              "Invalid source event " + kind)
    return value


def inspect_pair(sidecar: Path, prelude: Path) -> dict:
    if sidecar.with_suffix(sidecar.suffix + ".incomplete").exists() or (
            prelude.with_suffix(prelude.suffix + ".incomplete").exists()):
        raise AcceptanceError("INCOMPLETE_CAPTURE", "Source writer marked incomplete")
    try:
        native = load_live(sidecar)
        start = load_live(prelude)
    except (OSError, ValueError) as error:
        raise AcceptanceError("BLOCKED_SOURCE_PAIRING", str(error)) from error

    header = _one(native, "header", nested=True)
    identity = _one(native, "native_run_identity", nested=True)
    startup = _one(start, "run_started", nested=True)
    link = _one(start, "combat_link", nested=True)
    nterminal = _one(native, "terminal", nested=True)
    pterminal = _one(start, "terminal", nested=True)

    if (not header.get("run_id")
        or header["run_id"] != link.get("combat_run_id")
        or header.get("game_build") != startup.get("game_build")
        or identity.get("seed") != startup.get("seed")
        or identity.get("ascension") != startup.get("ascension")):
        raise AcceptanceError("BLOCKED_SOURCE_PAIRING",
                              "Native combat and run prelude identities disagree")
    if (nterminal.get("status") != "OBSERVED"
        or pterminal.get("status") != "SOURCE_PRELUDE_OBSERVED"):
        raise AcceptanceError("INCOMPLETE_CAPTURE",
                              "Source sidecar or native run prelude did not finish")
    if any(r.get("ordinal") != i for i, r in enumerate(native, 1)):
        raise AcceptanceError("BLOCKED_SOURCE_CHRONOLOGY",
                              "Native sidecar has noncontiguous source ordinals")

    player = [r for r in native if r.get("type") == "native_turn_checkpoint"
              and (r.get("data") or {}).get("kind") == "PLAYER_TURN_START"]
    return {
        "native_source_records": len(native),
        "player_turn_checkpoint_candidates": len(player),
        "canonical_action_starts": sum(
            row.get("type") == "action_started"
            and isinstance(row.get("canonical_action"), dict)
            for row in native),
        "native_pairing": "RUN_ID_BUILD_SEED_ASCENSION_MATCHED",
    }


def discover_pair(root: Path, min_player_turns: int = 6) -> tuple[Path, Path]:
    if not root.is_dir():
        raise AcceptanceError("BLOCKED_SOURCE_PAIRING",
                              "Native trace directory does not exist")
    preludes = list(root.rglob("run-prelude-*.jsonl"))
    sidecars = sorted(root.rglob("passive-*.actions.jsonl"),
                      key=lambda path: path.stat().st_mtime, reverse=True)
    fallback = None
    for sidecar in sidecars:
        for prelude in preludes:
            try:
                evidence = inspect_pair(sidecar, prelude)
            except AcceptanceError:
                continue
            if fallback is None:
                fallback = (sidecar, prelude)
            if evidence["player_turn_checkpoint_candidates"] >= min_player_turns:
                return sidecar, prelude
    if fallback is not None:
        return fallback
    raise AcceptanceError("BLOCKED_SOURCE_PAIRING",
                          "No complete native sidecar and matching run prelude found")


def probe_emulator_jsonl(dotnet: str, cli_dll: Path) -> dict:
    """Expose the actual .NET startup error before launching source replay.

    The source is never supplied to this probe: it sends one hello request to
    the existing JSONL protocol, closes stdin, and checks the negotiated schema.
    """
    request = json.dumps({"op": "hello", "request_id": "m1-preflight"}) + "\n"
    try:
        completed = subprocess.run(
            [dotnet, str(cli_dll), "prototype-ai-jsonl"],
            input=request, capture_output=True, text=True, check=False,
            timeout=15)
    except subprocess.TimeoutExpired:
        return {"status": "BLOCKED_EMULATOR_JSONL_STARTUP",
                "reason": "Emulator JSONL hello timed out after 15 seconds"}
    except OSError as error:
        return {"status": "BLOCKED_EMULATOR_JSONL_STARTUP",
                "reason": type(error).__name__ + ": " + str(error)}
    stderr = completed.stderr.strip()[-2500:]
    lines = completed.stdout.splitlines()
    first = lines[0] if lines else ""
    if completed.returncode != 0 or not first:
        return {"status": "BLOCKED_EMULATOR_JSONL_STARTUP",
                "reason": ("Emulator CLI JSONL hello exited "
                           + str(completed.returncode)
                           + " before a usable response"),
                "exit_code": completed.returncode,
                "emulator_stderr": stderr,
                "emulator_stdout": completed.stdout.strip()[-1000:]}
    try:
        response = json.loads(first)
        if not isinstance(response, dict) or response.get("requestId") != (
                "m1-preflight") or response.get("ok") is not True:
            raise ValueError("JSONL hello returned an invalid envelope")
        if response.get("wireSchemaId") != "prototype-ai-jsonl-v0" or (
                response.get("snapshotSchemaId") != "sts2-exact-snapshot-v1"):
            raise ValueError("Emulator JSONL schema negotiation failed")
    except (ValueError, TypeError) as error:
        return {"status": "BLOCKED_EMULATOR_JSONL_STARTUP",
                "reason": str(error), "exit_code": completed.returncode,
                "emulator_stderr": stderr,
                "emulator_stdout": completed.stdout.strip()[-1000:]}
    return {"status": "EMULATOR_JSONL_HELLO_VERIFIED"}


def acceptance_result(sidecar: Path, prelude: Path, *, dotnet: str,
                      cli_dll: Path, coordinator_report: Path,
                      min_player_turns: int) -> dict:
    evidence = inspect_pair(sidecar, prelude)
    result = {
        "schema": SCHEMA,
        "status": "BLOCKED_EMULATOR_EXECUTABLE_UNAVAILABLE",
        "native_source": "ORIGINAL_NATIVE_SOURCE_OBSERVED",
        "input_sidecar": sidecar.name,
        "input_prelude": prelude.name,
        **evidence,
        "native_live_lockstep": False,
        "native_settlement_certified": False,
        "native_full_state_parity": "NOT_TESTED",
    }
    if not cli_dll.is_file() or shutil.which(dotnet) is None:
        result["reason"] = "Build the .NET 9 emulator CLI before offline acceptance"
        return result
    diagnostic = probe_emulator_jsonl(dotnet, cli_dll)
    result["emulator_startup"] = diagnostic
    if diagnostic["status"] != "EMULATOR_JSONL_HELLO_VERIFIED":
        result["status"] = diagnostic["status"]
        result["reason"] = diagnostic["reason"]
        return result
    command = [
        sys.executable,
        str(ROOT / "tools/native_live_fidelity_coordinator.py"),
        "--sidecar", str(sidecar), "--prelude", str(prelude),
        "--dotnet", dotnet, "--cli-dll", str(cli_dll),
        "--out", str(coordinator_report),
    ]
    coordinator_report.unlink(missing_ok=True)
    completed = subprocess.run(command, text=True, capture_output=True,
                               check=False)
    if not coordinator_report.is_file():
        result.update(status="BLOCKED_EMULATOR_COORDINATOR",
                      reason="Coordinator produced no report",
                      coordinator_stderr=completed.stderr.strip()[-2500:],
                      coordinator_stdout=completed.stdout.strip()[-1000:],
                      return_code=completed.returncode)
        return result
    try:
        report = json.loads(coordinator_report.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        result.update(status="BLOCKED_EMULATOR_COORDINATOR",
                      reason="Invalid coordinator report: " + str(error),
                      coordinator_stderr=completed.stderr.strip()[-2500:],
                      return_code=completed.returncode)
        return result
    result["coordinator_status"] = report.get("status")
    result["player_turns_matched"] = report.get("player_checkpoints_matched", 0)
    result["emulator_actions_stepped"] = report.get("emulator_actions_stepped", 0)
    result["first_difference"] = (None if report.get("first_difference_path") is None
                                  else {"path": report["first_difference_path"],
                                        "reason": report.get("first_difference_reason")})
    if (completed.returncode == 0
        and result["coordinator_status"] == "SOURCE_TERMINAL_OBSERVED_UNCERTIFIED"
        and result["player_turns_matched"] >= min_player_turns
        and result["first_difference"] is None):
        result["status"] = "M1_SIX_TURN_OFFLINE_AGREEMENT_UNCERTIFIED"
        result["reason"] = "Independent native-prelude bootstrapped emulator projection agreed"
    else:
        result["status"] = "BLOCKED_M1_OFFLINE_ACCEPTANCE"
        result["reason"] = report.get("reason", "Emulator replay did not meet acceptance gate")
        result["return_code"] = completed.returncode
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--trace-dir", type=Path, default=Path(
        os.environ.get("STS2_FIDELITY_TRACE_DIR",
                       "/mnt/d/SteamLibrary/sts2-inspector-traces")))
    parser.add_argument("--sidecar", type=Path)
    parser.add_argument("--prelude", type=Path)
    parser.add_argument("--out", type=Path, default=Path(
        "/tmp/native-live-m1-acceptance.json"))
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--cli-dll", type=Path, default=(
        ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"))
    parser.add_argument("--min-player-turns", type=int, default=6)
    args = parser.parse_args()
    if (args.sidecar is None) != (args.prelude is None):
        parser.error("Specify both --sidecar and --prelude, or neither")
    if args.min_player_turns < 1:
        parser.error("--min-player-turns must be positive")
    try:
        sidecar, prelude = ((args.sidecar, args.prelude)
                            if args.sidecar else discover_pair(args.trace_dir,
                                                             args.min_player_turns))
        report = acceptance_result(sidecar, prelude, dotnet=args.dotnet,
            cli_dll=args.cli_dll, coordinator_report=args.out.with_name(
                args.out.stem + ".coordinator.json"),
            min_player_turns=args.min_player_turns)
    except AcceptanceError as error:
        report = {"schema": SCHEMA, "status": error.status, "reason": str(error),
                  "native_live_lockstep": False, "native_settlement_certified": False}
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n",
                        encoding="utf-8")
    print(json.dumps(report, sort_keys=True))
    return 0 if report["status"] == "M1_SIX_TURN_OFFLINE_AGREEMENT_UNCERTIFIED" else 2


if __name__ == "__main__":
    raise SystemExit(main())

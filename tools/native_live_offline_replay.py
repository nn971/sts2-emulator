#!/usr/bin/env python3
"""Offline rehearsal of the native fidelity SSE -> persistent-emulator pipeline.

Accept either a real `*.fidelity.jsonl` live journal or a recorded native
`*.actions.jsonl` sidecar, converted through the exact live envelope schema.
Validate content fingerprints, epochs, sequence continuity and duplicate
replays before regenerating the existing source-only action journal. Optional
--prelude runs the already audited independent emulator replay; no original
native snapshot is imported.

This is offline transport validation, not original-game live lockstep parity.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path

from native_lockstep_source_journal import build as build_source_journal

SCHEMA = "sts2-native-live-event-v1"
CHANNEL = "fidelity"


class LiveEventError(ValueError):
    """A stream mismatch that prevents trustworthy emulator replay."""

    def __init__(self, status: str, detail: str):
        super().__init__(detail)
        self.status = status


class _LexicalFloat(float):
    """Remember the JSON number's exact lexical spelling during SSE parsing."""

    def __new__(cls, source: str):
        value = super().__new__(cls, source)
        value.source_lexeme = source
        return value


def _dump_json(value: object, *, sort_keys: bool) -> str:
    """Match .NET Utf8JsonWriter's canonical JSON, including numeric lexemes.

    System.Text.Json's JsonElement.WriteTo preserves uppercase E exponents
    from source-native RNG draws. Ordinary json.loads/json.dumps changes them.
    Retaining raw float tokens also makes re-archived SSE events verifiable.
    """
    if isinstance(value, dict):
        keys = sorted(value) if sort_keys else value
        return "{" + ",".join(
            _dump_json(key, sort_keys=sort_keys) + ":" +
            _dump_json(value[key], sort_keys=sort_keys) for key in keys
        ) + "}"
    if isinstance(value, list):
        return "[" + ",".join(_dump_json(x, sort_keys=sort_keys)
                              for x in value) + "]"
    if isinstance(value, str):
        # .NET's default JavaScriptEncoder escapes HTML-sensitive characters.
        encoded = json.dumps(value, ensure_ascii=True)
        # Default Utf8JsonWriter hex-escapes quotation marks in values.
        # Python json.dumps uses a short \" escape for the same character.
        encoded = encoded.replace(r'\"', r'\u0022')
        for character, escape in (("<", "\\u003C"), (">", "\\u003E"),
                                  ("&", "\\u0026"), ("'", "\\u0027"),
                                  ("+", "\\u002B"), ("`", "\\u0060")):
            encoded = encoded.replace(character, escape)
        return encoded
    if isinstance(value, _LexicalFloat):
        return value.source_lexeme
    if isinstance(value, (int, float)) or value is None or (
            isinstance(value, bool)):
        return json.dumps(value, separators=(",", ":"), allow_nan=False)
    raise ValueError("Non-JSON type in native fidelity envelope")


def _digest(payload: object) -> str:
    canonical = _dump_json(payload, sort_keys=True)
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()


def _envelope(run_id: str, seq: int, kind: str, payload: dict,
              source_ordinal: int | None = None) -> dict:
    return {
        "schema": SCHEMA, "channel": CHANNEL, "run_id": run_id,
        "session_epoch": 1, "sequence": seq, "kind": kind,
        "decision_id": None, "source_ordinal": source_ordinal,
        "content_fingerprint": _digest(payload),
        "payload": payload,
    }


def simulate_sidecar(records: list[dict]) -> list[dict]:
    """Build an in-order simulated fidelity stream from a native sidecar."""
    if not records or records[0].get("type") != "header":
        raise LiveEventError("INVALID_SOURCE_HEADER", "Missing native sidecar header")
    run_id = (records[0].get("data") or {}).get("run_id")
    if not isinstance(run_id, str) or not run_id:
        raise LiveEventError("INVALID_SOURCE_RUN", "Header lacks a native run ID")
    events = [_envelope(run_id, 1, "session_started",
                        {"status": "NATIVE_COMBAT_ATTACHED",
                         "source_settlement": "UNVERIFIED"})]
    for record in records:
        ordinal = record.get("ordinal")
        kind = record.get("type")
        if type(ordinal) is not int or ordinal != len(events) or not isinstance(kind, str):
            raise LiveEventError("INVALID_SOURCE_RECORD",
                                 "Native sidecar ordinal is invalid or noncontiguous")
        events.append(_envelope(run_id, len(events) + 1, kind,
                                record, ordinal))
    return events


def recover_sidecar(events: list[dict]) -> list[dict]:
    """Idempotent stream consumption; conflicting duplicates fail closed.

    Every duplicate must reproduce the identical event, including payload and
    fingerprint. Missing sequences, wrong epochs or changed run IDs block.
    """
    if not events:
        raise LiveEventError("EMPTY_LIVE_STREAM", "No fidelity events")
    records: list[dict] = []
    seen: dict[int, dict] = {}
    run_id: str | None = None
    epoch: int | None = None
    expected = 1
    source_ordinal = 0
    for row in events:
        if not isinstance(row, dict) or row.get("schema") != SCHEMA or (
                row.get("channel") != CHANNEL):
            raise LiveEventError("INVALID_LIVE_ENVELOPE",
                                 "Unsupported channel or event schema")
        seq = row.get("sequence")
        if type(seq) is not int or seq < 1:
            raise LiveEventError("INVALID_LIVE_SEQUENCE", "Invalid event sequence")
        if seq in seen:
            if row != seen[seq]:
                raise LiveEventError("CONFLICTING_DUPLICATE_EVENT",
                                     f"Sequence {seq} has different evidence")
            continue
        if seq != expected:
            raise LiveEventError("MISSING_LIVE_EVENT",
                                 f"Expected event {expected}, received {seq}")
        if not isinstance(row.get("run_id"), str) or not row["run_id"]:
            raise LiveEventError("INVALID_LIVE_RUN", "Missing native run identity")
        if type(row.get("session_epoch")) is not int or row["session_epoch"] < 1:
            raise LiveEventError("INVALID_LIVE_EPOCH", "Invalid session epoch")
        payload = row.get("payload")
        if not isinstance(payload, dict):
            raise LiveEventError("INVALID_LIVE_PAYLOAD",
                                 f"Event {seq} has no object payload")
        if row.get("content_fingerprint") != _digest(payload):
            raise LiveEventError("LIVE_FINGERPRINT_MISMATCH",
                                 f"Event {seq} failed content verification")
        if expected == 1:
            if row.get("kind") != "session_started" or (
                    row.get("source_ordinal") is not None):
                raise LiveEventError("MISSING_LIVE_SESSION_START",
                                     "First event must start a native session")
            run_id = row["run_id"]
            epoch = row["session_epoch"]
        else:
            if row["run_id"] != run_id or row["session_epoch"] != epoch:
                raise LiveEventError("LIVE_SESSION_CHANGED",
                                     "Unpaired native session or epoch")
            if row.get("kind") != payload.get("type"):
                raise LiveEventError("LIVE_SOURCE_KIND_MISMATCH",
                                     f"Event {seq} has mismatched source type")
            ordinal = row.get("source_ordinal")
            if type(ordinal) is not int or ordinal != source_ordinal + 1 or (
                    payload.get("ordinal") != ordinal):
                raise LiveEventError("LIVE_SOURCE_ORDINAL_MISMATCH",
                                     f"Event {seq} breaks source ordering")
            source_ordinal = ordinal
            records.append(payload)
        seen[seq] = row
        expected += 1
    if not records or records[0].get("type") != "header":
        raise LiveEventError("MISSING_SOURCE_HEADER", "Stream lacks source header")
    source_run = (records[0].get("data") or {}).get("run_id")
    if source_run != run_id:
        raise LiveEventError("LIVE_SOURCE_RUN_MISMATCH",
                             "Source header and envelope run IDs disagree")
    if records[-1].get("type") != "terminal":
        raise LiveEventError("INCOMPLETE_LIVE_CAPTURE",
                             "No native terminal marker yet")
    return records


def load_live(path: Path) -> list[dict]:
    try:
        rows = [json.loads(line, parse_float=_LexicalFloat)
                for line in path.read_text(encoding="utf-8").splitlines()
                if line.strip()]
        if path.with_suffix(path.suffix + ".incomplete").exists():
            raise ValueError("Live journal writer reported dropped records")
        if any(not isinstance(row, dict) for row in rows):
            raise ValueError("Native live journal has non-object row")
        return rows
    except (OSError, ValueError) as error:
        raise LiveEventError("LIVE_JOURNAL_READ_FAILED", str(error)) from error


def write_jsonl(path: Path, rows: list[dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as output:
        for row in rows:
            output.write(_dump_json(row, sort_keys=False) + "\n")


def prepare(events: list[dict], out_dir: Path) -> dict:
    records = recover_sidecar(events)
    source = build_source_journal(records)
    out_dir.mkdir(parents=True, exist_ok=True)
    sidecar_path = out_dir / "reconstructed.actions.jsonl"
    journal_path = out_dir / "source-action-journal.json"
    events_path = out_dir / "validated.fidelity.jsonl"
    write_jsonl(sidecar_path, records)
    write_jsonl(events_path, events)
    journal_path.write_text(json.dumps(source, indent=2, sort_keys=True) + "\n",
                            encoding="utf-8")
    return {
        "status": "OFFLINE_LIVE_EVENT_REPLAY_PREPARED",
        "live_stream_status": "CONTIGUOUS_FINGERPRINT_VERIFIED",
        "native_source_journal_status": source["status"],
        "source_events": len(records),
        "session_epoch": events[0]["session_epoch"],
        "source_run_id": source["run_id"],
        "reconstructed_sidecar": str(sidecar_path),
        "source_journal": str(journal_path),
        "fidelity_events": str(events_path),
        "native_live_lockstep": False,
        "native_settlement_certified": False,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--sidecar", type=Path,
                        help="Simulate transport from existing native actions JSONL")
    source.add_argument("--fidelity-events", type=Path,
                        help="Consume a recorded live fidelity event JSONL")
    parser.add_argument("--prelude", type=Path,
                        help="Independently initialize a v111 emulator and replay")
    parser.add_argument("--out-dir", type=Path, required=True)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--cli-dll", type=Path)
    args = parser.parse_args()
    try:
        if args.sidecar:
            records = load_live(args.sidecar)
            events = simulate_sidecar(records)
        else:
            events = load_live(args.fidelity_events)
        result = prepare(events, args.out_dir)
        if args.prelude is not None:
            replay_path = Path(__file__).resolve().with_name(
                "native_turn_checkpoint_replay.py")
            report_path = args.out_dir / "independent-emulator-replay.json"
            cmd = [sys.executable, str(replay_path),
                   "--journal", result["source_journal"],
                   "--sidecar", result["reconstructed_sidecar"],
                   "--prelude", str(args.prelude), "--out", str(report_path),
                   "--dotnet", args.dotnet]
            if args.cli_dll is not None:
                cmd.extend(["--cli-dll", str(args.cli_dll)])
            completed = subprocess.run(cmd, capture_output=True,
                                       text=True, check=False)
            result["emulator_process_returncode"] = completed.returncode
            result["emulator_replay_report"] = str(report_path)
            if report_path.exists():
                emulator = json.loads(report_path.read_text(encoding="utf-8"))
                result["emulator_replay_status"] = emulator["status"]
                result["player_turns_matched"] = len(
                    emulator.get("player_checkpoints", []))
                result["enemy_turns_deferred"] = len(
                    emulator.get("enemy_checkpoint_candidates", []))
            else:
                result["emulator_replay_status"] = "EMULATOR_PROCESS_FAILED"
                result["emulator_stderr"] = completed.stderr[-2000:]
        print(json.dumps(result, indent=2, sort_keys=True))
        source_ok = result["native_source_journal_status"] in (
            "SOURCE_ACTION_CHRONOLOGY_OBSERVED",
            "SOURCE_ACTION_CHRONOLOGY_HISTORY_RESET_UNRESOLVED")
        emulator_ok = (args.prelude is None or result.get(
            "emulator_replay_status") ==
            "SOURCE_PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED")
        return 0 if source_ok and emulator_ok else 2
    except (LiveEventError, OSError, ValueError) as exc:
        print(json.dumps({
            "status": exc.status if isinstance(exc, LiveEventError)
            else "OFFLINE_LIVE_EVENT_REPLAY_FAILED",
            "reason": str(exc),
            "native_live_lockstep": False
        }, indent=2, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

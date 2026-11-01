#!/usr/bin/env python3
"""Persistent native-fidelity SSE consumer and independently bootstrapped emulator.

The source is authoritative only for *observations*. The emulator starts from
an audited native run prelude and advances through its own legal actions.
Player-turn comparisons are provisional until native settlement is verified.

This runner is read-only with respect to native gameplay. Its only POST sends
diagnostic results to the separate fidelity endpoint, never player actions.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings" / "python"))

from native_action_envelope_audit import load as load_sidecar
from native_live_offline_replay import SCHEMA, CHANNEL, _digest, simulate_sidecar
from native_lockstep_source_journal import build as build_source_journal
from native_lockstep_shadow import _resolve
from native_run_bootstrap import run_bootstrap
from native_run_prelude_audit import load as load_prelude, audit as audit_prelude
from native_turn_checkpoint_replay import (
    check_prelude_pair, derive_crosswalk, first_difference,
    _selected_choice, replay,
)

RESULT_SCHEMA = "sts2-native-fidelity-result-v1"
ACCEPTED_STATUSES = frozenset((
    "AWAITING_SOURCE_PRELUDE", "AWAITING_FIRST_PLAYER_CHECKPOINT",
    "EMULATOR_BOOTSTRAPPED_UNCERTIFIED",
    "PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED",
    "SOURCE_PLAYER_CHECKPOINT_DIFFERENCE_UNCERTIFIED",
    "BLOCKED_NATIVE_PRELUDE_PAIRING", "BLOCKED_EMULATOR_BOOTSTRAP",
    "BLOCKED_SOURCE_CHRONOLOGY", "BLOCKED_EMULATOR_LEGAL_ACTION",
    "BLOCKED_EMULATOR_STEP", "BLOCKED_NESTED_SOURCE_CHOICE",
    "BLOCKED_SOURCE_AUDIT", "INCOMPLETE_CAPTURE",
    "BLOCKED_STREAM_INTEGRITY", "EMULATOR_COORDINATOR_ERROR",
    "SOURCE_TERMINAL_OBSERVED_UNCERTIFIED",
))


class LiveFidelityError(ValueError):
    def __init__(self, status: str, reason: str):
        super().__init__(reason)
        self.status = status


class FidelityEventCursor:
    """Validate one append-only fidelity session, including reconnections."""

    def __init__(self):
        self.sequence = 0
        self.epoch = None
        self.run_id = None
        self.source_ordinal = 0
        self.seen: dict[int, dict] = {}
        self.closed = False

    def accept(self, row: dict) -> dict | None:
        if not isinstance(row, dict) or row.get("schema") != SCHEMA or (
                row.get("channel") != CHANNEL):
            raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY",
                                    "Unexpected source event schema or channel")
        seq = row.get("sequence")
        if type(seq) is not int or seq < 1:
            raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY", "Invalid SSE sequence")
        if seq in self.seen:
            if self.seen[seq] != row:
                raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY",
                                        "Conflicting duplicate source sequence")
            return None
        if seq != self.sequence + 1:
            raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY",
                                    "Missing source stream sequence")
        if not isinstance(row.get("run_id"), str) or not row["run_id"] or (
                type(row.get("session_epoch")) is not int
                or row["session_epoch"] < 1):
            raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY",
                                    "Invalid native session identity")
        payload = row.get("payload")
        if not isinstance(payload, dict) or (
                row.get("content_fingerprint") != _digest(payload)):
            raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY",
                                    "Native payload fingerprint mismatch")
        if self.sequence == 0:
            if row.get("kind") != "session_started" or (
                    row.get("source_ordinal") is not None):
                raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY",
                                        "Expected native session_started first")
            self.run_id = row["run_id"]
            self.epoch = row["session_epoch"]
        else:
            if self.closed or row["run_id"] != self.run_id or (
                    row["session_epoch"] != self.epoch):
                raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY",
                                        "Native session changed without rebootstrap")
            ordinal = row.get("source_ordinal")
            if row.get("kind") != payload.get("type") or (
                    type(ordinal) is not int or ordinal != self.source_ordinal + 1
                    or payload.get("ordinal") != ordinal):
                raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY",
                                        "Native source record is missing or reordered")
            self.source_ordinal = ordinal
            if row["kind"] == "terminal":
                self.closed = True
        self.sequence = seq
        self.seen[seq] = row
        return None if seq == 1 else payload


class IncrementalFidelityCoordinator:
    """One persistent emulator process, with no native-state injection."""

    def __init__(self, session, prelude: dict, *, bootstrap=run_bootstrap):
        self.session = session
        self.prelude = prelude
        self.bootstrap = bootstrap
        self.cursor = FidelityEventCursor()
        self.records: list[dict] = []
        self.initial_snapshot: str | None = None
        self.state = None
        self.crosswalk = None
        self.pending: dict[int, dict] = {}
        self.pending_order: list[int] = []
        self.choices: list[dict] = []
        self.matched: list[dict] = []
        self.deferred: list[dict] = []
        self.stepped = 0
        self.status = "AWAITING_SOURCE_PRELUDE"
        self.reason = "Awaiting native run identity and first combat prelude link"
        self.first_difference: dict | None = None
        self.stopped = False
        self._last_fingerprint = None

    def _fail(self, status: str, reason: str, first: dict | None = None):
        self.status, self.reason, self.first_difference = status, reason, first
        self.stopped = True

    def _receipt(self) -> dict:
        diff = self.first_difference or {}
        detail = diff.get("difference") if isinstance(diff.get("difference"), dict) else diff
        return {
            "schema": RESULT_SCHEMA,
            "run_id": self.cursor.run_id,
            "session_epoch": self.cursor.epoch,
            "through_sequence": self.cursor.sequence,
            "status": self.status,
            "reason": self.reason[:300],
            "player_checkpoints_matched": len(self.matched),
            "enemy_checkpoints_deferred": len(self.deferred),
            "emulator_actions_stepped": self.stepped,
            "first_difference_path": (detail.get("path") or None) if detail else None,
            "first_difference_reason": (detail.get("reason") or None) if detail else None,
        }

    def _maybe_receipt(self) -> dict | None:
        if not self.cursor.sequence:
            return None
        key = (self.status, self.reason, len(self.matched),
               len(self.deferred), self.stepped, json.dumps(
                   self.first_difference, sort_keys=True))
        if key == self._last_fingerprint:
            return None
        self._last_fingerprint = key
        return self._receipt()

    def consume(self, row: dict) -> dict | None:
        try:
            record = self.cursor.accept(row)
        except LiveFidelityError as err:
            self._fail(err.status, str(err))
            return self._maybe_receipt()
        if record is None:
            return None  # Session marker or idempotent reconnect duplicate.
        self.records.append(record)
        if self.stopped:
            return None
        try:
            self._process(record)
        except (KeyError, TypeError, ValueError, RuntimeError) as err:
            self._fail("EMULATOR_COORDINATOR_ERROR",
                       type(err).__name__ + ": " + str(err))
        return self._maybe_receipt()

    def _identity(self) -> dict | None:
        if not self.records or self.records[0].get("type") != "header":
            return None
        run = next((r.get("data") for r in self.records
                    if r.get("type") == "native_run_identity"), None)
        if not isinstance(run, dict):
            return None
        header = self.records[0].get("data") or {}
        return {
            "run_id": header.get("run_id"),
            "game_build": header.get("game_build"),
            "run_seed": run.get("seed"),
            "ascension": run.get("ascension"),
        }

    def _bootstrap_if_ready(self):
        if self.initial_snapshot is not None or self.stopped:
            return
        identity = self._identity()
        if identity is None:
            return
        if identity["run_id"] != self.cursor.run_id:
            self._fail("BLOCKED_NATIVE_PRELUDE_PAIRING",
                       "Native action header and SSE run identity disagree")
            return
        pairing = check_prelude_pair(identity, self.prelude)
        if pairing is not None:
            self._fail("BLOCKED_NATIVE_PRELUDE_PAIRING", pairing)
            return
        report, snapshot = self.bootstrap(self.prelude, self.session)
        if snapshot is None or (
                report.get("status") != "CANDIDATE_COMBAT_REACHED_UNCERTIFIED"):
            self._fail("BLOCKED_EMULATOR_BOOTSTRAP",
                       report.get("status", "EMULATOR_BOOTSTRAP_FAILED") + ": " +
                       report.get("reason", "No independently reached combat"),
                       report.get("first_difference_or_blocker"))
            return
        exact = json.loads(snapshot)
        if exact.get("schema") != "sts2-exact-snapshot-v1" or (
                exact.get("state", {}).get("run_seed") != identity["run_seed"]
                or exact["state"].get("ascension") != identity["ascension"]):
            self._fail("BLOCKED_EMULATOR_BOOTSTRAP",
                       "Independent emulator combat identity differs")
            return
        self.initial_snapshot = snapshot
        self.state = self.session.load_snapshot(snapshot)
        self.status = "EMULATOR_BOOTSTRAPPED_UNCERTIFIED"
        self.reason = "Independent seed/Neow/map startup; waiting for player turn 1"

    def _compare(self, source: dict, ordinal: int, turn: int):
        if self.initial_snapshot is None:
            self._fail("BLOCKED_EMULATOR_BOOTSTRAP",
                       "Native checkpoint arrived before independent startup")
            return
        if self.pending_order:
            self._fail("BLOCKED_SOURCE_CHRONOLOGY",
                       "Player checkpoint intersects an unfinished native canonical action")
            return
        exact = json.loads(self.state.save_snapshot())
        if self.crosswalk is None:
            if turn != 1 or self.matched:
                self._fail("BLOCKED_SOURCE_CHRONOLOGY",
                           "First source player checkpoint is not turn 1")
                return
            try:
                self.crosswalk = derive_crosswalk(source, exact)
            except (KeyError, TypeError, ValueError) as err:
                self._fail("BLOCKED_EMULATOR_BOOTSTRAP", str(err))
                return
        difference = first_difference(source, exact, self.crosswalk)
        if difference is not None:
            self._fail("SOURCE_PLAYER_CHECKPOINT_DIFFERENCE_UNCERTIFIED",
                       "First projected native/emulator player-turn difference",
                       {"turn": turn, "ordinal": ordinal, "difference": difference})
            return
        self.matched.append({
            "turn": turn, "source_ordinal": ordinal,
            "comparison": "PROJECTED_MECHANICS_AGREE",
            "native_settlement": "SETTLEMENT_UNVERIFIED",
            "rng_comparison": "PENDING_SOURCE_BRACKET",
        })
        self.status = "PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED"
        self.reason = "Independent projected player-turn states agree; settlement unverified"

    def _advance_completed(self):
        while self.pending_order and self.crosswalk is not None:
            aid = self.pending_order[0]
            command = self.pending[aid]
            if not command.get("complete"):
                return
            self.pending_order.pop(0)
            self.pending.pop(aid)
            canonical = command["action"]
            action_id, problem = _resolve(canonical, self.state.actions(),
                                          self.crosswalk["cards"],
                                          self.crosswalk["enemies"])
            if problem:
                self._fail("BLOCKED_EMULATOR_LEGAL_ACTION", problem, {
                    "path": "$.legal_actions", "reason": problem})
                return
            self.state = self.state.step(action_id)
            self.stepped += 1
            matching = [x for x in self.choices
                        if x.get("containing_executor_action_id") == aid]
            if matching:
                selected, error = _selected_choice(self.choices, aid,
                                                    self.crosswalk, self.state.actions())
                if error:
                    self._fail("BLOCKED_NESTED_SOURCE_CHOICE", error, {
                        "path": "$.nested_choice", "reason": error})
                    return
                self.state = self.state.step(selected)
                self.stepped += 1
                self.choices = [x for x in self.choices if x not in matching]

    def _process(self, record: dict):
        kind = record.get("type")
        if kind == "observation_failure" or kind == "anomaly":
            self._fail("BLOCKED_SOURCE_CHRONOLOGY",
                       "Native capture reports observation failure/anomaly")
            return
        self._bootstrap_if_ready()
        if self.stopped:
            return
        if kind == "native_turn_checkpoint":
            data = record.get("data") or {}
            if data.get("settlement_status") != "SETTLEMENT_UNVERIFIED":
                self._fail("BLOCKED_SOURCE_CHRONOLOGY",
                           "Unknown native checkpoint settlement contract")
                return
            if data.get("kind") == "ENEMY_TURN_START":
                self.deferred.append({"turn": data.get("turn"),
                    "source_ordinal": record["ordinal"],
                    "emulator_comparison": "DEFERRED_NO_PRE_ENEMY_EFFECT_BOUNDARY"})
            elif data.get("kind") == "PLAYER_TURN_START":
                if self.crosswalk is None and self.pending_order:
                    self._fail("BLOCKED_SOURCE_CHRONOLOGY",
                               "Player action started before first player checkpoint")
                else:
                    self._compare(record.get("state"), record["ordinal"], data["turn"])
            else:
                self._fail("BLOCKED_SOURCE_CHRONOLOGY", "Unknown turn checkpoint kind")
        elif kind == "action_started" and isinstance(record.get("canonical_action"), dict):
            aid = record.get("action_id")
            if type(aid) is not int or aid in self.pending or self.crosswalk is None:
                self._fail("BLOCKED_SOURCE_CHRONOLOGY",
                           "Unpaired or premature canonical action")
                return
            self.pending[aid] = {"action": record["canonical_action"],
                                 "start": record["ordinal"], "complete": False}
            self.pending_order.append(aid)
        elif kind == "native_player_choice":
            data = record.get("data") or {}
            if type(record.get("action_id")) is int:
                self.choices.append({
                    "containing_executor_action_id": record["action_id"],
                    **data})
        elif kind == "action_completed":
            aid = record.get("action_id")
            if aid in self.pending:
                self.pending[aid]["complete"] = True
                self._advance_completed()
        elif kind == "terminal":
            if self.pending_order:
                self._fail("BLOCKED_SOURCE_CHRONOLOGY",
                           "Native terminal has outstanding canonical actions")
                return
            if (record.get("data") or {}).get("status") != "OBSERVED":
                self._fail("INCOMPLETE_CAPTURE", "Native terminal reports incomplete capture")
                return
            source = build_source_journal(self.records)
            if source.get("status") != "SOURCE_ACTION_CHRONOLOGY_OBSERVED":
                self._fail("BLOCKED_SOURCE_AUDIT",
                           "Final native action chronology: " + source.get(
                               "status", "UNKNOWN"))
                return
            if not self.matched:
                self._fail("BLOCKED_SOURCE_CHRONOLOGY",
                           "No projected player-turn checkpoint was comparable")
                return
            final = replay(source, self.initial_snapshot, self.session, self.records)
            if final["status"] != (
                    "SOURCE_PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED"):
                self._fail("BLOCKED_SOURCE_AUDIT",
                           "Final independent replay: " + final["status"],
                           final.get("first_difference"))
                return
            if len(final.get("player_checkpoints", [])) != len(self.matched):
                self._fail("BLOCKED_SOURCE_AUDIT",
                           "Incremental and final replay checkpoint counts differ")
                return
            self.status = "SOURCE_TERMINAL_OBSERVED_UNCERTIFIED"
            self.reason = (
                "Independent full-source replay agrees at observed player turns; "
                "native settlement and complete state parity uncertified")
            self.stopped = True


def parse_sse(stream):
    """Yield verified-format SSE event dictionaries, ignoring heartbeats."""
    event_id = None
    event_type = None
    data: list[str] = []
    for raw in stream:
        if isinstance(raw, bytes):
            raw = raw.decode("utf-8", errors="strict")
        line = raw.rstrip("\r\n")
        if len(line) > 2_000_000:
            raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY", "Oversized SSE field")
        if not line:
            if data:
                if event_type != "native" or event_id is None:
                    raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY",
                                            "Unrecognized SSE data frame")
                value = json.loads("\n".join(data))
                if value.get("sequence") != int(event_id):
                    raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY",
                                            "SSE ID differs from envelope sequence")
                yield value
            event_id, event_type, data = None, None, []
        elif line.startswith(":"):
            continue
        elif line.startswith("id:"):
            event_id = line[3:].strip()
            if not event_id.isdecimal():
                raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY", "Bad SSE ID")
        elif line.startswith("event:"):
            event_type = line[6:].strip()
        elif line.startswith("data:"):
            data.append(line[5:].lstrip(" "))
    if data:
        raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY", "Partial SSE frame on disconnect")


def _loopback_base(value: str) -> str:
    parts = urllib.parse.urlsplit(value)
    if parts.scheme != "http" or parts.hostname not in (
            "localhost", "127.0.0.1", "::1") or parts.username or (
            parts.password) or parts.query or parts.fragment or (
            parts.path not in ("", "/")):
        raise ValueError("Fidelity bridge must be a loopback HTTP origin")
    if parts.port is None:
        raise ValueError("Fidelity bridge URL requires an explicit port")
    return value.rstrip("/")


def post_feedback(base: str, token: str, receipt: dict, timeout: int = 10):
    body = json.dumps(receipt, separators=(",", ":")).encode("utf-8")
    request = urllib.request.Request(
        base + "/v1/fidelity/result", data=body, method="POST",
        headers={"Authorization": "Bearer " + token,
                 "Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        if response.status != 200:
            raise RuntimeError("Fidelity feedback endpoint rejected report")


def follow(base: str, token: str, coordinator: IncrementalFidelityCoordinator,
           *, max_reconnects: int = 3):
    """Reconnect from the last verified cursor; refusal/gaps stop replay."""
    cursor = coordinator.cursor
    reconnects = 0
    while not coordinator.stopped:
        request = urllib.request.Request(
            base + "/v1/fidelity/stream",
            headers={"Authorization": "Bearer " + token,
                     "Last-Event-ID": str(cursor.sequence)})
        try:
            with urllib.request.urlopen(request, timeout=20) as response:
                reconnects = 0
                for row in parse_sse(response):
                    report = coordinator.consume(row)
                    if report is not None:
                        post_feedback(base, token, report)
                    if coordinator.stopped:
                        return
        except (TimeoutError, OSError, urllib.error.URLError, EOFError):
            reconnects += 1
            if reconnects > max_reconnects:
                raise LiveFidelityError("EMULATOR_COORDINATOR_ERROR",
                                        "SSE disconnect/reconnect limit reached")
            time.sleep(min(reconnects, 3))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prelude", required=True, type=Path,
                        help="Native audited RunStarted JSONL from the same run")
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--follow", action="store_true",
                        help="Consume the active native fidelity SSE stream")
    source.add_argument("--fidelity-events", type=Path,
                        help="Replay saved v1 fidelity envelopes offline")
    source.add_argument("--sidecar", type=Path,
                        help="Simulate live stream from a recorded native sidecar")
    parser.add_argument("--bridge", default="http://127.0.0.1:39221")
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--cli-dll", type=Path, default=(
        ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"))
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    try:
        records, error = load_prelude(args.prelude)
        native_prelude = audit_prelude(records, error)
        if native_prelude.get("status") != "RUN_PRELUDE_SOURCE_OBSERVED":
            raise LiveFidelityError("BLOCKED_NATIVE_PRELUDE_PAIRING",
                                    "Native prelude is incomplete or unaudited")
        from sts2_emulator import Session
        with Session([args.dotnet, str(args.cli_dll), "prototype-ai-jsonl"]) as session:
            live = IncrementalFidelityCoordinator(session, native_prelude)
            if args.follow:
                base = _loopback_base(args.bridge)
                token = os.environ.get("STS2_INSPECTOR_FIDELITY_TOKEN", "")
                if len(token) < 24:
                    raise ValueError("STS2_INSPECTOR_FIDELITY_TOKEN required (24+ chars)")
                follow(base, token, live)
            else:
                if args.sidecar:
                    source_rows, source_error = load_sidecar(args.sidecar)
                    if source_error is not None:
                        raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY",
                                                source_error)
                    envelope_rows = simulate_sidecar(source_rows)
                else:
                    envelope_rows, error = load_sidecar(args.fidelity_events)
                    if error:
                        raise LiveFidelityError("BLOCKED_STREAM_INTEGRITY", error)
                for row in envelope_rows:
                    live.consume(row)
                    if live.stopped:
                        break
            result = live._receipt() if live.cursor.sequence else {
                "status": "AWAITING_SOURCE_PRELUDE"}
    except (LiveFidelityError, ValueError, OSError, RuntimeError,
            urllib.error.HTTPError) as error:
        result = {"status": error.status if isinstance(error, LiveFidelityError)
                  else "EMULATOR_COORDINATOR_ERROR", "reason": str(error),
                  "native_live_lockstep": False}
    result["native_live_lockstep"] = False
    result["native_settlement_certified"] = False
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n",
                        encoding="utf-8")
    print(json.dumps(result, sort_keys=True))
    return 0 if result["status"] == "SOURCE_TERMINAL_OBSERVED_UNCERTIFIED" else 2


if __name__ == "__main__":
    raise SystemExit(main())

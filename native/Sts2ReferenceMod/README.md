# STS2 native fidelity inspector — foundation

This directory contains game-independent instrumentation and a conditional STS2
loader entrypoint. The gameplay hooks, pinned-game adapter, input dispatcher and
Godot overlay are still required. No native fidelity result is claimed.

Implemented: JSONL 0.2 trace DTOs and async bounded writer; append-only event
journal with independent readers; scenario-local identity registry; decision
coordinator with step, pause, resume, stop, replay-ID binding, and one
in-flight action; passive recording facade and typed adapter contract; strict
offline comparison and import-coverage reporting; optional authenticated loopback HTTP transport.

## Local build (fish shell)

    set -l game "/mnt/c/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2"
    set -l data "$game/data_sts2_windows_x86_64"
    cat "$game/release_info.json"
    cat "$data/sts2.runtimeconfig.json"
    test -f "$data/sts2.dll"; and test -f "$data/0Harmony.dll"
    dotnet build native/Sts2ReferenceMod/Sts2ReferenceMod.csproj -c Release -p:Sts2DataDir="$data"

Building without Sts2DataDir uses the independent .NET 9 contract target, as
default CI does. When Sts2DataDir is set, the loader entrypoint references the
pinned native sts2.dll. The checked-in mod_manifest.json is a starting example;
validate it against your installed game's loader and PCK packaging requirements
before deploying. Never redistribute game DLLs.

## Native integration contract

Implement INativeCaptureAdapter at audited input-ready boundaries. Invoke
NativeRecordingSession.OnStableDecision, OnHumanAction and OnSemanticEffect
on the Godot main thread, without RNG consumption or frame-based delays.
Use stable canonical identities and documented field coverage. The recorder
delegates disk IO to a separate worker.

The native plugin currently logs that hooks are unwired; it is not a certified
passive instrumentation mod. See docs/NATIVE_FIDELITY_INSPECTOR_IMPLEMENTATION_STATUS.md.

## Offline tooling

    python tools/native_scenario_import.py native.jsonl --decision 1 --out import.json
    python tools/native_parity.py native.jsonl emulator.jsonl --decision 1 --out parity.json

The comparator requires a post-action boundary, verified equivalent pre-state,
same normalized action and legal actions, and declared event/field coverage.
A synthetic test comparison is evidence of protocol behavior only.

## Optional loopback controller protocol

LocalBridgeServer is instantiated by the native host only after controlled action
hooks have been audited. It binds 127.0.0.1 (default port 39221), requires
Authorization: Bearer <configured-secret> and exposes:

- GET /v1/status — current decision ID/revision/hash and controller phase.
- GET /v1/events?after_seq=42 — independent chronological reader cursor.
- POST /v1/actions — JSON ActionSubmission with request_id, decision_id,
  revision, observation_hash and normalized action.
- POST /v1/pause, /v1/step, /v1/resume, /v1/stop — safe semantic controls.

Request handlers authorize commands only. Dispatch must happen on the Godot main
thread at a proven decision boundary; no transport thread touches native game
objects. The server is intentionally not started by the current loader entrypoint.

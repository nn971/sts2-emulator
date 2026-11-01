# v111 native live event bridge — M1 first slice

**Development branch:** `fidelity/unified-live-event-bridge-20261102`; stacked on
[PR #231](https://github.com/nn971/sts2-emulator/pull/231) and tracked by
[issue #122](https://github.com/nn971/sts2-emulator/issues/122).

## Scope and authority

The game is authoritative. The native Inspector emits observations and source
action-executor callbacks; the independent persistent emulator simulates from
the native **run prelude**, not a captured native combat snapshot.

This first slice reuses the previously spectator-only `LocalBridgeServer`,
`NativeAgentBoundaryFeed`, `NativeActionEnvelopeRecorder`, and the existing
Python `native_turn_checkpoint_replay.py`. It adds a transport/journal layer,
not a second combat decision detector.

## Authenticated loopback HTTP

The Reference Mod server binds **only** `127.0.0.1` (default port 39221)
and is enabled with:

- `STS2_FIDELITY_CAPTURE=1` (existing opt-in for native capture)
- `STS2_INSPECTOR_AGENT_BRIDGE=1`
- `STS2_INSPECTOR_AGENT_TOKEN=<unique 24+ character public token>`
- `STS2_INSPECTOR_FIDELITY_TOKEN=<different 24+ character privileged token>`
- Optional `STS2_INSPECTOR_AGENT_PORT=39221`
- Optional `STS2_FIDELITY_TRACE_DIR=<existing writable trace folder>`

| HTTP route | Credential | Content |
|---|---|---|
| `GET /v1/agent/status` | Public | Existing spectator status |
| `GET /v1/agent/boundary` | Public | Existing whitelisted public decision |
| `GET /v1/public/stream` | Public | SSE public decision/readiness events |
| `GET /v1/fidelity/stream` | Fidelity | SSE privileged source action/RNG/checkpoint events |

The fidelity stream is disabled if its credential is absent. Each bearer token
is accepted for its own endpoints only. Native action POST endpoints remain
forbidden. Transport handlers never call Godot or native gameplay APIs.

A client can resume from a sequence cursor:

```text
GET /v1/fidelity/stream
Authorization: Bearer <fidelity-token>
Last-Event-ID: 27
```

The server responds with standard SSE frames (`id`, `event: native`,
`data`) and sends periodic comment heartbeats. A client may also pass
`?after_seq=27`. The `Last-Event-ID` header takes precedence. Each stream
has its own sequence sequence and replay history.

## Envelope and disk recording

```json
{
  "schema": "sts2-native-live-event-v1",
  "channel": "fidelity",
  "run_id": "<native combat session ID>",
  "session_epoch": 1,
  "sequence": 2,
  "kind": "header",
  "decision_id": null,
  "source_ordinal": 1,
  "content_fingerprint": "<SHA-256 of canonical payload JSON>",
  "payload": {"type":"header","ordinal":1,"data":{}}
}
```

The envelope shown above is illustrative; actual `data` contains the
original source header, including privileged provenance. It must stay in the
fidelity channel.

`NativeLiveEventHub` uses bounded append-only memory for reconnection and
`AsyncTraceWriter` for nonblocking JSONL disk output. The generated files
are `live-<timestamp>-<id>.public.jsonl` and
`live-<timestamp>-<id>.fidelity.jsonl`. Source ordinals and stream sequences
serve different purposes. A new combat increments the session epoch; each
channel's sequence remains monotonic for the lifetime of the process.

The public channel serializes **only** a previously whitelisted
`NativeAgentVisibleBoundary` or a readiness-status marker. Privileged action
and RNG evidence are sourced from the *existing* native action sidecar callback.
A full queue or memory capacity reports an explicit fail-closed condition.

## Offline six-turn rehearsal (no game launch)

Use the original native action sidecar and independently captured prelude from
the same Silent A10 run. This command constructs the live transport envelopes,
validates/reconstructs the sidecar, rebuilds the source-action journal and
invokes the existing independent persistent-emulator replay:

```fish
python tools/native_live_offline_replay.py \
    --sidecar "/path/to/captured.actions.jsonl" \
    --prelude "/path/to/run-prelude.jsonl" \
    --out-dir /tmp/sts2-live-rehearsal
```

For a future live journal already on disk, substitute
`--fidelity-events path/to/live.fidelity.jsonl` for `--sidecar`.

Output artifacts include `validated.fidelity.jsonl`,
`reconstructed.actions.jsonl`, `source-action-journal.json`, and (when a
prelude is provided) `independent-emulator-replay.json`.

The acceptance target is **six projected player-turn checkpoints**, with no
first observed difference, using a real captured source prelude. The result
must continue to report `SETTLEMENT_UNVERIFIED` and incomplete legal-menu
coverage, even when its projected mechanics and RNG comparisons agree.

## Test gates and remaining M1 work

Focused C# tests check authentication separation, SSE cursor replay, disk
privacy and explicit overflow. Python tests check idempotent duplicate event
delivery, missing event rejection, fingerprint verification, epoch/run
identity, and ordinal continuity. The native game is not launched by these
tests.

Remaining M1 scope:

1. Connect an external live coordinator that consumes SSE, drives the persistent
   emulator as source actions arrive, and returns first-difference results.
2. Wire returned synchronization status into F8 with authenticated validation.
3. Verify native-source event/no-perturbation behavior on the exact pinned
   installed DLL in a controlled game launch after offline acceptance.
4. Add journal-on-process-restart recovery. Current SSE replay uses in-memory
   events for an active server process; JSONL is durable for offline recovery.
5. Broaden the native legal-menu/source-action observer in M2; the current
   strict decision trace can end during overlapping executor activity.

This first slice **does not certify** original-game live lockstep, native
queue settlement, full continuation-state parity or native autoplay.

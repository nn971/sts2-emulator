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
has its own sequence and replay history.

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

## Persistent live coordinator (second M1 slice)

`tools/native_live_fidelity_coordinator.py` consumes the privileged SSE
stream using Python's standard library, keeps one separate emulator JSONL
process alive, independently bootstraps from the original run prelude and
advances its own state as native canonical action completions arrive.
It reports projected player-turn differences as soon as the corresponding
native turn-start candidate has been observed. After the native terminal,
the previously established source journal and full offline replay independently
audit the incremental result.

For **offline** rehearsal, use an already captured sidecar and prelude:

```fish
python tools/native_live_fidelity_coordinator.py \
    --sidecar "/path/to/captured.actions.jsonl" \
    --prelude "/path/to/run-prelude.jsonl" \
    --out /tmp/native-live-coordinator-report.json
```

For an original-game **live** session (only after the game-local safety gate),
run the Reference Mod on Windows with a *separate* private token and configure
the bridge. Then start this external command after the original run prelude
has been captured and is available to the coordinator:

```fish
set -gx STS2_INSPECTOR_FIDELITY_TOKEN "<unique-private-token>"
python tools/native_live_fidelity_coordinator.py \
    --follow \
    --prelude "/path/to/run-prelude.jsonl" \
    --bridge http://127.0.0.1:39221 \
    --out /tmp/native-live-coordinator-report.json
```

The Windows game exposes loopback HTTP only. A WSL consumer may require
Windows-side execution or WSL mirrored loopback support; do not expose the
privileged endpoint on a public network to solve connectivity. The Python
coordinator will refuse a non-loopback bridge.

The **only** POST performed by the coordinator is
`POST /v1/fidelity/result`, using the private bearer token. Feedback is
accepted by the Reference Mod only for its current run ID, session epoch,
and an observed source sequence. Duplicate results at an identical cursor
must be byte-equivalent in meaning; stale or conflicting reports are refused.
F8 displays these as `External emulator` diagnostics and retains the
`NOT_CERTIFIED` statement.

The current player-turn comparer is deliberately scoped to the existing
projected mechanics; enemy-turn boundaries remain deferred because the
fast emulator resolves `end_turn` atomically. Before settlement is audited,
even an agreement is labelled `PROJECTED_AGREEMENT_UNCERTIFIED`.

### Live integration gates still open

- **Complete:** independently replayed the original six-turn native
  sidecar and prelude in the M1 persistent coordinator, with six projected
  matches, 22 actions, terminal audit and no first difference.
- Verify the real game-local `STS2_NATIVE` build and perform one controlled
  native live capture/no-perturbation test.
- Persist native journal indexing across Reference Mod process restarts. A
  reconnect to a currently running server resumes by `Last-Event-ID`;
  a native server process restart currently requires explicit rebootstrap.
- Full-run multi-combat rebootstrap and complete macro legal-action coverage
  remain future M1/M2 work, respectively.
- Source callback ordering and accepted-player action coverage need further
  native hook auditing. The coordinator freezes at unsupported or ambiguous
  source events.

## Recorded native transport-contract rehearsal

A private native v111 Silent A10 first-combat sidecar and its paired prelude
were checked locally without publishing native snapshots or source trace
contents. The independent source identities and prelude combat link agreed.
The sidecar contains 896 ordered records, including six player-start and five
enemy-start candidates, 21 canonical executor starts, and an observed terminal.
Rewrapping those records in 897 fidelity envelopes (including the session
marker) retained all source ordinals and passed uninterrupted fingerprint and
sequence checks. Two source RNG floats used uppercase scientific exponents;
the Python live validator now preserves their original lexical representation.

This establishes transport preservation. In the user's local
`~/projects/sts2-inspector` checkout, the new persistent coordinator has
also independently replayed the original native capture using a compiled
emulator. The M1 acceptance report confirmed **6/6 projected player-turn
checkpoints matched**, **22 emulator actions stepped**, **5 enemy-turn
candidates deferred**, **no first projected difference**, and a successful
full-source terminal audit. Its exact final status was
`M1_SIX_TURN_OFFLINE_AGREEMENT_UNCERTIFIED`. Native settlement and complete
state parity remain unverified.

## One-command offline acceptance of an existing native capture

The offline-only `tools/native_live_m1_acceptance.py` scans the
`/mnt/d/SteamLibrary/sts2-inspector-traces` folder recursively by default,
selects the newest complete six-turn original-game capture when available,
and pairs its `passive-*.actions.jsonl` sidecar with its own
`run-prelude-*.jsonl` by **native combat run ID, game build, seed, and
ascension**. It requires observed terminal records and contiguous source
ordinals. It invokes the persistent coordinator against the pre-existing
capture, using the independently bootstrapped emulator, and writes a small
final acceptance report without copying private trace data to GitHub.

From the `~/projects/sts2-inspector` working tree, rebuild the .NET 9 emulator CLI (to avoid a stale Release DLL), then run the offline acceptance:

```fish
dotnet build src/Sts2Emulator.Cli/Sts2Emulator.Cli.csproj -c Release
python tools/native_live_m1_acceptance.py \
    --trace-dir /mnt/d/SteamLibrary/sts2-inspector-traces \
    --out /tmp/native-live-m1-acceptance.json
```

Before opening a native source replay session, the acceptance runner now
launches a separate emulator `prototype-ai-jsonl` hello probe, checks the
wire and snapshot schemas, and surfaces a bounded copy of the CLI exit code,
stdout, and stderr if startup fails. The original-game trace is never provided
to that probe. A failed coordinator also retains its captured stderr in the
local acceptance JSON report. If a prior Release executable lacks the JSONL
protocol, rebuild as shown above.

This defaults to **at least six observed projected player-turn comparisons**,
a terminal full-source replay, and no first detected difference. The result
`M1_SIX_TURN_OFFLINE_AGREEMENT_UNCERTIFIED` carries the usual
`native_settlement_certified: false` and `native_live_lockstep: false`.
If local `dotnet` or the release CLI DLL is missing, the acceptance script
reports `BLOCKED_EMULATOR_EXECUTABLE_UNAVAILABLE`, rather than claiming
mechanics agreement.

For a specific pair, pass both `--sidecar /path/to/capture.actions.jsonl`
and `--prelude /path/to/run-prelude.jsonl`; source identities are verified
even when files are specified explicitly. The command never launches the
original game and does not require a new native capture.

### CI evidence for the new coordinator

The focused v111-fidelity-integration workflow successfully built the
repository and passed **177/177 targeted C# tests**, **33/33 dedicated M1
Python tests**, and the real emulator JSONL process smoke. The wider existing
native audit suite still has two pre-existing Neow source-route expectation
failures from the underlying PR #231; they are tracked separately and are
not M1 transport failures.

The dedicated M1 Python tests (**33/33**) and the emulator-derived
**real JSONL-process** smoke passed in the focused GitHub Actions gate on
the 2026-11-02 M1 branch. The latter matched two projected player-turn
checkpoints and correctly froze an injected HP mismatch at `$.player.hp`.
Its source was explicitly **EMULATOR_DERIVED_SYNTHETIC_ONLY**; it is a
coordinator/process test, not new original-game parity evidence.

The original six-turn independent replay report from the prior fidelity
branch had 6/6 projected checkpoints with no first difference, and native
RNG checkpoint comparisons of four 15-stream agreements plus two
source-uncomparable windows. That report is the reference for the new M1
acceptance command.

## Live M1 prelaunch gates (do not launch the game yet)

The pre-existing native build gate checks the installed **pinned v111**
`sts2.dll` SHA-256 (`0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`),
read-only PE metadata, compiles the game-bound `STS2_NATIVE` mod, and
stages its DLL/PCK/manifest outside Steam. Run it from the
`~/projects/sts2-inspector` checkout; replace `--game-root` with the actual
Steam installation root if different:

```fish
python tools/native_map_build_preflight.py \
    --game-root "/mnt/d/SteamLibrary/steamapps/common/Slay the Spire 2" \
    --stage /tmp/sts2-native-live-m1-stage
```

This writes
`/tmp/sts2-native-live-m1-stage/native-map-build-preflight.json`.
The success status must be `NATIVE_SOURCE_COMPILED_STAGED_NOT_INSTALLED`;
installation, game launch, live capture, and runtime hook parity have not
yet occurred. Preserve the report for review before changing game files.

After staging has passed, install the staged mod for the **Windows game
process** and launch only once with the expected hash, capture, trace
directory and dual credentials explicitly configured. The tokens must each
have at least 24 characters and be different. The game process itself
must receive `STS2_FIDELITY_CAPTURE=1`,
`STS2_FIDELITY_EXPECT_SHA256=<pinned-sha256>`,
`STS2_FIDELITY_TRACE_DIR=D:\\SteamLibrary\\sts2-inspector-traces`,
`STS2_INSPECTOR_AGENT_BRIDGE=1`,
`STS2_INSPECTOR_AGENT_TOKEN`, and
`STS2_INSPECTOR_FIDELITY_TOKEN`.
Environment variables exported solely in WSL do not configure an
independently launched Windows Steam process.

**Before starting a game run**, check the loopback server from the same
environment in which the external coordinator will run:

```fish
python tools/native_live_bridge_preflight.py \
    --out /tmp/native-live-bridge-preflight.json
```

This probe sends only four authenticated **GET** requests. It checks the
public status endpoint, verifies the private SSE credential through the
server's immediate `INVALID_EVENT_CURSOR` rejection of
`/v1/fidelity/stream?after_seq=-1`, and checks cross-token isolation.
It neither starts a game run nor opens a privileged event stream. It
does **not** print or save bearer tokens. It refuses redirects and nonlocal
HTTP origins. Success is `LIVE_BRIDGE_AUTHENTICATION_READY`, which proves
transport reachability/auth separation but not native mechanics or live
source parity.

If WSL cannot connect to Windows `127.0.0.1:39221`, prefer running the
external probe/coordinator on the Windows host. Never expand the native
listener to non-loopback interfaces for connectivity.

Once these gates pass, begin exactly one **Silent A10 fixed-seed** native
run; at the first combat, start the external coordinator against the
already-written run prelude, and compare each stable player-turn projection.
Preserve the final F8 diagnostic screenshot and small coordinator report;
raw action sidecars and private bearer tokens stay local.

## Test gates and remaining M1 work

Focused C# tests check authentication separation, SSE cursor replay, disk
privacy and explicit overflow. Python tests check idempotent duplicate event
delivery, missing event rejection, fingerprint verification, epoch/run
identity, and ordinal continuity. The native game is not launched by these
tests.

Remaining M1 scope:

1. **Complete:** coordinator focused CI and the original six-turn
   offline native capture acceptance. Live fidelity-result feedback remains
   to be verified against the actual game transport.
2. Verify native-source event/no-perturbation behavior against the pinned
   installed game DLL in one controlled native launch.
3. Add journal-on-process-restart recovery. Current live SSE replay uses
   in-memory events for a running server; JSONL is durable for offline recovery.
4. Broaden the legal-menu/source-action observer in M2; the current strict
   decision trace can stop during overlapping executor activity.

This first slice **does not certify** original-game live lockstep, native
queue settlement, full continuation-state parity or native autoplay.

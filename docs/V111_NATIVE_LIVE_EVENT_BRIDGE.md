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
| `GET /v1/fidelity/status` | Fidelity | Read-only latest native-accepted coordinator receipt, journal status, sequence |

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

### Deploy the staged build (game closed)

The local pinned-v111 build report from 2026-11-02 returned
`NATIVE_SOURCE_COMPILED_STAGED_NOT_INSTALLED`: installed `sts2.dll`
SHA-256 matched the pin; all required PE metadata checks passed;
the STS2_NATIVE DLL, PCK and manifest were staged. This is **not**
runtime hook validation.

From fish in WSL, while the Windows game is **closed**:

```fish
function install_sts2_live_m1
set -l game "/mnt/d/SteamLibrary/steamapps/common/Slay the Spire 2"
set -l stage /tmp/sts2-native-live-m1-stage/mods/Sts2ReferenceMod
set -l target "$game/mods/Sts2ReferenceMod"

for name in Sts2ReferenceMod.dll Sts2ReferenceMod.pck Sts2ReferenceMod.json
    test -f "$stage/$name"; or begin
        echo "Missing staged artifact: $name"
        return 2
    end
end

if test -d "$target"
    set backup "/tmp/sts2-reference-mod-backup-"(date +%Y%m%d-%H%M%S)
    cp -a "$target" "$backup"; or return 2
    echo "Backed up existing mod to $backup"
end
mkdir -p "$target"; or return 2
for name in Sts2ReferenceMod.dll Sts2ReferenceMod.pck Sts2ReferenceMod.json
    cp "$stage/$name" "$target/$name"; or return 2
    cmp -s "$stage/$name" "$target/$name"; or return 2
end
# This loose file is for PCK generation; the game loader must not see it.
rm -f "$target/mod_manifest.json"
echo "Staged mod installed; game has not been launched."
end
install_sts2_live_m1
functions --erase install_sts2_live_m1
```

Configure the **Windows User** environment in Windows PowerShell (not WSL
`set -gx`, because Steam is a Windows process). This sets capture and
source hooks, creates two independent 64-character random GUID-based
credentials if not already configured, and never prints them:

```powershell
$settings = @{
  STS2_FIDELITY_CAPTURE = '1'
  STS2_FIDELITY_EXPECT_SHA256 = '0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9'
  STS2_FIDELITY_TRACE_DIR = 'D:\\SteamLibrary\\sts2-inspector-traces'
  STS2_FIDELITY_RNG_HOOK = '1'
  STS2_FIDELITY_RNG_STREAM_CHECKPOINTS = '1'
  STS2_FIDELITY_INPUT_HOOK = '1'
  STS2_INSPECTOR_AGENT_BRIDGE = '1'
  STS2_INSPECTOR_AGENT_PORT = '39221'
}
foreach ($entry in $settings.GetEnumerator()) {
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'User')
}
foreach ($name in @('STS2_INSPECTOR_AGENT_TOKEN', 'STS2_INSPECTOR_FIDELITY_TOKEN')) {
    $value = [Environment]::GetEnvironmentVariable($name, 'User')
    if ([string]::IsNullOrWhiteSpace($value) -or $value.Length -lt 24) {
        $value = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
        [Environment]::SetEnvironmentVariable($name, $value, 'User')
    }
}
```

Check that the two existing credentials differ if they were already
configured. Fully **exit** Windows Steam and restart it, so the game
inherits the new User environment. Clear any old Steam **Launch Options**
that override these values (especially an old `cmd /c set ...` line).
Start the game **once** and remain at the main menu; verify the mod
initializes and the passive hook is enabled before beginning a run.

To run the GET-only bridge probe from WSL fish, retrieve the private
credentials from Windows User environment without printing either value:

```fish
set -gx STS2_INSPECTOR_AGENT_TOKEN (powershell.exe -NoProfile -Command '[Environment]::GetEnvironmentVariable("STS2_INSPECTOR_AGENT_TOKEN","User")' | string trim)
set -gx STS2_INSPECTOR_FIDELITY_TOKEN (powershell.exe -NoProfile -Command '[Environment]::GetEnvironmentVariable("STS2_INSPECTOR_FIDELITY_TOKEN","User")' | string trim)
python tools/native_live_bridge_preflight.py \
    --out /tmp/native-live-bridge-preflight.json
```

A success report is `LIVE_BRIDGE_AUTHENTICATION_READY`. Share that
report (no credentials in it) and confirm the original game's
`CAPTURE_STATUS=HOOK_ENABLED` and
`AGENT_BRIDGE_STATUS=SPECTATOR_READY` messages in its `godot.log`.
Only then start the Silent A10 fixed-seed run in the **same** game process.
The source run prelude is written as the run approaches first combat;
the external coordinator will use it to replay buffered SSE from sequence 0.

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

## Live first-combat fingerprint recovery without relaunch

On 2026-11-02, the original game's first live Silent A10 prelude passed
`RUN_PRELUDE_SOURCE_OBSERVED` for seed `6JK8MD0VQQTA`, and both
`CAPTURE_STATUS=HOOK_ENABLED` and
`AGENT_BRIDGE_STATUS=SPECTATOR_READY` appeared in `godot.log`.
The first coordinator run instead stopped at SSE sequence 1, reporting
`BLOCKED_STREAM_INTEGRITY: Native payload fingerprint mismatch` for
the first subsequent native source record. The original .NET default
canonical encoder additionally escapes `+`, grave accents and quote
characters; the Python validator has now been aligned, with matching
C# and Python canonical fixture tests.

The original terminal diagnostic receipt is intentionally irreversible
within a combat session. For *the same game process and combat*, replay
the preserved SSE history from sequence zero with the **GET-only**
`--no-feedback` mode. It keeps all original hash, ordering, source
identity and emulator comparison checks, and sends zero feedback POSTs:

```fish
cd ~/projects/sts2-inspector
git pull
python tools/native_live_fidelity_coordinator.py \
    --follow --no-feedback \
    --prelude "$prelude" \
    --bridge http://127.0.0.1:39221 \
    --out /tmp/native-live-m1-readonly-recovery.json
```

This command can run while the game is already open at the first combat;
the previous session's terminal failure remains visible to the native
process and cannot be rewritten. The command produces a local report
once a source terminal arrives or a blocker is detected. The `$prelude`
variable is the exact first-combat run-prelude path already audited.

The in-game F8 CanvasLayer has independent visibility diagnostics for
the **next** mod build/launch:
`INSPECTOR_PANEL_STATUS=CREATED` at UI construction and
`INSPECTOR_PANEL_STATUS=F8_TOGGLED` on each detected key press. Setting
`STS2_INSPECTOR_PANEL_SHOW_ON_START=1` in the Windows game process
environment before a later launch exposes the panel immediately even
when the F8 key event is not delivered. The current loaded mod cannot
pick up that code change without a restart, so preserve the current
capture for the GET-only replay first.

## Native live SSE recovery verified and next feedback gate

The real original-game recovery run for Silent A10 seed
`6JK8MD0VQQTA` now returned
`SOURCE_TERMINAL_OBSERVED_UNCERTIFIED` with **six of six matched projected
player-turn checkpoints, 22 emulator actions stepped, five enemy-turn
candidates deferred, 910 live fidelity sequences consumed, and no projected
first difference**. This fixes the first-live-capture canonical fingerprint
blocker, independently confirms the native SSE transport and full-source
replay, and establishes the GET-only recovery gate for this original
combat. The session had already accepted an irreversible error receipt
before this recovery, so its current in-game F8 result was not updated.

For the **next** controlled game launch, rebuild/reinstall the updated
`STS2_NATIVE` mod using `tools/native_map_build_preflight.py` and the
staging procedure above (while the game is closed). Before restarting
Windows Steam, set the optional User environment variable:

```powershell
[Environment]::SetEnvironmentVariable(
  'STS2_INSPECTOR_PANEL_SHOW_ON_START', '1', 'User')
```

The modified panel will appear on the first Godot frame if it can be
constructed, and the game will log
`INSPECTOR_PANEL_STATUS=CREATED` and
`INSPECTOR_PANEL_STATUS=F8_TOGGLED` on F8 key transitions.
On the next combat, run the normal coordinator with `--follow`
**without `--no-feedback`** so the game receives the projected
comparison results. To independently inspect the native server's
accepted status even if the panel is invisible, use:

```fish
python tools/native_live_fidelity_status.py \
    --out /tmp/native-live-feedback-status.json
```

This is a private-token-authenticated GET on
`/v1/fidelity/status`, not an input or game mutation; the report
contains the latest accepted coordinator receipt, latest source cursor
and any journal failure, never bearer credentials. It only exists in
the **newly rebuilt** native mod, so it cannot be queried successfully
from the previously loaded game DLL.

The successful read-only native replay proves that SSE source
observations and independent projected mechanics agree. The next
feedback acceptance gate separately requires in-game receipt delivery
and a concurrently advancing source/emulator run. Native queue settlement,
full state parity, and autoplay remain unverified.

## F8 panel initialization fix after main-menu runtime observation

The second pinned-v111 native build loaded successfully and served
`GET /v1/fidelity/status` at the main menu, returning
`NATIVE_FIDELITY_STATUS_READY_NO_RECEIPT` with source sequence zero.
The user observed **no F8 overlay**, despite the native log:

```text
CAPTURE_STATUS=HOOK_ENABLED
INSPECTOR_PANEL_STATUS=CREATED; visible=False; opt_in_show_on_start=True
```

This identifies an overlay initialization path issue, separately from
the functioning HTTP bridge and source capture. The first implementation
created a `CanvasLayer` and called `Toggle()` to show it, while relying
on Godot calling the managed `_Ready()` override to initialize its controls
and hide it first. Dynamically loaded native mods may not have that
managed script callback registered; toggling a still-visible default layer
then hides an uninitialized panel.

The fix now calls `FidelityInspectorPanel.Initialize()` explicitly after
adding the layer to the scene tree. The `_Ready` callback also calls
`Initialize()`, which is idempotent. `SetShown(showOnStart)` explicitly
sets the intended initial visibility, instead of relying on `Toggle()`
and an implicit prior state. The panel-creation log also includes
`controls_created` and `in_tree` booleans for non-UI diagnostics.

**This code is not loaded into a currently running game process.**
The mod must be rebuilt against the pinned DLL, restaged and installed
while the original game is closed; validate `INSPECTOR_PANEL_STATUS=CREATED;
visible=True; controls_created=True; in_tree=True;
opt_in_show_on_start=True` at the main menu of the next controlled launch,
then verify the drawn overlay and F8 toggling. This remains a runtime
hypothesis until native visual confirmation.

## Native action-queue quiescence evidence (next fidelity increment)

The original game uses a separate `ActionQueueSet`, `ActionExecutor`
and `ActionQueueSynchronizer` (pinned
`Flaaax/STS2-source@706ef1c9`, v0.111.0). A rendered idle player frame
alone could conceal queued or deferred actions. The new read-only
player-turn checkpoint additionally captures eight boolean source
observations:

1. `ActionQueueSet.IsEmpty` and
   `ActionQueueSet.BecameEmpty().IsCompletedSuccessfully`;
2. `!ActionExecutor.IsRunning`,
   `ActionExecutor.FinishedExecutingActions().IsCompletedSuccessfully`,
   `CurrentlyRunningAction == null`, and `!IsPaused`;
3. `ActionQueueSynchronizer.CombatState == PlayPhase` and an empty
   source-private `_requestedActionsWaitingForPlayerTurn` list.

The inspector never invokes `GetReadyAction()`, which can remove
canceled items, never awaits tasks, and never changes the native
action queue. The private deferred list is read through pinned
reflection. If a source member is unavailable or reading it fails,
evidence is `NOT_CAPTURED`, not an empty-queue assertion.

When all eight flags are true, the checkpoint includes
`queue_quiescence_status:
SOURCE_QUEUE_AND_EXECUTOR_QUIESCENT_AT_FRAME`. Otherwise it has
`SOURCE_QUEUE_QUIESCENCE_UNVERIFIED`. The source auditor recomputes
the status from the eight individual booleans and rejects malformed,
contradictory, or enemy-turn assertions; the live coordinator validates
the same contract **before** comparing that player turn. Old sidecars
without these fields remain acceptable and are marked
`NOT_CAPTURED_LEGACY`. The offline source journal and emulator
turn-checkpoint replay carry the validated status forward.

**Precise guarantee:** the native action queue, executor and
synchronizer were jointly observed quiescent *at that sampled Godot
player frame*. The later scheduling of asynchronous hooks, animation
completion, native phase-handoff ordering and globally settled
mechanical state remain unverified. In particular,
`settlement_status: SETTLEMENT_UNVERIFIED`,
`complete_native_queue_verified: false` and
`native_phase_handoff_order_verified: false` remain unchanged.

**Original-game acceptance (2026-11-02):** Native Silent A10, seed
`6JK8MD0VQQTA`, produced six `PLAYER_TURN_START` checkpoints
(ordinals 47, 230, 415, 576, 768, 854); **6/6** carry
`SOURCE_QUEUE_AND_EXECUTOR_QUIESCENT_AT_FRAME`. The independent
coordinator matched **6/6** player turn projections, stepped 22
canonical commands, and observed 918 authenticated source sequences,
with no first difference. Five enemy-start candidates remain
`NOT_CAPTURED` for quiescence. The action-envelope audit is
`CALLBACK_TIMELINE_CONSISTENT`, with 26/26 action
start/completions and 26 source request/execution links; a strict
decision-gap at ordinal 461 remains isolated to the older strict
input collector. The new observation **does not upgrade**
`SETTLEMENT_UNVERIFIED` or `native_live_lockstep=false`.
This gate requires no further identical native game run.

The PE-metadata-only native preflight now requires the queue/executor
members and the private deferred-request list on the pinned local
`sts2.dll` before compilation and staging. The pinned WSL source-compiled build passed all 32 metadata
checks and was verified in the original game at six turn starts.
Existing live-feedback functionality remains operational.

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


## Source-pinned native phase-handoff chronology (2026-11-02)

This increment builds on the six native queue-quiescent player starts. It adds
three **read-only** `CombatManager` event subscriptions, scoped to one recorded
combat and detached on combat termination:

- `AboutToSwitchToEnemyTurn`: after the automatic phase-one end-turn
  sequence has requested `ReadyToBeginEnemyTurnAction`, with
  `EndingPlayerTurnPhaseTwo=true`; this event occurs **before** the
  phase-two hand flush and `AfterSideTurnEnd` hooks.
- `TurnEnded`: emitted by `SwitchSides` **after** `CurrentSide` changes,
  the player turn number increments on entry to the player side, and creature
  `OnSideSwitch` callbacks run. It is therefore a **side-switched**
  observation, not a pre-switch notice.
- `TurnStarted`: emitted on the newly selected side after its start-turn
  processing. For the player, `RunAutoPrePlayPhase` has already completed,
  `PlayerTurnPhase.Play` is active, and `ActionQueueSynchronizer` is in
  `PlayPhase`; for the enemy, `IsEnemyTurnStarted` is true.

Source: `Flaaax/STS2-source@706ef1c9`, v0.111.0,
`CombatManager.cs` (`AfterAllPlayersReadyToBeginEnemyTurn`,
`SwitchSides`, `StartTurn`) and
`ActionQueueSynchronizer.cs` (`SetCombatState`).

Each event is persisted as `native_phase_handoff` in the existing action
sidecar and authenticated fidelity stream, with source callback provenance,
player turn number, side, player phase, synchronizer state, three source
phase flags, and `SETTLEMENT_UNVERIFIED`. The recorder does not wait on
tasks or touch game queues, RNG or input. The independent
`tools/native_phase_handoff_audit.py`, included in
`native_action_envelope_audit.py`, checks callback provenance,
transition order, phase invariants and turn-number arithmetic. It accepts
an initially missed opening `TurnStarted` callback, supports extra
player turns, preserves legacy captures with no events, and distinguishes
a partial tail from an invalid or forged chronology.

A complete normal observed handoff follows

`AboutToSwitchToEnemyTurn(t) -> TurnEnded(Enemy,t) ->
TurnStarted(Enemy,t) -> TurnEnded(Player,t+1) -> TurnStarted(Player,t+1)`.

An extra player turn follows
`AboutToSwitchToEnemyTurn(t) -> TurnEnded(Player,t+1) ->
TurnStarted(Player,t+1)`.
`SwitchSides` increments the player turn number before emitting
`TurnEnded`. Terminal combat may interrupt a sequence; such an observed
tail remains explicitly partial.

The independent audit reports
`SOURCE_PHASE_HANDOFF_ORDER_OBSERVED`,
`SOURCE_PHASE_CALLBACKS_PARTIAL` or
`INVALID_PHASE_HANDOFF_CHRONOLOGY`. No event or audit result promotes
`native_phase_handoff_order_verified` in a source checkpoint,
`settlement_status`, or `native_live_lockstep`. Even a complete observed
callback chain plus a quiescent player frame cannot exclude delayed
gameplay-affecting tasks scheduled to run after the observed frame.
Next, identify and instrument completion of relevant automatic hooks and
phase handoffs before considering any stronger certification.

**Validation:** Offline Python adversarial chronology tests and C# recorder
serialization regressions were added. A new installed-game DLL build and
original-game runtime observation are pending; do not rerun the earlier
six-turn combat merely to repeat the old queue gate.


The action-sidecar audit also correlates each sampled `PLAYER_TURN_START`
checkpoint with an earlier source `TurnStarted(Player)` callback for the
same player turn number. It reports
`player_checkpoint_phase_callback_pairs` and
`player_checkpoints_without_observed_start_callback` separately. A first
turn whose callback occurred before observer attachment remains acceptable
but unpaired. A callback for the same turn arriving **after** an already
sampled player-frame checkpoint, or a duplicate player-turn-start callback,
fails closed. This connects the source handoff chronology to the existing
eight-flag queue snapshot without claiming globally settled effects.


## Awaited turn-loop stages and tracked automatic-effect evidence (2026-11-02)

The pinned v0.111 source establishes which asynchronous work is actually
awaited by the original combat turn loop:

- `AboutToSwitchToEnemyTurn` occurs after the phase-one
  `EndPlayerTurnPhaseOneInternal` awaited chain and before phase-two hand
  flushing/after-side-end hooks.
- `TurnEnded(Enemy)` is emitted after `EndPlayerTurnPhaseTwoInternal`
  and the side switch, meaning the awaited player phase-two hook chain
  has returned.
- `TurnStarted(Enemy)` follows awaited enemy-side start hooks but precedes
  `ExecuteEnemyTurn` (enemy actions have **not** run yet).
- `TurnEnded(Player)` after an enemy turn follows the awaited enemy
  `TakeTurn` actions and `EndEnemyTurnInternal` hook chain. If the player
  receives an extra turn, the player-side switch instead occurs immediately
  after player phase two, and `Hook.AfterTakingExtraTurn` may still be
  pending when `TurnEnded` fires.
- `TurnStarted(Player)` with `PlayerTurnPhase.Play` follows completion of
  `RunAutoPrePlayPhase`, including awaiting the setup task, draw, start-turn
  hooks and `Hook.AfterAutoPrePlayPhaseEntered`.

**Important source exception:** `HookPlayerChoiceContext.AssignTaskAndWaitForPauseOrCompletion`
can return when a start-of-turn hook pauses for a player choice. In that
case `CombatManager.TurnStarted(Player)` may fire with phase `Start` or
`AutoPrePlay` and synchronizer `PlayPhase`. The independent auditor
records an `AWAIT_PLAYER_START_HOOK_COMPLETION` state. A later native
`PLAYER_TURN_START` frame with phase `Play` is evidence that the
auto-pre-play phase completed. If the run progresses to another end-turn
without an observed playable frame, the unpaired completion remains
explicitly counted rather than fabricating the missing frame.

The phase auditor now reports `awaited_source_milestones`, plus
`player_start_hook_pauses_observed`,
`player_start_hook_resumes_observed` and
`player_start_hook_resolutions_without_frame`. Milestones are strictly
bounded to what the associated source `await` chain or observed
`PlayerTurnPhase.Play` proves.

At each player-frame candidate, the Reference Mod additionally samples
three read-only source flags:
`!CombatManager.IsExecutingCardOrPotionEffect(player)`,
`!EndingPlayerTurnPhaseOne && !EndingPlayerTurnPhaseTwo`, and
`!IsEnemyTurnStarted`. The first query directly checks the game's
tracked card/potion effect-body nesting depth, including nested
auto-plays. Its status is
`SOURCE_TRACKED_EFFECT_BODY_INACTIVE_AT_FRAME` only if all three
flags are true; any false flag yields
`SOURCE_TRACKED_EFFECT_ACTIVITY_UNVERIFIED`.
Missing evidence yields `NOT_CAPTURED` (or
`NOT_CAPTURED_LEGACY` on older sidecars).
The source auditor and live independent coordinator recompute the status
from exact boolean fields, fail closed on forged claims, and preserve
the prior independent eight-flag queue evidence.

The metadata-only preflight now verifies the three
`CombatManager` source events, its three phase-state properties,
and `IsExecutingCardOrPotionEffect` in the pinned installed assembly,
in addition to the previous native queue members.

These observations prove **awaited game turn-loop stages and tracked
card/potion effect inactivity at a particular frame**. They do not
prove that every detached task, animation-triggered hook or future effect
has settled. `SETTLEMENT_UNVERIFIED`,
`native_phase_handoff_order_verified=false`,
`native_settlement_certified=false`, and
`native_live_lockstep=false` remain unchanged.

Offline C# and Python tests cover the bounded status, malformed values,
legacy sidecars, synthetic phase handoffs and a start-of-turn choice pause.
An installed-game build and any runtime validation of the new callbacks
remain pending.

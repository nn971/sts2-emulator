# L0 native Inspector acceptance: game-local handoff and code reconciliation

**2026-10-31; branch:** `fidelity/l0-native-acceptance-20261031` (stacked on
`feature/v111-native-agent-spectator-bridge`). Canonical objective:
[Live lockstep native fidelity plan](https://github.com/nn971/sts2-emulator/blob/docs/live-lockstep-fidelity-roadmap-20261031/docs/LIVE_LOCKSTEP_NATIVE_FIDELITY_PLAN.md),
[issue #122](https://github.com/nn971/sts2-emulator/issues/122).
**This L0 implementation provides a single source-evidence gate; it does not
start an emulator lockstep session or complete L0 until native no-perturbation
evidence is separately obtained.**

## 1. Code ownership audit and reconciliation decision

- The current `main` and the native Inspector review line have **diverged**.
  GitHub's `main...feature/v111-native-agent-spectator-bridge` comparison on
  2026-10-31 reported 359 commits ahead and 178 behind (merge base
  `db751d80cdbd7e458d60646658398126e3bc1b9d`). This is **not** a
  clean forward merge onto `main`, nor a replacement for H15.
- The live reference-mod leaf **#158** is seven commits after the native
  `NextFloat` source-observer leaf **#153**. Its unique native code is
  `NativeAgentBoundaryFeed`, `LocalBridgeServer`, spectator integration in
  `NativeV111PassiveBridge`, F8 capability display, client and tests.
- Ancestor stack: **#137** (all-Act native/mod + actual-engine importer),
  **#138** (persistent source stream continuity), **#139** (double/bool source
  probes), **#140** (15-stream checkpoints), **#153** (float source observer),
  **#158** (authenticated spectator). Preserve the stack as a **review
  lineage**, not six alternate implementations.
- The actual-engine diagnostic sources `NativeEngineFidelityV111`,
  `NativeEngineFidelityBatchV111` and source journals were previously
  incorporated from **#127–#129 / fidelity-only #132** into #137's line.
  **#132 was closed without merge as superseded** because its later moving
  head included unrelated gameplay. Retain its historical diff, not its
  broad branch ancestry.
- The existing `tools/native_game_preflight.py`,
  `native_action_envelope_audit.py`, `native_rng_boundary_verify.py`,
  and `native_rng_checkpoint_audit.py` have separate responsibilities.
  `tools/native_l0_evidence.py` **composes** those four without a second
  native sampler, RNG provider or mechanical resolver. Existing source
  verifiers remain the authority for raw xoshiro/stream evidence.
- Game-native actions observed by the current mod cover primarily
  `PlayCardAction` and `EndPlayerTurnAction`; capture begins at the first
  detected combat frame. The existing spectator endpoint is intentionally
  read-only, and the native trace run seed is an environment-supplied
  `STS2_FIDELITY_RUN_SEED` or `UNVERIFIED`, **not a verified read of the
  actual game's seed**. These are explicit L1 blockers for a continuous
  run-from-seed emulator session. Do not convert that environment variable
  into a claimed native seed observation.

**Consequence:** Develop L0 as a **small stack on #158** and reconcile only
audited native/CLI source files later into the current game implementation
via a dedicated integration PR. Keep H15 and active training pins unchanged.

## 2. Real-game capture evidence: 2026-10-31 Silent A10

One uploaded `godot.log` and two matching combat traces from game
**v0.111.0** (installed `sts2.dll` SHA-256
`0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`)
showed:

| Native encounter | Source calls | `NextFloat` calls | 15-stream checkpoints | Observed persistent advances | Unobserved persistent advances | Strict trace |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `FUZZY_WURM_CRAWLER_WEAK` | 438 | 404 | 55 | 19 | 0 | stopped after 14 decisions at Survivor discard choice |
| `SLIMES_WEAK` | 433 | 402 | 52 | 13 | 0 | stopped after 15 decisions before subsequent Shiv play |

The trace headers contained `UNVERIFIED` for run seed; the *game log*
independently identified Silent A10, seed `6JK8MD0VQQTA`. No change to the
seed-capture code was made. Native `NextFloat` was installed and observed;
only **one** observed float call on `run.monster_ai` is present in the
Slimes encounter. Unattributed/context source calls remain an owner-identity
coverage gap. The game also loaded BaseLib, which patched unrelated game
functions; disable it during controlled passive-on/off tests.

**Verifier correction:** the JSON wire stores `System.Single` as
shortest-roundtrip decimal, which Python parses as binary64. The old
`verify_scalar_call` required `f32(json_value) == json_value`, falsely
rejecting all 806 legitimate float calls. The corrected verifier first
restores `float_min`, `float_max` and `selected_float` to binary32, then
recomputes the C# arithmetic. All 806 uploaded float records pass that
formula; two reduced real source examples are in
`tests/fixtures/native_nextfloat_json_single_v111.jsonl`. The original
xoshiro state, counter and output tamper checks remain enabled.

**Strict decisions:** the action sidecars ended with 26 and 25 completed
executor actions respectively and no unfinished actions, but their paired
strict `passive-*.jsonl` traces have terminal `INCOMPLETE_CAPTURE`.
After Survivor's nested discard or Leading Strike's new Shivs, later
`PlayCardAction` callbacks occurred **without a new recorded strict legal
menu**. Sidecar player-action observations preserve evidence but cannot
serve as a retroactively proven menu. The L0 CLI automatically audits the
paired strict trace and reports `STRICT_DECISION_TRACE_INCOMPLETE` separately
from source RNG integrity. Native accepted-input/menu hooks must eventually
close this gap for L1 lockstep.

These captures establish installed native hooks and internally consistent
observed source RNG. **No native/emulator parity or passive
nonperturbation is certified.**

## 3. Local native acceptance (fish, WSL on Windows game install)

Perform all game-local operations on the existing `sts2-inspector` worktree,
with the actual game **closed while building/installing**. This example
uses the existing Steam library path; resolve `GAME_ROOT` to your installed
directory.

```fish
cd ~/projects/sts2-inspector
git fetch origin fidelity/l0-native-acceptance-20261031
git switch --detach origin/fidelity/l0-native-acceptance-20261031

set -gx GAME_ROOT "/mnt/d/SteamLibrary/steamapps/common/Slay the Spire 2"
set -l data "$GAME_ROOT/data_sts2_windows_x86_64"
set -l trace /mnt/d/SteamLibrary/sts2-inspector-traces
mkdir -p "$trace"
python tools/native_game_preflight.py "$data" --out "$trace/l0-game-pin.json"
sha256sum "$data/sts2.dll"
fish scripts/build-native-inspector.fish "$GAME_ROOT"
```

The build **must compile the `STS2_NATIVE` code against the actual installed
`sts2.dll`**; no-game-asset CI cannot substitute for this step.
`build-native-inspector.fish` installs only `mods/Sts2ReferenceMod` under
`GAME_ROOT`. Save the shell output and record `git rev-parse HEAD`.

For the installed game, enable **only the explicit opt-in flags** below in
Windows Steam launch options, replacing the hash with the continuous
64-character `sts2.dll` SHA-256 printed above:

```text
cmd /c "set STS2_FIDELITY_CAPTURE=1&&set STS2_FIDELITY_RNG_HOOK=1&&set STS2_FIDELITY_RNG_STREAM_CHECKPOINTS=1&&set STS2_FIDELITY_EXPECT_SHA256=EXACT_64_CHAR_SHA256&&set STS2_FIDELITY_TRACE_DIR=D:\SteamLibrary\sts2-inspector-traces&&%command%"
```

Start a **human Silent A10** run. For this L0 test, choose an encounter with
random enemy intentions, ideally multiple enemies, allow several enemy turns,
then complete/end combat so the sidecar is flushed. Press F8 to check the
native capability display. A successful recorder should log
`CAPTURE_STATUS=HOOK_ENABLED` and
`RNG_HOOK_STATUS=INSTALLED_BOUND_INT_OVERLOADS` with
`float_source_hook=INSTALLED`. These are installation observations, not
native/emulator parity.

Select the **fresh** `passive-*.actions.jsonl` path, not an old October 15
sidecar, and locate the actual `godot.log` produced by the *same* game
process. The evidence command makes no game changes:

```fish
cd ~/projects/sts2-inspector
set -l trace /mnt/d/SteamLibrary/sts2-inspector-traces
set -l sidecar "/mnt/d/SteamLibrary/sts2-inspector-traces/REPLACE_WITH_NEW.actions.jsonl"
set -l game_log "/path/to/the/current/game/godot.log"
python tools/native_l0_evidence.py \
    --preflight "$trace/l0-game-pin.json" \
    --sidecar "$sidecar" \
    --game-log "$game_log" \
    --out "$trace/l0-evidence.json"
```

The L0 CLI also audits the matching strict decision trace next to the\nselected `*.actions.jsonl` automatically (or accepts `--strict-trace`).\nAn incomplete strict trace downgrades the source report to partial while\npreserving independently validated primitive math. The report validates the installed-assembly hash against the **sidecar's
source-reported `game_build`**, authentic callback timeline, declared
hook flags, source primitive results and state transitions, stream conflicts,
15 persistent stream checkpoints and **observed `run.monster_ai`
`NextFloat` calls**. A zero-call / unexercised monster-AI encounter
receives a partial verdict; unaccounted advances or missing hook installation
remain explicit, even when the primitive math is correct.

The report can return:
- `L0_EVIDENCE_REJECTED` (invalid provenance/record/cursor),
- `L0_SOURCE_EVIDENCE_PARTIAL` (missing or insufficient evidence), or
- `L0_SOURCE_EVIDENCE_CONFIRMED_NONPERTURBATION_PENDING` (all listed
  capture-source gates observed, **still not L0 complete**).

The last category **still requires a separate native game on/off
nonperturbation comparison** with controlled equivalent input and matching
run configuration; report source-hook-induced behavior changes or uncertainty
explicitly. Human-play action timing and seed alone may leave comparisons
uncontrolled. If the installed game DLL or the mod changes, start a new
pinned evidence set. Keep the raw source observations, log and report local;
share only selected sanitized captures and summaries.

## 4. L0 acceptance and next engineering gate

- [ ] Exact installed DLL native mod compile, install and launch, hash pinned
- [ ] Fresh sidecar and startup log from the **same** session; hooks attached
- [ ] Genuine multi-enemy `NextFloat` source primitive and persistent
  `run.monster_ai` cursor evidence; remaining unexplained advances enumerated
- [ ] Native passive no-perturbation experiment (separate evidence)
- [ ] Review integrated source ownership before any merge onto newer emulator
- [ ] Explicit first L1 design for actual run seed/Neow/map/native macro
  boundaries and a persistent independently initialized emulator state

**Next code milestone:** L1 forwards *accepted human actions* from native
stable decisions to one separately maintained `Native` emulator session.
The original-game state/cursors are diagnostic inputs to the comparator,
never a replacement for emulator-predicted outcomes. 

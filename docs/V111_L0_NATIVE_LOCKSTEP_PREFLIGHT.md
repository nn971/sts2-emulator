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

A newer, completely offline paired-trace tool now pinpoints the first
candidate that the strict recorder missed:

```fish
python tools/native_decision_gap_audit.py \
    --strict "$trace/passive-20261031-151012598-c4d5c60eccdb498598392ad721c69547.jsonl" \
    --sidecar "$trace/passive-20261031-151012598-c4d5c60eccdb498598392ad721c69547.actions.jsonl"

python tools/native_decision_gap_audit.py \
    --strict "$trace/passive-20261031-151134082-0b0c1084b8d74e779e672649b85c57b6.jsonl" \
    --sidecar "$trace/passive-20261031-151134082-0b0c1084b8d74e779e672649b85c57b6.actions.jsonl"
```

An independent structural alignment over the uploaded records found **14**
strict actions matched by the Wurm sidecar followed by **7** unmatched
canonical player-action candidates. First unmatched source event: ordinal
**524**, executor action **18**, Defend; it follows a source-observed
Survivor `nested_choice_pause` (ordinal **507**). In Slimes **15** strict
actions matched, followed by **6** unmatched candidates; the first
missing action is ordinal **636**, executor action **19**, Shiv. There
are changed-state frame observations between Leading Strike finishing
and that new source-executor action, but no intervening strict decision
record. This is evidence of a missing strict boundary, **not proof of
the exact native input-ready moment**.

The current branch also adds a `strict_input_gap` diagnostic event when
a new source-executor action finds no unique strict decision. It records
the last main-frame gate reason and the decision-generation counters.
The sidecar remains valid and the original strict trace still stops
fail-closed. This diagnostic cannot retroactively appear in older captures.
No native-game rerun is needed merely to test the offline pairing code;
collect the extra diagnostic only in a later combined game validation.

## 3. Offline-audited source-input observation (staged; native-game test deferred)

**Source locator:** `Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20`:
`src/Core/GameActions/Multiplayer/ActionQueueSynchronizer.cs`,
`ActionQueueSet.cs`, `PlayerChoiceSynchronizer.cs`, `GameAction.cs`,
`src/Core/Entities/Multiplayer/NetPlayerChoiceResult.cs`, and
`src/Core/Runs/RunRngSet.cs`.

The new read-only `NativeV111InputHooks` provides an **optional**
`STS2_FIDELITY_INPUT_HOOK=1` source patch, separate from RNG and passive
capture. It instruments the actual
`ActionQueueSynchronizer.RequestEnqueue(GameAction)` method with a void
prefix and void postfix; both call observer code only and leave the original
game action, queue and input arguments unchanged.

| Evidence | Source boundary | What it establishes |
| --- | --- | --- |
| `native_input_request` | `RequestEnqueue` prefix | original requested action object, type, canonical candidate and pre-request state; **not** acceptance |
| `native_input_outcome` | `RequestEnqueue` postfix | actual native action ID and state after original method returned: deferred/un-enqueued, canceled, or assigned queue ID and still not canceled |
| `action_started` | existing `ActionExecutor.BeforeActionExecuted` | the same action object reached execution, linked to its source request ID where observed |
| `native_player_choice` | `PlayerChoiceSynchronizer.PlayerChoiceReceived` event | source-synchronized selection ID, type and selected combat-card IDs, indexes or canonical card models; Survivor's discard can be identified |
| `native_run_identity` | `RunState.Rng.StringSeed` + `RunState.AscensionLevel` | the game's original seed and ascension, also written into the paired strict header |

The source synchronizer may *defer* a card play until the next player
turn, or the underlying queue may *cancel* an input after assigning its
native ID. In singleplayer a post-return
`ASSIGNED_QUEUE_ID_AND_NOT_CANCELED_AT_RETURN` is a genuine observation of
queue admission at that instant. It is not a proof that the input originated
from the human UI or was a member of the complete legal menu. The recorder
retains `human_origin=UNVERIFIED` and
`accepted_choice_menu=NOT_CAPTURED` until those independent source hooks
exist. The spectator interface remains **read-only**, and its public
projection still excludes the native seed.

**Offline test status:** the new recorder contract and sidecar auditor can
run in standard GitHub CI without a game launch. This source patch is staged
behind a separate environment flag; it has not been compiled against or
executed within the user's installed v111 game DLL. The static source audit
and standard .NET contract tests alone cannot certify Harmony attachment,
actual native input event ordering, nonperturbation, or emulator parity.

### Additional read-only UI/manual-play hooks (staged offline)

Pinned `src/Core/Models/Cards/Survivor.cs` calls
`CardSelectCmd.FromHandForDiscard` → `CardSelectCmd.FromHand`,
which reserves a player-choice ID, suspends the parent action, invokes
`NPlayerHand.SelectCards` for the native selection UI, then sends the
selected combat-card IDs through `PlayerChoiceSynchronizer`.

The new **optional** `NPlayerHand.SelectCards` Harmony **postfix**
captures the UI's already-filtered visible `NHandCardHolder` nodes
*after* `UpdateSelectModeCardVisibility`, at the method's first
suspension awaiting a user selection. It reads the native
`CardSelectorPrefs.MinSelect/MaxSelect/RequireManualConfirmation`, the
source model, display order, and card identities from
`NetCombatCardDb.TryGetCardId`. The pinned native ID getter is a
dictionary lookup; the Inspector never re-evaluates the selection
predicate, registers cards, or draws RNG. If any displayed card lacks
a native ID, the menu is explicitly
`INCOMPLETE_NATIVE_CARD_ID_MAPPING`.

New `native_hand_menu` records carry `menu_id`,
`mode`, `source_model_id`, `min_select`, `max_select`,
`options` (card IDs, model IDs, UI holder order), and
`coverage=SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS` when mapped in
full. These establish the native UI's offered visible hand choices **at
one instant**.

**Native reserved-choice ID binding (new):** v0.111 `CardSelectCmd.FromHand`
reserves its actual `choiceId` using
`PlayerChoiceSynchronizer.ReserveChoiceId(player)` before calling
`NPlayerHand.SelectCards`. Two additional observer-only Harmony hooks
record the *actual returned* `uint` from the reservation method
and open a narrow async-execution-context scope around `FromHand`.
The UI-menu postfix reads the captured scope; if exactly one reservation
belongs to the same player, it adds `selection_owner_id`,
`selection_choice_id` and
`selection_id_binding=SOURCE_ASYNC_FLOW_RESERVED_ID`. This is the
source value, not a latest-counter estimate or retrospective matching
based on selected cards. Nested flows get independent scopes. Missing,
duplicate or cross-player reservations downgrade to explicit unknown or
ambiguous binding, never a fabricated ID.

The independent sidecar auditor validates this source linkage and, when
`PlayerChoiceReceived` reports the same owner and choice ID, checks
whether the selection was in the **initial** native UI visible
candidate set.

**Observed menu revalidation and lifecycle (new):** The original
`NPlayerHand.UpdateSelectModeCardVisibility` already runs the game's
selection predicates and changes the `Visible` flags of its native hand
holders. The Inspector installs an observer-only **postfix** on that
method and reads those flags *after* the native update. The original
`SelectCards` postfix opens the corresponding menu lifetime with a
source-reserved choice ID, and subsequent changed observations produce
`native_hand_menu_revision` records with monotonically increasing
revisions and the **same native menu ID**. Identical snapshots are
deduplicated. Source `AfterCardsSelected` and `_ExitTree` hooks record
`native_hand_menu_closed`, preserving the native closure reason and
final observed revision.

The native peek view deliberately exposes cards that may be ineligible
for selection. Peek revisions therefore report
`coverage=NATIVE_PEEK_VIEW_NOT_SELECTABLE`, with an empty selectable
offer set; a later return from peek produces a normal visible-offer
revision. The Inspector never calls the selection predicate, creates
new card IDs, or advances RNG. Missing native card IDs are reported as
incomplete UI projection.

For a synchronized `PlayerChoiceReceived`, the auditor now reports
both `selection_in_initial_visible_options` and
`selection_in_latest_captured_options`. The latest result becomes
unknown if the last observation was a peek view or had an incomplete
card-ID mapping. These are **source snapshot comparisons**, not a
certification that every intermediate native UI change was observed
or that all hand-card interactions are legal. In particular,
`complete_menu_history=NOT_VERIFIED` remains explicit. Other card
selection screens are still uninstrumented; the full combat legal
menu remains `NOT_TESTED`.

For rapid card plays, optional Harmony **postfixes** on native
`NPlayerHand.CanPlayCards()` and
`NPlayerHand.AreCardActionsAllowed()` capture the *original boolean
results* of the mouse/click and shortcut input gates. These source
methods are already called by the game's UI; the Inspector never
invokes them itself. New `native_ui_hand_gate` records include the
gate name, its actual returned value, the current hand mode, and
whether a previous card play was active. A `true` result means that
the particular native UI gate evaluation passed. It does not imply
the chosen card and target were legal, that the queue accepted an
action, or that a complete legal-action menu was enumerated.

For rapid card plays, an optional Harmony **prefix** on pinned private
`NPlayerHand.StartCardPlay(NHandCardHolder,bool)` records
`native_ui_play_begin`, the original card ID, and
`started_via_shortcut`. Both mouse-click and shortcut paths call this
method only after their own native UI guards. This is a genuine native
UI card interaction; drag or targeting can subsequently cancel it.
`manual_play_accepted` and `full_legal_menu` remain `NOT_TESTED`
at this early boundary.

A separate Harmony **postfix** on pinned
`CardModel.TryManualPlay(Creature?)` records the original boolean
return (`native_manual_play_result`), card and target IDs. The source
returns `true` after `CanPlayTargeting` and calling
`EnqueueManualPlay`; therefore `true` proves a successful *manual
play request* at that source, independently from the eventual queue
state, execution, or a complete stable menu. Source request and executor
object correlation already record subsequent queue lifecycle. Source
`human_origin` remains `UNVERIFIED` since a mod could call the same
public manual-play method.

All the optional input/UI hooks share the existing
`STS2_FIDELITY_INPUT_HOOK=1` opt-in; their individual installation
statuses are logged and declared in sidecar headers, allowing partial
coverage if one native method has changed. The first-gap report now
includes displayed native hand options, UI-origin interactions, and
manual-play return candidates alongside the executor chronology.
Matching by card ID is a **candidate correlation**, not verified
unique input-to-executor identity. The existing strict
decision recorder remains fail-closed at unknown boundaries and the
spectator endpoint remains read-only.

**Implementation boundary:** the audited v111 hand UI validates
inputs through several separate methods (hand-mode gates, card
targetability, native `TryManualPlay`, queue admission and later
execution). These are event-driven checks; the audited code does not
expose one comprehensive enumeration of all legal card/target
combinations. The Inspector now records each observed gate *without
calling native eligibility checks a second time*. A future exhaustive
native menu would require additional proof of how and when every
candidate is checked. Complete menu coverage remains `NOT_TESTED`.

For **mechanics lockstep**, however, full native legal-menu enumeration
is not a prerequisite: the observer has actual source-requested,
queue-returned and executor-started actions with real card/target
identities and pre-request snapshots, plus choice ID/selected card
evidence. The next engineering milestone should consume that
source-observed action chronology in a persistent emulator, preserving
async queue ordering, while testing native/emulator state and RNG
parity at well-defined settlement boundaries. Compare the **emulator's**
own legal menu with observed accepted commands as a separate fidelity
check. The local native DLL build, real Harmony hook attachment,
passive nonperturbation and lockstep parity remain untested.

### Source chronology handoff toward persistent emulator parity

`tools/native_lockstep_source_journal.py` is an offline, fail-closed
translation of the independent native action sidecar into a compact
source-chronology JSON report:

```fish
python tools/native_lockstep_source_journal.py \\
    --sidecar "$trace/passive-<combat>.actions.jsonl" \\
    --out "$trace/source-action-journal.json"
```

The extractor first requires
`native_action_envelope_audit.CALLBACK_TIMELINE_CONSISTENT`. It
retains the actual run seed and ascension read from `RunState.Rng`,
original request ordinal and canonical action, eventual queue-return
classification, executor start/completion ordinals and measured
before/after snapshots. Source identity is joined only by the **same
native GameAction object** already carried into the sidecar; asynchronous
execution before the request method's postfix is supported. A choice
result is attached by its real synchronized (owner, choice ID), and the
initial hand menu is referenced only when a source-reserved ID binding
exists.

The journal distinguishes `SOURCE_ACTION_CHRONOLOGY_OBSERVED` from
`SOURCE_INPUT_COVERAGE_PARTIAL`, `SOURCE_IDENTITY_MISSING` and
`INVALID_SOURCE_CAPTURE`. Unlinked canonical executor actions and
requests that were deferred or canceled without execution remain
visible. This is **source evidence for a future replay adapter**:
`originating_human_input=UNVERIFIED`,
`emulator_replay=NOT_EXECUTED`, and `emulator_parity=NOT_TESTED`.
The extractor neither applies actions nor assumes a stable pre-request
decision menu. The next lockstep adapter should stage the source
requests in order and compare native and emulator states at agreed
settlement boundaries, with native RNG stream checkpoints.

The new async choice-ID, menu-refresh and UI-gate hooks still need a
single combined validation against the installed v111 assembly.
Opening, revising and closing the native hand-selection UI are now
instrumented offline; further unobserved UI transitions are possible.
The current source linkage is verified by isolated flow tests but
requires installed-game compilation and runtime evidence to certify
actual Harmony async execution-context behavior.
A following native DLL build and passive-on/off game test will be bundled
with these changes, not requested separately for this interim slice.

**Next combined native validation:** only after all source hooks, seed
projection, strict menu/source acceptance and tests are ready, rebuild the
mod once against the installed game, enable the new
`STS2_FIDELITY_INPUT_HOOK=1` alongside the existing L0 flags, and exercise
a Silent A10 run with a Survivor discard and rapid consecutive card plays.
Include passive capture on/off against the same seed. One game session should
collect both the request/choice source evidence and persistent RNG checkpoints;
a separate controlled comparison run is needed to test nonperturbation.

## 4. Local native acceptance (fish, WSL on Windows game install)

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

## 5. L0 acceptance and next engineering gate

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

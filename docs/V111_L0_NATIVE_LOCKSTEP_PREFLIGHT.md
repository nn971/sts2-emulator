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

### Source run-start to first-combat prelude (offline installed-hook proposal)

The previous combat-only action sidecar began recording only after the
first native combat frame, so it could not prove which map coordinates
or run-start settings preceded that encounter. The reference mod now
subscribes, **only after exact native DLL SHA pin acceptance**, to pinned
`RunManager.RunStarted(RunState)` and `RunManager.RoomEntered`. The
source callback reports the true `RunState.Rng.StringSeed`, ascension,
character ID and first-act ID. Each room-entered callback reports its
actual `RunState.CurrentMapCoord`, act/floor, room model/type, HP and
gold. An independent bounded, nonblocking
`NativeRunPreludeRecorder` writes to a
`run-prelude-<timestamp>-<run-id>.jsonl` file.

At run start, each source room arrival, and first combat attachment,
the mod now also reads **both** the native `RunRngSet.ToSerializable()`
and `PlayerRngSet.ToSerializable()` values. These methods copy the
source RNG counters and all four xoshiro words from each named run and
player stream; they do not sample or advance RNG. The resulting
`run_rng_snapshot` records include the source site, player RNG seed,
scope/stream identity, exact counters and unsigned 64-bit words. The
offline auditor checks the 64-bit ranges, stream set consistency,
source ordering, read-only status and run-to-combat linkage. These
checkpoints are significant for diagnosing **precombat RNG cursor
differences**, while `native_rng_precombat_history_complete=false`
and `emulator_rng_alignment=NOT_TESTED` remain explicit: snapshots
do not reveal every stochastic call or guarantee equivalent
emulator world-generation state.

When the source input hook is separately enabled, it also observes
precombat `VoteForMapCoordAction` and `MoveToMapCoordAction`
`RequestEnqueue` prefix/postfix pairs. In the pinned v111 source
their `ToNetAction()` methods only project private map fields into
value-type structs, so reading their source/destination coordinates
does not sample RNG or enqueue actions. This preserves **human-origin
and queue-acceptance uncertainty**, and the independent
`RoomEntered` callback provides actual visited coordinates without
asserting that a particular queued request caused them.

The same existing pinned `RequestEnqueue` hook now also captures
**opaque precombat queue action types** using only
`GameAction.GetType().FullName`, paired with their source
`RequestEnqueue` returns by reference identity. The
`opaque_action_request` / `opaque_action_outcome` records help identify
which previously missing precombat decisions reach the native action queue.
No payload, choice value, owner or accepted executor action is inferred.
The independent auditor checks these pairs and reports
`observed_precombat_queue_action_types` separately from mapped moves.
An unreturned opaque request keeps the prelude open or marks it incomplete.
This leaves Neow/event/reward selection and replay completeness explicitly
**unverified**, particularly for decisions implemented outside the queue.

### Original event-option selection before first combat

In pinned `Flaaax/STS2-source@706ef1c9`, **Neow is an
`EventModel` with choices executed through
`EventSynchronizer.ChooseLocalOption(int)`**. It is not a
`PlayerChoiceSynchronizer.PlayerChoiceReceived` choice. An optional
read-only Harmony prefix/postfix on the original event synchronizer now
records the local mutable event's `Id.Entry`, owner, exact ordered
`EventModel.CurrentOptions` (option index, `TextKey`, locked/proceed
flags and associated relic model ID) and the selected native option index.
The postfix pairs the same request with the method's synchronous return.
The offer is read **before** the original method executes, because an
event selection can immediately replace `CurrentOptions`.

The event-choice prefix additionally snapshots the *event-local*
`EventModel.Rng.ToSerializable()` counter and four xoshiro words.
Pinned `Rng.ToSerializable()` only copies the source state; it never
calls `Next*`. This is distinct from named run/player RNG streams,
and provides a checkable **post-offer, pre-selection** local-event RNG
checkpoint. It cannot reconstruct the random calls used to generate
the offer or certify an emulator event RNG alignment.

The records `event_choice_request` and `event_choice_return` are
source evidence of a local synchronizer call and its return. The return
can precede completion of async `EventOption.Chosen()` effects.
Only the complete original-game prelude can later certify the resulting
resource/relic/deck changes and RNG. A missing postfix fails the audit.
The observer does not run any event option callbacks, roll RNG,
inject a choice, or infer any `PROCEED` button (which takes a separate
UI path). A dedicated pinned-assembly build and runtime attach are still
required before claiming that this source-level hook works in the game.

The existing `PlayerChoiceSynchronizer.PlayerChoiceReceived` callback
is now subscribed at **source RunStarted** (after synchronizer construction
in pinned `RunManager.Launch`). Prior to combat attachment, it records
`player_choice_result` with the original source owner/choice ID and
selected Index/CombatCard/DeckCard/CanonicalCard/Player result values.
MutableCard results preserve count and owner, not full card content.
The bridge checks the synchronizer reference when entering combat to
avoid duplicate subscriptions, and keeps the combat sidecar's existing
choice handler. This source result journal complements the event-option
hook: **Neow is an event option**, while nested card/deck selections
may use PlayerChoiceSynchronizer. These results lack corresponding offered
options and choice-context effects; the auditor reports them separately,
rejects duplicate (owner, choice ID), and never marks macro replay complete.

On its first combat, the run prelude records a `combat_link` containing
the exact combat trace run ID and filename. The actual
`RunManager.RoomEntered` callback runs **after** awaited native
`room.Enter`, and may therefore arrive after the combat observer has
already attached on a frame. The prelude stays open until both the
linked combat's actual room-arrival callback and all outstanding map
`RequestEnqueue` postfixes have been observed; it then closes.
If combat ends before those callbacks arrive, it terminates as
`INCOMPLETE_CAPTURE`, preserving the missing evidence. It remains
**source route evidence**, with
`source_macro_choices_complete=false`. In particular, Neow choices,
events, card/reward/shop/rest choices, potion/relic/deck changes, and
full **precombat native RNG continuation** are not yet captured as an
executable macro action transcript.

To audit the run prelude alone, or pair it against the existing
independently audited action sidecar:

```fish
python tools/native_run_prelude_audit.py \\
    --prelude "$trace/run-prelude-<timestamp>-<run-id>.jsonl" \\
    --combat-sidecar "$trace/passive-<combat>.actions.jsonl" \\
    --out "$trace/run-prelude-audit.json"
```

Pairing checks the native DLL fingerprint, source seed/ascension,
exact combat trace ID, awaited matching room-arrival evidence, and
zero missing native map queue postfixes. It keeps `native_macro_action_transcript_complete`,
`emulator_combat_bootstrap` and `native_emulator_parity` negative or
unverified even when the visited map coordinate and queued move request
happen to coincide. Older captures lack this source prelude: the
auditor cannot infer missing evidence from their combat-only traces.
This integration remains **offline-tested only** until the next
combined installed-DLL build/game validation.

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

### Persistent emulator shadow session (offline implementation)

`tools/native_lockstep_shadow.py` consumes a fresh raw sidecar (audited
through `native_lockstep_source_journal.build`) and opens **one real
persistent JSONL emulator process** through the existing
`bindings/python/sts2_emulator.Session`. It loads an **independently
prepared exact emulator snapshot** with
`sts2-exact-snapshot-v1`/ `Session.load_snapshot`, retains the state
handle between source actions, looks up the uniquely matching emulator
`legal_actions` entry and calls `step`. The source and emulator
observation surfaces are compared only on structurally compatible
visible scalar and card-hand fields.

It deliberately **does not** synthesize an emulator combat state from
the native partial `CaptureCanonicalState` projection. The core
`NativeCombatSnapshotImporter` explicitly reports
`ReadyForDeterministicStep=false`: full pending effects, hidden card
state, relic/potion continuations and native RNG are not imported. A
naive `Session.reset(seed)` also begins at run start, so it cannot
automatically align with a captured combat.

A genuine invocation needs the matching snapshot exported by the
emulator after an independently verified route to this combat, plus
an explicit native-to-emulator card and creature ID crosswalk:

```fish
python tools/native_lockstep_shadow.py \\
    --sidecar "$trace/passive-<combat>.actions.jsonl" \\
    --emulator-snapshot "$trace/aligned-emulator-exact-snapshot.json" \\
    --identity-crosswalk "$trace/native-emulator-ids.json" \\
    --cli-dll ./src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll \\
    --out "$trace/native-emulator-shadow.json"
```

Crosswalk schema (explicit identities, **never automatically guessed**
from similar-looking numeric IDs):

```json
{
  "schema": "native-emulator-id-crosswalk-v1",
  "cards": {"7": 101, "9": 102},
  "enemies": {"creature:2": 5}
}
```

The example IDs illustrate wire formatting **only**; they are not
mappings for a real game capture. The shadow runner checks run
seed/ascension, presence of a real emulator combat continuation,
source visible-state alignment and exact action payload matching
(native `target_id` versus the actual emulator `TargetEnemyId` in the `PlayCardPayload` JSON element; its card field is `CardInstanceId`) before
stepping. It stops on unmatched/multiple legal commands, source
nested-choice continuations, incomplete or wrong ID maps, and rapid
overlapping native requests (whose pre-request snapshots are not
sequential emulator decision states). It never substitutes an
automatic action choice to make progress.

Results such as `SHADOW_STEPPED_VISIBLE_ONLY` or
`SHADOW_VISIBLE_DIFFERENCE_UNCERTIFIED` explicitly retain
`native_game_parity=NOT_TESTED`,
`emulator_rng_alignment=NOT_TESTED`,
`native_boundary_settlement=UNVERIFIED`, and
`verified_native_state_import=false`. The code and isolated tests
demonstrate persistent stepping, **not** real-game lockstep fidelity.
Reaching a full fidelity verdict still requires verified native
continuation/initial combat alignment, native RNG provider and settled
boundary attribution.



The new native run-prelude and RNG snapshot observers, async
choice-ID, menu-refresh and UI-gate hooks still need a single
combined validation against the installed v111 assembly.
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

### Independent run-start to first-combat bootstrap (first offline slice)

`tools/native_run_bootstrap.py` now accepts the **independently audited**
`run-prelude-*.jsonl` and starts a fresh persistent `reset_v111` session
using the exact native seed/ascension and Silent/Overgrowth identity. It
requires the unique `start_run` action, then independently observes
Neow's three offered choices. The recorded native Neow `relic_model_id`
and original `NEOW.pages.INITIAL.options.RELIC_ID` text keys are matched
against the pinned supported Neow relic catalogue and translated into
emulator `take_proto.native.neow.<name>` IDs. The **entire ordered offer**
must match both the emulator observation and its legal menu before the
observed selected option is executed.

The first slice supports exactly one Neow selection and a single
source-observed first-floor room arrival leading directly into combat.
It uses the actual native `RoomEntered` coordinate as the destination
evidence; matching a `map_vote` request is insufficient. The emulator
map node must be present in the generated map and in the unique legal
`choose_map_node` action. It then steps into actual emulator combat,
compares source room-callback HP/gold with the emulator initial combat
observation **as an uncertified temporal comparison**, and can export a
candidate **emulator-produced** exact snapshot:

```fish
python tools/native_run_bootstrap.py \\
    --prelude "$trace/run-prelude-<timestamp>-<run-id>.jsonl" \\
    --cli-dll ./src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll \\
    --candidate-snapshot-out "$trace/candidate-emulator-first-combat.json" \\
    --out "$trace/native-first-combat-bootstrap-report.json"
```

The adapter stops on unaudited prelude, other queued actions, unresolved
nested choices, unhandled multi-room routes, missing native identities,
disagreeing Neow offers, unsupported post-Neow choice continuation,
missing/legal-map-node divergence, or unexpected combat transition. It
never selects a substitute card, route, or event action. Results include
`SOURCE_NEOW_OFFER_DIFFERENCE` and
`CANDIDATE_COMBAT_REACHED_UNCERTIFIED`; neither is a certified
native/emulator parity verdict. The native event-local xoshiro state
and emulator prototype RNG **are not directly comparable** under the
current stochastic provider. Source RNG history, async Neow effect
settlement, native mechanics, identity crosswalk and lockstep alignment
remain unverified. A candidate snapshot must not be supplied to
`native_lockstep_shadow.py` as an aligned checkpoint without a separate
fidelity proof.

The integration workflow also runs `native_run_bootstrap_jsonl_smoke.py`: it
constructs an explicitly **emulator-derived synthetic** Neow offer and
first-floor source-shaped journal, passes the strict source auditor, then
replays it through a fresh *real emulator JSONL process*. This exercises
transport, source identity mapping, legal Neow/map actions and actual
combat snapshot export; by construction it proves **no native parity**.

For Neow's finished event page, pinned emulator `GetEventActions`
exposes a unique empty-payload `continue_event` before map choice.
The adapter steps this **only when it is the sole legal action**, and
records `native_proceed_ui=NOT_CAPTURED`: the native `NEventRoom.Proceed`
click is a distinct UI path, not recorded in the source event-choice
prelude. Nested deck/card/potion selection and any nonunique terminal
menu still fail closed. This extra transport step retains the
`CANDIDATE_*_UNCERTIFIED` status.

### Independent native Neow local-RNG reconstruction

`tools/native_neow_rng_verify.py` reconstructs Neow's **native** RNG
from the original `RunState.Rng.StringSeed`, using the pinned
`RunRngSet(string)` xxHash64 seed, `EventModel.BeginEvent`'s
singleplayer Neow ID/slot mix, `MegaRandom.Splitmix64` and
`xoshiro256**` state updates. Each native Neow `NextInt` or
`NextBool` consumes one 64-bit draw; the recorded preselection
`event_rng.counter` therefore determines the four words without
replaying offers or inspecting the hidden game state.

The bootstrap now checks this counter/word reconstruction **before
starting its emulator session** and blocks on the first mismatching
word as `BLOCKED_NATIVE_NEOW_RNG_RECONSTRUCTION`. This separates
native source capture/seed errors from the emulator's independent
`PrototypeRng` offer generation. The independent native state
reconstruction is source-algorithm consistency, **not** native-vs-emulator
RNG or offer parity. The current verifier deliberately blocks legacy
`old...` seed hashing, excessive counters, and unknown event IDs.
Testing includes standard xxHash64 vectors and deliberately perturbed
source words, with no installed-game launch.

### Pinned Neow event-local RNG verification

`NativeNeowOfferV111` reuses the independently implemented
`NativeRngV111` SplitMix64/xoshiro256** codec and derives its
standalone event-local seed as `RunSeed(seed) + xxHash64("NEOW")`.
The pinned `Neow.GenerateInitialOptions` call graph is implemented
without affecting the emulator's prototype RNG, including conditional
positive groups and order-dependent Fisher–Yates shuffle.

The source's `Kaleidoscope` and `ScrollBoxes` availability depends
on unlocked card pools, so the predictor enumerates all four hypotheses.
Two hypotheses yield the **same RNG state** (equal positive-pool sizes)
and may yield different offers. Consequently the verifier checks the
**entire ordered offer AND all five RNG snapshot fields**, never inferring
an unlock status from the counter alone.

A read-only `native_neow_rng_probe` JSONL operation returns those four
hypotheses. The strict `native_neow_rng_probe_audit.py` checks source
offer/model IDs and the original `event_rng` snapshot against the
predicted source RNG. The bootstrap now requires a single matching
hypothesis for captures whose `game_build` has the pinned native
assembly/mvid/SHA-256 format. It stops before emulator reset if source
Neow reproduction differs, or if the build format is unrecognized.
Only explicitly `EMULATOR-DERIVED-SYNTHETIC-NOT-NATIVE` data bypass
this verification, retaining the explicit skip indicator.

This is **source Neow RNG reconstruction only**, not native/emulator
Neow-menu parity, map RNG parity or runtime Harmony validation.
A matched hypothesis means its unlock filter is *consistent with*
the capture, rather than independently observed unlock state.

### Native named-stream source reconstruction

`tools/native_run_rng_verify.py` independently checks the 12 pinned
`RunRngType` streams and 3 `PlayerRngType` streams. It derives each
seed using the native source's UTF-8 xxHash64, snake-case name hash,
SplitMix64 and xoshiro256**. It validates source `player_rng_seed`,
stream identity, counter, and all four state words at the recorded
run-start/room/combat checkpoints. The first mismatch has an exact path.

On pinned native source builds, the independent bootstrap now checks these
streams **before emulator reset**, and blocks on a mismatch. Synthetic
fixtures are explicitly excluded from the native-source verification.
This check certifies source-algorithm consistency of captured native
checkpoints only. The emulator's prototype map generation and native RNG
parity remain separately unverified.

### Live v111 Neow event-local native RNG integration

For the explicit `v111-experimental-native-neow-opening-v1` replay
profile, `V111RunFactory.Initialize` constructs Neow's actual three
offered actions through `NativeNeowOfferV111.Predict` and
`NativeRngV111`, using the independent content-local event seed,
inside the ordinary `start_run` state transition. The adapter retains
the source-equivalent order, curse exclusions, conditional extra options,
and Fisher–Yates shuffle. The pinned v111 simulation profile is fully
unlocked, so the implemented runtime assumption is that both
Kaleidoscope and ScrollBoxes are available; the source inspector's
four-hypothesis Neow offer probe remains the independent check against
that assumption. All 15 persistent named run/player streams remain
unaltered by local Neow draws.

Other event models, native map RNG/call-order, and actual-game hook
fidelity are still unverified. The original prototype-only
`PrototypeNativeOvergrowthRunFactory` remains unchanged for existing
legacy training/research consumers. Runtime tests assert the *actual*
v111 `start_run` event menu equals native-algorithm offer reconstruction,
including the native RNG named-stream nonmutation invariant.

### Native first-event ownership in the actual v111 transition

The v111 `start_run` path generates Neow's options **inside**
`PrototypeNativeOvergrowthRunFactory.Create(..., useNativeNeowEventRng: true)`.
This uses the same pinned `NativeRngV111` event seed and source draw
sequence as the independent predictor. It skips the prototype `event`
generator completely for Neow, rather than generating and discarding
an alternative event menu. The post-offer local xoshiro state is retained
as `RunState.NativeEventRng` (deep-forked and included in exact snapshots)
until the Neow event instance exits. All native named run/player streams
are independently retained in `RunState.NativeRunRng`; local Neow
generation must not advance those persistent counters. The explicit
legacy `PrototypeNativeOvergrowthRunFactory.Create(seed, ascension)` path
keeps its original prototype behavior for research compatibility.

**Map RNG ownership correction.** Pinned source
`StandardActMap.CreateFor` constructs `new Rng(runState.Rng.Seed,
"act_{CurrentActIndex+1}_map")`. This is a separate *content-local
act-map stream*, already modeled by
`NativeRngV111Contexts.ForActMap(seed, actNumber)`, not
`RunRngSet.UpFront`. Exact map geometry and RNG draw/call-order still
need native source-equivalent integration. The existing
`PrototypeNativeOvergrowthMap.Generate` uses prototype RNG; simply
changing its stream name would not establish map parity. The inspector
must report the first native-vs-emulator map divergence before entering
first combat and never infer native success from synthetic data.

**Remaining boundaries.** Source Neow unlock availability is assumed
fully unlocked for the current v111 simulation profile; the source
capture/probe continues to test four unlock hypotheses independently.
Native mod runtime nonperturbation, map generator, settled event effects,
and first-combat parity are not yet certified.

### Replay-only native Neow opening profile and training isolation

Use `reset_v111` with `fidelity_id=v111-experimental-native-neow-opening-v1`
for strict first-combat source-prelude replay. The independent bootstrap
selects this profile explicitly. In this mode the Neow event-local native
RNG is initialized and retained in `NativeEventRng` by the opening
factory without consuming a discarded prototype event draw. Ordinary
v111 simulations and legacy training preserve their previous Neow
choices and RNG. Source-side unlock gates and game-runtime fidelity are
still unverified; the inspector must compare the observed complete
source menu, not accept a model-predicted menu as source evidence.

### Experimental source-equivalent StandardActMap opening (2026-11-01)

The native-game `StandardActMap.CreateFor` in pinned
`Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20`
allocates an independent `Rng(runSeed, "act_1_map")` for both Overgrowth
and Underdocks. The **type-count Gaussians precede the seven source paths**;
the complete source algorithm also performs type-queue assignment,
duplicate-path pruning and repair, then deterministic geometry cleanup
(center, spread, straighten). These operations cannot be replaced with
an equivalent-looking prototype map or a changed RNG stream name.

`NativeStandardActMapV111.Generate(seed, ascension)` implements this
source-shaped sequence with the existing audited native RNG codec and
`NativeRngV111Contexts.ForActMap`. Its diagnostic result exposes
`AfterQuotas`, `AfterPaths`, `AfterTypes`, `AfterPruning` and
`FinalRng` snapshots, the finalized `MapState`, and its source
type quotas. The translated graph and stream are independent of
`NativeRunRng` (15 persistent streams) and `NativeEventRng` (Neow).

The separate, **explicitly experimental** v111 fidelity profile
`v111-experimental-native-standard-map-opening-v1` selects both this
native-map construction and the existing native Neow event-local RNG
in the persistent `start_run` transition. `RunState.NativeMapRng`
is retained through snapshots and forks. All prior training and
Neow-only profiles keep their previous prototype map generator.

The prelude bootstrap now selects this map profile **only for pinned native
source-build captures**; explicitly emulator-derived synthetic fixture
transport remains on the older Neow-only profile. The bootstrap still
reports an uncertified candidate until the source offers, native full map
and map-local RNG checkpoints, selected room, mechanics and decision
boundaries have been separately verified against the installed game.

**Acceptance still outstanding:** capture and compare the *entire*
original `StandardActMap` topology and room types, original map-local
RNG state/counter at a certified source boundary, postprocessing node
coordinates, and the first accepted map choice. A matching first visited
node alone is insufficient. This update does not complete L1, claim
original-game parity, or require a new game launch.

### Full source-map capture and comparison

The passive reference mod records an initial_map_snapshot containing the finalized native StandardActMap graph (starting ancient, boss, all grid points, types and edges) and the read-only map-local RNG cursor. Capturing the cursor uses ToSerializable and does not advance the generator.

The run-prelude auditor checks provenance and structural consistency. The native_map_fidelity Python comparator checks all nodes, edges, entry choices and map RNG words against an independently generated emulator start_run snapshot. The native prelude bootstrap runs this comparison before replaying any observed Neow or map choice.

The original game and emulator map are not yet proven equal: a fresh v111 source capture and local STS2_NATIVE build are outstanding. Synthetic test data provides regression coverage only.

## Original-game v111 pinned-seed acceptance evidence (2026-11-01)

A single staged reference-mod run against the pinned native v0.111.0
`sts2.dll` (SHA-256 `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`)
produced a complete first-combat run prelude and two combat trace/sidecar
pairs. The minimum public, mechanics-only expectations are retained as
`NativeObservedMapV111Tests`; no user IDs or raw logs are committed.

**Independently matched:**

- Silent A10 seed `6JK8MD0VQQTA`, Overgrowth, exact first
  `StandardActMap`: 74 source nodes including Ancient and Boss,
  73 emulator map nodes (Ancient is represented as entry IDs),
  all room types, coordinates, links and entry nodes.
- Source map-local `act_1_map` cursor **272**, with **all four
  xoshiro words equal** to the independently generated emulator map RNG.
- Neow offered `PHIAL_HOLSTER`, `LOST_COFFER`,
  `SILVER_CRUCIBLE` in that order; independently reconstructed
  event-local `NEOW` RNG cursor **19** and all four words.
  The matching availability profile permits Kaleidoscope and Scroll Boxes.
- First map visit (row 1, col 0) and weak combat identity
  `FUZZY_WURM_CRAWLER_WEAK`; first combat player HP **56**,
  gold **99**, single Fuzzy Wurm Crawler HP **59/59**.

**First combat's native opening shuffle/HP are now reproduced.**

The pinned source's `Player.PopulateCombatState` calls
`CardPile.RandomizeOrderInternal`, which performs
`UnstableShuffle(run.Shuffle)` across the entire ordered 13-card
Silent A10 starting deck (including Ascender's Bane). The native
draw pile exposes its next card at the **front**, whereas the
emulator's existing engine draws from its **last** internal index.
The explicit native-map fidelity profile now runs the independent
native xoshiro stream's Fisher–Yates and reverses only its stored
post-shuffle array. The ordinary/training profile is untouched.

The source-observed first hand now matches exactly, in order:
`Strike, Neutralize, Ascender's Bane, Defend, Strike, Defend, Strike`.
The remaining six-card draw pile also matches in top-first order.
Additionally, native encounter formation is now enabled for this
profile, so Fuzzy Wurm Crawler's **59/59 HP** is sampled from
`run.niche` instead of the prototype combat stream. Persistent
`run.shuffle` counter **12** and `run.niche` counter **1**,
including all four state words of each, match the native source
capture in `NativeObservedMapV111Tests`.

**Full-unlock Phial Holster RNG and source-ordered UpFront startup reconstruction.**

- `run.combat_potion_generation`: native first combat **4** and
  emulator first combat **4**, including **all four identical
  xoshiro state words**. The source's `PhialHolster.AfterObtained`
  uses `PotionFactory.CreateRandomPotionsOutOfCombat` on the three
  `Silent4Epoch` models followed by the 45 shared potion models.
  The newly implemented source-ordered 48-model selection makes a
  rarity float roll and one bounded choice per potion, then removes
  that potion before the next selection. On this seed the predicted
  rarities are Uncommon and Common, and the resulting potions are
  **Fortifier** and **Vulnerable Potion**. The native passive trace
  did not record potion IDs: **the cursor is source-observed parity,
  while those two identities are source-derived predictions**.
- `run.up_front`: Native `RunStarted` at **412**. The source
  uses different relic-bag overloads: shared `Populate(IEnumerable)`
  retains all **118** source relic models across six rarity buckets,
  including one Event relic and two Ancient relics. Shuffling the
  two-Ancient bucket consumes **one** extra draw (the Event singleton
  consumes zero). Player `Populate(Player)` excludes Event/Ancient
  rarities, leaving 122 eligible relics in four buckets. The native
  relic shuffles therefore consume **112 + 118 = 230** UpFront draws,
  correcting the previous erroneous 111 + 118 = 229 model.
- For seed `6JK8MD0VQQTA`, corrected source-ordered checkpoints are
  **230** after relic bags, **232** after allocating shared Ancients,
  **262** after Act 1 event shuffle, **295** after Act 1,
  **355** after Act 2, and **412** after Act 3. The final
  182 calls include five rejected grab-bag candidates (1, 2, 2).
  The final four xoshiro words remain
  `[14133024965363381498, 3052595882297166200,
  14908445774459425184, 16826779579573568376]`.
- On **2026-11-01 10:37 UTC**, an independently observed native
  `startup_room_queues` record captured every event, normal encounter,
  elite, boss, second boss and Ancient identity for all three acts at
  `RunManager.RunStarted` (17-record run prelude). The corrected
  C# reconstruction matches **every native queue identity and position**
  in a sanitized golden regression. The omitted shared-bag draw had
  shifted Act 1 events/encounters, but an extra rejection later
  cancelled that shift at the end-of-act counter. It also placed the
  shared Ancient Darv in Hive instead of Glory, predicting Nonupeipe
  rather than the native-observed Glory Ancient **Tanx**.
  With the fix, Act 1's first three weak encounters are
  Fuzzy Wurm Crawler, Slimes and Nibbits, and Darv belongs to Glory.
  Neither earlier matching final UpFront words nor a matched boss
  could independently establish this queue identity agreement.
- Native map combat entry now records a forkable, diagnostic-only
  `native_startup_queue_trace`: source queue index, source encounter ID,
  uniquely crosswalked prototype encounter ID, actual entered encounter ID,
  and a match/difference status. This leaves encounter selection, training
  profiles and all RNG streams unchanged. Crosswalking supports both
  `proto.encounter.*` and region-qualified `proto.native.<region>.encounter.*`,
  with fail-closed act/room/uniqueness checks. The corrected first
  source queue predicts `FuzzyWurmCrawlerWeak`, matching the native
  observation. Actual prototype combat selection remains independent
  of this diagnostic trace.
- Native prelude replay now separately checks the **actual** emulator
  encounter identity against the original game's `RoomEntered.room_model_id`
  after entering combat; a different or missing identity blocks that
  comparison even if RNG/HP/gold happened to agree.
- `native_run_bootstrap.py` separately compares the observed
  encounter prefix and all three native `ActModel._rooms` queues
  against source-generated predictions when captured. The 2026-11-01
  prelude is complete and supplies all three arrays; source queues
  remain **diagnostic-only** and do not change prototype combat
  selection. The full source-replay route is Overgrowth -> Hive ->
  Glory. Experimental Underdocks retains the corrected 230-draw
  relic-bag prefix until its source room generator is implemented.
- The first combat HP, map, Neow, encounter, opening hand/draw pile,
  `run.shuffle`, `run.niche`, and Phial Holster's native potion
  RNG are verified against original-game observations. Native potion
  identities, broader per-action combat transitions and postcombat
  rewards remain separate acceptance gates. Selected early combat
  actions and startup room/Ancient identities now have independently
  checked native visible projections for this one pinned seed.

**Capture limitations and later trace coverage:**

- Run prelude: 16 ordered records, one complete source map snapshot,
  Neow choice, first map vote/move outcomes and combat link. It still
  declares `source_macro_choices_complete=false`; queue acceptance
  and human-origin provenance are not fully certified.
- First combat: 16 complete strict decision/action/transition
  lifecycles; the strict trace safely stops at Survivor's nested card
  choice. The action sidecar continues through 26 executor actions.
- Second combat: two strict decisions, of which the second is
  incomplete due to `executor_action_without_unique_strict_decision`
  during a card-play animation. Its sidecar observes 21 executor
  actions. It is not evidence of strict lockstep success.
- Hook installation, map observation and first-combat capture are
  confirmed by original-game logs. These do not establish that every
  source draw or mechanical operation has been reproduced by the
  emulator.

### 2026-11-01 independent combat action-prefix replay (native observations)

A new C# regression, `NativeObservedFirstCombatActionsV111Tests`,
reconstructs Silent A10 seed `6JK8MD0VQQTA` independently using the
source-shaped Neow choice (Phial Holster) and first map node `1:1:0`.
It builds an explicit, identity-checked native-to-emulator card map from
the source-observed initial hand and top-first draw pile; the game and
emulator have distinct card instance numbers.

The test replays six original native player card actions and two
`end_turn` commands through the **actual `PrototypeGameEngine.Step`**.
It compares executor-callback HP, energy, block, enemy HP and visible
hand/discard sequences for Neutralize, Defend, Strike, Strike (turn 1)
and Strike, Strike (turn 2). It separately checks stable turn-2 and
turn-3 decisions, enemy Weak and Strength(7), draw/discard order, and
the source `run.shuffle` cursor. The second end-turn is a source
`CardPileCmd.Shuffle` recycle: the pin has ten fresh RNG selections,
so the state advances from 12 to **22** calls on `run.shuffle`,
independently of initial-shuffle setup.

This exposed a real source-shuffle difference: the ordinary prototype
combat `ShuffleRecycle` uses its synthetic stream and unsorted discard
order; pinned v111 `CardPileCmd.Shuffle` uses
`StableShuffle` (source model/upgrade order, then `run.shuffle`).
`NativeSourceRecycleShuffleV111` now reconstructs the source-shuffle
order and stores the reversed top-first pile within the **native-map
fidelity profile only**. Its RNG advances are applied at the
combat-owned RNG boundary, so `NativeRunRngV111.ReconcileAfterStep`
retains the advanced stream rather than restoring the prior cursor.
Other combat-owned streams are preserved. The existing explicit
scripted shuffle replay takes precedence, and ordinary training runs
retain the existing shuffle implementation. The source RNG is forked
only when recycling occurs.

**Evidence level:** these are independently simulated *visible
mechanics matches at selected observed callback/decision boundaries*
for one seed and one first combat. Source executor callbacks are not
certified settled decision boundaries. Full legal menus, queued or
overlapping inputs, nested choices, downstream combat completion and
other seeds remain unverified. The strict decision-9 gap remains a
separate inspector capture issue.

### 2026-11-01 strict decision-gap retrospective (source-only)

The newly captured Silent A10 first-combat run for seed
`6JK8MD0VQQTA` has a **complete 17-record run prelude**,
**9 strict decisions** (8 complete transitions, decision 9 incomplete)
and a **909-record native action sidecar**. The sidecar observes
26 source executor start/completion pairs across six native turns,
including automatic `ReadyToBeginEnemyTurnAction` entries.
These are source observations, not emulator lockstep evidence.

The **first strict input gap is action-sidecar ordinal 459**,
executor action ID 12 (`PlayCardAction`, Defend). Its source timeline is:

| Native ordinal | Observed event |
| --- | --- |
| 414 | Strict stable decision 9, starting turn 3 |
| 417–419 | UI begins Neutralize and native executor action 11 starts |
| 445–446 | Original native UI card-action gates return allowed |
| 447 | Native UI begins Defend while action 11 is still running |
| 453 | Native action 11 completes |
| 456–457 | Defend's native enqueue request and executor start |
| 459 | `strict_input_gap`, cached gate `WAITING_FOR_DECISION:CARD_PLAY_ANIMATION` |

The UI-start/predecessor-completion/enqueue order demonstrates a
**native UI play beginning during the preceding executor action**.
It explains why a frame-only strict recorder can miss the next fresh
decision boundary. The game subsequently observed the Defend request,
execution and manual-play return. This does **not** establish a
new full legal menu or human-origin proof.

`tools/native_decision_gap_audit.py` now separately reports this
source-observed overlap, the request-to-executor identity correlation,
the cached strict frame gate, and observed state-snapshot agreement,
while keeping the primary gap status
`FIRST_UNCAPTURED_EXECUTOR_ACTION_FOUND` and
`legal_menu_reconstruction=NOT_PERFORMED`. Missing or contradictory
timing evidence is classified `TEMPORAL_CAUSE_UNVERIFIED`.
`tools/native_lockstep_source_journal.py` now retains **all native
executor start/complete observations**, including automatic actions,
with explicit `NATIVE_EXECUTOR_CALLBACKS_NOT_SETTLED` provenance.
It labels request/execute and prior-completion/request state agreement
without manufacturing an executable decision boundary or asserting
emulator mechanics parity.

#### Verified terminal history-clear checkpoint (2026-11-01)

The 909-record original v111 sidecar ends with: action #26 completion
(904), `after_action` RNG checkpoint (905), a read-only frame observation
(906), `history_reset` 123→0 (907), `combat_end` RNG checkpoint (908),
and terminal `Combat ended / OBSERVED` (909). All 15 source-observed
RNG stream snapshots match exactly between the two checkpoints.

The strict action-envelope auditor now recognizes this **specific**
terminal-cleanup ordering when no action remains active, all between-checkpoint
observations are frame polls, the preceding RNG checkpoint directly
follows an action completion, and the two checkpoints expose identical
stream snapshots and read-only provenance. This accepts the original
capture without changing any raw records or manufacturing a strict
decision after decision 9. Other history resets remain fail-closed;
the native RNG inventory is still explicitly incomplete.

```fish
python tools/native_action_envelope_audit.py /path/to/existing/actions.jsonl --out /tmp/native-envelope-audit.json
python tools/native_lockstep_source_journal.py --sidecar /path/to/existing/actions.jsonl --out /tmp/native-source-action-journal.json
```

#### Source executor callback comparison after decision 9

The source journal now compares each of the 26 observed executor
start/complete pairs as **source-only callback projections**. It records
measured changes during the executor interval separately from measured
changes between the closest earlier completed executor and the next start.
The comparison also reports the earlier completion ID, callback snapshot
agreement, any still-active executor IDs, and observed intervening record
types. This handles nested executor intervals without confusing them with
the distinct *UI began during previous execution* chronology at the
strict decision-9 gap.

Using the **existing** native action sidecar (no original-game launch):

```fish
python tools/native_lockstep_source_journal.py --sidecar /path/to/existing/action-sidecar.jsonl --out /tmp/native-source-action-journal.json
```

Inspect `source_executor_observed_transitions` by `start_ordinal`; decision 9
was observed at sidecar ordinal 414, followed by the first strict gap at
executor #12 / ordinal 459. The new fields
`source_executor_observed_delta` and
`preceding_completion_to_start_observed_delta` measure only fields
present in both snapshots. `active_prior_executor_action_ids` distinguishes
a genuinely overlapping executor from an earlier UI event that overlaps
a completed executor but queues later. An empty delta is evidence only
for the measured projection; a missing snapshot is reported as null.

The journal also separates canonical player-command candidates from
noncanonical native executor callbacks. The pinned capture has **21**
canonical play/end-turn commands and **5** linked
`ReadyToBeginEnemyTurnAction` executor requests. They are reported in
`source_linked_commands` and `source_linked_noncanonical_executor_actions`,
respectively; neither category is permitted to invent a strict decision
boundary. `source_requests_without_executor_start` represents genuinely
unmatched requests and is empty on the pinned native capture.

The strict decision recorder and its fail-closed legal-menu requirements
are unchanged. This journal can isolate the first observed mechanics gap
and guide a later mapped emulator replay; source callback agreement
alone cannot certify native state settlement, RNG parity, or human input.

**Source UI-input window analysis (existing capture):** the original
sidecar has 16 play-card executor requests but 17 `native_ui_play_begin`
observations. The source journal's `source_ui_request_windows` associates
15 requests with a single temporally matching native UI-begin card ID.
The initial Neutralize input has *two* UI-begin observations (ordinals
50 and 53), so its precise UI-to-request association stays ambiguous.
Every uniquely matched attempt is immediately preceded by both original
native UI guard callbacks returning `true`.

At decision 9, both native guards return `true` at 445–446,
Defend #11 UI begins at **447** while Neutralize executor #11 is
active, Neutralize completes at **453**, Defend request #12 arrives
at **456**, its executor starts at **457**, and the strict gap is emitted
at **459**. The last frame poll preceding the UI interaction was stale:
Neutralize is added to the discard pile before the source request.
This is a real overlapping *UI-input window*, with sequential executor
intervals. The source UI/request correlation is qualified as a temporal
card-instance match; it never certifies an entire legal menu, original
human input, or a settled native decision.

**Prepared future native UI evidence:** `native_ui_play_begin` now stores
an optional canonical state projection from the original
`NPlayerHand.StartCardPlay.prefix` callback, labelled
`ui_state_observation=SOURCE_UI_BEGIN_CANONICAL_SNAPSHOT` (or
`UNOBSERVED` when unavailable). The envelope auditor checks marker/state
consistency, while the journal compares that callback-time snapshot with
the later source request. Previous captures remain valid and explicitly
unobserved in this field. This extra snapshot is read-only and requires
a *future* original-game capture; none is needed for current offline work.

**History-reset issue resolved:** the v111 strict source auditor now
accepts the observed terminal sequence with the unchanged read-only
`combat_end` RNG checkpoint between the 123→0 history reset and the
`Combat ended` terminal event. The uploaded sidecar now reports
`CALLBACK_TIMELINE_CONSISTENT`; the source journal reports
`SOURCE_ACTION_CHRONOLOGY_OBSERVED`. The raw data is unchanged.

**Current first-combat result (2026-11-01):** the independent typed
emulator golden regression now extends through the original 26-executor
first combat. It replays all source-observed card plays, five end-turn
transitions (including automatic enemy-turn setup), and Survivor's selected
nested discard. Measured HP/block/energy, enemy HP and powers, card-zone
ordering, and run.shuffle at both the 22-draw and 31-draw checkpoints
match through lethal on turn 6. The emulator enters Reward afterwards.
This establishes the tested source-observable projection, not full
native-state or legal-decision parity.

**Next acceptance gate — default turn-boundary parity:** there are
**two default checkpoints in every complete combat round**:

1. `PLAYER_TURN_START`: after the previous enemy turn, all
   start-of-player-turn mechanics and card draws have resolved, with
   the player entering the playable phase.
2. `ENEMY_TURN_START`: after player end-of-turn cleanup and effects
   have resolved, **before the first enemy action/effect is applied**.

Both must be emitted from a source-observed phase/settlement boundary,
not inferred merely from `combat.turn`, a UI frame, an `EndPlayerTurnAction`
request/completion, or a `ReadyToBeginEnemyTurnAction` callback.
A checkpoint requires evidence that the phase's previous automatic effects
are complete; there is no queued/running/suspended action to be resolved
*before* the boundary; relevant pending card animations, targeting, choices,
or overlays have ended; and the native phase and state were captured
consistently. In the enemy-start case, an atomic source hook at the
handoff to enemy processing may be necessary because the executor
could be continuously busy across the phase change. Such a hook
must capture the pre-enemy state before the first enemy effect and
should not be replaced by a later idle frame.

Until that hook/settlement evidence exists, emit
`SETTLEMENT_UNVERIFIED` and retain only diagnostic source snapshots.
A missing checkpoint is acceptable; an invented checkpoint or a
post-enemy-action observation labelled `ENEMY_TURN_START` is not.

**Implementation status (2026-11-01):** the reference mod now emits
`native_turn_checkpoint` **candidate** records:
`PLAYER_TURN_START` at the first source-observed idle playable frame
of a turn before any observed player action in that turn, and
`ENEMY_TURN_START` immediately before the native
`ReadyToBeginEnemyTurnAction` executor callback. Both continue after
strict player-input capture stops. Each carries the native state projection,
turn, phase, and source observation site. The envelope auditor checks
uniqueness, the native phase/turn fields, and the exact callback ordering
for the enemy candidate; the source journal exposes these under
`source_turn_checkpoint_candidates`.

Every current candidate is labelled `SETTLEMENT_UNVERIFIED`,
`complete_native_queue_verified=false`, and
`native_phase_handoff_order_verified=false`. These are recorded
input points for a later source-verified settlement barrier, **not**
actual emulator state parity results. In particular, source
`ReadyToBeginEnemyTurnAction` must be verified to occur after all
player-end effects and before the first enemy effect in the installed
v111 build before `ENEMY_TURN_START` is eligible for strict comparison.
If a player acts before the first idle rendered frame of a turn, no
player-start candidate is fabricated. The old pinned native capture has
no such records and remains valid. No game launch was required to add
the hook or offline auditor.

Use executor starts/completions, UI-play-begins, request callbacks and
RNG checkpoints for **diagnostics**, including locating the first
mechanical difference after a turn-boundary mismatch; these asynchronous
observations are not parity checkpoints. If source evidence of quiescence
is incomplete, report `SETTLEMENT_UNVERIFIED` and defer the parity
comparison rather than interpreting a transient animation state as a
divergence. The strict player-decision trace remains fail-closed at its
source-specific gap (decision 9 in the older capture, decision 2 in the
newer capture); independent turn checkpoints can resume after that gap
without claiming the missing intermediate decisions or a complete legal
menu. The pinned first-combat golden replay still tests its source-observed
mechanical projections independently of this future native settlement
contract. No original-game launch is required for the present offline work.

### Installed v111 turn checkpoint capture and independent replay (2026-11-01)

The second captured Silent A10 fight, on the original seed
`6JK8MD0VQQTA` but using a **different sequence of card plays**, confirms
that the **installed native reference mod** emitted all expected provisional
checkpoints over a complete six-turn combat:

- `PLAYER_TURN_START` ordinals **47, 224, 404, 561, 750, 835**:
  all six canonical state projections exactly equal the next native
  player-command request projection of the same turn.
- `ENEMY_TURN_START` ordinals **181, 297, 522, 652, 791**:
  all five projections exactly equal the adjacent native
  `ReadyToBeginEnemyTurnAction` pre-executor snapshot. All follow
  a completed `EndPlayerTurnAction`. The turn-one hand/exhaust
  projection differs from that earlier end-turn completion because
  Ascender's Bane is exhausted in the intervening interval.
- The action envelope is `CALLBACK_TIMELINE_CONSISTENT` and the
  source journal is `SOURCE_ACTION_CHRONOLOGY_OBSERVED`:
  26 completed native executor actions, all 26 input requests linked,
  21 canonical play/end-turn commands and five automatic enemy-setup
  requests; no unmatched requests or journal errors. The linked run
  prelude completed `SOURCE_PRELUDE_OBSERVED` with startup room queues.
- The separate strict player-decision trace reaches only **two**
  stable decisions in this capture, then reports a genuine strict
  input gap at native ordinal **111** during card animation.
  The earlier native capture reached decision 9 before a similar gap.
  These strict traces remain incomplete; source executor chronology
  and candidate checkpoints continue independently.

Added `NativeObservedTurnCheckpointsV111Tests`, which bootstraps the
typed emulator independently from the source seed, maps native card
identities through the matching source hand/draw piles, then replays
the second native action sequence including Survivor's nested discard
(Defend instance 10). The test matches **all six** observed player-start
projections (HP, block, energy, enemy HP/Strength, ordered hand/draw/
discard/exhaust piles) and completes the lethal combat into Reward.
It validates persistent native-run RNG ownership and the observed
`run.shuffle` counter-31 xoshiro state. This is **source-pinned offline
emulator mechanics agreement** for two action sequences on one seed,
not native in-process lockstep or parity over unobserved continuation
fields. The five enemy-start source states are independent diagnostics;
the current emulator `end_turn` API consumes the intervening enemy turn
before presenting the next player-start state.

Each new native checkpoint remains labelled
`SETTLEMENT_UNVERIFIED`. In the installed game the enemy-start site
still reports `native_side=Player`, `player_phase=End`, as expected
for a pre-enemy-setup callback; it has **not yet** been proven to be an
atomic pre-first-enemy-effect semantic handoff. The source
state also marks complete RNG streams, pending effects, creature move
history, relic/potion mutable state and other continuation fields
unsupported. To certify full turn-boundary parity, establish native
settlement, align both engines' RNG and complete state there, and
test diverse encounters/seeds.

### Historical native turn-boundary candidate diagnostics (no game launch)

For an existing `.actions.jsonl` capture, use:

```fish
python tools/native_turn_boundary_diagnostics.py \
    --sidecar "$sidecar" \
    --out /tmp/native-turn-boundary-diagnostics.json
```

The diagnostic requires a consistent native action-envelope audit and reports
the *last observed changed frame* before each turn's first player-command
request, plus the native executor-start snapshot for each
`ReadyToBeginEnemyTurnAction` that immediately follows a completed
`EndPlayerTurnAction`. It checks callback/turn chronology, whether an
executor is active at the observed frame, and whether the frame projection
matches the subsequent request. This is useful for validating candidate
locations against older native captures that predate the dedicated
`native_turn_checkpoint` events; it does not retroactively create such
native source hooks.

For the original 909-record Silent A10 fight, all **6** observed player-start
candidate frames are idle and agree exactly with their first input-request
snapshots; all **5** enemy-start candidates follow an end-turn completion.
The first enemy-start candidate changes the state between the end-turn
completion and automatic enemy-setup start; the other four preserve the
observed projection. These are **11 source-observed candidates**, **zero
settlement-certified checkpoints**, and **zero independently checked parity
checkpoints**. No phase/queue settlement is claimed.

The offline native assembly preflight now also verifies that the installed
pinned v111 `sts2.dll` defines the source members required by the new
turn-boundary hooks: combat-state/manager access, executor before/after events,
and `EndPlayerTurnAction`/`ReadyToBeginEnemyTurnAction` types.
The validator rejects a metadata probe response that omits these checks.
Passing this static API contract does not prove runtime callback ordering.
Run the existing `native_map_build_preflight.py --metadata-only` and then
its normal isolated build/staging flow against the installed game DLL
before requesting another original-game launch.

### Offline installed-assembly API check (no game launch)

The v111 preflight now performs **read-only PE metadata inspection**
of the installed, SHA-pinned `sts2.dll` before compiling the native
reference mod. Its required-member contract covers `ActModel._rooms`,
the `RoomSet` queues, counters, boss/Ancient properties,
`StandardActMap._rng`, `RunManager.RunStarted`/`RoomEntered`,
and the `RunState` properties used by the new native prelude capture.
The tool is `tools/NativeV111MetadataProbe`; it uses .NET's
`System.Reflection.Metadata` and **does not load game dependencies or
execute native game code**. An absent member blocks staging.

With the installed game root in a fish variable `$game_root`, run:

```fish
python tools/native_map_build_preflight.py --game-root "$game_root" --stage /tmp/sts2-inspector-stage --metadata-only
```

For the full compile-and-stage validation (also without launching or
modifying the game):

```fish
python tools/native_map_build_preflight.py --game-root "$game_root" --stage /tmp/sts2-inspector-stage
```

Both commands refuse a different native DLL SHA. The second command
emits `/tmp/sts2-inspector-stage/native-map-build-preflight.json`,
which now includes the checked member list and DLL fingerprint.
A successful metadata check proves member *presence*; the native
assembly build proves public API compatibility; actual reflection
behavior, hooks, queue identities and gameplay parity remain pending
original-game validation.

**Next gate:** the native three-act room-queue identities are now
verified for `6JK8MD0VQQTA`. Before another original-game launch,
analyze the already captured passive combat trace and action sidecar,
and extend native-to-emulator first-combat and action-boundary checks.
A separate seed will eventually be useful for generalization, but is
not required to resolve the original startup queue discrepancy.
Exact potion inventory remains a separate unobserved native output.

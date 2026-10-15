# Nature issue #143: playable controlled-chance integration

**Tracking issue:** [#143](https://github.com/nn971/sts2-emulator/issues/143).
**Base:** the isolated fidelity/RNG integration branch (PR [#137](https://github.com/nn971/sts2-emulator/pull/137)); the Silent all-Act gameplay snapshot in `main` is unchanged.
**Status:** PRs #144–#147 merged; PR #148 adds real random-effect and targeted Sly auto-play suspension. Issue #143 remains open for remaining combat chance sites and complete terminal-rollout coverage.

## Delivered by the event-metadata slice

- `hello.natureEventMetadataSchemaId = sts2-v111-nature-event-metadata-v1`; existing `natureBoundarySchemaId` and `natureBoundaryScope=isolated-combat-entry-only` remain unchanged.
- `hello.naturePlayable = false` explicitly prevents interpreting an isolated `natureHandle` as an executable `stateHandle`.
- `nature_inspect(nature_handle)` returns source site, stream, owner, occurrence ordinal, source version, operation kind, ordered candidate IDs or bounded count, support size/fingerprint, stable event ID, ordered previous choices, and `probabilityStatus=UNVERIFIED`. It exposes neither hidden exact state nor native RNG cursor; full trusted state remains available only through opt-in snapshot calls.
- Event identity is computed from the pinned source request and ordered prior selected indices, independent of session handle allocation and worker scheduling. After a successful `nature_resolve`, the previous pending event becomes the final element of the replay transcript. HP→intent composition retains the HP prefix.
- Python `NatureBoundary.inspect()` exposes the same read-only interface with capability negotiation.
- Focused C# tests exercise candidate metadata, snapshot/fork identity, ordered HP→intent provenance, detached view arrays, and deterministic 15-worker isolated fixture execution.

### Example request

```json
{"op":"nature_inspect","nature_handle":"n1"}
```

The response includes `schemaId`, `kind`, `mode`, `natureHandle`, `fingerprint`, `complete`, `pending` and `transcript`. The `pending` object is null after every isolated choice is resolved. Outcome selection remains `nature_resolve(nature_handle, fingerprint, selected_index)`, validating the full state fingerprint before publishing the child.

## Remaining issue acceptance gates

| Gate | Status | Work |
| --- | --- | --- |
| G0: playable controlled RunState + fair observation | **PARTIAL (#145)** | A real `RunState` with starter player/deck and unchanged controlled enemy HP/intent can play deterministic actions after Nature resolves shuffle; the fixture bypasses native combat-start hooks and is not equivalent to arbitrary full-run state. |
| G1: mechanically resumed action→chance→action | **PARTIAL (#145/#146)** | Besides opening shuffle, the real end-turn pipeline suspends immediately before `AdvanceTurn` intent commitments, persists preceding enemy effects, resolves repeated Nature tickets, and continues after the saved index. Other in-action/nested chance sites remain blocked. |
| G2: live chance-site coverage | **PARTIAL (#145/#146)** | Opening shuffle and end-turn next-intent decisions have typed control. Legacy RNG calls within other effects fail with `UNSUPPORTED_CONTROLLED_CHANCE site=... operation=...` before the transaction publishes a child. Formation, draw/reshuffle, targeting, summons and nested hooks remain unmigrated. |
| G3: extended chance operations | **PARTIAL** | `choose_index` and compact `bounded_integer` exist. Other source-shaped operation types need requests and execution integration. |
| G4: stable event IDs and transcript | **PARTIAL (#144/#146)** | Typed events now interleave controlled initial HP, intents, shuffle, and the first mid-action next-turn intent decisions, with ordered replay, source site and support identity. Other live RNG events remain unmigrated. |
| G5: fair player boundary | **PARTIAL (#145)** | `controlled_observe` exposes only the standard `prototype-fair-v0` observation, after all opening chance requests resolve; Nature metadata remains separately accessible to the chance controller. |
| G6: 15-worker isolation | **PARTIAL (#144/#145/#146)** | Deterministic 15-worker tests now cover next-turn intent suspension as well as opening fixture; full controlled terminal rollout and throughput remain incomplete. |
| G7: conditional native chance law | **DEFERRED** | Explicitly `UNVERIFIED`; source law and importance-sampling weights require separate calibration. |

## Engineering sequencing

1. Use the existing source-shaped HP→intent provider and `RandomBoundaryJournalV111` as the internal event codec.
2. Integrate the provider at **real** combat entry, including formation and shuffle, with a persisted `RunState` continuation. Keep existing native `step` unaffected.
3. Implement repeated chance suspension/resume and strict failure for uncovered RNG calls.
4. Expose a new `controlledChanceSchemaId` **only after** playable controlled game states, fair player actions and exact snapshot continuation are tested.
5. Add joint player/chance replay, targeted special encounters, 15-worker end-to-end stress and eventually calibrated native chance laws.

**Compatibility:** `sts2-v111-nature-isolated-boundary-v1` is unchanged. No native seeded RNG cursor is claimed to match counterfactual controlled outcomes. The proposed [desired RNG implementation](DESIRED_RNG_IMPLEMENTATION.md) and [Inspector plan](NATIVE_FIDELITY_INSPECTOR_PLAN.md) are unchanged.

## PR #145: opt-in playable controlled opening fixture

The new `controlledChanceSchemaId` identifies the **limited** `sts2-v111-controlled-playable-fixture-v1` protocol. `controlled_begin` accepts a completed HP→intent Nature handle plus the identical seed, checks seed/source provenance, and creates a separate `controlledHandle`. Its first boundary is the opening Fisher–Yates shuffle; repeated `controlled_resolve` calls apply selected swap indices **in place to each forked child** until a five-card opening hand exists.

`controlled_observe` and `controlled_legal_actions` expose the ordinary `PrototypeAiEnvironment` public player view. `controlled_step` executes the real `PrototypeGameEngine.Step` with a fail-closed chance guard. Prototype/native RNG fallback raises an explicit `UNSUPPORTED_CONTROLLED_CHANCE` error with site and operation details before a child is stored. Save/load recomputes the state from verified controlled HP/intents, ordered shuffle outcomes and player action IDs; fork/release/trace use independent controlled handles.

This is a **gameplay-capable fixture**, initialized from the legacy run's starter player/deck, not a source-authentic native combat opening. Combat-start relic hooks, initial card effects, future shuffle/draw and nested random events still need correct stage-by-stage resumable engine support. For example, a guarded `end_turn` action that reaches an unmigrated RNG draw fails explicitly; that entire action is aborted atomically. True mid-action suspension will require a persisted mechanical continuation, not rerunning the whole action.

`natureBoundaryScope=isolated-combat-entry-only` and `naturePlayable=false` remain unchanged for backwards-compatible isolated Nature handles. The new controlled fixture is a separate opt-in capability. The probability status remains `UNVERIFIED`.

## PR #146: live end-turn intent continuation

The `controlled_step` command now runs the real end-turn pipeline through enemy actions and end-turn effects, stopping at the next-turn `AdvanceTurn` commitment **before** its first RNG call. The saved `RunState` includes all earlier hooks and a typed automatic-pipeline index. `controlled_resolve` sequentially commits the source-shaped intent tickets by the pure `EvaluateEnemyIntentChoice` callback. After the last ticket, `ResumeControlledTurnAfterIntents` performs turn-increment/postwork precisely once and resumes at the following automatic stage. The HP/decision indices and earlier side effects stay stable across Nature branches; no earlier gameplay hooks are rerun.

Versioning remains `sts2-v111-controlled-playable-fixture-v1` with the additional supported site `combat.next_turn_intent.index_draw`. `controlled_trace`, `controlled_step` and `controlled_load` report `next_turn_intent` and a pending typed finite event as appropriate. Only after that event resolves is the normal `prototype-fair-v0` observation accessible again. Snapshots verify replay against the joint ordered player/chance transcript, and strict stale/index validation keeps rejected requests atomic.

**Limit:** This is a single mechanically meaningful suspension stage inside live player end-turn, not generic .NET stack capture. RNG calls before that stage (enemy effects) or after it (draw/reshuffle, random cost, targeting or nested hooks) still fail closed. They need additional explicit stage/operation continuations in later patches. All native chance-law probabilities remain `UNVERIFIED`.


## PR #147: live hand-draw recycle

The controlled end-turn pipeline now saves the already-executed pre-draw hooks and card-draw effects on an empty draw pile, with the remaining count and original pipeline stage. Nature chooses ordered swap indices under `combat.draw_pile_recycle.swap_index`. The emulator then applies actual postshuffle ordering and effects, draws the remaining cards, and resumes at the next pipeline stage. Chronological intent/recycle events are replay-validated across multiple turns. Other random draw effects, target selection, generated-card inserts, summons and nested hooks remain unsupported. See [the recycle contract](NATURE_LIVE_RECYCLE_143.md).

## PR #148: random-enemy effects and targeted Sly auto-play

The real `ResolveOperations` kernel suspends at `combat.effect.random_enemy`, preserving prior operations and nested choice/card continuations. The ordered source-shaped `choose_index` request exposes currently eligible enemy instance IDs. A Nature resolution supplies the selected enemy to the saved operation, then continues the original effect chain. Discard-induced targeted Sly auto-play suspends analogously at `combat.sly_autoplay.random_enemy` and persists the selected index/target to the standard `AutoPlayTargetRolls` combat history. Target support is validated against the live enemy formation, and fair player observations are withheld until the pending chance resolves.

This extension preserves the chronological `LiveTranscript` and exact replay across player actions, target decisions, intent and shuffle decisions. Focused Ricochet and Storm of Steel/Sly tests use explicit combat-fixture card substitutions; initial native run/deck acquisition is not claimed. See [the target-choice design and coverage](NATURE_RANDOM_TARGET_143.md). Other dynamic chance and nested operations still need migration. All original conditional laws remain `UNVERIFIED`.

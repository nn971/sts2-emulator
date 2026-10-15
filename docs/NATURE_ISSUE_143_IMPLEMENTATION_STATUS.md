# Nature issue #143: playable controlled-chance integration

**Tracking issue:** [#143](https://github.com/nn971/sts2-emulator/issues/143).
**Base:** the isolated fidelity/RNG integration branch (PR [#137](https://github.com/nn971/sts2-emulator/pull/137)); the Silent all-Act gameplay snapshot in `main` is unchanged.
**Status:** PRs #144–#152, #155–#157 and #159–#163 merged into the isolated RNG/Inspector integration branch. PR #155 adds live drawn-card cost continuations and synchronizes the revised desired RNG architecture from main PR #154; #156–#157 add semantic deck-state and actual DrawCards integration, and #159 adds source-timed semantic recycle. Issue #143 stays OPEN for full semantic gameplay/JSONL integration, source-timed recycle and draw-cost continuations, weighted visible intents, native parity and complete controlled terminal rollouts.

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
| G1: mechanically resumed action→chance→action | **PARTIAL (#145–#151)** | In addition to earlier live mechanics, all source-filtered card generation pools can now suspend for each full Fisher–Yates swap and resume original choice/direct-generation effects without repeating earlier hooks. |
| G2: live chance-site coverage | **PARTIAL (#145–#152)** | Four generated-choice and distinct-card operation kinds now own full-pool shuffle tickets, including Attack/Skill/Power and Colorless Potions, Discovery and Jack of All Trades. Enemy-side insertion, random costs and nested summon/death remain. |
| G3: extended chance operations | **PARTIAL** | `choose_index` and compact `bounded_integer` exist. Other source-shaped operation types need requests and execution integration. |
| G4: stable event IDs and transcript | **PARTIAL (#144–#151)** | Nature events include exact current eligible card-model order and bounded full-pool swaps. Every partial permutation is independently replayed against the recorded swap prefix and original eligibility, with stable source/stream identity. |
| G5: fair player boundary | **PARTIAL (#145–#151)** | Player choices resume through original select_cards boundary after Nature completes shuffle; candidate pool is privileged Nature metadata and fair player observation remains gated. |
| G6: 15-worker isolation | **PARTIAL (#144–#151)** | Deterministic 15-worker tests cover full-pool shuffle prefixes alongside earlier chance sites. Full combat-to-terminal throughput remains untested. |
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


## PR #149: live random generated-card effects

The actual `ResolveOperations` queue now creates Nature chance boundaries for five source-shaped single-card generation effects: free random character cards, zero-cost cards, random Attack, random Skill, and random Power. The selected card is created exactly once, then the original queued effects and card/event/choice continuation resume; multi-card generation creates successive chance events. The `choose_index` request uses the original ordered source eligibility pool. Nature also receives a detached `pending.candidateCardIds` array, while the player's ordinary fair observation remains separate. Pool identity is included in the event owner fingerprint.

Tests exercise all three consecutive Orobic Acid generation effects in actual combat using an explicitly injected fixture potion, plus malformed support rejection, independent branches and 15-worker deterministic choices. The test fixture is not a claim of native initial potion acquisition, and unchanged starter-state snapshot replay remains the authoritative provenance contract. Original source laws remain `UNVERIFIED`. See [the generation-specific implementation note](NATURE_GENERATED_CARD_143.md).


## PR #150: generated-card random draw-pile placement

The real combat `ResolveOperations` kernel now suspends at two additional source-defined effects. A generated fixed card has one bounded insertion-position ticket; a generated random Attack has a source-ordered model-selection ticket followed by a position ticket. The original operation and continuation queue are preserved, so card materialization and each insertion occur exactly once. Multi-insertion operations issue successive choices, and the player remains outside the fair decision boundary until the operation completes. See [draw-insertion implementation notes](NATURE_DRAW_INSERTION_143.md).

Tests exercise both mechanics through a narrow guarded internal diagnostic entry on a real combat `RunState`. Neither operation is currently reached by a standard Silent starter card action, so this does not establish full JSONL `controlled_step` or native-entry replay provenance. Remaining enemy-side insertion and other random-effect continuations remain explicitly unsupported. Law status is `UNVERIFIED`.


## PR #151: complete source-shaped generation-pool shuffle

The real `ResolveOperations` pipeline now suspends before full-pool Fisher–Yates shuffles used by `ChooseGeneratedCards`, `ChooseGeneratedColorlessCards`, `CreateDistinctColorlessCardsInHand` and `CreateDistinctCharacterCommonCardsInHand`. The controller gets a typed, bounded swap event for **every** eligible card in the complete source-filtered pool even when the game selects only one or three. The saved card-event/choice continuation resumes once after the last swap; choice effects restore the ordinary player `select_cards` action, while direct generation adds the selected prefix to hand. All partial permutations are checked against recorded swaps, guarding against invalid reordered snapshots.

Focused tests exercise real Attack and Colorless Potion effect kernels using explicit fixture-injected potion inventory, distinct common generation through an internal operation fixture, forged permutation rejection and 15-worker deterministic execution. Native-provenance potion acquisition and full controlled snapshot replay from those modified fixtures remain unverified; source laws are `UNVERIFIED`. Details in [the full-pool continuation contract](NATURE_FULL_POOL_GENERATION_143.md).


## Next slice: controlled hand-cost operations (#143)

The real `ResolveOperations` kernel suspends before choosing a random
positive-cost hand card for a temporary cost change and before each bounded
cost roll in `RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed`. All already
applied effects, parent card/choice/event continuation and remaining queued
operations are preserved. An all-hand reroll visits eligible card instances
once in their original hand order. Every outstanding ticket is source-validated;
Nature sees only explicit `choose_index` or `bounded_integer` support,
with `UNVERIFIED` source probability status.

Coverage is established with narrow actual-operation fixtures, including
invalid cursor/support rejection, independent forks and schedule-independent
15-worker tests. These starter-fixture diagnostic entrances do not establish
unmodified player-action replay provenance. Draw-triggered relic/Slither
random costs, random pile selection shortlists, enemy-side insertion,
death/summon continuations, native laws and full terminal rollout remain.


## New slice: draw-triggered relic/Slither costs

During the actual automatic `DrawPlayerHand` pipeline, an already-drawn
physical card now suspends before randomizing its cost. Snecko Eye cost
and Slither enchantment cost become successive Nature tickets when both
are present; `CardDrawn` dispatch occurs once after the final cost roll,
then the saved remaining hand draw resumes without replaying prior hooks.
Earlier cards and source instance identity remain unchanged. The latest
desired RNG plan (PR #154 on main) requires a separate opt-in semantic
provider-owned draw-pile protocol, with native exact order and deferred
Nature per-card draw identities. This legacy fixture continues to expose
its *old* controlled shuffle swaps; it adds no second identity chance on
top. See [draw-cost coverage](NATURE_DRAW_COST_143.md).

Additional gates remain: opening/action-triggered draw-cost support,
nested `CardDrawn` hooks, semantic deck state/forced positions, weighted
visible intents, true native parity and 15-worker terminal rollouts.


## Semantic deck-state kernel (opt-in, provider-owned)

A new `sts2-v111-semantic-deck-v1` pure kernel implements the revised
desired RNG state separation. Native mode stores committed exact
top-first card identities; Nature mode stores unresolved, *shared*
instance pools across physical position slots. A known card inserted in
the middle splits the unknown slots without splitting their underlying
permutation group. Subsequent draws/peeks/scry reveal a concrete card
instance only once; earlier known/revealed cards stay forced and do not
produce redundant Nature events. Source-triggered reshuffling creates
a new unresolved epoch, and legacy precommitted Fisher–Yates swap
transcripts stay on their separate `controlled-v111-playable-fixture-v1`
protocol.

Pure-state tests enumerate small constrained decks and exercise known
top/bottom and middle placements, source-ordered candidate identities,
peek reuse, scry/removal, snapshot/fork/tamper checks, native committed
permutations and 15 workers. See
[Nature semantic deck state](NATURE_SEMANTIC_DECK_143.md).
The kernel is **not yet wired** into the normal `DrawCards`/opening-hand/
automatic-pipeline mechanics or JSONL; integration and true native parity
remain explicitly pending.


## Real `DrawCards` semantic draw segment (PR #157)

The separate semantic deck state now has a guarded actual-mechanics bridge
through `PrototypeGameEngine.DrawCards`. Each *unknown* physical card is
selected by Nature before it enters hand; the same original draw kernel
executes its deterministic `CardDrawn` hooks and counters, then requests
the next card. The physical pile and provider's constrained instances
must match at every draw. A narrow `SemanticCombatDrawEngineV111`
envelope stores the conditioned source combat, semantic transcript and
remaining draw count; restore replays the source state and *all* selected
card identities through actual mechanics before checking the entire
snapshot. No legacy Nature opening-shuffle swap tickets are consumed.

This is still a **draw-segment fixture**, not a full player-driven
combat or JSONL semantic mode. Source-timed discard recycle/on-shuffle
hooks, drawn Snecko/Slither costs and nested choice continuations
explicitly fail closed, and opening/end-turn player-action integration
remains. Focused tests cover one-card draw-hook timing, identity support,
state tampering, saved mid-action replay, 2-from-3 complete enumeration,
and deterministic 15-worker sessions. Details:
[Nature real combat semantic draw](NATURE_SEMANTIC_COMBAT_DRAW_143.md).


## Semantic source-timed recycle with Perfect Fit priority (merged PR #159)

Within the isolated real `SemanticCombatDrawV111` segment, actual
`DrawCards` exhaustion now recycles discard instances into a *new
unresolved semantic shuffle epoch*, then resumes the original physical
draw and `CardDrawn` pipeline. The source Perfect Fit enchantment
reorders eligible cards toward the top by introducing distinct deferred
Perfect Fit and ordinary pools; Nature reveals each eligible instance
when drawn. The source-aligned native stream implementation remains
unchanged, and controlled Nature uses no shadow Fisher–Yates tickets.
Current Stratagem player selection and relic after-shuffle random card
insertion fail explicitly before recycling. Tests cover repeated
source-timed draws, Perfect Fit support, snapshot round-trip and
invalid nested shuffle scenarios. Scope:
[Nature semantic recycle](NATURE_SEMANTIC_RECYCLE_143.md).

## Semantic per-card Snecko/Slither cost continuation (merged PR #160)

The opt-in conditioned `SemanticCombatDrawEngineV111` now supports
draw-time Snecko/relic and Slither **after** a deferred card identity is
committed but **before** its normal `CardDrawn` hook. It suspends for
source-bounded relic cost first, then the 0–3 Slither overwrite if the
same physical card has Slither. The saved remaining draw count resumes
through the actual `DrawCards` loop without repeating prior draws or
hooks. The player's physical hand, card-instance identity, energy-cost
mutation, and deferred deck position state are saved and forked intact.

A separate serialized semantic cost transcript tracks each bounded
random cost. Snapshot restore replays **interleaved card-identity and
cost choices** from the conditioned source snapshot, validating
eligible bounds/source owner/ordinal and complete mechanics state.
Focused regressions include one-card terminal cost decisions,
two-card Snecko draws, effect-style `FromHandDraw=false`, combined
Snecko+Slither, forged cost contexts and 15-worker deterministic
replay. Source probability provenance remains `UNVERIFIED`.
See [Nature semantic drawn costs](NATURE_SEMANTIC_DRAW_COST_143.md).

This is still a conditioned real-draw segment. Nested `CardDrawn`
pending choices, Stratagem and after-shuffle random insertions, plus
opening/automatic/player-step/JSONL semantic rollout, remain separate
gates.


## Semantic DrawPlayerHand automatic-stage checkpoint (merged PR #161)

The opt-in `SemanticCombatDrawEngineV111.BeginAutomaticHandDraw`
enters the **original** `ResolveAutomaticStep(DrawPlayerHand)` from
a saved source-shaped pipeline checkpoint, executes `BeforeHandDraw`
and all ordinary draw-count/hand-bonus rules exactly once, then stores
a distinct `ControlledSemanticDrawRemaining` cursor. Subsequent
per-card identity, Snecko and Slither Nature decisions resume
`DrawCards` directly. Completion executes `RemoveAfterHandDraw`
and `DecrementAfterHandDraw` cleanup once and persists the
**next automatic-step index** for later whole-action resumption.

The automatic-stage mode is separately tagged in the replayable
semantic draw envelope; restoration replays the original stage from
the checkpoint, verifies the interleaved Nature transcript and
compares complete `RunState` including stage cursor. Tests cover
card-draw timing, Snecko's extra hand cards, staged costs, recycle,
invalid cursor, and 15 workers. See
[Nature semantic automatic hand draw](NATURE_SEMANTIC_AUTOMATIC_HAND_DRAW_143.md).

This remains a **single original automatic pipeline stage**, with
prior enemy-intent resolution and later automatic steps supplied/
continued elsewhere. A full player end-turn `step` → Nature →
next `step` controller and JSONL bridge are not yet complete.


## First restricted real end-turn action-to-action Nature path (merged PR #162)

A separately versioned `sts2-v111-semantic-end-turn-v1` controller
connects the real `PrototypeAiEnvironment.Step(end_turn)` action
through the saved post-enemy-action `AdvanceTurn` checkpoint,
source-shaped controlled enemy intent choices, the genuine intermediate
automatic pipeline, the semantic `DrawPlayerHand` continuation,
and the **remaining real turn-start automatic steps**. It validates that
the result is a genuine player decision with legal actions and no
outstanding continuation. The separate `BeginAtAdvanceTurn` entry
supports testing/source-shaped checkpoints. Source and action
fingerprints, enemy intent tickets, per-card draw identities,
Snecko/Slither costs and the complete next player state all round-trip
with full mechanical replay; 15-worker deterministic tests included.

**Hidden-state restriction:** entry requires an exhausted draw pile
at the actual AdvanceTurn checkpoint and a nonempty discard. Hence
the next hand draw creates a **genuine fresh source-time recycle**
without double counting an earlier precommitted shuffle through both
legacy swaps and newly deferred card identities. A previously committed
nonempty pile explicitly fails until its original order constraints
can be retained by a persistent provider across gameplay actions.

See [Nature semantic end-turn](NATURE_SEMANTIC_END_TURN_143.md).
This achieves one restricted **real `step` → Nature choices →
next `step`** flow. Further player actions using a persistent semantic
deck, opening-hand/card-effect draws, full terminal episodes, weighted
intents and the externally negotiated semantic JSONL bridge remain open.


## Persistent provider-owned deck across player actions and turns (merged PR #163)

The real `CombatState.SemanticDeck` now survives supported player
actions and subsequent `DrawPlayerHand` automatic stages **without
reinitializing unresolved positional card pools or replaying previous
Nature shuffle choices**. `SemanticPlayerActionEngineV111` runs
source-audited basic Silent Strike/Defend through the genuine AI action
engine under the controlled chance guard, accepts only unchanged
physical draw-pile sequence and identical private deck state, and
replays the complete action from a saved source snapshot.

`SemanticEndTurnEngineV111` now accepts an existing, valid, idle
semantic provider on the next `end_turn`, including a **nonempty**
draw pile. The authentic next hand draw reuses the provider's previous
known and unknown positions, and source-time discard recycling still
creates a new shuffle epoch when needed. The original empty-pile
first-entry rule continues to prevent counting both legacy swaps and
semantic per-card identities. Each hand-draw segment's ticket list now
starts at its source transcript offset, while the provider carries the
full cumulative multi-turn history; complete source replay remains
deterministic. Tests cover two full real end turns with an intervening
basic card play, retained known-top constraints, complete provenance,
tampered physical/private zones and 15-worker parallel equality.
See [Nature persistent semantic deck](NATURE_SEMANTIC_PERSISTENT_DECK_143.md).

Further work: other cards/potions, source-authentic deck mutations,
opening-hand and card-effect draws, nested chance hooks, public JSONL
schema, weighted intents and terminal training rollouts. Source/native
probability law remains UNVERIFIED.

# Nature issue #143: playable controlled-chance integration

> **Scope revision:** The chronological implementation notes below preserve interim acceptance language. Current #143 scope is the playable two-site Nature opponent, engine-owned ordinary stochastic delegation and emulator A/B control experiment. Native game parity belongs to [Inspector issue #122](https://github.com/nn971/sts2-emulator/issues/122); original-game probability calibration belongs to N5.

**Architecture and acceptance:** [Unified stochastic architecture](UNIFIED_STOCHASTIC_ARCHITECTURE.md) defines ownership; [roadmap](STOCHASTIC_ARCHITECTURE_ROADMAP.md) separates structural Chance Law acceptance, the two-site #143 comparator, and native inspector fidelity.

**Tracking issue:** [#143](https://github.com/nn971/sts2-emulator/issues/143).
**Base:** the isolated fidelity/RNG integration branch (PR [#137](https://github.com/nn971/sts2-emulator/pull/137)); the Silent all-Act gameplay snapshot in `main` is unchanged.
**Status:** PRs #144–#152, #155–#157 and #159–#165 are merged into the isolated RNG/Inspector integration branch. The P0 JSONL protocol has an opt-in real Act 1 source route, pre-intent suspension, source-filtered weighted intent IDs, deferred card identities, and a paired engine-owned policy baseline. The source-routed victory smoke, replay check and 1-vs-15 process comparison pass. Remaining #143 acceptance is complete declared engine-owned stochastic delegation and the matched two-site Nature/control A/B. Conditional laws remain `UNVERIFIED`; native fidelity and source-law calibration are separately gated by #122 and N5.

The non-Nature provider now retains the complete Native run bundle with per-stream cursor origins and a mixed typed transcript for registered Native boundaries. The Nature mechanics path routes Native-labeled hand-return swaps and supported draw-insertion positions through `run.shuffle`; drawn-card costs, random hand-cost target selection and values, registered random-enemy targets, registered combat card and potion model picks, card-transform replacements, generated-card full-pool swaps, Anointed Rare-card and random-shortlist swaps, saved random autoplay/replay card-instance selections, Jeweled Mask's random draw-pile card pick, and the saved enemy-theft card selection also use their registered Native streams while retaining their semantic mechanics continuations. Other ordinary stochastic boundaries and the matched A/B remain open.

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
| G2: live chance-site coverage | **PARTIAL (#145–#152)** | Four generated-choice and distinct-card operation kinds own full-pool shuffle tickets, including Attack/Skill/Power and Colorless Potions, Discovery and Jack of All Trades. A serialized per-potion continuation now covers `combat.potion_generation.random_model` through Alchemize. Enemy-side insertion, random costs and nested summon/death remain. |
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

The `controlled_step` command now runs the real end-turn pipeline through enemy actions and end-turn effects, stopping at the next-turn `AdvanceTurn` commitment **before** its first RNG call. The saved `RunState` includes all earlier hooks and a typed automatic-pipeline index. `controlled_resolve` sequentially commits the source-shaped intent tickets by the pure `EvaluateEnemyIntentChoice` callback. After the last ticket, `ResumeControlledTurnAfterIntents` performs turn-increment/postwork precisely once and resumes at the following automatic stage. The selector uses the live formation at this boundary, including combatants added during the completed enemy-action stage. The dynamic-formation extension has build-only verification; previous fixed-formation continuation coverage is unchanged. The HP/decision indices and earlier side effects stay stable across Nature branches; no earlier gameplay hooks are rerun.

Versioning remains `sts2-v111-controlled-playable-fixture-v1` with the additional supported site `combat.next_turn_intent.index_draw`. `controlled_trace`, `controlled_step` and `controlled_load` report `next_turn_intent` and a pending typed finite event as appropriate. Only after that event resolves is the normal `prototype-fair-v0` observation accessible again. Snapshots verify replay against the joint ordered player/chance transcript, and strict stale/index validation keeps rejected requests atomic.

**Limit:** This is a single mechanically meaningful suspension stage inside live player end-turn, not generic .NET stack capture. RNG calls before that stage (enemy effects) or after it (draw/reshuffle, random cost, targeting or nested hooks) still fail closed. They need additional explicit stage/operation continuations in later patches. All native chance-law probabilities remain `UNVERIFIED`.


## PR #147: live hand-draw recycle

The controlled end-turn pipeline now saves the already-executed pre-draw hooks and card-draw effects on an empty draw pile, with the remaining count and original pipeline stage. Nature chooses ordered swap indices under `combat.draw_pile_recycle.swap_index`. The emulator then applies actual postshuffle ordering, pauses for after-shuffle relic insertion positions and Stratagem selection when needed, draws the remaining cards, and resumes at the next pipeline stage. Chronological intent/recycle events are replay-validated across multiple turns. Other random draw effects, target selection, summons and nested hooks remain unsupported. See [the recycle contract](NATURE_LIVE_RECYCLE_143.md).

## PR #148: random-enemy effects and targeted Sly auto-play

The real `ResolveOperations` kernel suspends at `combat.effect.random_enemy`, preserving prior operations and nested choice/card continuations. The ordered source-shaped `choose_index` request exposes currently eligible enemy instance IDs. A Nature resolution supplies the selected enemy to the saved operation, then continues the original effect chain. Discard-induced targeted Sly auto-play suspends analogously at `combat.sly_autoplay.random_enemy` and persists the selected index/target to the standard `AutoPlayTargetRolls` combat history. Target support is validated against the live enemy formation, and fair player observations are withheld until the pending chance resolves.

This extension preserves the chronological `LiveTranscript` and exact replay across player actions, target decisions, intent and shuffle decisions. Focused Ricochet and Storm of Steel/Sly tests use explicit combat-fixture card substitutions; initial native run/deck acquisition is not claimed. See [the target-choice design and coverage](NATURE_RANDOM_TARGET_143.md). Other dynamic chance and nested operations still need migration. All original conditional laws remain `UNVERIFIED`.


## PR #149: live random generated-card effects

The actual `ResolveOperations` queue now creates Nature chance boundaries for five source-shaped single-card generation effects: free random character cards, zero-cost cards, random Attack, random Skill, and random Power. The selected card is created exactly once, then the original queued effects and card/event/choice continuation resume; multi-card generation creates successive chance events. The `choose_index` request uses the original ordered source eligibility pool. Nature also receives a detached `pending.candidateCardIds` array, while the player's ordinary fair observation remains separate. Pool identity is included in the event owner fingerprint.

Tests exercise all three consecutive Orobic Acid generation effects in actual combat using an explicitly injected fixture potion, plus malformed support rejection, independent branches and 15-worker deterministic choices. The test fixture is not a claim of native initial potion acquisition, and unchanged starter-state snapshot replay remains the authoritative provenance contract. Original source laws remain `UNVERIFIED`. See [the generation-specific implementation note](NATURE_GENERATED_CARD_143.md).


## PR #150: generated-card random draw-pile placement

The real combat `ResolveOperations` kernel now suspends at two additional source-defined effects. A generated fixed card has one bounded insertion-position ticket; a generated random Attack has a source-ordered model-selection ticket followed by a position ticket. The original operation and continuation queue are preserved, so card materialization and each insertion occur exactly once. Multi-insertion operations issue successive choices, and the player remains outside the fair decision boundary until the operation completes. See [draw-insertion implementation notes](NATURE_DRAW_INSERTION_143.md).

Tests exercise both mechanics through a narrow guarded internal diagnostic entry on a real combat `RunState`. Neither operation is currently reached by a standard Silent starter card action, so this does not establish full JSONL `controlled_step` or native-entry replay provenance. A later continuation also handles enemy draw/discard insertion through the saved enemy-action frame; other random-effect continuations remain unsupported. Law status is `UNVERIFIED`. See [draw-insertion implementation notes](NATURE_DRAW_INSERTION_143.md).


## PR #151: complete source-shaped generation-pool shuffle

The real `ResolveOperations` pipeline now suspends before full-pool Fisher–Yates shuffles used by `ChooseGeneratedCards`, `ChooseGeneratedColorlessCards`, `CreateDistinctColorlessCardsInHand` and `CreateDistinctCharacterCommonCardsInHand`. The controller gets a typed, bounded swap event for **every** eligible card in the complete source-filtered pool even when the game selects only one or three. The saved card-event/choice continuation resumes once after the last swap; choice effects restore the ordinary player `select_cards` action, while direct generation adds the selected prefix to hand. All partial permutations are checked against recorded swaps, guarding against invalid reordered snapshots.

Focused tests exercise real Attack and Colorless Potion effect kernels using explicit fixture-injected potion inventory, distinct common generation through an internal operation fixture, forged permutation rejection and 15-worker deterministic execution. Native-provenance potion acquisition and full controlled snapshot replay from those modified fixtures remain unverified; source laws are `UNVERIFIED`. Details in [the full-pool continuation contract](NATURE_FULL_POOL_GENERATION_143.md).

The semantic P0 bridge now opts into the same continuation with a seeded
Colorless Potion fixture. It exposes each bounded swap through
`semantic_resolve_pool_shuffle`, resumes to the original `select_cards`
player boundary, and stores both decision types in semantic snapshot replay.
Only the fixture potion route is enabled in this bridge; native acquisition
and source-law calibration remain unverified. The JSONL/Python contract is in
[Nature P0 semantic JSONL](NATURE_P0_SEMANTIC_JSONL_143.md).


## Controlled hand-cost operations (#143)

### S0 stochastic boundary: random hand-card selection and energy costs

The real `ResolveOperations` kernel suspends before choosing a random
positive-cost hand card for a temporary cost change and before each bounded
cost roll in `RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed`. All already
applied effects, parent card/choice/event continuation and remaining queued
operations are preserved. An all-hand reroll visits eligible card instances
once in their original hand order. Every outstanding ticket is source-validated;
Nature sees only explicit `choose_index` or `bounded_integer` support. The
fair player-facing projection omits pending support and request details; it
shows the ordinary hand and committed cost after the source operation
resolves.
Chance Law queries report exact Native `NextInt` masses as
`SOURCE_VERIFIED` for both the hand-card selection and bounded-cost
transforms on their declared supports. Source-pool completeness, stream
attribution, seed frequencies, and cursor parity remain open.

Coverage is established with narrow actual-operation fixtures, including
invalid cursor/support rejection, independent forks and schedule-independent
15-worker tests. These starter-fixture diagnostic entrances do not establish
unmodified player-action replay provenance. The semantic JSONL bridge now
exposes both source stages: the fixed-cost single-card selector using
`fixture_set_random_hand_card_cost`, and the sequential bounded cost-roll
stage using `fixture_randomize_hand_costs_max_exclusive`. The selector event
identifies the ordered eligible hand-card instances; each reroll event
identifies its current hand card. Both resume the source operation and replay
independently. The Seeker Strike source shortlist is also exposed as ordered
without-replacement instance choices before its ordinary player selection;
contiguous unresolved-pool removal is preserved without exposing a hidden
position; pools split by known anchors still fail closed. Enemy draw/discard
insertion now uses the saved enemy-action frame and replays the source resolver;
see [draw-insertion implementation notes](NATURE_DRAW_INSERTION_143.md).
Draw-triggered relic/Slither costs beyond their existing event path,
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
Nature shuffle choices**. `SemanticPlayerActionEngineV111` runs its
audited draw-pile-neutral Silent action allowlist through the genuine AI
action engine under the controlled chance guard, accepts only unchanged
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


## Source-shaped card-selection deck mutations (merged PR #164)

The original `PrototypeGameEngine.ApplyCardSelection` now updates
`CombatState.SemanticDeck` when a player selects known card instances
from hand/discard/exhaust to **`MoveToDrawTop`**, preserving source
ordering (the last physical append is the first card drawn). The
existing unresolved pool and earlier revealed position constraints
remain unchanged; subsequent `DrawCards` consumes the fixed top card
without requiring a Nature identity ticket. Explicit
`MoveToHand`/`MoveToDiscard`/`MoveToExhaust` selections from
a draw pile also update the provider for **previously revealed
position-known** instances. Hidden-position removals fail before
publishing a new action result.

`SemanticCardSelectionEngineV111` replays a legal real
`select_cards` action from a source-shaped *already-pending*
choice checkpoint. Tests cover forced multi-card top order,
next-turn card-draw hook/candidate behavior, peek-revealed
removals, forged hidden pool rejection, complete source replay
and independent 15-worker outcomes. Focused CI covers this
versioned choice-resolution slice; see
[Nature semantic card selection](NATURE_SEMANTIC_CARD_SELECTION_143.md).
The semantic playable pilot admits Survivor, Hand Trick, Nightmare and Wish
whenever one is present in hand, then exercises this engine after their real
effects. It publishes a typed `player_card_selection` event and source legal
actions, then replays the action/choice pair from the saved source state. The
explicit seeded fixture remains available to guarantee Survivor in the opening
hand. This does
not establish native acquisition for these cards or generic nested-choice
coverage. Scry/random-position selection, other insertion effects and
card-effect draws remain separate integration milestones.

The semantic JSONL/Python bridge also exposes fixed-model random draw-pile
insertion and the two-stage random-Attack insertion continuation through
explicit seeded fixtures. The latter publishes its source-ordered card-model
event followed by a bounded position event. Each position resolves at the
real `ResolveOperations` continuation, then adds the generated instance to
the provider deck as a private known anchor without revealing the surrounding
unresolved order. The events and fixture setup replay from the seeded entry;
native fixture acquisition remains uncovered and source laws are
`UNVERIFIED`. See
[the insertion contract](NATURE_DRAW_INSERTION_143.md).

The semantic protocol also exposes a capped one-event enumerator. It creates
an independent replayable child for each outcome up to the caller's limit,
with a hard maximum of 256 and no truncation. Player card-selection actions
remain player choices rather than Nature enumeration outcomes.


## P0 reprioritization: first external two-site playable semantic JSONL (merged PR #165)

Issue #143 was raised to **P0** on 2026-10-16 as the next
`sts2-ai` Nature training blocker, requiring the first real
Silent combat pilot to optimize only **enemy intents and deferred
card identities**. Additional unrelated chance-site breadth is now
P1 until the external executable protocol is viable.

Merged #165 adds `sts2-v111-semantic-playable-p0-v1`, a distinct,
versioned JSONL/Python `semantic_*` session capability with real
player-only observations and legal actions, Nature pending intent/draw
metadata, physical draw card-model labels, strict parent fingerprints,
opaque forkable session handles and complete save/load mechanical
replay. Precommitted opening deck order becomes private *known*
semantic positions, not newly issued Nature draw choices; later
source-time recycle remains deferred. Drawn Snecko/Slither cost
picks retain the **unverified engine-owned fallback** by default; a separate
opt-in seeded fixture can expose them as typed `drawn_card_cost` Nature
events. Focused **173/173 C# tests**
and the first live Python semantic JSONL session smoke passed,
including older Inspector and Nature protocol checks.

**The new interface still requires a completed older controlled
opening fixture** and opaque source intent index tickets; it has
not yet demonstrated a source-authentic full battle through terminal,
weighted filtered intent supports or calibrated native/default
fallback. This is a *P0 interoperability milestone*, not P0
acceptance or training evidence. Next: independent initial combat
handshake, raw filtered intent weights/IDs, provider parity, and one
fully replayable true-terminal Silent pilot with deterministic 1-vs-15
external worker comparison. See
[Nature P0 semantic JSONL](NATURE_P0_SEMANTIC_JSONL_143.md).


## Local feature branch: source-disjoint Nature/native policy comparison

The feature branch `feature/nature-143-native-policy-comparator` adds an
opt-in `native_policy_baseline` to the seeded semantic JSONL entry. It
constructs Nature and native-policy sessions from the same pinned weak
Act 1 Silent opening and exposes a shared source-opening fingerprint and
the same initial fair observation. The native branch then runs the real
engine policy step behind a restricted RNG guard, records native-v111
decision requests/cursors, and uses an exact deck mode that commits each
recycle Fisher–Yates order once at the source shuffle point. Later draws
reveal that committed order without treating each physical card identity
as a new native random choice.

The independent-process smoke replays both branches with the same
legal-action policy, reaches real terminal boundaries, checks save/load
replay, and compares one process against fifteen parallel workers. The
recorded local run produced matching source-opening fingerprints and fair
opening observations; Nature and native outcomes were both terminal
victories with different exit HP, and the native path recorded nonzero
decision and shuffle counts. This is paired weak-entry evidence only.
The native comparator and conditional probability laws remain explicitly
`UNVERIFIED`.

This did not close #143. The comparator smoke at that earlier checkpoint
used the source-shaped fixture entry, and its native provider and conditional
laws were unverified. The focused .NET test invocation at that checkpoint
could not start because MSBuild was denied its local named-pipe bind. Later
source-route verification and test results are recorded below.

## Working-tree follow-up: first-combat route entry

An opt-in `source_route_entry` now builds the configured Act 1 Overgrowth
or Underdocks Silent run, resolves the opening event and first map choice
through legal engine actions, and reaches combat through the real
`PrototypeGameEngine.StartCombat` path and its native weak-entry provider.
The route choice sequence, first-floor node, resolved encounter, native
entry name, and initial native decisions are retained in a replayable
source-route audit. Nature and native-policy sessions split from this same
completed opening. The native baseline keeps native stream ownership; the
Nature branch removes native combat/run ownership after entry and retains
the native cost stream separately in its private `OtherChance` state. The
initial intent now suspends at the real
`combat.committed_intent.index_draw` site: Nature sees its weighted support,
while the baseline commits the same pre-intent checkpoint through the
native provider. The shared source-opening fingerprint is captured before
that provider split.

For the Native-policy baseline, the source-route audit also retains the
existing typed encounter-formation result and its exact formation-index
decisions. Validation pairs those choices with the mixed Native trace and
checks the saved formation against the combat origin. This adds replay
evidence for the generated result; encounter-source completeness and native
parity remain unverified.

Encounter-formation audit schema V2 also pairs each setup `run.niche`
initial-HP ticket with its enemy combat identity, ordered HP support, and
selected HP. Restore checks the annotations against the mixed Native trace,
formation contexts, and source-route combat origin. V1 and unmarked audit
snapshots retain their prior requirements. This records emulator setup
results; installed-game HP parity and source-pool fidelity remain unverified.
Static source review and `git diff --check` only; no tests or builds were run.

The read-only `combat.initial_unique_hp` query now binds a
`creature:<CombatId>` owner, ordered HP support, and source ordinal to the
`run.niche` `NextInt` grid law. It reports exact mass over support indices and
fingerprints the caller's declared support values and order; source pool
construction, seed frequencies, and end-to-end cursor parity remain open.

The read-only `combat.tough_egg.hatchling_hp` query accepts a canonical
`creature:<CombatId>` owner, Ascension, and source ordinal zero, then derives the
source's 19–22 or 20–23 HP support and returns exact `run.niche` `NextInt`
grid masses. Event eligibility and the in-place hatch boundary remain caller
responsibilities; no source-pool or cursor-parity claim is added.

This removes the fabricated encounter formation from the opt-in route mode,
but does not make route probabilities, Neow choices, shuffle-order hooks,
or all combat entry hooks native-parity verified. The initial draw-pile
Fisher–Yates shuffle now advances the persistent `run.shuffle` stream before
native HP initialization, using the pinned v0.111 swap bounds and source
deck order. The Nature side of this source route also retains a private
native `run.combat_energy_costs` cursor for Snecko/Slither draw costs. The
source-route and default seeded paths keep costs engine-owned; the explicit
seeded `fixture_nature_draw_costs` option exposes those same draw-cost
continuations as typed Nature events with `UNVERIFIED` support. Replay checks
the source cursor or typed cost transcript according to the selected mode.
The stream assignment and original conditional laws remain `UNVERIFIED`.

The CLI Release build passes with zero warnings or errors after the
source-route cost-stream change. The updated
`tools/nature_semantic_victory_jsonl_smoke.py` passed: the paired Nature and
native-policy sessions both reached victory (55 and 53 HP), save/load
replay matched, the native path recorded 39 decisions and 35 shuffles, and
the one-vs-fifteen independent-process comparison was identical. The
focused `SemanticPilotCombatV111Tests` and `SemanticOtherChanceV111Tests`
suite passed 14/14 tests.

The full core suite reports 1,521 passed and 296 failed. Running the same
suite from the exact `origin/integration/fidelity-rng-on-silent-all-act-v111`
base produced 1,520 passed and the same 296 failing test names; the feature
branch adds one passing test and no new failures. This comparison exposed
and removed an overstrict initial-continuation room check before the final
run.

This is source-routed paired mechanics evidence, not probability
calibration: route/Neow laws, conditional intent laws, non-intent native
stream assignments and wider encounter coverage remain `UNVERIFIED`. The
branch remains a work in progress, and the issue stays open for those P0
gates.

The Python smoke now runs the 1-vs-15 independent-process paired replay
for five source-route seeds: the canonical Overgrowth route, Overgrowth
Shrinker Beetle (57/62 HP), Overgrowth Fuzzy Wurm Crawler (54/54),
Underdocks Toadpoles (42/47), and Underdocks Sludge Spinner (56/56).
Every pair independently rebuilds the same source opening and checks fair
opening observations and save/load replay. This expands weak-encounter
mechanics evidence but does not calibrate source probabilities or cover
normal encounters broadly.

The separate seeded-entry smoke now pairs Nature and native-policy
branches on the Act 2 Hive Exoskeletons Normal formation. Both reach defeat
after four end turns at 0 HP from the same source-opening fingerprint and
fair opening observation, with snapshot replay and 1-vs-15 process equality.
This is seeded fixture coverage; it does not establish a full-map normal
encounter route.

A separate local Inspector sidecar audit is pinned by matching `sts2.dll`,
`0Harmony.dll`, runtime and release hashes to
`data/reference_builds/v0.111.0-41cef1ea.json`. It verifies 3 native
`run.niche` initial-HP draws and 5 shuffle operations containing 52
`run.shuffle` primitive draws. That capture contains no monster-intent or
`run.combat_energy_costs` calls and remains incomplete with native/emulator
parity `NOT_TESTED`; it does not calibrate the source-route policy or cost
provider.

## 2026-10-17: resumable semantic card-effect draws

The semantic player pilot admits ten Silent draw cards for Nature semantic
draws: seven direct draws and Acrobatics, Dagger Throw and Prepared, which
prompt for a discard after drawing. An internal `controlled_semantic_draw`
checkpoint persists the remaining draw count and the original effect-queue
continuation. Each identity choice resumes the same source `DrawCards`
operation, dispatches its draw hook once, and then continues the queued effects.
The three follow-up discard prompts use the ordinary semantic
`select_cards` action and resume the source completion event. `semantic_draw`
history replay validates each selected physical card identity and source event
transcript. The saved-state invariants bind the internal choice to one pending
Nature draw and reject conflicting semantic boundaries.

Draw-time randomized costs now compose through the existing typed semantic
cost event and `ControlledDrawCost` checkpoint, preserving relic-before-Slither
ordering and dispatching `CardDrawn` once before resuming the remaining draw.
Pending card keyword overrides are delayed until the complete `DrawCards`
operation returns, across identity, cost and nested hook-choice suspensions.
The Release solution build passes with zero warnings or errors. Tests were not
run for this implementation checkpoint. The card-effect draw frame crosses
the existing Nature recycle epoch, preserves its Perfect Fit constraints and
resumes Stratagem selection through the existing semantic `select_cards` event.
Standard choices opened by `CardDrawn` hooks compose through serialized event
and outer-choice continuations, preserving the remaining draw cursor, delayed
keyword updates and queued effect suffix. Knowledge Demon choices and
standalone or automatic draw-pipeline frames remain outside this continuation.
After-shuffle relic insertion uses the existing semantic draw-insertion event
and resumes the card-effect draw frame; other draw-pipeline recycle contexts
with that insertion remain unsupported. Conditional Nature probabilities
remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_EFFECT_DRAWS_143.md`](NATURE_SEMANTIC_EFFECT_DRAWS_143.md).

## 2026-10-17: standalone semantic after-shuffle insertions

The conditioned `SemanticCombatDrawV111` segment now suspends when a recycle
activates after-shuffle relic insertions. Each insertion uses the existing
typed semantic position event, synchronizes the semantic deck at the committed
physical position, and resumes the same draw. Save/restore replays insertion
events interleaved with card identity and cost decisions. The insertion event
request retains the `prototype.combat` stream and relic application owner.
The Release solution build passes with zero warnings or errors; tests were not
run for this checkpoint. Stratagem selection during the standalone recycle
and automatic draw-pipeline insertions remain unsupported. Conditional
probabilities remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_COMBAT_DRAW_143.md`](NATURE_SEMANTIC_COMBAT_DRAW_143.md).

## 2026-10-17: automatic CardDrawn hook choices

The automatic `DrawPlayerHand` segment now persists ordinary source action
choices opened from `CardDrawn` hooks. The saved automatic cursor retains the
requested and remaining hand-draw counts; resolving the hook action re-enters
the same `DrawCards` operation and performs the hand-draw cleanup once. Nested
controlled chance boundaries remain unsupported. The Release solution build
passes with zero warnings or errors; tests were not run for this checkpoint.
Conditional probabilities remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_COMBAT_DRAW_143.md`](NATURE_SEMANTIC_COMBAT_DRAW_143.md).

## 2026-10-17: automatic hand-draw after-shuffle insertions

The automatic `DrawPlayerHand` continuation now suspends on the typed semantic
insertion event when an after-shuffle relic inserts a generated card. The
requested and remaining draw counts stay on the automatic pipeline cursor;
each chosen insertion position updates the semantic deck and resumes the same
`DrawCards` call. Draw-cost continuations use the same cursor, and the existing
post-hand-draw cleanup runs once after the draw finishes. The Release solution
build passes with zero warnings or errors; tests were not run for this
checkpoint. Stratagem combined with an insertion remains unsupported.
Conditional probabilities remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_AUTOMATIC_HAND_DRAW_143.md`](NATURE_SEMANTIC_AUTOMATIC_HAND_DRAW_143.md).

## 2026-10-17: standalone Stratagem draw selection

The standalone semantic draw now suspends at Stratagem's source `select_cards`
choice after a Nature recycle, then resumes the same remaining draw. The
selection is identified by a serialized marker on its queued draw continuation
and recorded as a semantic card-selection decision for replay. This also
composes with standalone after-shuffle insertion and draw-cost stages.
Automatic-pipeline Stratagem selection and nested `CardDrawn` choices were
unsupported at this checkpoint. The Release solution build passes with zero warnings or errors;
tests were not run for this checkpoint. Conditional probabilities remain
`UNVERIFIED`; see
[`NATURE_SEMANTIC_COMBAT_DRAW_143.md`](NATURE_SEMANTIC_COMBAT_DRAW_143.md).

## 2026-10-17: automatic hand-draw Stratagem selection

The semantic automatic `DrawPlayerHand` segment now suspends on Stratagem's
source `select_cards` event after recycle, then resumes the saved remaining
draw. The selection composes with after-shuffle insertions and draw-cost
stages, and the existing hand-draw cleanup still runs once at completion. The
shared source selection path synchronizes the semantic deck before queued
draw operations resume.
The Release solution build passes with zero warnings or errors; tests were not
run for this checkpoint.
Conditional probabilities remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_AUTOMATIC_HAND_DRAW_143.md`](NATURE_SEMANTIC_AUTOMATIC_HAND_DRAW_143.md).

## 2026-10-17: end-turn relay for automatic draw choices

The restricted complete `SemanticEndTurnV111` action now exposes automatic
after-shuffle insertion events and Stratagem source selections. Snapshot replay
consumes those transcripts in their mechanical order alongside card identity,
draw-cost and random pile-card decisions. The Release solution build passes
with zero warnings or errors; tests were not run for this checkpoint.
Conditional probabilities remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_END_TURN_143.md`](NATURE_SEMANTIC_END_TURN_143.md).

## 2026-10-17: standalone CardDrawn hook choices

The standalone semantic draw now persists the remaining `DrawCards` operation
when a `CardDrawn` hook opens an ordinary source action choice. A typed hook
choice event records legal action support and replays the selected action;
the same draw resumes once the choice resolves. Controlled nested chance
boundaries remain unsupported. The
Release solution build passes with zero warnings or errors; tests were not run
for this checkpoint. Conditional probabilities remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_COMBAT_DRAW_143.md`](NATURE_SEMANTIC_COMBAT_DRAW_143.md).

## 2026-10-17: semantic end-turn CardDrawn hook relay

`SemanticEndTurnV111` and the playable semantic JSONL session now relay typed
ordinary action choices from automatic `CardDrawn` hooks as player-choice
boundaries. `semantic_step` resolves the listed action ID; save/load replays
the hook decision with the source draw and end-turn transcript. The Release
solution build passes with zero warnings or errors; tests were not run for this
checkpoint. Conditional probabilities remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_END_TURN_143.md`](NATURE_SEMANTIC_END_TURN_143.md).

## 2026-10-17: automatic BeforeHandDraw source choices

The automatic semantic hand-draw continuation now also suspends on ordinary
source action choices opened by `BeforeHandDraw`. The typed hook-choice event
identifies this pre-draw stage; resolving an action resumes the saved event
dispatcher before the hand-draw prelude, without replaying earlier
subscribers. Controlled nested chance choices remain unsupported. The Release
solution build passes with zero warnings or errors; tests were not run for this
checkpoint. Conditional probabilities remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_AUTOMATIC_HAND_DRAW_143.md`](NATURE_SEMANTIC_AUTOMATIC_HAND_DRAW_143.md).

## 2026-10-17: player-action auto-play targets

The controlled semantic target path now suspends a targeted
`AutoPlayCombatCard` operation inside a player action, exposes its living enemy
support, and resumes the same operation with the selected instance ID. The
remaining operation queue and outer card-action continuation stay attached;
late automatic-pipeline auto-play still needs an end-turn target boundary.
The Release solution build passes with zero warnings or errors; tests were not
run for this checkpoint. Conditional probabilities remain `UNVERIFIED`; see
[`NATURE_RANDOM_TARGET_143.md`](NATURE_RANDOM_TARGET_143.md).

## 2026-10-17: player-action random-card auto-play

The semantic pilot now exposes `combat.autoplay.random_card.select` through
the serializable card-shortlist continuation for Beat Down and Catastrophe.
After one card is selected, the saved auto-play operation resumes and
re-evaluates its source pool for each remaining repetition. Catastrophe's
draw-pile card identity is committed to the semantic deck before movement.
The same continuation now retains and resumes an automatic-pipeline cursor,
including across follow-on shortlist choices. The Release solution build
passes with zero warnings or errors; tests and mechanics regressions were not
run. Conditional probabilities remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_AUTOPLAY_143.md`](NATURE_SEMANTIC_AUTOPLAY_143.md).

## 2026-10-17: automatic-pipeline random-enemy auto-play target

The controlled playable continuation now permits a saved
`combat.autoplay.random_enemy` choice while an automatic-pipeline cursor is
present. Resolving the target resumes the saved auto-play operation and then
the real pipeline cursor. Semantic end-turn event exposure and focused
mechanics coverage remain incomplete; the Release build is the current
verification evidence.

## 2026-10-17: player-action random replay-card selection

Hidden Gem's `combat.draw_pile.random_replay_card.select` now uses the shared
serializable card-shortlist continuation. The source-preferred Attack/Skill/
Power pool and fallback eligibility are retained; resolving the selected
instance applies its replay count and resumes the saved effect suffix without
changing draw-pile order. Multi-candidate automatic-pipeline contexts remain
unsupported. The Release solution build passes with zero warnings or errors;
tests and mechanics regressions were not run. Conditional probabilities
remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_RANDOM_REPLAY_143.md`](NATURE_SEMANTIC_RANDOM_REPLAY_143.md).

## 2026-10-17: automatic-pipeline card shortlists

Random auto-play card selection, random draw-pile replay selection, and
random card shortlists can now suspend with an automatic-pipeline cursor
saved alongside their source operation continuation. Follow-on shortlist
choices preserve the same cursor until the operation completes. The Release
solution build passes with zero warnings or errors; tests and mechanics
regressions were not run. Conditional probabilities remain `UNVERIFIED`.

Random hand-card selection and random energy-cost effects also preserve the
automatic-pipeline cursor while their source operation continuation resolves.
Their Release build passes; tests and native cursor regressions were not run.

## 2026-10-17: player-action Anointed rare-card shuffle

The semantic pilot now exposes Anointed's full Rare draw-pile shuffle through
the existing bounded pool-shuffle continuation. Each Nature event resolves
the source swap index over stable card-instance tokens; the final permutation
moves the selected prefix into hand and removes those identities from the
semantic deck. A contiguous-pool removal that cannot preserve hidden position
constraints still fails closed. The Release solution build passes with zero
warnings or errors; tests and mechanics regressions were not run. Conditional
probabilities remain `UNVERIFIED`; see
[`NATURE_SEMANTIC_ANOINTED_143.md`](NATURE_SEMANTIC_ANOINTED_143.md).

## 2026-10-17: random combat-potion model generation

`AcquireRandomCombatPotion` now suspends on a typed ordered potion-model
choice, applies one selected potion, and resumes the saved operation queue.
The controlled fixture exposes the same site through its normal bounded-index
request. The semantic JSONL/Python bridge reaches it with an explicit
Alchemize opening-hand fixture, records replayable model-ID outcomes, and
supports bounded semantic enumeration. The Release solution build passes with
zero warnings or errors; tests and mechanics regressions were not run.
Conditional probabilities, native stream assignment and cursor parity remain
`UNVERIFIED`. See the [semantic P0 contract](NATURE_P0_SEMANTIC_JSONL_143.md).

## 2026-10-17: Bottled Potential hand-return shuffle

The semantic pilot now resolves Bottled Potential's full draw-pile-plus-hand
shuffle through the saved controlled pool-shuffle continuation. It commits the
Nature-selected order to the semantic deck, preserves Perfect Fit ordering,
resolves active after-shuffle relic insertions through the existing insertion
event, then resumes the potion's five-card draw. The semantic JSONL protocol
exposes `hand_return_shuffle`. The Release solution build passes with zero
warnings or errors; tests and mechanics regressions were not run. Native stream
assignment, cursor parity and conditional probabilities remain `UNVERIFIED`.
See [`NATURE_HAND_RETURN_SHUFFLE_143.md`](NATURE_HAND_RETURN_SHUFFLE_143.md).

## 2026-10-17: Thieving Hopper persistent-card selection

The existing serialized enemy-action replay now also suspends at
`combat.enemy.theft_card.select`, then replays the action with its selected
card-instance index. The source action removes the chosen persistent card from
the draw/discard pile and run deck. Nature sessions reject the chance boundary
before publication when any draw-pile candidate cannot be removed while
preserving hidden deck constraints. The Release solution build passes with
zero warnings or errors; tests and mechanics regressions were not run. Native
stream assignment, cursor parity and conditional probabilities remain
`UNVERIFIED`; see
[`NATURE_ENEMY_THEFT_143.md`](NATURE_ENEMY_THEFT_143.md).

## 2026-10-17: controlled legacy enemy summon pools

The enemy-action replay now handles the modeled legacy summon-pool selection.
When a summoned enemy has a variable HP range, the replay serializes that
second choice and injects the summon selection again before resolving the HP
outcome. It also handles variable HP from a legacy Tough Egg hatch during the
enemy stage. Native summon-pool selection remains fail-closed pending source
RNG audit; native Tough Egg hatching uses its separate audited provider. The
Release solution build passes with zero warnings or errors; tests and mechanics
regressions were not run. The new nested continuation is `UNVERIFIED`; see
[`NATURE_ENEMY_SUMMON_143.md`](NATURE_ENEMY_SUMMON_143.md).

## 2026-10-17: controlled Personal Hive reactions

Powered hits against Personal Hive now suspend before the first random Dazed
insertion. Nature resolves each stack in order; the already committed hit is
not replayed, and the original card-operation queue resumes after the final
insertion. Candidate positions are preflighted against semantic deck state.
The Release solution build passes with zero warnings or errors; tests and
mechanics regressions were not run. This continuation remains `UNVERIFIED`.

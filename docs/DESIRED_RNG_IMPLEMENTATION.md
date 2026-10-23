# Desired RNG Implementation

**Status:** accepted architecture target (2026-10-16), partially implemented through independent versioned slices. Earlier D0–D6 sequencing notes retain their historical context.
**Scope:** `sts2-emulator`, pinned STS2 v0.111.0, single-player first.
**Primary goal:** faithful mechanics and ordered triggers with an interchangeable source of random decisions.
**Related plans:** [RNG model](RNG.md), [v111 native RNG fidelity matrix](reference-builds/v0.111.0-native-rng-fidelity-matrix.md), [complete implementation roadmap](FULL_IMPLEMENTATION_ROADMAP.md), [configured v111 interface](V111_INTERFACE.md), [controlled event-choice continuation](CONTROLLED_EVENT_CHOICE_D3.md).

**Normative ownership and Chance Law contract:** [Unified stochastic architecture](UNIFIED_STOCHASTIC_ARCHITECTURE.md), [ADR 0004](adr/0004-provider-owned-stochastic-state-and-chance-law.md), and [staged roadmap](STOCHASTIC_ARCHITECTURE_ROADMAP.md). This document retains detailed migration guidance; it does not define a second ownership model.

Chance Law is an optional read-only service over pre-outcome support and declared constraints. Native RNG and the game bridge never call it. ReferenceNature consumes the law through the same mechanics continuation; native cursor parity and original-game probability calibration remain separate gates.

This document records the **desired RNG implementation** for the emulator. It
specifies the target architecture; it does not claim that the full interface,
all source migrations or probability calibrations are complete. See the
[migration matrix](DESIRED_RNG_MIGRATION_STATUS.md) and [semantic playable
protocol](NATURE_P0_SEMANTIC_JSONL_143.md) for implemented slices. Extend the
existing v111 native RNG work rather than discarding it.

## 0. Architecture update: stochastic state and semantic Nature (2026-10-15)

**Decision:** unify the *mechanical* interface while allowing the native and
Nature providers to store different private stochastic state. The native
provider retains exact source-seeded RNG streams, committed permutations and
other hidden values. Nature may represent the same game state by unresolved,
constrained random objects and select outcomes when the mechanics actually
need them. Both providers execute the same card, enemy, relic, potion,
visibility and trigger rules. The native stream/cursor audit remains intact.

**Implementation progress (2026-10-17):** the repository now has a provider-
owned semantic deck state with committed and unresolved positions, typed
weighted intent events, and replayable semantic events for deferred card
draws, random-enemy targets, generated-card model pools and an opt-in drawn-
card cost continuation, plus a source-authentic generated-card draw-pile
insertion, sequential hand-cost rerolls, random hand-card selection, and
without-replacement random pile shortlists. The semantic playable pilot now
composes that same typed drawn-cost protocol with card-effect draw frames,
including source event order, nested relic/Slither stages and replayable cost
decisions. The
semantic JSONL/Python pilot resumes the original draw, target, generation and
cost continuations, including draw-time hooks, exposes source `select_cards`
choices after Survivor, Hand Trick and Wish plays or the supported selection
potions, and can opt into typed bounded swaps for a generated card pool
before returning the ordinary player choice or a bounded insertion position
while preserving unresolved deck order. The deck kernel also supports exact
moves of known cards to a forced offset while keeping unresolved positions
constrained. Rebound returns remove a known card identity from semantic deck
state without revealing its unresolved position, and fail closed if that
removal would break split-pool constraints. The saved real player-action
segment admits audited draw-pile-neutral Silent actions: Strike, Defend,
Deflect, Neutralize, Slice, Poisoned Stab, Deadly Poison, Sucker Punch,
Dagger Spray, Dash, Cloak and Dagger, Blade Dance, Piercing Wail, Backstab,
Leg Sweep, Skewer, Footwork, Accuracy, Phantom Blades, Tracking, Infinite
Blades, Suppress, Anticipate, Snakebite, Assassinate, Catalyst, Expose,
Malaise, Crippling Cloud, Blur, Dodge and Roll, Sidestep, Pounce, Finisher,
Flechettes, Bubble Bubble, Strangle, Corrosive Wave and Leading Strike. It
also accepts deterministic Silent effects Noxious Fumes, Speedster, Serpent
Form, Envenom, Afterimage, Untouchable, Flick-Flack, Abrasive, Haze, Memento
Mori, Murder, Precise Cut, Pinpoint, Grand Finale, Die Die Die, Master Planner,
Burst, Bullet Time, Accelerant, Blade of Ink, Fan of Knives, Up My Sleeve and
Predator, Shiv, Mirage, Tactician, Tools of the Trade, Shadowmeld, The Hunt,
Well Laid Plans, Wraith Form and Storm of Steel. It rejects a chance or turn
boundary it cannot resume with a typed unsupported-operation error. The
semantic playable pilot also admits Survivor, Hand Trick, Nightmare and Wish
through its persisted source card-selection continuation. JSONL `hello` now
advertises the retained-deck action schema, its scope and sorted supported card
IDs as an explicit capability.
Ordinary event choices also have an opt-in D3 continuation: the engine stores
the partially updated player/world state and current effect index at a
supported legacy chance site, then resumes after an external outcome without
replaying earlier effects. The D4 JSONL/Python adapter exposes this through
`controlled_event_begin` and `controlled_event_resolve`; candidate labels and
the completed controlled transcript are serialized with the run. This slice
covers random upgrades, supported random card/potion/relic event rewards,
selected multi-draw custom card and relic offers, empty-slot potion filling,
the legacy event-acquired Toy Box wax relic offer, and the post-reward
modifier-curse draw through a separate event-reward frame.
Event reward actions advance through `controlled_event_advance`; a suspended
frame preserves completed reward choices and extends the original event trace.
Event-effect frames resume the current effect with its saved draw prefix while
retaining earlier completed effects. This does not establish native v0.111.0
stream ownership, source pool parity or reference probabilities; other event,
reward and relic continuations remain separate gaps.
Physical draw-pile moves now keep provider state synchronized across exact
insertions, removals, transformations, and zone transfers. Native hand-return
shuffles commit the resulting top-first order; controlled Nature also resumes
Bottled Potential's hand-return shuffle, applies Perfect Fit ordering, and
continues through after-shuffle relic insertions and its five-card draw. The
native stream assignment and provider-specific cursor parity remain unverified.
After-shuffle relic cards record their source-selected insertion position in
native exact mode. Top-draw autoplay consumes the exact provider order and
commits native recycle shuffles; it fails closed for deferred Nature until it
has a resumable draw continuation. Native exact mode also rejects operations
without their native combat stream owner instead of silently falling back to
the legacy stream. Nature still fails closed where a shuffle or after-shuffle
insertion lacks a saved continuation. Run-state validation now checks semantic
deck membership against combat zones, verifies NativeMode's top-first provider
order against the bottom-first physical pile, and requires its native combat
stream owner.
The Orobas, Pael and Tezcatara ordered relic offer selections; Darv's
offer-variant and ordered relic-pool shuffle; and the Nonupeipe, Tanx and
Vakuu relic shuffles now pass through the shared index provider, with opt-in
audit results and native compatibility entry points. All seven persist
nonnative provider mode in completed event state. Orobas and Darv validate
that counterfactual offers leave the native event cursor untouched, and
replay native offers and cursors while their opening event remains unchosen.
Pael, Tezcatara, Nonupeipe, Tanx and Vakuu retain strict seeded validation
for native mode. Vakuu's request ordinals span its three pool shuffles. The
existing source-pinned Fisher–Yates loop now accepts an arbitrary named
stream and provider without duplicating its swap mechanics. Jeweled Mask's
random Power selection and native generated-card full-pool shuffle also reach
typed native providers. Jeweled Mask preserves its legacy `combat` stream
through the typed legacy adapter. Random generated-card model picks now use
`run.combat_card_generation` in native combat and the retained `combat` stream
through the legacy provider. Semantic-controlled Jeweled Mask publishes its
ordered eligible Power instances and resumes the saved `BeforeHandDraw`
continuation after the selection, without replaying prior hooks. Reference-law
calibration and native cursor parity remain open.
The opt-in Overgrowth map uses the `context.act_1_map` stream, and new
Overgrowth Neow offers use a persisted `context.event.NEOW.player_0` stream
through typed native, scripted, or controlled providers. Their legacy helper
remains available; map and Neow source call-order parity remain open, as do
Neow unlock and `IsAllowedAtNeow` filters.
The shared `EventState.NativeEventRng` owner now routes Trash Heap rewards,
Tablet of Truth upgrade picks, Doors of Light's shuffle, Fake Merchant's
stock shuffle, both Tinker Time offer shuffles, and Stone of All Time's
occupied-potion selection and choice ticket, Battleworn Dummy's setting 2
upgrade shuffle, Fragrant Mushroom's upgrade shuffle, both Reflections
card-selection passes, Trial's court case choice, The Future of Potions'
entry offers, Infested Automaton's card choice, and Endless Conveyor's dish
and card-selection draws, plus Punch Off and This or That's entry gold rolls
and Lost Wisp's entry unit-float roll, through their event contexts. Other
supported Overgrowth and Underdocks entry-gold rolls now use their per-event
contexts too; native event result/cursor parity remains unverified.
The opt-in native Punch Off event fight now persists the fixed
`PunchOffEventEncounter` formation and each construct's `MonsterModel.Rng`
cursor; its 2–9 HP reduction uses the matching CombatId-owned stream.
Unopted and legacy combat retain the prototype path, and seeded native
combat parity remains open.
Configured merchant generation now routes sale-slot, card-pool, potion-pool,
and item-price draws through the existing native player streams. Card and
relic rarity use `player.rewards`; card and potion offers and merchant prices
use `player.shops`; the persistent rarity-partitioned relic queues initialize
from `run.treasure_room_relics`. Legacy prototype paths remain available.
The restricted pools, unconfigured profile, and controlled shop continuation
remain separate gaps, and native result/cursor parity is still unverified.
Configured Overgrowth combat reward generation now carries the persistent
`player.rewards` stream through potion pity, gold bounds, weighted potion
selection, card rarity/model/upgrade draws, relic rarity, and boss relic offer
shuffling, and Lava Rock's extra boss relic picks. The base card offer
precedes relic population; combat-earned extra offers and Black Star relic
draws precede Prayer Wheel's appended group. Driftwood rerolls and Dream
Catcher rest rewards also preserve the advanced native stream. Source-shaped
pools remain restricted, and native result/cursor parity is unverified.
Configured Overgrowth and Underdocks run starts now initialize the persistent
relic pools as canonical `NatureUnordered` rarity sets. They do not construct
the Native `run.treasure_room_relics` queue order; subsequent supported Nature
pulls select identities through their existing Treasure, merchant, reward,
event and Shovel continuations. Ordinary Native starts still build and shuffle
the source-owned queues. This is a provider-state choice, not evidence for the
source pool contents, identity probabilities, or Native cursor parity.
`ReferenceNatureCombatRewardSessionV111` wraps a legal combat action that
immediately wins and generates its reward. It samples configured Native pity,
gold, PotionFactory, card, relic, boss-offer, and Lava Rock requests in replay
order, and also captures a registered subset of retained Prototype
`reward`-stream index requests with equal-index `UNVERIFIED` laws. It commits
the reward and Nature cursor together. Other legacy reward hooks and the
ordinary Native relic-bag queue shuffle remain outside this session;
source-pool completeness, stream attribution, and Native cursor parity remain
open. See the [combat reward session contract](REFERENCE_NATURE_COMBAT_REWARD_D3.md).
Punch Off's relic and potion custom reward picks now use that player stream in
configured regional runs; their source stream assignment remains unverified.
War Historian Repy's two independent potion offers also use typed `player.rewards`
requests under an explicit stream hypothesis, with legacy fallback retained.
Its distinct relic offers use the shared native Fisher–Yates provider under
the same stream hypothesis, while source pool order remains unverified.
Configured Overgrowth treasure relics now use the same persistent
`player.rewards` stream and rarity-partitioned relic queue as reward and
merchant draws. Treasure RNG state is saved on both successful and exhausted
bag paths; source result/cursor parity remains unverified.
The configured Shovel rest-site reward uses that same player reward stream and
retains its advanced bundle while the relic reward is pending. The legacy
`start_run` boss encounter draw has an opt-in saved run-action continuation.
Separately, the legacy Shovel unowned-relic index draw has a saved run-action
continuation; the configured native relic-bag route remains source-owned.
Neow's nested random relic pickup also advances the same bundle while
preserving its existing reward continuation.
Random relic gains from configured ordinary event choices use the same bag
stream and preserve it through the event continuation.
The same applies to Pael's Wing, the configured Glory Trial, Battleworn and
Round Tea Party relic rewards, and Calling Bell and Toy Box relic offer
generation. Their advanced bundles survive the pending event or reward.
The potion factory now routes its unit-float rarity
sample and within-tier model index through separate typed providers in source
order. Native effect-random-target and Sly auto-play selections use typed
index requests on `run.combat_targets`; player-action auto-play targets now
resume the saved auto-play operation after Nature selects a live enemy.
Supported automatic-pipeline autoplay now resumes through saved cursors for
Mayhem and History Course, Whispering Earring's opening sequence, and the
Imbued late-relic sequence. Other source-specific autoplay boundaries remain
gated, and native cursor parity remains open. Random-card auto-play now has a
saved card-shortlist continuation for
`combat.autoplay.random_card.select`, shared with the serializable card
shortlist mechanism. Beat Down selects from its discard-pile Attack pool;
Catastrophe selects from the draw pile and commits the removed identity to the
semantic deck before playing it. The source pool is re-evaluated after each
auto-play. The continuation preserves and resumes automatic-pipeline cursors,
including across follow-on shortlist choices; native cursor parity remains
open. See [the auto-play continuation note](NATURE_SEMANTIC_AUTOPLAY_143.md).
Hidden Gem's random draw-pile replay selection also resumes through the same
card-shortlist continuation, updating the selected card's replay count while
preserving draw-pile order and its automatic-pipeline cursor; see
[the replay-selection note](NATURE_SEMANTIC_RANDOM_REPLAY_143.md).
Random hand-card selection and energy-cost effects keep their automatic
pipeline cursor while Nature resolves the selected card and any per-card cost
tickets.
Generated-card and potion-model choices also preserve that cursor while the
saved effect queue resumes, including successive generated items.
Full-pool random card effects preserve it through each saved descending swap
and resume the effect queue after the permutation commits.
Anointed's full shuffle over Rare draw-pile instances now resumes from each
saved bounded swap and moves the resulting prefix into hand while updating the
semantic deck. The player-action fixture route uses stable card-instance
tokens in the shared pool-shuffle event; see [the Anointed continuation note](NATURE_SEMANTIC_ANOINTED_143.md).
The supported generated draw-pile operations now use typed model-pick requests
on native `run.combat_card_generation` and position requests on `run.shuffle`;
the legacy profile preserves `combat`. Post-shuffle relic insertion now has
its own typed site, and enemy-side insertion now has typed sites. The saved
enemy-action replay also resolves Thieving Hopper's persistent-card selection,
removes the selected card from its combat and run zones, and updates the
semantic deck when the card came from the draw pile. It fails closed before
presenting the choice if any draw-pile candidate cannot be removed while
preserving Nature's constraints. Native stream assignment remains unverified.
After-shuffle insertion composes with card-effect draws, standalone conditioned
draws, and the automatic `DrawPlayerHand` continuation. Standalone Stratagem
selection resumes the same saved draw across the source `select_cards` choice.
Automatic Stratagem selection resumes the same draw and composes with
after-shuffle insertion and draw-cost stages. Legacy controlled end-turn
recycling also pauses for the ordinary Stratagem card selection and resumes
the saved hand-draw cursor through later cost and recycle boundaries. Ordinary
source action choices from standalone and automatic `CardDrawn` hooks now
preserve and resume their draw frames. A guarded legacy card-effect draw
without a semantic deck now also saves its
`RecycleDrawPileForEffectDraw` continuation: discard swaps resolve
through the existing pool-shuffle choice, a singleton discard recycles without
a chance ticket, after-shuffle insertions remain in the saved effect suffix,
and the original draw resumes after the shuffle. Controlled Nature's standalone,
nested effect-draw, and automatic hand-draw chance boundaries now save typed
continuations for deferred draws, draw costs, after-shuffle insertions,
Stratagem selection, and supported `CardDrawn` hook choices. Recycle commits
the Nature deck epoch and physical zone transfer through the shared draw-pile
adapter before resuming those frames. `ResolveOperations` captures the source
context and operation suffix for resumable `DrawCards` and hand-return shuffle
effects through one owner, including saved draw-resume operations. The legacy
and semantic effect-draw paths share deferred card-ID accumulation and keyword
application. Each caller still interprets its own draw boundaries and hook
policy; contexts without a matching serializable frame still fail closed, and
broader draw-resume consolidation remains open. Personal Hive's Dazed insertions after a powered
hit now suspend in the saved card-operation queue, then resume the remaining
hit hooks before the original queue continues.
Ordinary source action
choices from automatic `BeforeHandDraw` hooks now resume the saved dispatcher
before the hand-draw prelude.
Legacy combat-entry and summoned-enemy HP rolls, Overgrowth encounter-bag
choices, weighted encounter tickets, encounter-formation selections, and
opening and next-turn AI intent choices now pass through the shared typed
adapter while retaining their existing streams and draw bounds; opening and
later-turn intent choices have separate stable site IDs. The controlled legacy enemy
action replay now covers summon-pool choices and any subsequent summoned-enemy
HP choices, variable legacy Tough Egg hatchling HP, card theft and random pile
insertions. The playable controlled turn-intent continuation now evaluates the
live formation after that enemy-action stage, so newly summoned enemies are
included in the next visible intent boundary; this extension currently has
build-only verification. Native summon-pool selection remains fail-closed
pending source audit. The ReferenceNature run-action session now captures
configured experimental Native Act 1 encounter-pool and formation choices,
supported Overgrowth/Underdocks weak, normal, and elite pool indices, and the
paired pre-combat PotionFactory requests. Other combat-entry requests keep
their current providers; source support/order, seed derivation, and cursor
parity remain open.
Beautiful Bracelet's eligible-deck shuffle and Fur Coat's current-act combat
node shuffle now use typed native swap requests as well. The ReferenceNature
event/reward session covers its admitted relic-acquisition paths; a general
controlled continuation for other acquisition and combat-start contexts
remains pending. Distinguished Cape's two
source-ordered modifier-curse selections now pass through the typed index
provider in native and legacy-prototype modes; the legacy adapter preserves
the original sampler. Tough Egg's hatch request uses the native formation
CombatId as its owner. The six-floor prototype map's width, edge,
convergence, and room-assignment draws now use typed legacy requests on the
existing map stream; this maps the emulator algorithm without establishing
native map fidelity. The later-act map's bounded path
starts, delta shuffles, Glory rest-count pick and room-assignment shuffles now
also use typed native requests. Its Gaussian quota samples now use typed
provider requests. Default Act 1 boss selection, event empty-potion fills,
weighted event tickets, and event-entry gold bins use typed legacy providers
on their existing run streams. Random relic-acquisition card and potion models
use native `player.rewards` index requests when native run RNG is present, with
the legacy providers retained; these stream assignments are hypotheses pending
source validation. Run-effect empty-potion fills also use a native
`player.rewards` index request when the native bundle is present, under the
same stream hypothesis. Event-choice random upgrades, Rare cards, potions, empty
potion-slot fills, and legacy unowned relic picks retain typed legacy requests.
Configured custom random-relic offers use the native `player.rewards` index
provider under the same explicit stream hypothesis. Shop-stock
shuffles, card rarity/model picks and prices, random potion/relic rewards, and
persistent-deck transformations now use typed legacy providers. Fishing Rod's
every-third combat deck upgrade and War Hammer's elite-victory shuffle now use
typed native `run.niche` requests when native run ownership is enabled,
synchronizing the persistent run and active combat stream copies; prototype-only
fallback still uses the legacy provider. The configured Native merchant and
combat-reward sessions capture their registered requests; other legacy paths
keep their typed providers, while source/native parity and unregistered
continuation coverage remain open. The remaining gameplay callsites no longer
call `PrototypeRng` directly: those API references
are confined to the typed legacy adapter and replay support. Native event
cursor validators now replay through the typed native provider without adding
duplicate entries to an active boundary journal.
A typed Gaussian-double adapter now wraps the native
rejection sampler and journals its complete cursor span, including rejected
samples. A typed ranged-float adapter now preserves the native `NextFloat(min,
max)` rounding, and a ranged-double adapter wraps `NextDouble(min, max)`; both
journal their cursor spans. Neither primitive has an additional gameplay call
site mapped yet. The opt-in emulator RNG journal now also carries one
mixed-operation sequence across every typed provider family, alongside its
legacy per-type arrays. It preserves order only for migrated provider calls;
full source call-order parity is still open. `ScriptedRandomBoundaryProviderV111`
replays that mixed transcript through the six typed primitive-provider
interfaces, checking operation family and complete request identity before
each outcome. It rejects the first mismatch and can assert that the full
transcript was consumed; replay does not advance an RNG stream.
An opt-in bounded
enumerator now resolves one small semantic chance support into independent
child sessions; it rejects supports above the requested cap instead of
truncating them.
Several extensions use explicit fixture loadouts. Their source laws remain
`UNVERIFIED`; the shared native provider seam, broad player-action coverage
and native cursor parity for the remaining operations are still open work.

### State ownership and operation contract

The engine owns all actual game objects, their instance identities, zone
membership, enemy AI memory, hook order, eligibility and visibility rules.
The provider owns stochastic realization/constraint data associated with
those objects. A shared semantic operation layer covers:

- **Initialize/commit:** shuffled decks, weighted enemy-intent selections,
  random offers or multi-pick pools. Native may commit an exact realization
  immediately; Nature can retain unresolved legal possibilities.
- **Resolve/reveal:** request one card instance when drawing or revealing an
  unresolved top card; resolve one random target, weighted intent, cost,
  generated card or pool selection when that choice is needed. Revealing a
  previously committed value does not create an additional Nature event.
- **Deterministic update:** put a card on top/bottom, insert at an exact or
  randomly chosen position, remove/return/move a specific instance, peek,
  scry, reorder, exhaust and transform. Preserve forced positions and all
  information already shown to the player.
- **Reset:** a source-triggered reshuffle replaces the relevant unresolved
  permutation constraints; apply on-shuffle relic/card hooks at the original
  time, before any subsequent draw.

Semantic decisions have their own event IDs, support, conditioning state
and transcript; native primitive calls retain separate source stream/cursor
provenance. A native Fisher–Yates shuffle can consume many primitive draws
while Nature may generate one `draw_card` event per later unknown card.
Do **not** expose both shuffle-swap and deferred-card decisions for the same
randomization in one controlled trace or multiply their likelihoods twice.
Consecutive draws and automatic draws each resolve one card before executing
its `CardDrawn` hooks and subsequent effects.

The deferred draw-pile representation must support exact known prefixes/
suffixes and constrained hidden positions, not only an unordered multiset:
an inserted/returned known-top card is forced next, partial peeks/scry lock
revealed identities, and an insertion into the middle constrains the
remaining relative order. Preserve card **instance** identity, enchantments
and mutable attributes. Implement a general constraint representation or
support a proven restricted set of operations with explicit unsupported
errors. Never silently relax constraints to a uniform unknown bag. All
provider-private state must round-trip through fork/save/load/replay.

### Enemy intents: observable result, internal history, exposed weights

The next enemy intent is visible when the player chooses an action. The
emulator retains the enemy's previous moves, AI phase/counters, anti-repeat
filters and other transition state and computes the **current** eligible
intent candidates with their source-defined weights at the original
commitment boundary. Expose to Nature `enemy_intent` metadata with stable
enemy/intent IDs, eligible candidate ordering, nonnegative raw weights,
normalization/total weight, and explicit probability provenance
(`UNVERIFIED` until source-audited). Use the normalized weights as
reference probabilities only when the actual source selection law has been
verified. After Nature selects an intent, commit and reveal it through
the ordinary engine path; subsequent player actions see the committed
intent. Future AI state updates belong to mechanics, not Nature.

The existing bounded-integer/source-shaped intent tickets may continue
internally for exact native RNG parity; they are distinct from Nature's
weighted **semantic intent** choice. Likewise, a multi-card reward offer or
generated-card pool can be selected sequentially without replacement while
preserving the complete offer before any player-choice boundary.

### Scope and scheduling

- **Map:** the player sees the Act map. Preserve ordinary Native source
  generation and visibility. An opt-in `ReferenceNatureMapGenerationSessionV111`
  now replays the source-shaped Overgrowth, Hive, and Glory generators from a
  seed with a saved typed Nature prefix; it does not change default routing or
  claim Native map-order/cursor parity ([session contract](REFERENCE_NATURE_MAP_GENERATION_D3.md)).
  Other map generators remain source-owned.
- **Rewards, shops, encounter formations:** continue ordinary audited
  source-site migrations; no special hidden-state architecture milestone.
- **Cross-stream RNG correlations:** no new structural Nature dependency.
  Counterfactual controlled rollouts need legality and mechanical
  consistency. Any later claim of *original-game* importance-weighted
  unbiasedness still requires a stated, justified event reference law;
  do not label an unverified model as exact.
- **Integration:** preserve isolated #143 work, the active Silent all-Act
  branch, and existing native compatibility. Introduce opt-in semantic
  schema/capability negotiation; do not silently reinterpret old controlled
  Fisher–Yates transcripts.

**Implementation order / acceptance:** (1) native exact vs deferred Nature
provider-owned state and typed semantic events; (2) intent weighted support
and visible-commit timing; (3) deck shuffle/recycle/draw/forced-top and
bottom/insertion/peek/scry with draw-time card hooks; (4) distinct multi-pick
offers and generated pools; (5) fork/replay and 15-worker deterministic
stress. Enumerate small decks to compare sequential draws against
once-shuffled distributions, including constrained insertions, partially
revealed order, and multiple draw hooks. Verify native seeded shuffle counts,
stream cursors and intent parity independently. Unsupported deck operations
fail atomically and explain the missing capability.

## 1. Governing decision

Separate **mechanical execution** from **the selection of random outcomes**.

Conceptually, a player action executes ordinary game rules and ordered hooks. The mechanics invoke semantic stochastic operations at source-authentic effect and visibility boundaries. A provider may resolve immediately or maintain private committed/unresolved state until a value is needed. The mechanics then resume using the selected outcome. Native RNG and externally controlled Nature use the *same* mechanical continuation.

```text
Player action
    -> mechanics / ordered triggers
       -> random-choice request (call site, stream, eligible outcomes)
          -> native stream provider  OR  externally controlled provider
       -> chosen outcome
    -> mechanics / ordered triggers continue
       -> another random choice, player choice, or terminal boundary
```

A random outcome may determine which hooks fire next, which random choices arise, and how many draws a later operation consumes. **Order is defined by executing mechanics**, not by requiring a predetermined list of RNG calls.

### Priorities

1. **Mechanical fidelity:** correct rules, eligibility, timing, side effects, hook order, and persistent state for a supplied sequence of outcomes.
2. **Interchangeability:** native RNG, externally selected Nature outcomes, scripted replay, and finite enumeration use the same mechanics.
3. **Native RNG fidelity:** retain stream-specific codecs, stream ownership, native draw order, and exact seeded replay as a separate, valuable validation mode.
4. **Probability fidelity:** later validate original outcome laws, including conditional distributions given public history, before claiming unbiased importance-corrected training.

Exact native RNG cursor agreement is required **in native-mode parity tests**. A Nature-controlled trajectory is an explicitly counterfactual run; it need not have a compatible native seed/cursor. A parallel shadow cursor is not required in controlled mode.

For training, mechanically legal outcomes are sufficient to build and test the architecture. Optimization against the *original* stochastic game subsequently requires a defined and justified reference distribution.

## 2. Boundaries and ownership

| Component | Responsibility |
| --- | --- |
| Game mechanics | Execute card/enemy/relic/potion/map/event/reward effects, ordered hooks, choices, and state changes. |
| Random-operation layer | Define typed semantic operations and private stochastic-state transitions alongside source-authentic primitive requests, stable call-site identity, stream ownership, eligibility, validation, and outcome application. |
| Native provider | Resolve using v111 named/context streams and native primitive algorithms, mutating the correct stream state. |
| Controlled provider | Pause at a choice, accept an external legal outcome, record intervention provenance, and resume mechanically. |
| Trace/replay provider | Consume a recorded choice transcript, asserting call-site/order/eligibility agreement. |
| Enumerator | Fork a supported finite-choice boundary once per eligible outcome for tests and search. |
| `sts2-ai` | Implement/learn Nature policies, chance proposals, importance ratios, gradient objectives, search, and evaluation. |

The emulator must remain independent of `sts2-ai`. Nature is a consumer-supplied outcome controller, never a policy/network embedded in the emulator.

The RNG abstraction should live in the core gameplay path, **not** as a post-step mutation of a draw pile, target, enemy intent, or RNG state. Rewriting an already-resolved result can skip triggers or destroy hidden-state consistency.

## 3. Two related abstractions

### 3.1 Primitive native RNG operations

Keep a low-level native stream API for `NextInt`, `NextDouble`, shuffle primitives, Gaussian/rejection routines, stream construction, and exact cursor state. The existing `NativeRngV111` implementation, 12 run + 3 player streams, and context-owned map/encounter/monster/event streams are the starting point.

Native mode must preserve *every* audited draw, including `NextInt(1)` calls which consume a native generator step. Preserve source call ordering, stream lifetime, owner identity, and variable draw counts. Use the pinned source and trace evidence, never generic approximations where native behavior is audited.

### 3.2 Mechanic-level random decisions

At game call sites, request semantically described random outcomes. A conceptual API is:

```csharp
// Illustrative design, not an existing repository API.
RandomChoiceRequest request = new(
    SiteId: "combat.monster.unique_hp",
    StreamId: "run.niche",
    Operation: "choose_index",
    Candidates: eligibleHpValues);

RandomChoiceResult result = randomness.Resolve(request);
int hp = eligibleHpValues[result.Index];
```

A request describes:

- **Stable site ID:** identifies the mechanic, not an ephemeral stack trace.
- **Stream owner:** named run/player stream or derived context/creature identity.
- **Operation kind:** bounded integer, weighted choice, selection from a pool, shuffle swap, etc.
- **Eligibility/candidates:** ordered identifiers or a compact, auditable generator/parameterization; preserve stable instance identity when relevant.
- **Mechanical source version:** v111 pin, provenance, and supported fidelity status.
- **Occurrence identity:** local sequence number within the current transition, with a stable pending-event identity for save/fork/replay.
- **Reference probabilities (optional):** declared probability model and verification status, separate from the outcome itself.

The interface must support compact non-enumerated outcomes (e.g., large shuffle spaces) as well as finite lists. For Fisher–Yates, start by treating each native swap/index draw as a separate operation; a higher-level shuffle summary can be layered on later. For Gaussian or other variable-consumption operations, preserve the native primitive sequence and expose controlled operation support incrementally.

**Do not skip a mechanically meaningful RNG call merely because the eligible outcome set is a singleton.** In native mode, such calls must still consume the native draw if the original source does.

When the legal set depends on current game state, compute it at the actual call site, after all preceding hooks have executed. Reject Nature selections not in that set.

## 4. Providers and execution semantics

### Native provider

Uses the existing v111 native stream inventory and context ownership, generating the same primitive results and stream states for the same exact state and call sequence. It must reproduce observed native seeded behavior as independent RNG/whole-run fidelity work progresses.

Preserve the legacy six-stream prototype behind its existing versioned profile. Do not silently switch old training/default consumers to the new mode.

### Controlled Nature provider

At each supported choice boundary, return a serializable pending decision. A caller chooses an outcome; the engine validates it and resumes from the same mechanical location. The controlled state records a mode identifier and intervention transcript rather than pretending to be native-seeded continuation.

The provider may choose a bad or unlikely *legal* outcome; no reference probability is required for this first structural milestone. The engine need not advance a shadow native cursor merely to imitate an unselected random draw. State, replay, and compatibility identifiers must clearly distinguish controlled runs from native ones.

One action may encounter **multiple chance decisions**, interleaved with ordinary effects and additional player-owned choices. Support nested/triggered randomness (death triggers, summoning, generated cards, random targeting), and preserve all intermediate state.

### Trace/replay and enumeration providers

A transcript provider supplies a fixed recorded sequence of choice identities/outcomes and fails at the first mismatch in operation, owner, candidates, or order. `ScriptedRandomBoundaryProviderV111` consumes the journal's mixed transcript across all six typed primitive-provider interfaces and exposes `AssertComplete()` to reject unused outcomes. This enables mechanical differential testing using supplied native-observed outcomes even when the native seed differs.

An enumeration provider forks at a pending small finite-support choice, resolves each legal outcome, and runs the shared continuation. Do not enumerate enormous shuffles or continuous supports by default.

### Persisted continuations

Extend existing forkable/serializable choice-boundary patterns (e.g., enemy-origin blocking player choices), rather than retaining non-serializable C# stack frames or replaying an action from its beginning after every intervention.

The canonical exact state/snapshot of a controlled run must include the pending call-site, operation parameters, already-applied side effects, continuation position, provider mode, and transcript identity. Forking a pending chance boundary yields independent children. Invalid or stale outcomes fail without partial state mutation.

**Compatibility:** an ordinary player-level `step` call in native mode can auto-resolve every intermediate chance decision, preserving current caller-visible APIs. Controlled execution opts into `advance_until_boundary` / `resolve_chance`-style operations via a versioned interface; exact names are implementation choices.

## 5. Current code: initial migration targets

The v111 code already includes `NativeRngV111`, native persistent run streams, context factories, `NativeEncounterFormationFactoryV111`, and `NativeCombatRngEntryV111` with source-specific HP and intent draws.

Move call sites gradually, using small parity-protected batches:

1. **`run.niche` monster initial HP**: current calls in `NativeCombatRngEntryV111.AttachAndRollInitialHp`. Retain duplicate-HP filtering, creation order, and singleton consumption.
2. **`run.monster_ai` committed intent**: preserve move-state eligibility, no-repeat conditions, and commit timing.
3. **Encounter-local formation picks**: `NativeActOneWeakCombatEntryV111` and `NativeEncounterFormationFactoryV111`, including Slimes Weak's three draws and Corpse Slugs Weak's rotation.
4. **Monster-local and summoned-unit randomness**: retain stream-owner/combat-ID identity across creation, death, hatch, and replay.
5. **Source-timed shuffle/recycle, deferred-Nature individual card draw/reveal, forced draw-pile placements, semantic weighted intents, card selection, random targets, generated cards/costs, and reward streams**; native primitive call order is audited independently.
6. **Native Act map, events, shops, treasures, encounter selection, and other context streams**, then remaining single-player content. Defer controlled Nature map generation.

Per migration, the native provider must give identical old/new selected results, exact hashes, named stream counters and final native stream words on the same seeds. Controlled mode must pass legality and hook-order tests. Keep unrevised mechanics explicitly marked as native-only/unsupported in controlled mode; never silently fall back to an unrelated prototype RNG.

## 6. Test contract: separate kinds of fidelity

### A. Mechanical equivalence (highest priority)

With the same mechanically valid initial state, player decisions, and matching supplied chance outcomes:

- identical ordered event/hook transcript, including dynamically triggered calls;
- identical resulting *game-mechanics* state (HP, statuses, card instances, enemy IDs, rewards, phase, etc.);
- identical player legal-action menu and public observation at boundaries;
- correct fork isolation, snapshot/restore, deferred choices, and terminal transitions;
- fail-closed invalid outcome, out-of-order transcript, wrong stream owner, stale pending event, and unsupported call sites.

Compare state while deliberately excluding provider-specific cursor/provenance fields. A mechanically meaningful difference is a failure.

### B. Native seeded RNG parity (separate gate)

Under the native provider, reproduce the source's stream ownership, draw count/order, outcomes, exact post-call cursors, and eventually real-game trace agreement. This gate remains part of the v111 emulator roadmap and must not be weakened by introducing Nature.

### C. Conditional chance-law calibration (later milestone)

After structural correctness, establish and test reference probability kernels, distinguishing:

- primitive/declared synthetic law;
- audited mechanic-level law with verified eligibility and weights;
- correctly conditioned law given public history, including hidden state correlations.

Do not label unverified `p` as the original game's probability. Machine learning with exact `p/q` weighting must require an appropriate calibrated/declared target kernel. Mechanics and controlled Nature demonstrations may proceed earlier.

### D. Throughput and compatibility

Measure native transition/whole-run throughput against the pre-refactor baseline and controlled boundary/fork overhead separately. Keep provider resolution inexpensive in native mode; no Python round trip for native RNG, and no intermediate chance traces unless requested. Restore existing snapshot and JSONL behavior unless an explicit new schema/profile is chosen.

## 7. Implementation batches and acceptance gates

| Batch | Deliverable | Acceptance gate |
| --- | --- | --- |
| **D0 — Spec + inventory** | This document; audit gameplay RNG call sites and classify stream, source operation, conditional support, and migration status. | Versioned inventory with ambiguous call sites called out. |
| **D1 — Core providers** | Shared request/result types, native adapter, scripted replay adapter, optional typed adapter for legacy compatibility, outcome validation. | Native zero-behavior-change tests for initial migrated operations. |
| **D2 — First migrated mechanics** | Monster HP, committed enemy intents, then audited formation picks. | Seeded native result/cursor parity + controlled fixed-outcome hook parity. |
| **D3 — Serializable choice boundaries** | Suspend/resume controlled choice in existing engine continuation framework; pending IDs, fork/save/load. | Nested calls and interrupted enemy-turn/trigger chains replay identically. |
| **D4 — Consumer protocol** | Add opt-in capability/version negotiation, controlled advance/resolve, traces, optional enumerator and Python binding. | Full supported combat segments under native and scripted Nature; legacy `step` unchanged. |
| **D5 — Broad RNG migration** | Provider-owned deck state with per-draw Nature events, weighted semantic intent, targeting, without-replacement generation, reward and remaining mechanics. Preserve native maps; defer Nature-map control. | Exact native shuffle parity plus constrained Nature deck/intent hooks, fair observations, and explicit unsupported operations. |
| **D6 — Reference-law audit** | Conditional probability research, model/version tags, statistical and exact-support checks. | Probability-sensitive consumers can certify target law and compute valid ratios. |
| **D7 — Optimization** | Batch event resolution, lower overhead, cheap forks, provenance/reporting. | Reproducible performance and accuracy regressions. |

Keep a **per-call-site completion matrix** with independent flags: source-audited, native-implemented, native-parity-tested, controlled-implemented, controlled-mechanically-tested, and reference-law-calibrated. Checkmarks in one category do not imply the others.

The current emulator-side site inventory and its deliberately separate status
flags are maintained in
[`DESIRED_RNG_MIGRATION_STATUS.md`](DESIRED_RNG_MIGRATION_STATUS.md). Sites not
listed there remain unassessed; an empty provider journal or missing matrix row
does not imply that the native game made no random call.

The reproducible direct-call census at
[`RNG_DIRECT_CALLSITE_CENSUS.md`](RNG_DIRECT_CALLSITE_CENSUS.md) tracks the
currently visible `PrototypeRng`, `NativeRngV111`, and
`NativeFisherYatesV111` call expressions by source location. It resolves
static shuffle `siteId` arguments and checks them against exact migration
matrix rows. The census also resolves the three generic Native request
constructors through the finite set of mapped Native player-choice/helper
callsites inside Core; callers outside Core are outside its scope. Indirect
calls and native game source operations still need separate tracing.

## 8. First concrete PRs

**PR 1 — D0/D1 scaffolding:** introduce typed random-decision descriptors and a native adapter around existing `NativeRngV111` calls; add a scripted recording/replay provider, a migration inventory and strict no-behavior-change regression tests. Preserve the legacy prototype path and v111 default modes.

**PR 2 — D2 first seam:** route `run.niche` initial HP and `run.monster_ai` intent selection through the shared interface; cover singleton rolls, filtering, ordered summons, and native cursor equality.

**PR 3 — D3 controlled suspension:** implement a serializable pending chance boundary and external outcome resolution using the common mechanics; test trigger cascades, snapshots, forks, and invalid-outcome rejection.

**PR 4 — D4 bridge:** expose controlled opt-in commands and capabilities through the existing JSONL/Python bindings; run complete supported combat snapshots using scripted Nature.

Subsequent PRs broaden source-audited RNG migration. `sts2-ai` implements neural Nature and its training objectives **after** the controlled bridge exists; those changes belong in the AI repository.

## 9. Non-goals of this initial milestone

- Training or embedding a Nature neural network in the emulator.
- Proving the native RNG posterior conditional on public player history.
- Implementing unbiased importance-weighted learning before a justified target probability law exists.
- Forcing Nature-controlled trajectories to match any specific native RNG seed/cursor.
- Replacing the source-audited v111 native codec or weakening native deterministic replay.
- Enabling multiplayer-only mechanics or silently broadening cross-character features.

**Success for the desired RNG implementation:** we can run the same source-authentic game mechanics with (a) native seeded streams or (b) independently selected legal outcomes, preserving hook order, explicit mode provenance, serializable forks, and an unchanged native replay gate. Conditional probability correctness is a later, separately verifiable milestone.

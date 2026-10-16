# Issue #143 P0 — first external playable semantic Nature API

**Schema:** `sts2-v111-semantic-playable-p0-v1`  
**API:** `prototype-ai-jsonl` and `bindings/python/sts2_emulator`  
**Source:** pinned v0.111.0 engine, opt-in RNG/Inspector integration branch

## Why this was prioritized

The first `sts2-ai` Nature training pilot requires real combat through
external JSONL / Python handles, with **exactly two optimized Nature
choice sites**: `enemy_intent` and `draw_card`. Previous #143 work
implemented the original mechanics internally but exposed only
isolated legacy Fisher–Yates and HP/intent fixtures.

This P0 bridge is an opt-in external boundary, not yet full acceptance for
source-calibrated training episodes. It now has both an isolated seeded
entry and a source-routed Act 1 entry that reaches the real first-combat
hook. The source-route smoke reaches true victory on five weak encounters
and replays each across fifteen workers. Native probability and complete
non-Nature RNG parity remain **UNVERIFIED**.

## Capability / negotiated protocol

`hello` advertises:

- `semanticChanceSchemaId = sts2-v111-semantic-playable-p0-v1`.
- `semanticDrawChoiceSchemaId = sts2-v111-semantic-deck-v1`.
- `semanticTargetChoiceSchemaId = sts2-v111-semantic-target-choice-v1`.
- `semanticCardSelectionChoiceSchemaId =
  sts2-v111-semantic-card-selection-choice-v1`.
- `semanticCostChoiceSchemaId = sts2-v111-semantic-cost-choice-v1`.
- `semanticPoolShuffleChoiceSchemaId =
  sts2-v111-semantic-pool-shuffle-v1`.
- `semanticDrawInsertionChoiceSchemaId =
  sts2-v111-semantic-draw-insertion-v1`.
- `semanticExtendedControlledSites = [random_enemy_target, generated_card_pool,
  drawn_card_cost, generated_card_pool_shuffle, generated_draw_pile_insertion,
  random_draw_attack_card_model]`;
  fixture-specific sites require their corresponding seeded-entry options.
- `semanticExtendedPlayerChoices = [select_cards]`.
- `semanticPlayable = true`, scope
  `seeded-source-route-or-post-committed-opening-fixture-restricted-Silent-combat`.
- `semanticControlledSites = [enemy_intent, draw_card]`.
- `semanticOtherChanceProvider =
  engine-owned-deterministic-unverified-v1`.
- `semanticOpeningProvider = precommitted-fixture-v1`.
- `semanticSeededOpeningProvider =
  engine-owned-seeded-source-shaped-unverified-v1` and
  `semanticSeededRouteEntryProviderId =
  engine-owned-v111-native-weak-route-entry-unverified-v1`.
- `semanticProbabilityStatus = UNVERIFIED`.

Legacy `nature_*` and `controlled_*` retain their distinct
schemas, opaque handle namespaces and unchanged behavior.

**Legacy fixture startup:** `semantic_begin` accepts a completed
source-shaped `ControlledPlayableCombatV111` opening created through the
older Nature/controlled endpoints. The newer seeded and source-route entry
paths avoid that external setup chain:

| Operation | Inputs | Result |
| --- | --- | --- |
| `semantic_begin` | `controlled_handle` | Separate `semantic_handle`, phase, pending, fingerprint |
| `semantic_begin_seeded` | seed and pinned encounter parameters | Engine-owned seeded opening; optional source-route mode |
| `semantic_status` | `semantic_handle` | Current player / Nature / terminal boundary |
| `semantic_observe` | `semantic_handle`, `policy_id=prototype-fair-v0` | Existing **player-only** observation and legal action IDs |
| `semantic_step` | handle, fingerprint, legal `action_id` | Child player action / Nature / terminal |
| `semantic_resolve` | handle, fingerprint, legal `selected_index` | Child Nature / player / terminal |
| `semantic_resolve_draw_card` | handle, fingerprint, pending `event_id`, legal `card_instance_id` | Child Nature / player / terminal |
| `semantic_resolve_random_target` | handle, fingerprint, pending `event_id`, legal `enemy_instance_id` | Child Nature / player / terminal |
| `semantic_resolve_generated_card` | handle, fingerprint, pending `event_id`, legal `card_model_id` | Child Nature / player / terminal |
| `semantic_resolve_cost` | handle, fingerprint, pending `event_id`, legal cost value | Child Nature / player / terminal |
| `semantic_resolve_pool_shuffle` | handle, fingerprint, pending `event_id`, bounded `swap_index` | Child Nature / player / terminal |
| `semantic_resolve_draw_insertion` | handle, fingerprint, pending `event_id`, bounded `position` | Child Nature / player / terminal |
| `semantic_step` at `player_choice` | handle, fingerprint, listed legal `select_cards` action ID | Child player / Nature / terminal |
| `semantic_fork` | handle | Independent child handle |
| `semantic_save` | handle | Privileged snapshot string |
| `semantic_load` | snapshot | New replay-validated handle |
| `semantic_release_many` | `semantic_handles` | Number released |

Semantic status-changing routes return `requestId`, top-level
`ok=true`, and a nested `result` with
`semanticHandle`, `fingerprint`, `boundary`, `pending`,
`terminal`, `outcome` and `exitHp`.
Python `Session.begin_semantic_combat` returns a typed `SemanticCombat`.
`begin_seeded_semantic_combat` constructs a restricted seeded entry, and
`begin_seeded_semantic_route` resolves the Act 1 start event and first
combat-map choice through legal actions before entering the real native weak
combat hook. Source-route mode currently covers first-floor Overgrowth and
Underdocks weak encounters.

**Nature-only event metadata:** pending `enemy_intent` contains the source
selector site/stream, stable enemy owner, bounded support, ordinal,
source-filtered semantic intent IDs and move IDs, raw branch weights,
ticket intervals and total raw weight. The implementation checks that
weights are positive, intervals are contiguous, and their sum matches the
source ticket bound. These are source-shaped weights, not independently
calibrated native conditional probabilities: `weightsVerified=false` and
`probabilityStatus=UNVERIFIED` remain explicit. A pending `draw_card`
contains ordered legal physical card-instance IDs and corresponding
card-model IDs, source shuffle epoch, pool/constraint fingerprint, stable
event ID and `UNVERIFIED` law.

The normal player observation comes **exclusively** from
`PrototypeAiEnvironment.Observe` under the existing fair policy.
Only Nature status/resolve reveals candidate physical IDs. No private
draw-pile permutation, RNG streams, provider pools or snapshots are
included in `semantic_observe`.

## RNG ownership boundary

Legacy fixture startup arrives with its opening draw order already
committed. Source-route startup commits the native opening shuffle through
`run.shuffle` before the Nature/native policy split. In both paths the
semantic provider imports that order as privately **known** positions
without new Nature choice tickets. A subsequent
source-triggered recycle creates a fresh unresolved pool and produces
per-card Nature choices only as actual draws need them. This prevents
simultaneously issuing old Fisher–Yates swap tickets and new
`draw_card` tickets for the **same** shuffle epoch inside the new
semantic protocol.

Drawn-card Snecko/Slither costs use an engine-owned provider, never a third
Nature action. Legacy seeded fixture entry defaults to the deterministic
unverified fallback; source-route entry retains the private native-codec
`run.combat_energy_costs` stream. Its assignment and native call-order parity
remain unverified. Unsupported randomness elsewhere fails explicitly with
source site and operation.

## Current legal actions and replay

The initial playable slice supports real Silent Strike/Defend and
`end_turn` through the original engine. End turn composes next-turn
enemy intents, automatic turn stages, new semantic draws and normal
`CardDrawn` hooks. Known positions are drawn directly. Supported
player-actions preserve the provider-owned deck across turns.

Session snapshots record fixture or seeded route provenance and every
legal player/selected Nature action. Loading **replays** that entry to the
same resulting RunState and private provider state. Stale decisions and
unsupported effects leave their parent handle unchanged. Processes and
handles have disjoint private copies.

A transition to `RunPhase.Reward` after defeating the enemy is
classified as `outcome=victory`; `RunPhase.Terminal` is
`outcome=defeat`, with `exitHp` from the actual RunState.
An outstanding end-turn continuation must complete before publishing a
terminal outcome. Source-route victory and replay are verified for five
weak encounters. A seeded Hive Exoskeletons Normal fixture also reaches
defeat on both Nature and native-policy branches from the same source
opening, with save/load and 1-vs-15 replay. Normal full-route coverage and
source-route defeat remain uncovered.

## Outstanding strict P0 acceptance criteria

1. **Independent initial-combat handshake - mechanically partial.** Seeded
   fixture entry and first-floor source-route entry exist. Route mode reaches
   the actual `StartCombat` weak-encounter hook and commits initial shuffle
   and intent entry. Full native parity for route, Neow and entry RNG still
   needs native trace verification.
2. **Filtered weighted intent support - mechanically implemented.** Stable
   enemy and move IDs, post-filter branch weights, denominator and ticket
   intervals are exposed and invariant-checked. Native conditional-law
   calibration remains outstanding; committed deterministic intents stay in
   fair player observations.
3. **Source-law/default provider - incomplete.** Native/default RNG is not
   verified at every non-Nature hook. Reward generation still uses the
   explicitly unverified prototype reward stream; cost-stream assignment and
   other chance-site parity also remain open.
4. **Genuinely terminal supported combats - partial.** Five weak source-route
   victories pass save/restore and 1-vs-15 transcript checks. A seeded Hive
   normal fixture also reaches paired terminal defeat and passes 1-vs-15
   replay, but normal full-route coverage and source-route defeat are not
   established.
5. **Consumer verification - outstanding.** The semantic API has not been
   verified against `sts2-ai` PR #78. A consumer check must negotiate the exact
   schema, keep player and Nature observations separate, and reach a terminal
   combat. Held-out native trials are also required before training claims.

**Do P0 before additional card-effect/Nature chance-site expansion.**
Frozen all-Act Silent `main` and the old
`sts2-v111-controlled-playable-fixture-v1` API remain unchanged.

## 2026-10-16 incremental P0: independent engine-owned seeded opening

The separate `semantic_begin_seeded` entry removes the **external**
`nature_begin` / `nature_advance` / `controlled_begin` / legacy
shuffle-ticket setup chain for the first restricted experiment. Inputs are
`seed`, `act`, `encounter_id`, `native_entry`, `total_floor`,
optional `ascension`, `first_monster_combat_id`, `map_column` and
`map_row`. In Python:

```python
combat = session.begin_seeded_semantic_combat(
    "pilot-seed", act=2,
    encounter_id="proto.native.hive.encounter.exoskeletons_normal",
    native_entry="ExoskeletonsNormal", total_floor=20,
    map_column=3, map_row=5)
```

- The engine constructs the same isolated source-shaped Hive/Glory formation,
  samples unique monster HP on `run.niche`, runs the true source-shaped
  intent eligibility evaluator with `run.monster_ai` bounded tickets,
  and resolves the existing Fisher–Yates opening on `run.shuffle`.
  All setup tickets are **engine-owned**. No HP or shuffle options are exposed
  to the external Nature model, and the already committed deck imports into
  fixed private semantic positions with no second Nature shuffle.
- `hello.semanticSeededOpeningProvider` and each semantic status's
  `sourceOpeningProvider` explicitly say
  `engine-owned-seeded-source-shaped-unverified-v1`. The old
  `semantic_begin` and `precommitted-fixture-v1` still work.
- Seeded entry parameters are persisted privately in the semantic snapshot.
  `semantic_load` reconstructs **the opening from its original seed and
  encounter parameters** before replaying every player/Nature transition.
  Modifying entry parameters or past outcomes breaks exact-state replay.
  Forks and worker processes own independent RNG copies.
- The Python smoke test `tools/nature_semantic_seeded_jsonl_smoke.py`
  verifies seeded handshake, fair observation, deterministic next-turn
  continuation, snapshot round-trip, and transcript equivalence between
  **one and fifteen independent external JSONL processes**.

**Scope / outstanding P0:** This seeded entry is a reproducible *isolated
fixture*; by itself it is not a full game start or native combat entry.
Initial card draw and other entry hooks remain restricted and its source law
is `UNVERIFIED`. A separate source-route entry is documented below. Weighted
semantic intent IDs and weights were added in the following section; native
conditional-law calibration, a validated non-Nature provider for every
remaining RNG site, broader terminal combat coverage and held-out native
evaluation remain open. CI smoke evidence is not decision-grade Nature
training evidence.

## 2026-10-16 P0: filtered semantic enemy-intent support

At a genuine suspended `enemy_intent` source call, the original
`PrototypeGameEngine.EvaluateEnemyIntentChoice` now optionally reports its
already-filtered move candidates without consuming an RNG ticket. The
`UniformRandomAfterOpener` policy supplies one raw weight per legal move;
state-machine `Random` states supply the actual positive branch weights
**after** anti-repeat, phase, summoning and formation filters. Conditional
states are traversed by the source selector first. This inspection replays
only the pure, already-recorded selector prefix and never runs gameplay hooks
or advances the native RNG. Encounter/enemy-specific move policies are
handled by the original move selector.

`hello.semanticIntentSupportSchemaId` =
`sts2-v111-semantic-weighted-intent-v1`, and
`hello.semanticIntentEventSchemaId` =
`sts2-v111-semantic-intent-event-v1`; both implement the shared
`sts2-v111-semantic-chance-event-v1` descriptor contract already used by the
semantic draw-pile event. The existing pending
`enemy_intent` object now carries `semanticCandidates` in source branch
order, with stable `intentId`, `moveId`, `moveIndex`,
`rawWeight`, normalized weight and `ticketStart`; also `rawEligibleWeights`,
`totalRawWeight`, `semanticSupportFingerprint` and a stable `eventId` bound
to the exact pending state and support. The sum of weights
must equal the source primitive pending support count, and contiguous
`ticketStart` intervals partition it. Each candidate is a **legal
branch outcome**, allowing two distinct weighted branches to share one
move ID while retaining different next-AI state effects.

`semantic_resolve_intent(semantic_handle, fingerprint, intent_id)` records a
typed semantic event containing the event ID, support fingerprint, selected
branch ID, raw weight and total weight, then resumes the original mechanics.
The transcript retains the full ordered support alongside the selected ID.
The source-shaped selector uses that branch's first ticket internally as a
deterministic realization; this ticket is not recorded as a second Nature
event. Snapshot replay preserves the semantic decision identity separately
from native bounded-ticket transcripts. Python:
`combat.resolve_intent(event["semanticCandidates"][i]["intentId"])`.
Old `semantic_resolve` with an integer source ticket remains compatible,
including replay/fork/save. A stale fingerprint or forged candidate ID fails
before publishing a new handle.

For `draw_card`, `semantic_resolve_draw_card(semantic_handle, fingerprint,
event_id, card_instance_id)` selects by the stable physical card identity in
the pending event's support. It records a typed `semantic_draw` history entry
containing the complete event descriptor, ordered candidate IDs, selected
index and selected card ID; snapshot loading replays that event and outcome.
Python clients use `combat.resolve_draw_card(card_instance_id)`. The existing
integer `semantic_resolve` route remains compatible with older clients and
snapshots.

The optional `random_enemy_target` extension wraps the existing serialized
engine target continuation. The test loadout
`fixture_random_target_card_id="proto.silent.ricochet"` places Ricochet in the
opening hand, where its real multi-hit effect pauses once per random target.
Each `random_target` event exposes ordered live enemy instance IDs and the
`run.combat_targets` source site; the typed resolver resumes the saved effect
and records a `semantic_target` decision. Replay regenerates the same test
loadout from seeded-entry provenance. This extension is marked
`UNVERIFIED` and does not change the base two-site capability list.

The optional `generated_card_pool` extension wraps the existing serialized
generation continuation. The test loadout
`fixture_generation_potion_id="proto.potion.orobic_acid"` adds Orobic Acid
to the seeded combat. Its real source effects pause at each ordered attack,
skill and power card pool. `semantic_resolve_generated_card` selects by
stable card model ID, resumes the saved effect, and records a typed
`semantic_generated_card` event for replay. Candidate pools preserve source
ordering and remain `UNVERIFIED` as probability laws. The test loadout does
not claim native potion acquisition provenance.

The optional `select_cards` player-choice extension uses
`fixture_selection_card_id="proto.silent.survivor"` to put Survivor in the
first opening-hand slot. Its real play produces a `player_choice` boundary
with source-ordered candidate identities and legal selection action IDs.
`semantic_step` (or Python `resolve_card_selection`) resumes the original
source continuation and records a typed `semantic_card_selection` decision
for snapshot replay. This is an ordinary player choice, not an added Nature
chance site. It does not establish native fixture-loadout acquisition or
generic support for nested random continuations.

The optional `drawn_card_cost` Nature site is enabled with
`fixture_draw_cost_relic_id="proto.relic.snecko_eye"` and
`fixture_nature_draw_costs=true`. It pauses at the original drawn-card cost
continuation before `CardDrawn`, exposes bounded cost values by event ID, and
resumes the same saved hand-draw operation. When both relic and Slither cost
stages apply, they appear as separate events in source order. The default
two-site path keeps its declared engine-owned unverified cost fallback.

The opt-in full-pool generation fixture uses
`fixture_pool_generation_potion_id="proto.potion.colorless"`. Each source
Fisher–Yates swap is a typed `generated_card_pool_shuffle` event with a
bounded `swap_index`, original and current card-model order, and explicit
`UNVERIFIED` probability status. The final swap resumes the original potion
effect and exposes the source `select_cards` action as an ordinary
`player_card_selection` boundary. Python clients use
`SemanticCombat.resolve_pool_shuffle(swap_index)` and then the existing
`resolve_card_selection(action_id)` method. Snapshot replay includes every
swap and the subsequent player choice. This fixture does not claim native
potion-acquisition provenance or source-law calibration.

The draw-pile insertion fixture can use
`fixture_generated_draw_insertion_card_id="proto.silent.strike"` (or another
source-valid card model) for a bounded `generated_draw_pile_insertion` event,
or `fixture_random_free_attack_draw_insertion=true` to expose the source
card-model event followed by the bounded position event. Resolving the
position resumes the source insertion, preserves the unresolved hidden deck
pool and adds the generated card as a private known position. Python clients
use `SemanticCombat.resolve_draw_insertion(position)` after
`resolve_generated_card(card_model_id)` for the random-Attack path. Probability
laws and fixture acquisition provenance are `UNVERIFIED`.

**Probability status:** these are raw weights produced by the emulator's
source-shaped selector, **not** independently audited original-game
conditional probabilities. `weightsVerified=false` and
`probabilityStatus="UNVERIFIED"` with provenance
`source-shaped-unverified` remain explicit until N5 source-law calibration.
Pure deterministic intent commitments require no random ticket; they are
already committed in visible player observations.

Regression evidence: the focused C# gate exercises ID-to-ticket equivalence,
raw-weight sums, replay and invalid choices. Both prior and seeded JSONL
smokes inspect the candidates and resolve intents by ID, including the
15-independent-process equivalence check. This is still a restricted
playable two-site API; authentic opening hooks, native/default non-Nature
chance parity, and terminal combat remain separate P0 gates.

The seeded and victory JSONL smokes also resolve deferred draws with the new
event/card-identity operation. A focused C# regression reaches a natural
discard recycle, checks stale-event and out-of-support rejection, and verifies
typed draw history through save/load replay.

## 2026-10-16 P0: terminal-aware real end_turn transition

The actual guarded `end_turn` executes enemy attacks, powers, relic hooks
and subsequent automatic pipeline stages until its source-defined next
boundary. A fatal enemy action can reach `RunPhase.Terminal` **before**
the next turn's `AdvanceTurn` intent hook. The two-site semantic pilot
must return that true terminal RunState immediately, with
`boundary="terminal"`, `terminal=true`, `outcome="defeat"`,
and exit HP taken from the real player, without issuing an invented next
intent or draw event. The same applies to a genuine reward/victory outcome
from end-turn effects.

`SemanticEndTurnEngineV111.BeginFromPlayerActionOrTerminal` now executes
the ordinary end-turn mechanics **once**, returning either a supported
pending intent/draw continuation or the finished terminal state.
The existing `BeginFromPlayerAction` continuation-only API remains
source-compatible for nonterminal checkpoints, and an explicit exception
identifies a terminal run for callers that require a continuation.
The external semantic JSONL `semantic_step` and Python binding preserve
the exact terminal state, replay history, stale fingerprints, public
observation gating and independent session handles.

A regression drives an independently seeded restricted combat from an
ordinary fair player boundary, repeatedly chooses the legal `end_turn`
action and legal Nature intents/draws, and checks for genuine defeat,
the absence of phantom pending randomness, final exit HP and complete
snapshot reconstruction. This establishes a narrow real **defeat**
terminal path only when CI passes; complete victory and a sufficiently
broad encounter/player policy evaluation are still acceptance work.

## 2026-10-16 P0.4 slice: opt-in engine-owned native-codec cost stream

The prior two-site semantic pilot auto-resolved Snecko/Slither random
drawn-card costs using a deterministic hash of the session seed and current
cost checkpoint. That compatibility mode remains available and is still
the default for the existing `semantic_begin` and
`semantic_begin_seeded` clients.

**New explicit opt-in** on `semantic_begin_seeded`:
`other_chance_provider_id =
engine-owned-native-v111-cost-stream-unverified-v1`.
Python `Session.begin_seeded_semantic_combat(...,
other_chance_provider_id=...)` passes through this request.
`hello.semanticOtherChanceProviders` lists both supported providers;
each semantic `status` exposes the selected
`otherChanceProvider` and cumulative `otherChanceDecisionCount`.

The opt-in `source_route_entry` uses the same private native cost stream
automatically for its Nature branch, seeded from the real run stream bundle
at combat entry. Its native-policy baseline continues the stream directly
through native combat ownership. Source-route users therefore do not need
to request a third Nature choice or configure a separate cost provider.

The engine saves a *private, deeply forked* named v111 RNG bundle
immediately after its source-shaped seeded HP / initial intent /
Fisher–Yates opening setup. When the **real** DrawCards continuation
pauses at a Snecko or Slither energy-cost call, the owned cost provider
samples the pending bounded index with the native v111 NextInt codec from
`run.combat_energy_costs`, then feeds the selected cost into the existing
source-timed original mechanical continuation. The provider stores each
sampled request, selected value, source stream, native before/after cursor,
and `UNVERIFIED` source probability label in a provider-private transcript.
Only `enemy_intent` and unresolved `draw_card` are returned to Nature.
No shuffle-swap ticket is generated for an already committed deck epoch.

- The private stream is not part of fair player observations. State
  fingerprints, deep forks, snapshots and full player/Nature replay include
  this hidden state and its transcript. Stale decisions / unsupported chance
  sites abort before publishing a new child.
- `run.combat_energy_costs` is an **explicit source-shaped assignment
  hypothesis** for these two cost sites. Native stream ownership/call-order
  parity for the installed game still requires validation. This is
  stronger than the former hash-based approximation because it reproduces
  the version-pinned native codec and cursor behavior at the supported
  cost continuation; it is not a claim of exact unobserved native outcomes.
- An optional **isolated test-only loadout**
  `fixture_draw_cost_relic_id=proto.relic.snecko_eye`
  inserts Snecko after the committed opening to exercise the actual draw
  hooks. This does *not* establish native relic-acquisition provenance.
  CI checks the real Snecko cost path, exact private cursor progression,
  replay, and 1-vs-15 independent external JSONL process equality.
- Other non-intent/draw random mechanics (targeting, generation,
  random insertion, nested hooks, etc.) still fail explicitly at their
  unmigrated guarded sites. A complete multi-site native/default provider
  remains P0.4 work.

Example:
```python
pilot = session.begin_seeded_semantic_combat(
    seed="reproducible-run", act=2,
    encounter_id="proto.native.hive.encounter.exoskeletons_normal",
    native_entry="ExoskeletonsNormal", total_floor=20,
    other_chance_provider_id=
        "engine-owned-native-v111-cost-stream-unverified-v1")
```

## 2026-10-16 P0 opening mechanics: source-timed known-card draw hooks

For independent seeded sessions, the legacy controlled opening fixture
deals the first five cards directly after its committed Fisher–Yates
shuffle. The previous semantic `BeginSeeded` imported the remaining
physical pile as *known slots*, but that direct opening omitted the
regular `DrawCards`/`CardDrawn` hook chain.

**Opt-in** `replay_opening_draw_hooks=true` on
`semantic_begin_seeded` and
`Session.begin_seeded_semantic_combat(...)` enables a restricted source
mechanic continuation:
1. Reconstruct the exact pre-deal committed physical pile by appending
   the reverse of the visible opening hand to the remaining bottom-to-top
   draw pile. Check unique instance identities.
2. Re-initialize the private semantic deck from this **already fixed**
   permutation. Every position is known to the provider. Neither
   shuffle swaps nor deferred `draw_card` Nature decisions occur.
3. Execute the actual pinned `DrawCards` and `CardDrawn` hooks for the
   original number of opening cards **once**, under the controlled guard.
   Check that the resulting opening hand and residual physical pile
   exactly match the committed original. A random cost or nested card
   effect outside the restricted starter-hand scope fails explicitly.

`hello.semanticSeededOpeningDrawHooksId` advertises
`engine-owned-seeded-source-shaped-draw-hooks-unverified-v1`; the chosen
`sourceOpeningProvider` in semantic status distinguishes opted-in
sessions from the existing default direct deal. The setting is persisted
in the private seed provenance and round-trips through complete snapshot
replay. Normal player observations remain fair. The test-only Snecko
cost fixture is applied *after* opening hooks, preserving the supported
no-cost starter opening without pretending that Snecko's original
acquisition/first-hand trigger timing is native-authentic.

**Scope:** this ensures the ordered regular card-draw mechanics are
executed for an already source-committed starter hand. Authentic
`BeforeHandDraw`, entry relic/character/card powers, dynamic opening
draw counts, complete game-route combat creation, and real game-local
parity are separate gates. Unsupported effects abort before publishing a
semantic session. The default legacy and seeded APIs remain unchanged.

## 2026-10-16 P0 source-pinned Act 1 weak victory pilot

The independent seeded source-shaped handshake now accepts **two fixed,
named Overgrowth weak formations**, `ShrinkerBeetleWeak`
(`proto.encounter.shrinker_beetle_weak`) and `NibbitsWeak`
(`proto.encounter.nibbits_weak`), at `act=1`. Both are registered in
the source-audited native Act 1 weak encounter table. Native names are
validated exactly, and the native formation generator preserves its own
per-enemy creature RNG contexts and source HP eligibility.
Unsupported Act 1 formations, including the weak Slimes and Corpse
Slugs with encounter-specific random resolvers, fail explicitly.
Existing Hive/Glory entry behavior is unchanged.

For this restricted combat, the player's actual starting Silent deck
and state come from the normal `PrototypeGameFactory.Create`/
`start_run` path. The selected enemy formation is attached to this
isolated run outside map-room entry; it is **not** a claim of fully
replaying a native Act 1 route. The native-seeded opening is committed
once; `replay_opening_draw_hooks=True` executes the source
`DrawCards`/`CardDrawn` logic on the same physical opening deck.
No Nature draw ticket is duplicated.

A new fair-play acceptance test drives actual legal
Strike/Defend/`end_turn` actions against the source-shaped
Shrinker Beetle, with source-filtered intent IDs and deferred
physical-card draw choices. It requires the real combat engine to
reach `RunPhase.Reward`/`victory` with positive HP, no pending
Nature event, and exact full-session replay. The matching Python
JSONL smoke checks identical full actions/Nature transcripts across
**one and fifteen independent external processes**. Its policy is a
mechanical test of victory handling, not a trained model.

This also fixes a source boundary edge case: a genuine automatic
turn may finish *without any new Nature ticket*, when enemy moves are
deterministic and all drawn cards already have known physical positions.
The semantic player wrapper now immediately yields the next legal
player decision rather than retaining an already-complete end-turn
continuation.

**Open acceptance work:** matched held-out native-v111 policy baseline,
genuine full map/room start, additional combat mechanics and multi-site
native/default RNG delegation; native source parity and conditional
reference probabilities are still explicitly unverified.

### Terminal reward RNG is delegated explicitly

The genuine `PlayCard` victory path calls
`EnterCombatReward`/`EnterReward`; legacy reward population consumes
the `prototype_rng.reward` stream. The guarded two-site controller
now enables a **strictly scoped**
`engine-owned-prototype-reward-unverified-v1` fallback for that stream
on legal source actions. All combat/intent/draw/targeting RNG guards
continue to reject unmigrated sites, and this allowance is confined to
the two-site semantic gameplay action scope.

At true `RunPhase.Reward` the original engine consumes the combat
and creates an actual `RewardState`; the controller recognizes this
as terminal victory, with no next-decision deck or phantom Nature
request. Its private `SemanticRewardAuditV111` records reward-stream
`BeforeCalls`, `AfterCalls`, the exact provider name and
`UNVERIFIED` probability status. JSONL `hello` advertises the
reward provider, and terminal `semantic_status` reports
`rewardProvider` and `rewardDrawCount`. Full snapshot replay
repeats the same legal actions, scoped reward draws and exact cursor.

This source-timed **prototype** reward behavior is an explicitly
provisional non-Nature provider. Independent native v111 reward
selection/call-order parity and all other noncontrolled chance sites
remain separate acceptance gates.

## 2026-10-16 P0 follow-up: source-route entry and paired baseline

The opt-in `source_route_entry` now constructs an Overgrowth or
Underdocks Silent Act 1 run, resolves the opening event and first combat-map
choice with legal engine actions, then enters through the real
`PrototypeGameEngine.StartCombat` path. Its replay audit stores the route
actions, map node, resolved encounter, native entry and entry RNG decisions.
Initial native draw-pile order is committed through `run.shuffle`; the
initial intent suspends at `combat.committed_intent.index_draw` before its
first draw. The native-policy branch resolves that same checkpoint with
the native provider. The Nature branch exposes its source-filtered weighted
intent support, then controls later deferred draw identities.

Non-Nature Snecko/Slither draw costs remain engine-owned through a private
`run.combat_energy_costs` stream in the Nature branch. This does not expose
a third Nature decision. Its cursor and decision transcript participate in
snapshot replay.

`tools/nature_semantic_victory_jsonl_smoke.py` now passes for this real
source route: both paired branches reach victory, save/load replay matches,
the native path records decisions and shuffle epochs, and one-vs-fifteen
independent JSONL processes produce identical results. The focused semantic
pilot/cost tests pass 14/14. The full core test suite has 296 failures on
both this branch and its exact integration base, with identical failed test
names; this branch adds one passing test and no new failures.

The legacy restricted P0 semantic JSONL smoke and seeded-entry smoke also
pass after the route changes. The seeded smoke now pairs Nature and native
policy on the Hive Exoskeletons Normal fixture: both reach defeat after four
turns at 0 HP from the same source-opening fingerprint and fair opening,
with save/load and 1-vs-15 replay. It also covers the two Nature sites,
handles/forks, and seven private engine-owned cost rolls per worker; cost
and source laws remain `UNVERIFIED`. This normal encounter is a seeded
fixture, not a full map-route normal encounter.

The source-route smoke also reproduces paired victories on four additional
route seeds:
Overgrowth Shrinker Beetle (Nature/native exit HP 57/62), Overgrowth Fuzzy
Wurm Crawler (54/54), Underdocks Toadpoles (42/47), and Underdocks Sludge
Spinner (56/56). All five route seeds, including the canonical Overgrowth
case, now check the independently rebuilt shared source opening, fair
opening observation, save/load replay, and equality against fifteen
independent JSONL processes. These five weak encounters do not establish
broad coverage of normal encounters.

For each randomized Nature intent event in those paired transcripts, the
smoke checks the advertised weighted-support schema, positive raw weights,
contiguous ticket intervals, sum-to-source-support invariant, and explicit
`UNVERIFIED` probability status. The canonical route exercised one event
with two candidates; Sludge Spinner exercised two events with four
candidates. The other three runs had no randomized Nature intent event
under the chosen route seeds.

**Remaining P0 acceptance:** `UNVERIFIED` route/Neow and filtered-intent
probability laws; native reward and other non-Nature RNG parity; broader
source-routed encounter coverage, including normal encounters; and
`sts2-ai` consumer verification. The paired smoke is mechanics and replay
evidence, not calibration or training evidence. Issue #143 remains open.

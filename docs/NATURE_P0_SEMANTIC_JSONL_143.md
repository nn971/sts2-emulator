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

This P0 bridge is the **first restricted external boundary**, not yet
the final P0 acceptance of fully source-initialized, genuinely terminal
training episodes. All evidence from small fixture tests is protocol/
mechanics smoke only. Probability status is **UNVERIFIED**.

## Capability / negotiated protocol

`hello` advertises:

- `semanticChanceSchemaId = sts2-v111-semantic-playable-p0-v1`.
- `semanticPlayable = true`, scope
  `post-committed-opening-fixture-restricted-Silent-combat`.
- `semanticControlledSites = [enemy_intent, draw_card]`.
- `semanticOtherChanceProvider =
  engine-owned-deterministic-unverified-v1`.
- `semanticOpeningProvider = precommitted-fixture-v1`.
- `semanticProbabilityStatus = UNVERIFIED`.

Legacy `nature_*` and `controlled_*` retain their distinct
schemas, opaque handle namespaces and unchanged behavior.

**Current restricted startup:** first construct a completed source-shaped
`ControlledPlayableCombatV111` opening using the earlier
`nature_begin` / `nature_advance`, `controlled_begin` and
`controlled_resolve` endpoints. Then:

| Operation | Inputs | Result |
| --- | --- | --- |
| `semantic_begin` | `controlled_handle` | Separate `semantic_handle`, phase, pending, fingerprint |
| `semantic_status` | `semantic_handle` | Current player / Nature / terminal boundary |
| `semantic_observe` | `semantic_handle`, `policy_id=prototype-fair-v0` | Existing **player-only** observation and legal action IDs |
| `semantic_step` | handle, fingerprint, legal `action_id` | Child player action / Nature / terminal |
| `semantic_resolve` | handle, fingerprint, legal `selected_index` | Child Nature / player / terminal |
| `semantic_fork` | handle | Independent child handle |
| `semantic_save` | handle | Privileged snapshot string |
| `semantic_load` | snapshot | New replay-validated handle |
| `semantic_release_many` | `semantic_handles` | Number released |

Semantic status-changing routes return `requestId`, top-level
`ok=true`, and a nested `result` with
`semanticHandle`, `fingerprint`, `boundary`, `pending`,
`terminal`, `outcome` and `exitHp`.
The Python `Session.begin_semantic_combat` returns a typed
`SemanticCombat`, with `step`, `resolve`, `observe`, `actions`,
`fork`, `save_snapshot`, `status` and `release`.

**Nature-only event metadata:** pending `enemy_intent` currently
contains the source selector site/stream, stable owner, bounded index
support, ordinal and support fingerprint. **Raw weights and
eligible move IDs are not yet audited and are returned as
`rawEligibleWeights: null`**, not fabricated. A pending `draw_card`
contains ordered legal physical card-instance IDs **and corresponding
card-model IDs**, source shuffle epoch, pool/constraint fingerprint,
stable event ID and `UNVERIFIED` law.

The normal player observation comes **exclusively** from
`PrototypeAiEnvironment.Observe` under the existing fair policy.
Only Nature status/resolve reveals candidate physical IDs. No private
draw-pile permutation, RNG streams, provider pools or snapshots are
included in `semantic_observe`.

## RNG ownership boundary

The opening fixture already committed a concrete shuffled draw order.
The semantic provider imports that order as privately **known** top-first
positions without new Nature choice tickets. A subsequent
source-triggered recycle creates a fresh unresolved pool and produces
per-card Nature choices only as actual draws need them. This prevents
simultaneously issuing old Fisher–Yates swap tickets and new
`draw_card` tickets for the **same** shuffle epoch inside the new
semantic protocol.

A drawn-card Snecko/Slither cost is still resolved through source-shaped
original hooks, but its random selection is an **engine-owned,
deterministic UNVERIFIED fallback**, not exported as a third Nature
action and not represented as a native cursor-accurate sample. Unsupported
randomness elsewhere fails explicitly with source site and operation.
The fallback is a pilot diagnostic, subject to replacement with audited
native/default RNG before the full P0 training acceptance.

## Current legal actions and replay

The initial playable slice supports real Silent Strike/Defend and
`end_turn` through the original engine. End turn composes next-turn
enemy intents, automatic turn stages, new semantic draws and normal
`CardDrawn` hooks. Known positions are drawn directly. Supported
player-actions preserve the provider-owned deck across turns.

Session snapshots record the original completed opening fixture and
every legal player/selected Nature action. Loading fully **replays**
that source to the same resulting RunState and private provider state.
Stale decisions and unsupported effects leave their parent handle
unchanged. Processes and handles have disjoint private copies.

A transition to `RunPhase.Reward` after defeating the enemy is
classified as `outcome=victory`; `RunPhase.Terminal` is
`outcome=defeat`, with `exitHp` from the actual RunState.
An outstanding end-turn continuation must complete before publishing a
terminal outcome. Longer real-combat victory/defeat verification is
still an independent P0 task.

## Outstanding strict P0 acceptance criteria

1. **Independent initial-combat handshake** that sources initial enemy
   HP/intents, opening shuffle/card effects and native/default RNG
   without requiring the older controlled fixture API or user-selected
   Fisher–Yates swaps.
2. **Filtered weighted intent support:** stable source
   enemy-instance and actual move IDs, raw post-history/phase-filter
   move weights and denominator, publication of committed intent
   in fair player observations. Current bounded index is insufficient.
3. **Source-law/default provider:** correct native/default RNG at every
   *other* source hook with explicit provenance. The current deterministic
   cost fallback is intentionally provisional and **unverified**.
4. **Genuinely terminal supported combats:** real source-compatible
   opening and weak/normal encounter actions all the way through
   victory/defeat and exit HP, save/restore and 1-vs-15 independent
   process transcript equality.
5. **Consumer verification:** `sts2-ai` PR #78 can negotiate the
   exact semantic schema, consume player vs Nature observations
   separately and run a terminal combat. Full confidence about Nature
   training requires substantial multi-seed, held-out-native trials,
   not a few smoke rounds.

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

**Scope / outstanding P0:** This is a seed-reproducible *isolated fixture*,
not yet an audited full game start or native combat entry; initial card draw
and other entry hooks still follow the existing restricted opening and native
source law has status `UNVERIFIED`. The P0 independent handshake requirement
is partially addressed, while source-filtered *semantic* intent IDs/weights,
a validated non-Nature native/default provider for all remaining RNG,
and true terminal combat plus held-out native baseline remain open. CI smoke
evidence must not be interpreted as decision-grade Nature training performance.

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
`sts2-v111-semantic-weighted-intent-v1`. The existing pending
`enemy_intent` object now carries `semanticCandidates` in source branch
order, with stable `intentId`, `moveId`, `moveIndex`,
`rawWeight`, `ticketStart`; also `rawEligibleWeights`,
`totalRawWeight` and `semanticSupportFingerprint`. The sum of weights
must equal the source primitive pending support count, and contiguous
`ticketStart` intervals partition it. Each candidate is a **legal
branch outcome**, allowing two distinct weighted branches to share one
move ID while retaining different next-AI state effects.

`semantic_resolve_intent(semantic_handle, fingerprint, intent_id)` chooses
one eligible semantic branch via its ticket interval and resumes the original
mechanics. Python: `combat.resolve_intent(event["semanticCandidates"][i]["intentId"])`.
Old `semantic_resolve` with an integer source ticket remains compatible,
including replay/fork/save. A stale fingerprint or forged candidate ID fails
before publishing a new handle.

**Probability status:** these are raw weights produced by the emulator's
source-shaped selector, **not** independently audited original-game
conditional probabilities. `weightsVerified=false` and
`probabilityStatus="UNVERIFIED"` remain explicit until N5 source-law
calibration. Pure deterministic intent commitments require no random ticket;
they are already committed in visible player observations.

Regression evidence: the focused C# gate exercises ID-to-ticket equivalence,
raw-weight sums, replay and invalid choices. Both prior and seeded JSONL
smokes inspect the candidates and resolve intents by ID, including the
15-independent-process equivalence check. This is still a restricted
playable two-site API; authentic opening hooks, native/default non-Nature
chance parity, and terminal combat remain separate P0 gates.

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

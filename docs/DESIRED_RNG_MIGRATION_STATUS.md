# Desired RNG implementation — migration matrix (D0/D1)

**Baseline:** `agent/v111-hive-glory-pregenerated-encounter-queues-v20`
(pinned native v0.111.0 code). This work is stacked on that emulator
integration branch, **not** the lightweight inspector foundation branch.

**Modes and status.** `native-v111` chooses outcomes from the existing
source-audited xoshiro stream and consumes its cursor. `scripted-v111`
matches a complete, ordered request/outcome transcript and makes no native
draw. `controlled-v111` validates an external index at a single request
site, also without consuming native RNG. Both nonnative providers are
standalone D1 components; there is **not yet** a serializable controlled
game-mechanics continuation or a Nature-policy API. No `p/q` probability
or original conditional law is claimed. The legacy prototype and default
v111 consumer protocols have **not** been switched.

| Mechanics call site | Owner and stream | Native source audited | Existing native mode | Provider seam migrated | Controlled in live mechanics | Native cursor parity gate | Reference law calibrated |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Initial unique enemy HP | each creation-order creature; `run.niche` | Yes: filtered inclusive pool, fallback, singleton consumes | Yes | **Yes, typed finite index** | No, D3 continuation pending | Seeded hash/counter equivalence added | No |
| Committed enemy intent | current enemy; `run.monster_ai` | Supported moves audited per enemy | Yes (subset) | **Yes, bounded integer index with creature owner** | No, D3 continuation pending | Seeded exact hash/counter equality added | No |
| Encounter formation | encounter-local context RNG | Supported formations source audited | Yes (subset) | No | No | Existing tests | No |
| Hatch / death spawn HP | creature creation; `run.niche` | Selected source hooks audited | Yes (subset) | No | No | Existing tests | No |
| Monster-local randomness | monster CombatId owner stream | Partial | Yes (selected) | No | No | Existing tests | No |
| Card shuffle and target selection | named run and player context streams | Partial | Selected paths | No | No | Pending | No |
| Draw/generation/energy costs/reward | separate named run/player streams | Partial | Selected paths | No | No | Pending | No |
| Map/event/shop/treasure selection | act- and model-context streams | Partial | Selected paths | No | No | Pending | No |

## First migrated site: `combat.initial_unique_hp`

The source-shaped mechanic computes an **ordered** HP candidate list, excluding
previously selected monster maxima. If no distinct HPs remain, it restores the
full range. It asks the provider for a *candidate index* at the actual creation
site. This preserves ordered eligibility and native `NextInt(n)` behavior;
specifically, `NextInt(1)` is **not skipped**.

Request identity includes `SiteId`, `StreamId`, `OwnerId` (stable CombatId),
`Ordinal` (creation order), `OrderedCandidates`, and `SourceVersion`.
A transcript records selected index, provider provenance and, in native mode,
exact pre/post xoshiro counter and state words. Outcomes are **not** assigned
reference probabilities. The native attachment returns the unchanged
`CombatState` for existing callers, and optionally a separate audited
transcript via `AttachAndRollInitialHpWithAudit`.

Tests compare **whole exact state hashes** (including all named RNG streams)
against the pre-refactor deterministic native fixture across multiple seeds,
and assert ownership/order/counters. Standalone scripted replay additionally
rejects changed owner, eligibility, order, missing/excess choices, and invalid
controlled indices without consuming a native stream.

## D2 and D3 gates

**D2 progress:** both initial `run.niche` unique HP and
`run.monster_ai` committed intent index rolls now use the common provider.
The intent engine's eligibility logic (source move state, weighted
branches, anti-repeat, opening sequence and per-creature commit order)
remains inside `PrototypeGameEngine.SelectEnemyMove`. The callback
to its existing `nextIndex(maxExclusive)` asks for an ordered index
`[0,...,maxExclusive-1]` after the eligibility checks.
It records a per-enemy CombatId owner and global per-transition
occurrence sequence. `CommitIntentsWithAudit` is opt-in and returns
the selected indices and exact pre/post native stream words/counters.
Seeded fixture tests assert entire combat hashes and exact cursor
equality, including encounters with deterministic enemies that consume
no draws. None of these changes activate Nature-controlled intent
selection in the live engine.

**D2 remaining:** encounter formation, monster-local draws, summons,
shuffle/targets/rewards and other audited call sites; explicit weighted
outcome metadata at move-level instead of primitive index selections.

**D3:** persist the *pending* choice and continuation stage inside the
state (including already executed mechanics, encounter/owner identity and
transcript). Fork and restore at that pending boundary. Only then allow
externally chosen HP/intents in actual combat, with explicitly counterfactual
mode identity. A controlled choice must never be returned as a fake
native-seeded `NativeCombatRngV111` sidecar.

**D4:** expose opt-in controlled resolution in the JSONL consumer protocol.
Existing `step` and native RNG paths retain semantics. Full native-oracle
fidelity remains separate from seed/cursor equality inside the emulator.

## Known limitations

- This first provider handles finite ordered **index** decisions only.
  Native primitive float, Gaussian/rejection, and dynamic weighted choices
  need additional typed operations and call-site audits.
- The scripting provider validates an exact request sequence but is not yet
  a serializable resumable game controller.
- Provider instance state is not in canonical `RunState`, so callers must
  not treat replay/controlled providers as persisted Nature mode.
- The native Inspector is a **separate** read-only oracle; its current
  concrete Strike fixture validates only restricted visible mechanics.
- Native RNG cursor agreement on seeded emulator fixtures does not prove
  full native game agreement, conditional probability calibration, or
  mechanical equivalence at unmigrated call sites.

See [the complete desired RNG specification](DESIRED_RNG_IMPLEMENTATION.md).

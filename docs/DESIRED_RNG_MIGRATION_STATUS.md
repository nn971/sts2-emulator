# Desired RNG implementation — migration matrix (D0–D4)

**Baseline:** `agent/v111-hive-glory-pregenerated-encounter-queues-v20`
(pinned native v0.111.0 code). This work is stacked on that emulator
integration branch, **not** the lightweight inspector foundation branch.

**Modes and status.** `native-v111` chooses outcomes from the existing
source-audited xoshiro stream and consumes its cursor. `scripted-v111`
matches a complete, ordered request/outcome transcript and makes no native
draw. `controlled-v111` validates an external index at a single request
site, also without consuming native RNG. Both nonnative providers started as
D1 components; isolated **serializable D3 HP → intent continuations** and
an opt-in D4 JSONL/Python adapter now exist. There is **not yet** a
whole-run controlled game continuation or a trained Nature-policy API. No `p/q` probability
or original conditional law is claimed. The legacy prototype and default
v111 consumer protocols have **not** been switched.

| Mechanics call site | Owner and stream | Native source audited | Existing native mode | Provider seam migrated | Controlled in live mechanics | Native cursor parity gate | Reference law calibrated |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Initial unique enemy HP | each creation-order creature; `run.niche` | Yes: filtered inclusive pool, fallback, singleton consumes | Yes | **Yes, typed finite index** | Isolated D3 controlled entry only; not in regular run | Seeded hash/counter equivalence added | No |
| Committed enemy intent | current enemy; `run.monster_ai` | Supported moves audited per enemy | Yes (subset) | **Yes, bounded integer index with creature owner** | Isolated D3 controlled entry only; not in regular run | Seeded exact hash/counter equality added | No |
| Encounter formation | encounter-local context RNG | Supported formations source audited | Yes (subset) | No | No | Existing tests | No |
| Hatch / death spawn HP | creature creation; `run.niche` | Selected source hooks audited | Yes (subset) | No | No | Existing tests | No |
| Monster-local randomness | monster CombatId owner stream | Partial | Yes (selected) | No | No | Existing tests | No |
| Card shuffle and target selection | named run and player context streams | Partial | Selected paths | Partial: semantic deck draw/recycle state, typed random-enemy events, and opt-in fixed/random-model generated-card draw-pile insertion events; native operation seam pending | Restricted semantic player-action continuation | Native shuffle audits remain separate; Nature target is counterfactual | No |
| Draw/generation/energy costs/reward | separate named run/player streams | Partial | Selected paths | Partial: typed deferred draws, generated-card model pools, full-pool swaps, draw insertion, and opt-in drawn-card cost events; default costs/rewards retain explicit unverified providers | Restricted semantic draw, generation, cost, insertion and reward paths | Native stream/call-order parity pending for remaining sites | No |
| Map/event/shop/treasure selection | act- and model-context streams | Partial | Selected paths | No | No | Pending | No |

## First migrated site: `combat.initial_unique_hp`

The source-shaped mechanic computes an **ordered** HP candidate list, excluding
previously selected monster maxima. If no distinct HPs remain, it restores the
full range. It asks the provider for a *candidate index* at the actual creation
site. This preserves ordered eligibility and native `NextInt(n)` behavior;
specifically, `NextInt(1)` is **not skipped**.

Request identity includes `SiteId`, `StreamId`, `OwnerId` (stable CombatId),
`Ordinal` (creation order), `OrderedCandidates`, `OperationKind`, and `SourceVersion`.
The HP site uses `choose_index` with an ordered candidate array; the
committed-intent callback uses **compact** `bounded_integer` plus a positive
`BoundedCount`, because the sampled integer may represent a ticket within
weighted legal AI branches. Unlike an enumerated array, this supports even
large integer ranges without allocating the full support. Both use the
pinned primitive `NextInt`.
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
`[0,...,maxExclusive-1]` after the eligibility checks. The legal primitive range is
represented by `BoundedCount=maxExclusive`, not an allocated integer array.
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
The separate semantic playable API now exposes typed weighted intent events,
deferred physical-card draws, random-enemy targets, generated-card model
pools, opt-in drawn-card costs and source card-selection choices. Those
controlled events do not complete the native provider seam or native cursor
parity rows in the migration matrix.

**D3 partial:** the initial HP and committed enemy intent sites now have
persisted, source-reachable pending Nature choices, with fork, restore,
precise eligibility validation and controlled (non-native) provenance.
Completing HP resumes into intents **without rerolling HP**; prior HP
transcripts are part of the intent snapshot. For incomplete intent
decisions, only the side-effect-free AI selector is re-entered with
verified tickets. No prior game actions or hooks are rerun.
This is still restricted to isolated combat-entry fixtures; it does
**not** attach controlled continuation to general `RunState` / player
actions in the D3 HP/intent provider itself and must not fabricate a
`NativeCombatRngV111` sidecar. A separate P0 semantic pilot attaches selected
source continuations to `RunState`, including end turn, card draw, random
targets and generated-card pools; broad player-card and trigger coverage
remains outstanding.

**D4 partial:** opt-in `nature_begin`, `nature_resolve`,
`nature_advance`, `nature_fork`, `nature_save`, `nature_load`, and
`nature_release_many` are available in the JSONL server and through
the thin Python binding, using **distinct non-playable handles**. A separate
playable semantic schema now exposes seeded entry, player actions, weighted
intent IDs, typed draw/target/generated-card resolution, and exact snapshot
replay through the same JSONL/Python bridge.
Existing `step` and native RNG paths retain semantics. Next D3/D4
work must wire controlled choices into general game action continuation
and trained agent-facing observations. Full native-oracle fidelity
remains separate from seeded emulator RNG equality. See
[the isolated Nature bridge](DESIRED_RNG_D3_INTENT_BRIDGE.md).

## Known limitations

- The original `RandomIndex` provider handles finite ordered **index**
  decisions only. The semantic playable extension separately supports
  typed physical-card, enemy-instance and generated-card-model identities.
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

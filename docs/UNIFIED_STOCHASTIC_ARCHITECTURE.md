# Unified stochastic architecture

**Decision:** 2026-10-16. **Status:** accepted target contract; implemented only in part. **Target:** STS2 v0.111.0, single-player first.

This is the **canonical responsibility/ownership contract** for RNG, Nature, Chance Law, and the native bridge. [Desired RNG Implementation](DESIRED_RNG_IMPLEMENTATION.md) specifies the migration details; [staged roadmap](STOCHASTIC_ARCHITECTURE_ROADMAP.md) records implementation and acceptance. The code still contains legacy/prototype and isolated semantic implementations, described in the [migration matrix](DESIRED_RNG_MIGRATION_STATUS.md) and [Nature pilot history](NATURE_P0_SEMANTIC_JSONL_143.md). Those are implementation evidence, not alternate ownership rules.

## Responsibility boundaries

| Owner | Authoritative responsibility | May contain information hidden from player? |
| --- | --- | --- |
| **Game engine / mechanics** | Objects and stable IDs, zones and membership, move histories, eligibility and source weights, player actions, deterministic hooks, timing and consequences of selected outcomes | Yes: hidden **mechanical** history |
| **Native RNG provider** | Source primitive RNG, streams/cursors/seed derivation, committed unrevealed permutations and other stochastic realizations; immediate auto-resolution | Yes: hidden **stochastic** realizations |
| **Nature provider** | Deferred stochastic objects, unresolved permutation/selection constraints, previously committed/revealed constraints, validation and receipt of an externally chosen legal outcome | Yes: hidden **stochastic** constraints |
| **Chance Law** | Read-only conditional reference distribution of legal outcomes for a declared pre-outcome information/constraint state | It may inspect declared constraints, **never** a selected value or Native's sampled realization |
| **ReferenceNature** | Simple, nonlearned Nature policy that samples exactly from the Chance Law distribution | Only the Nature choice information supplied by its contract |
| **Scripted replay** | Substitute previously recorded semantic outcomes, checking operation/site/support/constraints | Recorded outcomes, with explicit provenance |
| **Observation/access layer** | Player-visible projection, Nature-choice projection, privileged inspector/debug projection | Separates audiences |
| **Native reference mod / Fidelity Inspector** | Capture installed-game evidence, audit source call/hook parity, compare native and emulator mechanics | Privileged diagnostics by explicit opt-in |
| **sts2-ai** | Learned Nature proposal distribution `q`, player policy, training/search/evaluation and corpus management | Only through negotiated interfaces |

**No runtime dependency** exists from Native RNG to Chance Law. The native mod does not call Chance Law. Chance Law is a callable analytical service primarily for Nature and tests; source-game captures can later *verify* it offline.

## State ownership

The engine owns the **complete simulation lifecycle** (fork, snapshot, restore, replay, hashing) of both mechanical state and the chosen provider's private stochastic state. This does not make the engine the authoritative owner of hidden stochastic representation. In particular:

- The engine owns **which physical card instances are in the draw pile** and every known/public placement or movement consequence.
- The active provider owns **the private ordering, committed permutation, or unresolved order constraints** and updates them on mechanical insert/remove/reveal/shuffle operations.
- The engine owns enemy history and eligibility filters; once an intent is resolved, the resulting committed enemy intent is mechanical state and becomes player-visible at the ordinary game boundary.
- Native provider state and Nature state are alternative realizations: a Nature counterfactual has no obligation to match a native seed or shadow cursor.
- A complete state fork or serialized snapshot must include both components **atomically**. No stale `CombatState.DrawPile` / `SemanticDeckStateV111` dual ownership is permitted in the target model.
- Exact snapshots and fingerprints are **privileged**; fair policy observations omit private order, RNG cursors, seeds and Nature-only candidate metadata.

The present `RunState.Rng`, `NativeRunRng`, optional `OtherChance`, concrete `CombatState.DrawPile`, and `SemanticDeckStateV111` have overlapping representation/lifecycle responsibilities. Introduce a conceptual `SimulationState { Mechanics, Stochastic }` envelope without immediately rewriting or breaking these versioned types. Make one authoritative write path and assert zone/constraint correspondence during migration.

## Semantic lifecycle and provider interchangeability

The **same game mechanics** issues semantic stochastic operations and applies their resolved outcomes regardless of provider:

1. **Commit / initialize** a source-triggered shuffle, offer, or intent decision. Native may immediately record a complete hidden result; Nature may record an unresolved object with constraints.
2. **Query / reveal** only when mechanics require a value. A committed value is deterministic and must not generate a second chance ticket. Nature may be asked for a value from an unresolved object; the choice must satisfy the constraint state.
3. **Consume / apply** the resulting physical identity or move through the ordinary engine; run its hooks **exactly once**, and continue to the next mechanical/chance/player boundary.
4. **Update / reset** the provider's ordering constraints in response to exact insertions, reveals, peeks/scry, removals, card creation, discard/recycle and source-time reshuffles.

A native Fisher–Yates shuffle and Nature's later `draw_card` events can express the **same underlying random permutation**. Never expose or likelihood-weight both for that single randomization. Source-native low-level primitive calls (including singleton draws) remain available for native parity instrumentation, independently of semantic Nature events. Preserve stable card **instance** identity and history-dependent eligibility.

## Chance Law and information conditioning

A chance-law query takes a **pre-outcome** semantic operation, legal support/weights from mechanics, and a declared information/constraint state. It returns a versioned conditional distribution `p` with ordered stable outcome IDs, exact/compact weights or probabilities, support/constraint fingerprints, conditioning policy and probability **provenance** (e.g., `UNVERIFIED` or source-verified).

- Its result must be independent of the outcome eventually selected by Native, ReferenceNature or learned Nature.
- Native provider's already-committed hidden realization is **not** a Chance Law input for Nature's pre-revelation distribution. Conditioning on a complete private realization would instead produce a degenerate distribution; it answers a different question.
- For uniform unknown permutations, sequential sampling over remaining eligible physical instances can produce the correct joint law, subject to known positions, shared pools, forced insertions, observations and other constraints. Compute the conditional law of the remaining random object, not merely an arbitrary uniform distribution over listed candidates.
- For enemy intents, reuse exactly the engine's current post-filter eligible moves and relative weights. Those weights' **original-game fidelity remains UNVERIFIED** until separately audited.
- Probability computation is **optional in normal Native execution**: Native invokes its own source sampling procedures, with no Chance Law round trip.
- `ReferenceNature` samples from reported `p`; learned Nature supplies `q`. Their selected outcomes enter the same continuation. Unverified `p` can support structural tests and exploratory training; unbiased original-game `p/q` claims require independent N5 calibration.

## Three independent validation tracks

1. **Structure and declared-law consistency:** legal normalized Chance Law; exact small-state enumeration; ReferenceNature sampled vs independently specified joint distributions; common mechanics; atomic forks/replay and scheduling independence. No original game or exact native streams required.
2. **Two-site Nature vs control training experiment** — [issue #143](https://github.com/nn971/sts2-emulator/issues/143): `enemy_intent` and unresolved `draw_card` only under Nature; all other randomness engine-owned. Same fair player/action contract, matched reproducible emulator A/B, 15 isolated workers. The control may use the declared-law ReferenceNature as a transparent sampling baseline; a separate native-style commit-once provider is also a useful implementation cross-check. Neither implies native cursor parity.
3. **Native-game fidelity** — [issue #122](https://github.com/nn971/sts2-emulator/issues/122) and [Inspector plan](NATIVE_FIDELITY_INSPECTOR_PLAN.md): installed-game captures, mechanics/hook/stream ordering, exact RNG call/cursor provenance. **N5 original-game probability calibration** uses independently sourced rules/evidence and is separately gated.

**ReferenceNature cannot certify its own Chance Law** merely by sampling from it. Correctness of `p` needs an independent oracle: exact enumeration for small decks, separately specified analytic weights/rules, and eventually pinned original-game evidence. A statistical comparison between Native and ReferenceNature is helpful only when Native's distribution at the compared information boundary is well specified.

## Compatibility rules

- No schema change to existing v111 `nature_*`, `controlled_*`, `semantic_*` or ordinary JSONL/Python interfaces merely to introduce the design. New capabilities and law/provenance fields are **opt-in and versioned**.
- Preserve existing snapshots, immutable forks and replay invariants; old histories retain their original meaning and probability labels.
- Make unsupported stochastic operations **fail atomically** and report the site; no undocumented prototype/hash fallback in a new claimed ReferenceNature/native control profile.
- Keep `sts2-ai` training policies out of the C# engine; a deterministic seeded ReferenceNature test sampler is emulator infrastructure.
- Preserve the ongoing Silent four-Act work, reference mods and inspector PR stacks. Any implementation migration must identify its own tests and feature gates.

## Reading order

1. This architecture contract.
2. [Roadmap and acceptance tests](STOCHASTIC_ARCHITECTURE_ROADMAP.md), with the current [site and state ownership inventory](STOCHASTIC_SITE_OWNERSHIP_INVENTORY.md).
3. [Desired RNG implementation](DESIRED_RNG_IMPLEMENTATION.md) and [RNG model](RNG.md).
4. [Nature pilot API](NATURE_P0_SEMANTIC_JSONL_143.md) and [migration status](DESIRED_RNG_MIGRATION_STATUS.md).
5. [Native Fidelity Inspector plan](NATIVE_FIDELITY_INSPECTOR_PLAN.md) and [status](NATIVE_FIDELITY_INSPECTOR_IMPLEMENTATION_STATUS.md).

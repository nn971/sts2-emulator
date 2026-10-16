# Unified stochastic architecture — implementation roadmap

**Recorded:** 2026-10-16. **Status:** proposed implementation milestones, **not completed work**. **Authority:** [Unified stochastic architecture](UNIFIED_STOCHASTIC_ARCHITECTURE.md).

**Implementation scope:** `sts2-emulator`, STS2 v0.111.0, single-player. The first vertical slice is two-site Silent combat. Keep the four-Act Silent gameplay and active native inspector developments on separate tracks. Existing versioned `controlled_*`, `nature_*`, `semantic_*` and ordinary AI protocols remain supported; no historic snapshot/probability reinterpretation.

## Priorities and milestones

| Stage | Priority | Deliverables | Acceptance gate |
| --- | --- | --- | --- |
| **S0 — Contract and inventory** | P0 | Canonical ownership model; document source-vs-model probability status; map all current RNG sites, private-state owners and overlapping code paths | Maintainers can identify for each site: owner, operation, private constraint, public visibility, resolver, continuation, provenance and unsupported handling |
| **S1 — Chance Law + ReferenceNature** | **P0 / next** | Pure query for `enemy_intent` weighted eligible support and unresolved `draw_card` conditional probabilities; typed provenance and conditioning ID; seeded simple `ReferenceNature` selector | Independent exact small-deck enumeration; intent eligibility/weights; distribution normalization; outcome-blindness; sample-vs-declared-law checks; deterministic fork/replay; zero original-game requirements |
| **S2 — Complete engine-owned RNG control arm** | **P0 / #143** | Auto-resolving emulator policy for the same two semantic sites; all other reachable stochastic sites delegated to explicit engine-owned provider; seedable/forkable state and full traces | Real supported Silent fights work under both ReferenceNature/control and learned/scripted Nature with the **same mechanical continuation**; no unknown RNG fallback; terminal replay; 1 vs 15 workers |
| **S3 — Unified provider-state envelope** | P1 | Mechanical/hidden stochastic state ownership contract; one authoritative deck-zone membership and private-order representation; stable shuffle epoch/commit/reveal/consume semantics; atomic snapshot/fork | Forced top/bottom insertion, middle insert, peek/scry, recycled draws, multiple shared hidden pools; no duplicate shuffle sampling, divergent zone copies, or accidental information leak |
| **S4 — Common mechanics continuation** | P1 | Migrate specialized controlled end-turn/draw/pilot staging to the ordinary engine's resumable chance suspension; preserve old adapters as facades | Native, ReferenceNature, scripted and learned Nature produce identical **mechanical** results for identical semantic outcomes, including nested hooks and terminal rewards; legacy tests remain green |
| **S5 — Shared observations and external contracts** | P1 | Versioned public player view, Nature-only chance view and privileged inspector view; stable semantic IDs and replay fingerprints; bridge capability negotiation | No private RNG/order leak to player; consistent actions/observations across emulator modes; native spectator declares gaps; schema compatibility and malformed/stale choice rejection |
| **S6 — Broaden stochastic coverage** | P2 | Select-without-replacement offers, rewards, shops, formations, generated cards, targeting, draw costs and later map operations | Every supported stochastic site declares its sampling law, provider ownership, source timing and supported hidden constraints; expanded source-independent tests |
| **S7 — Source-law calibration (N5)** | Parallel, independent | Audited source conditional laws, `p` provenance, original-game importance-correction eligibility | Explicit independent mathematical/source evidence for each law; no `UNVERIFIED` `p` used as original-game-unbiased likelihood |
| **F — Native Fidelity Inspector** | **Parallel / #122** | Source capture, hook/event/state diff, named RNG stream/cursor audits, exact source-based replay | Independently validated installed-game traces; no requirement for S1–S2 structural acceptance |

## S1 precise design

Implement a **read-only, outcome-blind** Chance Law query in `Sts2Emulator.Core` with:
- Semantic operation and call-site/owner/occurrence identifiers.
- Current legal **physical** outcome identities (distinct card instances) or stable intent IDs, eligibility and raw weights.
- Conditioning-policy ID and hidden **constraint** fingerprint; no selected native realization or RNG seed/cursor.
- Normalized/compact reference distribution, law version and `UNVERIFIED`/independently verified provenance.
- Immutable query result: the law cannot mutate RNG streams, card positions, game state or Nature's constraints.

`ReferenceNature` consumes that result, draws from it using an independent seeded test PRNG, returns one legal outcome and updates the Nature provider constraints through the existing selection/continuation mechanism. A known already-committed card produces **no Nature choice**. No native stream parity is required.

### Required independent tests

1. Weighted enemy intents after phase, previous-move, anti-repeat and formation eligibility filtering; candidate order changes must preserve stable identities and properly invalidate old support fingerprints.
2. Exhaustive **3–4 physical-card permutation** enumeration, including duplicate card models with distinct instances, known top/bottom positions, interleaved known and unknown slots, multiple draws, forced middle insertions, peek/scry and recycle/Perfect Fit restrictions.
3. Conditional joint probabilities after sequential decisions; compare exact small-state probabilities to independently enumerated reference permutations (not only Monte Carlo counts from ReferenceNature).
4. Proof-by-execution checks: Chance Law query must not change the state hash or depend on an already sampled realization when queried under the **same declared information state**; resolving a known card must never re-randomize it.
5. Invariant-based mechanical continuation: each chosen card triggers the real `DrawCards` / `CardDrawn` path once, with nested costs and interactions remaining engine-owned or explicitly unsupported.
6. Independent fork/save/load/replay, stale-event rejection, mode/provenance versions, state ownership and observation information-boundary tests.
7. Monte Carlo distribution smoke over many independently seeded episodes as a secondary check; a handful of rounds is **not** an evaluation of learned performance or general distribution accuracy.

## S2: training comparator and honest provenance

For #143 use two clear emulator policies:
- **ReferenceNature / declared-law control:** samples `p` at the semantic Nature sites without source cursor parity and delegates other chance to the engine's ordinary provider. This is the primary clear structural baseline.
- **Learned Nature:** selects `enemy_intent` and `draw_card` through `sts2-ai`; other stochastic operations go through the **identical** engine-owned provider.

Optionally check a **native-style commit-once control** (exact hidden deck realization using source-native RNG algorithms) against the ReferenceNature's specified conditional law. This is an additional distributional cross-check, not a requirement that counterfactual draws match native seed/cursor. Do not call an uncalibrated `p` the original-game conditional law.

Run matched source-state/seed sets; frozen player policy; fair observations/legal actions; independent full combat terminal outcomes; process-isolated 15-worker replay; report variance and multi-run comparisons. Keep toy/structural smoke distinct from performance evidence. `sts2-ai` owns optimizer, neural policy and experiments.

## S3–S5: migration without breaking the running project

- First introduce an adapter around existing `RunState`, `CombatState.DrawPile`, `SemanticDeckStateV111`, `SemanticOtherChanceStateV111` and named RNG bundles. Identify the sole writer of physical membership versus hidden order; reject mismatches atomically.
- Make the choice/continuation path consume semantic outcome identity, with a separate low-level native primitive journal. Preserve and version primitive-index scripts for compatibility.
- For each moved mechanic, add a native/scripted/ReferenceNature equivalence fixture and a legacy compatibility fixture **before** changing the default routing.
- Merge/rebase native inspector spectator/stream PRs independently on their current approved base. Carry forward source-specific hooks without binding them to Chance Law.
- Avoid touching the active Silent content branch while structural foundations are developed and tested on the isolated RNG integration branch.

## Deliverables, status discipline and ownership

- **S0:** docs in this PR; no code/CI claims.
- **S1:** core Chance Law and simple ReferenceNature implementation + independent tests; dedicated PR.
- **S2:** #143 training-owned default/non-Nature delegation + reproducible A/B report; the AI-side trainer is in `sts2-ai`.
- **S3–S6:** follow-up opt-in implementation PRs with versioned capabilities and targeted tests.
- **S7:** probability evidence/calibration; separately gated from #143.
- **F:** [inspector issue #122](https://github.com/nn971/sts2-emulator/issues/122) and [native plan](NATIVE_FIDELITY_INSPECTOR_PLAN.md).

**Success criterion for the unified architecture:** one mechanical execution path under Native, ReferenceNature, scripted replay and learned Nature; provider-private state and provenance are unambiguous; declared-law ReferenceNature passes independent conditional distribution tests; complete states replay/fork identically; player-visible observations remain fair. Source-exact RNG parity remains its own inspector acceptance gate.

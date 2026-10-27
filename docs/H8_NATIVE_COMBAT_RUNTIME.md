# H8: Native Combat Runtime and Stochastic Boundary Architecture

Status: **implementation in progress** (2026-10-28). Research branch:
`research/h8-native-combat-runtime-20261028`, initially based on H7 pooled-hash PR #204.
This document is the **design and acceptance contract**; an implemented slice
must be identified separately from future milestones.

## Why rewrite the execution architecture?

The H7 stage profile shows actual card-draw mechanics cost roughly 0.7 ms,
while a full externally mediated draw costs tens of milliseconds, and still
allocates about 19.6 MB per draw after PRs #198/#200/#201/#202/#203/#204.
The dominant operations are whole-state validation, canonical hashing,
nested semantic continuation projections, full-state forks, and public
JSONL descriptions. Those costs come from using audit/snapshot structures as
the *ordinary execution model*. Pooling hash buffers lowers allocations but
retains the wrong unit of work.

The goal is **maximal correct combat throughput** on the intended A10 Silent
training curriculum, including 15 concurrent workers and a ~14.66M-parameter
shared transformer. Compatibility with v111's internal record hierarchy,
fingerprint format, flattened JSON, and private helper APIs is deliberately
**not a design requirement**. Old code is an independent oracle during
migration; it is not the architecture to clone.

## Non-negotiable semantic requirements

1. **Interchangeable stochastic providers.** One typed request/answer
   interface for Native RNG, ReferenceNature, learned Nature and scripted
   deterministic decisions. Requests retain a chance-site identity,
   eligible support, distribution/Chance Law and provider-owned hidden
   randomness. Provider selection must not change deterministic mechanics,
   source-visible observation, or hook order. The runtime never allows an
   unclaimed random draw to fall back silently to a different provider.
2. **Reproducible decision histories.** Append an ordered, typed journal of
   *player and stochastic* choices, including the request/choice identity,
   selected physical card/enemy identity or numeric outcome, provider and
   deterministic occurrence ordinal. Replay reconstructs a state by running
   from a declared checkpoint and consuming the journal exactly. It must
   detect missing, extra, reordered, or unsupported entries.
3. **Correctness over schema compatibility.** Same modeled mechanics,
   physical card identity, HP/resource transitions, and source hook ordering;
   two different stochastic providers may produce different choices.
   Canonical legacy fingerprints need not survive the migration.
4. **No fidelity bottleneck.** Native Inspector comparison and exact native
   RNG parity are independent research/audit tracks and must not gate
   training. Mechanic/unit/branch/replay regressions *do* gate H8 adoption.
5. **Replay and branching.** Training may append decisions without rerunning
   the prefix. Full verification and re-execution occur at explicit audits.
   Search may request a fork/checkpoint; forked states must have independent
   mechanics/provider mutable ownership and deterministic log prefixes.
6. **Failure is atomic.** Stale version, illegal support, wrong request kind,
   or provider mismatch must leave the runtime state, provider cursor and
   decision journal unchanged.

## Proposed ownership model

```text
H8CombatRuntime
  ├── CombatState (single authoritative live mechanics owner)
  │    ├── card registry and zone membership
  │    ├── enemies, powers, HP, resources, turn counters
  │    └── deterministic hook/effect evaluation state
  ├── EffectCursor (discriminated, resumable mechanics continuation)
  │    ├── op code / card instance / target / effect index
  │    └── the currently suspended typed chance request, if any
  ├── IStochasticProvider (Native | ReferenceNature | Learned | Scripted)
  │    ├── provider-private RNG/constraints
  │    └── typed Chance Law / support
  ├── DecisionJournal (append-only player + Nature events)
  └── optional checkpoint/rewind and audit facility
```

The runtime owns the *sole* mutable combat state. A selected card must
update that state and the provider state in one successful transition.
There must not be simultaneous mutable `Origin`, `Run`, end-turn and hand
draw copies. A continuation is a cursor, not another suspended combat
object. In the first bridge slice, the existing v111 engine can temporarily
serve as the mechanics adapter; this is explicit temporary technical debt.

## Fast path and proof boundaries

- Live boundary identity: `runtime_id`, monotonically increasing
  `boundary_version`, local `request_sequence`. Each choice checks the
  expected version before touching mechanics. Structural request validation
  is localized to its support (e.g. selected physical card is eligible).
- Cryptographic hashes are reserved for exported snapshots, corpus
  provenance and audit/replay comparisons, not each local transition.
- Default runtime mode performs only local, cheap invariant checks. Explicit
  audit mode performs full invariant and end-to-end replay validation.
- All deterministic effects between two externally visible boundaries
  advance in one call. Observations are generated only when requested.
- Unrecognized stochastic sites fail closed with an actionable site ID.
- A fast resolver must never skip hook order or an unresolved stochastic
  choice. If a correctness assumption has not been proved, keep that segment
  on the checked path until the test and model support exist.

## Implementable milestones

### M0 — Provenance and measurements
- [x] Record architecture and speed/semantic acceptance contract.
- [ ] Add phase-based metrics: mechanic evaluation, continuation cursor,
  support construction, output observation, codec/IPC, checkpoint,
  per-site provider overhead, actor collation/forward/batching.
- [ ] Preserve matched A10 scenario seeds and source digests, and instrument
  allocation, GC pause, CPU and GPU waiting.

### M1 — Versioned runtime and journal (first live vertical slice)
- [ ] Implement a session-owned monotonically versioned boundary handle,
  typed request identity and append-only player/Nature decision journal.
- [ ] Prove stale/rejected transitions cannot modify state or journal.
- [ ] Exercise a **real** source-valid A10 Silent card draw through the H8
  boundary; compare source mechanics and outcomes to the legacy checked
  resolver. Provider selection is distinct from mechanics resume.
- [ ] Exercise forked replay and verify deterministic decision order.
- [ ] Expose an opt-in public H8 adapter, not a replacement of the old API.

### M2 — One mechanical owner and an effect cursor
- [ ] Replace `SemanticPilotCombatV111.PendingEndTurn`,
  `SemanticEndTurnV111.HandDraw`, and copied `Origin/Run` live frames.
- [ ] Introduce a tagged cursor that resumes a deterministic hook/effect
  suffix; no recursive re-projection on each support query.
- [ ] Migrate common real combat paths in order: player action -> hand draw
  -> recycle -> before/during/after draw hooks -> enemy intent and action.
- [ ] Keep source-visible mechanics and provider state synchronized.

### M3 — Typed providers and replayable checkpointing
- [ ] One explicitly typed support/outcome contract for index, unit float,
  ranged float, ranged double, Gaussian integer/double, as applicable.
- [ ] Native, reference, learned and scripted adapters; chance-site and
  distribution metadata from the same central Chance Law registry.
- [ ] Compact append-only choice journal; checkpoints at requested intervals
  or search branches, never full prefix replay after every step.
- [ ] Audited reconstruction from source plus journal; exact terminal
  equality; reject mutation, missing/extra choices and wrong-site scripts.

### M4 — High-throughput training transport
- [ ] Batched session advance over multiple combats and Nature boundaries.
- [ ] Compact typed/binary observations and legal-action views, with stable
  IDs, avoiding unnecessary JSONL and Python object construction.
- [ ] Shared-memory or native IPC path; keep JSONL solely for debugging and
  migration.
- [ ] Coalesce GPU inference across 15 workers and increase realized batches.
- [ ] H7/H8 paired profiler-OFF A/B/B/A at 128+ combats, 15 workers, same
  14.66M actor; repeat, record uncertainty, profile only for diagnosis.

## Acceptance tests and performance policy

- Source-valid A10 combat selection with at least 16 corpus cells and a
  128-combat/15-worker large-model run; no silent replacement by easy
  synthetic encounters.
- Exact deterministic mechanics after matched actions + matched stochastic
  outcomes: HP, resources, deck/hand/draw/discard/exhaust membership and
  physical instance IDs, enemy state, turn and effect/hook continuation.
- Independent provider tests: same deterministic request semantics with
  Native, ReferenceNature, learned and scripted provider choices.
- Journal replay: deterministic re-execution from a fixed checkpoint;
  sampled 1/8 for training, 100% for evaluation; forks diverge independently.
- Stale/illegal decision safety, no provider RNG cursor consumption on
  rejected choices, and no mutation of the parent when branching.
- Measure **emulator-only** throughput and **end-to-end large-transformer**
  training throughput separately. Prior H7 gains were from a 16-combat
  profiled CPU run; do not extrapolate those percentages to CUDA.
- Record negative results. A fast path with incorrect mechanics, missing
  chance sites, or nonreplayable history cannot replace the existing path.

## Migration and branch policy

Implement in an isolated H8 research branch without modifying the active
v111 feature branch or `sts2-ai` pinned emulator. The PR stays draft until
the new path actually executes real combat. Introduce H8-specific types,
runtime handles and tests first. Retire legacy projections **after** the
single-owner runtime is able to cover the training curriculum. Avoid adding
another permanent nested facade that reproduces v111's expensive structure.
Existing CI fixture/fidelity failures should be triaged separately.

**Critical next step:** M1 must contain a real executable decision path,
not merely interfaces or an architectural diagram. Report what still falls
back to the old engine and do not claim M2 or M3 are complete.

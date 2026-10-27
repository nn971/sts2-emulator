# H8 M2: Owned draw cursor and deferred audit — first executable slice

Status: experimental, stacked on H8 M1 PR #205. PR #206 isolates the M2
change; it is not a replacement for the entire H8 runtime roadmap in
[H8_NATIVE_COMBAT_RUNTIME.md](H8_NATIVE_COMBAT_RUNTIME.md).

## Observed deficit

Before M2, the version-checked H8 card-draw bridge still passed through
`SemanticPilotCombatV111.Resolve` ->
`SemanticEndTurnEngineV111.ResolveCore` ->
`SemanticCombatDrawEngineV111.ResolveCore`. That eagerly deep-forked the
entire nested end-turn record **in addition to** the RunState fork that the
actual card mechanics need, and recursively validated three output frames
during each accepted physical draw. Ordinary H8 status/support/choice queries
also independently reconstructed the projected pending end-turn object.

The per-transition costs are unrelated to stochastic semantics: selected
physical card identity is already checked against provider-owned support,
and the deterministic combat engine is responsible for applying the draw
and hooks. Full structural provenance is already reproducible by explicit
H8 decision-journal replay.

## Invasive implementation

- `H8CombatRuntimeV1` caches a **per-version** `H8DrawCursorV1`: the
  current pending `SemanticDeckEventV111`, immutable eligible physical
  card IDs, and the source automatic-end-turn frame when it is a supported
  automatic hand draw. H8 status and selection share the cursor.
- Every successful state commit increments `boundary_version` and clears
  the cached cursor. An illegal choice or stale/forked token leaves the
  state, journal and cached cursor unchanged. Forked runtimes never share
  a cached continuation.
- Automatic-hand-draw selections use an internal mechanics entry accepting
  the captured event, selected support index and captured end-turn frame.
  No public legacy SHA fingerprint is checked during this trusted H8 local
  execution path. Nonautomatic draws use the checked v111 fallback.
- Only the inner `RunState` actually advanced by mechanics must be forked;
  the enclosing end-turn record is used as an immutable copy-on-write
  source, avoiding its duplicate deep fork. Full nested hand/end-turn and
  final pilot recursive validations are **not** run at every draw.
- Physical draw eligibility, the correct controlled-source draw request,
  selected card identity, the committed draw decision and mechanical
  support transcript are still checked on the fast path. Every gameplay
  continuation, hook and effect is executed by the existing deterministic
  engine.
- `AuditReplay()` explicitly reconstructs the source, consumes *every*
  player/Nature journal choice and compares the final entire pilot state
  via canonical SHA. Audits are required for evaluation and periodic
  training sampling, never after each ordinary draw.

This patch changes the H8 execution strategy, **not** the old public
v111 contract. That old path exists temporarily as a mechanics test oracle
and can be retired once H8 has a native single-owner effect cursor.

## Correctness and performance gates

1. On the real Silent A10 Mawler encounter, compare the H8 fast draw with
   the fully checked resolver for exact output state equality.
2. Assert the profiler records the real draw mechanics but **no**
   `sub.semantic_draw_endturn_state_fork`,
   `sub.semantic_draw_combat_output_validate` or
   `sub.semantic_draw_endturn_output_validate` on the M2 path.
3. Reject foreign/stale tokens and ineligible physical card IDs before
   mechanics, provider advance or journal append. Forks independently
   advance and replay.
4. Compare **eight source-valid A10 scenarios** within the same test binary,
   alternating the prior H8 M1 path and H8 M2 path, using identical source
   seeds, player/Nature decision journals and final state fingerprints.
   Measure `GC.GetAllocatedBytesForCurrentThread` and wall-time on the
   single draw. This is a **small smoke microbenchmark**, not production
   throughput; do not impose flaky wall-time thresholds.
5. Run the existing H8 Python JSONL fork-and-replay integration and H7
   semantic hot-path regression. Do not repin sts2-ai without its full
   128-combat, 15-worker, 14.66M CUDA actor A/B/B/A acceptance.

## Incomplete architecture

The runtime still uses v111 as a deterministic mechanics adapter and
captures a projected `SemanticEndTurnV111` rather than having its own
compact `EffectCursor`. The underlying `RunState` uses immutable
record-like forks, and status still sometimes constructs derived v111
events. The H8 provider identifier is currently journal metadata; a
typed six-primitive Native/ReferenceNature/learned Nature/scripted provider
boundary remains M3. Batching and direct typed observations remain M4.
The next M2 implementation should migrate the **player action -> turn
resolution -> draw** mechanics to a single H8-owned effect cursor, retiring
both nested end-turn and hand frames along that path and measuring exact
allocation and replay consequences.

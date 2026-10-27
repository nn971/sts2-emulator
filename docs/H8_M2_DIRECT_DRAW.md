# H8 M2: Direct deterministic physical draw execution

Status: research PR #207, stacked on the version-owned cursor PR #206.
This is an **implemented, tested subset of the H8 M2 design**, not a
claim that all combat mechanics use a single native mutable state.

## Why the prior M2 bridge was still expensive

The H8 M2 cursor stopped full pilot input auditing and deep end-turn
forking, but the actual ordinary Nature card draw still invoked the v111
pilot -> end-turn -> hand-draw *resolver dispatchers*. Those methods
reconstructed and inspected many unrelated pending event kinds and
constructed intermediary continuation records. The original H8 M1
matched draw used 8.15 MB/draw; the first M2 bridge used ~4.74 MB/draw
on a small eight-combat CPU test.

## New execution path

`H8CombatRuntimeV1` owns a versioned session and the cached exact physical
support for one boundary. If an ordinary automatic-hand draw is pending,
`H8NativePhysicalDrawEngineV1.AdvanceAutomaticHandDraw`:

1. Validates this is the active clean automatic physical draw boundary;
   cost, pile insertion, random-pile, Stratagem, and draw-hook choices are
   excluded. All other supported choices retain the previous H8 fallback.
2. Selects the physical card instance within the provider's eligible
   support, forks **one** live `RunState`, and commits the provider-owned
   semantic deck choice through `SemanticDrawChoiceV111.Commit`. The
   physical pile and provider state remain synchronized atomically.
3. Runs the existing deterministic
   `PrototypeGameEngine.BeginSemanticCombatDraw` (including ordinary
   source CardDrawn hooks and consequent effects).
4. Validates the actual provider-owned recorded physical draw decision
   against the selected event and identity.
5. Updates the automatic hand progress and hands off to end-turn stage
   completion. The compatibility `SemanticEndTurnV111` projection is
   created **once on output**, and a tiny pilot adapter publishes the
   history/snapshot. Normal execution does not invoke nested pilot,
   end-turn, or hand-draw `ResolveCore`.
6. Appends the compact H8 player/Nature decision after the *complete*
   mechanics transition; all rejected decisions leave the owned runtime,
   provider state, version and journal unchanged.

The mechanics executor lives in an **H8-specific source file**, separate
from the legacy semantic pilot facade. Compatibility projection is
explicitly temporary, and legacy public fingerprints are not used to
authorize H8 execution. The long-term state owner remains
`H8CombatRuntime` with a native effect cursor; current `RunState.Fork()`
and projection publication are migration debt.

## Paired acceptance

The [initial CI run](https://github.com/nn971/sts2-emulator/actions/runs/40562685892)
compiled and passed 12 focused C# tests plus the live Python JSONL
source-valid A10 fork/replay integration. Eight source-matched A10 Mawler
card draws selected **identical physical card instances** and compared
exact entire v111 pilot states and explicit decision-journal replay.
The direct path also matched the fully checked v111 resolver.

| Isolated physical draw (8 A10 combats) | Prior M2 | Direct H8 | Change |
| --- | ---: | ---: | ---: |
| Managed allocations | 4.719 MB/draw | 0.888 MB/draw | **-81.2%** |
| Elapsed | 14.715 ms/draw | 2.440 ms/draw | **-83.4%** |

Metrics are from a **single shared CI CPU run** with alternating path
execution order, and exclude the model and the rest of a combat. Do not
extrapolate them to complete combat throughput. A subsequent commit moved
the direct executor to its own H8 source file and added stronger support
gating and a cross-boundary comparison; its CI regression is separately
tracked in PR #207.

## Required next steps

- Extend direct engine execution across initial intent, player action,
  nested effect draw, draw costs, insertion, recycle, random-target and
  CardDrawn hook choice boundaries. Each migration must retain identical
  source mechanics and matching typed stochastic decisions.
- Replace live copied v111 hand/end-turn projections with a native
  **tagged `H8EffectCursor`** plus a single authoritative mechanics owner
  and provider-private stochastic state. Keep conversion to versioned
  checkpoints and legacy audit structures outside the hot path.
- Implement the actual interchangeable six-primitive Native RNG,
  ReferenceNature, learned Nature and scripted provider interface;
  current journal `ProviderId` is only a record of the external chooser.
- Add batched native session advancement and typed observations, avoiding
  repeated JSONL transport. Validate full 128-combat, 15-worker,
  14.66M-parameter shared-CUDA actor A/B/B/A and reproducible player/Nature
  trajectories before repinning `sts2-ai`.
- Do not gate research training on native Inspector parity or unrelated
  legacy fixture failures. New mechanics and replay regressions do block
  adoption.

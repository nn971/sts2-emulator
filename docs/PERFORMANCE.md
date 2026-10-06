# Performance strategy

## What to measure

“Steps per second” alone is inadequate. Track at least:

1. **transition throughput** — `Step` calls per second by phase/workload;
2. **fork throughput/latency** — cost of creating an independent branch;
3. **canonical hash cost** — important for transposition-heavy search;
4. **bytes per state / bytes per branch**;
5. **allocation rate and GC time**;
6. **batch parallel scaling**;

## Workload classes

Benchmark separately:

- simple non-combat decisions;
- draw/card-play-heavy combat transitions;
- enemy turns with many triggers;
- reward generation;
- shop/event/rest transitions;
- whole-run random-policy simulation;
- fork-heavy tree search microbenchmarks.

A single synthetic hot loop can hide the real bottleneck.

## Initial targets

Targets are provisional and should be revised from real workloads.

| Metric | Initial aspiration |
|---|---:|
| Single-core lightweight transitions | >= 50k/s |
| Single-core mixed whole-run transitions | measure first; push toward 50k/s+ |
| Workstation aggregate independent transitions | >= 500k/s, then >= 1M/s |
| Fork | low enough that branch creation is not dominant |
| Hash | cheap enough for per-node use when needed |

The reference oracle can be much slower. Hundreds to thousands of decisions/s may already be sufficient for truth generation if capture can run unattended.

## Optimization order

1. Obtain representative traces/workloads.
2. Profile.
3. Remove accidental serialization/logging/allocation costs.
4. Improve state layout/forking.
5. Batch independent simulations.
6. Consider NativeAOT/interoperability optimizations.
7. Add approximate learned macro models only after exact semantics are stable.

## Performance regressions

Each benchmark result should include:

- commit;
- game/simulator version;
- hardware/OS/runtime;
- workload/trace ID;
- warmup policy;
- median and distribution, not a single lucky number.

Optimizations that alter canonical outputs are fidelity bugs unless explicitly part of a versioned semantic correction.

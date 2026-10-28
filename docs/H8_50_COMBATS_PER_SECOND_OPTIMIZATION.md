# H8: toward >50 complete combats/s (2026-10-28)

Branch `research/h8-trusted-player-hotpath-20261028` (stacked on
`research/h8-h7-reference-law-benchmark-20261028`, emulator PR #213).
This is a **measurement-first** speed project. Do not claim a speedup until
matched native-vs-legacy trajectories, sampled replay and real actor rates
pass on the same workload.

## Existing end-to-end baseline (measured)

- Full 80-round, 20,480-combat H8 A10 corpus: 15 workers, one
  14,659,972-parameter CUDA Transformer.
- 256-combat rounds: 36.35s rollout (7.04 *complete* combats/s) and
  22.34s PPO per round (4.36 complete combats/s counting updates).
- Roughly 7000–8000 Transformer inference requests and about 1400–1600
  CUDA batches per training round. Shared actor forward and tensor collation
  each take around 10s and 8s of measured cumulative service time.
- Earlier strict matched 128-fight benchmark: 22.639s native H8 vs
  35.039s legacy H7, **128/128 exact source trajectories** with a fixed
  random 14.66M actor. Model SHA:
  `aed2f662f03512b41120ef25c0288494764802dc511054baa0cdac26be44f71f`;
  source SHA:
  `f7e599e1154c99c59c52d85174daa7b329f456396d2f779bbd7d14304905c3b5`.

The **50/s** target corresponds to 256 complete combats in <=5.12s.
With the current shared CUDA actor, its *serial GPU forward time alone* is
about 10 seconds per 256 fights, creating a **<=25.6/s hard throughput
ceiling** at the current inference mix, before card encoding, IO and
mechanics. Emulator speedups are necessary but alone insufficient.
Investigate actor batching/collation and reduce policy inference cost
without changing model correctness once C# changes are measured.
Do not conflate scripted emulator-only throughput with full neural PPO.

## First isolated optimization: trustworthy H8 player transitions

1. Added `SemanticPilotCombatEngineV111.StepFromH8TrustedBoundary`:
   the H8 owner has already validated the `RuntimeId/Version` token,
   so it bypasses per-player `current.Validate()` and both pilot
   whole-state canonical SHA passes, and reuses the already-captured
   source-public `PrototypeAiFrame`.
   v111 checked API still takes fingerprints and validates.
2. Native end-turn checks reuse that same frame, instead of allocating
   and hashing another action observation.
3. Introduced `SemanticDeckStateV111.HasSameSerializedContentsAs` to
   compare every serialized deck property (mode, epoch, slots, pools,
   pending ticket and ordered transcript) with linear typed comparisons.
   H8 uses this for ordinary play/potion draw-pile nonmutation guards;
   public v111 APIs retain canonical SHA comparison.
4. H8 reference draw projection now uses
   `law.ConditioningFingerprint` instead of hashing the same Nature
   deck again in the same request. Event-scope and provider outcomes
   remain unchanged.

The source still uses legacy v111 mechanics continuations; the bigger
structural opportunities are native in-place action/hook cursors,
replacing deep `RunState.Fork()` and duplicated cross-zone validations,
and removing unnecessary nested state projections. These require
mechanics parity tests and profiling. No stored game decision journal
or interchangeable Nature interface was removed.

## Correctness and performance gates

Run `dotnet build`, then all `H8CombatRuntimeV1Tests`,
`SemanticDeckStateV111Tests`, JSONL H8 protocol, and
`tests/test_h8_benchmark_sessions.py`. The new C# test measures
ABBA checked-vs-trusted **real card plays** and asserts identical
pilot fingerprints, journal commits and exact replay. The deck unit
test compares the new typed equality result with the original canonical
SHA over forks, mutated pools, slots, pending and transcript fields.

Then run the *same full H7 policy actor* with the fixed seed/model and
full legacy source parity, on the workstation:

```fish
git -C ~/projects/sts2-emulator fetch origin
git -C ~/projects/sts2-emulator switch research/h8-trusted-player-hotpath-20261028
dotnet build ~/projects/sts2-emulator/Sts2Emulator.sln -c Release -m:1

cd ~/projects/sts2-ai
source .venv/bin/activate.fish
set emulator ~/projects/sts2-emulator
set dll $emulator/src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll
set rev (git -C $emulator rev-parse HEAD)
python -u tools/benchmark_h8_training_iteration.py \
  --emulator-dll $dll \
  --emulator-bindings $emulator/bindings/python \
  --emulator-revision $rev \
  --workers 15 --episodes 128 --max-boundaries 1200 \
  --replay-every 8 --actor-device cuda --learner-device cuda \
  --actor-max-batch 16 --actor-delay-ms 3 \
  --compare-legacy --output results/h8-trusted-player-128-a10.json
```

Run with AI research branch `research/h8-multiround-trainer-20261028`.
Compare to the previous **same 128-source model** report (copy it to the
shown path, if necessary):

```fish
python $emulator/tools/compare_h8_training_throughput.py \
  --before results/h8-reference-training-128-a10.json \
  --after results/h8-trusted-player-128-a10.json \
  --output results/h8-trusted-player-compare.json
```

The comparator requires the same model SHA, source digest, 128 complete
combat outcomes, audit schedule, and **128/128 legacy H7 trajectory
matching**. Compare matched ABBA repetitions before accepting a
small or noisy improvement.

## Next optimization slices (profile before execution)

- Classify H8 decision latency by player card, player end-turn, intent
  and physical draw, including allocations and time for source mechanics
  vs source observation/JSONL transport.
- Eliminate repeated full `RunState.Fork()` for the common immutable
  card-effect path via a native mechanics ownership cursor, without
  introducing mutable aliasing into forks or losing replay.
- Replace repeated semantic draw and intent ticket hashing with typed,
  cached per-boundary event identity without changing H7 reference
  Chance Law outcomes and source RNG keys.
- For **50 neural combats/s** specifically: jointly profile actor
  collation, CUDA forward batches, output serialization and PPO sample
  utilization (only 4096/roughly 7300 rollout player decisions optimized).
  The current actor forward time already exceeds the target wall clock.

**Status:** source optimizations committed and tests/CI wired;
no local dotnet SDK or live workstation CUDA in the remote execution
environment; benchmark gain remains **unmeasured**, target **not reached**.

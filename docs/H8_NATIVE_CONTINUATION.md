# H8: native hot-path continuation and reference Nature (PR #219)

This is an executable structural refactor stacked on H8 M4 native turn owner
(PR #217). It targets the observed expensive v111 history/allocation and
repeated Python/C# stochastic/policy RPC, not new batching experiments.

## Changes actually implemented

1. **Persistent H8 semantic continuation history.** H8 already writes a
   versioned compact journal of source-valid player and Nature choices.
   The v111 compatibility adapter also appended to an immutable-array
   `History` by copying the whole growing array at *every* transition.
   H8 now uses `H8HistoryTrailV1`: a persistent linked immutable history
   with per-kind ordinal counters. Normal appends/ordinal lookups are
   constant in history length; complete array materialization occurs on
   explicit v111 audit, snapshot, and source fingerprint access. Non-H8
   v111 sessions retain original behavior. Explicit compatibility forks
   retain independent full history and reconnect a new H8 owner afterward.

2. **In-emulator reference Nature.** Opt-in `local_reference_seed` on
   `h8_begin_seeded` or `h8_step` samples consecutive physical
   card-draw and enemy-intent boundaries in the C# runtime. A separate
   `h8_continue_reference` command works from an already exposed
   Nature boundary. The local provider uses event-keyed deterministic
   SHA256/rejection sampling; physical eligible draws are uniform and
   supported source-verified intents use the 53-bit Native NextDouble
   integer grid. It records `h8-local-reference-v1` in the existing
   exact-choice journal, and `h8_replay_audit` replays those same
   decisions using explicit identities. The Python H7 MT19937 event RNG
   is a *different ticket generator*; don't demand identical outcomes
   across reference providers. External scripted/learned Nature keeps
   explicit `h8_reference_law` / `h8_resolve_*` boundaries.

3. **Inline typed public player frame.** `include_player_frame=true`
   on H8 begin/status/step/resolve adds `result.playerFrame.observation`
   and `result.playerFrame.actions` only at a real player boundary.
   The public observation is serialized with the same snake_case
   contract as `h8_observe`, but directly into the status JSON.
   H8 already cached this public frame to construct the status menu;
   the extra `h8_observe` JSONL request and nested JSON string decode
   can therefore disappear. External Nature and old observation API
   remain available unchanged.

## Benchmark and training usage (fish)

Use the *matching experimental branches*, not the M3 DLL. Do not reset
other active worktrees; use the existing project directories:

```fish
git -C ~/projects/sts2-emulator fetch origin
git -C ~/projects/sts2-emulator switch research/h8-persistent-history-owner-20261028
dotnet build ~/projects/sts2-emulator/Sts2Emulator.sln -c Release -m:1

git -C ~/projects/sts2-ai fetch origin
git -C ~/projects/sts2-ai switch research/h8-native-reference-continuation-20261028
cd ~/projects/sts2-ai
source .venv/bin/activate.fish

set emulator ~/projects/sts2-emulator
set dll $emulator/src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll
set revision (git -C $emulator rev-parse HEAD)

# Smoke: existing real H8 replay, including in-process Nature and typed
# observation parity with the original source-public h8_observe.
env STS2_EMULATOR_DLL=$dll python -m unittest discover \
    -s $emulator/tests -p test_h8_runtime_jsonl.py -v

# Actual 15-worker H8 packed Transformer + local reference Nature:
python -u tools/benchmark_h8_h7_policy_pilot.py \
    --emulator-dll $dll \
    --emulator-bindings $emulator/bindings/python \
    --emulator-revision $revision \
    --output results/h8-local-reference-1024.json \
    --order native --episodes 1024 --workers 15 \
    --warmup-per-worker 2 --max-boundaries 1200 \
    --replay-every 16 --sampled-mechanics-audit \
    --measure-rpc --actor-device cuda --actor-collation packed \
    --local-reference-nature --inline-player-frame

# End-to-end PPO iteration (same semantics and journal, one frozen actor):
python -u tools/benchmark_h8_training_iteration.py \
    --emulator-dll $dll \
    --emulator-bindings $emulator/bindings/python \
    --emulator-revision $revision \
    --output results/h8-local-reference-ppo-128.json \
    --episodes 128 --workers 15 \
    --actor-device cuda --learner-device cuda \
    --local-reference-nature --sampled-mechanics-audit
```

Compare RPC counts and throughput with the preceding M3/packed baseline.
Source seeds and model architecture may be fixed, but this local reference
provider samples a *different trajectory* from Python MT19937. Raw
completed-combats/s comparisons therefore include possible workload
differences. Measure the direction and major changes; avoid claiming an
exact causal speedup until calibrated on comparable work.

## Remaining major work

This refactor removes duplicate **history copying** in hot H8 transitions;
it does not pretend to have replaced every `RunState.Fork()` or the
remaining v111 end-turn/hand-draw projection. Those are still dominant
mechanics allocation targets, followed by direct state ownership.
Keep local Nature sampling separate from external learned/scripted
provider decisions; do not force source RNG parity or Inspector fidelity
checks into the training release gate. Use the existing smoke/replay
validation, not a new test framework.

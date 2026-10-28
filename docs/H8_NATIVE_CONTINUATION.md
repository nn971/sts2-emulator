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

## Measured 2026-10-28 results and next structural target

The uploaded pinned reports used emulator `c2751560`, 15 workers, the
14,659,972-parameter Transformer, packed CUDA actor, in-emulator local
reference Nature, and inline player frames.

- 1,024 terminal combats: 71.0165 s full run (14.4192 combats/s),
  including warmup. Steady-state: 994 combats / 58.7369 s =
  **16.9229 combats/s**. Previous packed baseline: approximately
  17.04 combats/s; no improvement established. All 1,024 terminated
  and 64 sampled terminal replays verified.
- 128-source PPO iteration: 19.3296 s rollout (6.622 combats/s),
  17.3568 s PPO update, 36.6863 s total; all 128 terminated and
  16 replays verified. Rollout and PPO use different workloads.
- Steady-state 994-combat run: 27,051 `h8_step` RPCs; cumulative
  cross-worker step RPC time 280.15 s, begin RPC time 31.12 s.
  Shared actor reports 26.21 s forward and 7.51 s collation across
  the full cohort. These times overlap with worker execution, so
  do not add them to rollout wall-clock. At the same actor work per
  fight, approximately 33.72 s of serial actor service for 1,024
  combats already places an optimistic ceiling near 30 combats/s:
  reaching 50 requires actor improvements alongside mechanics refactoring.

Subsequent hot-path changes on PR #219:
- Defer duplicate automatic-turn legal-action enumeration until the
  cached H8 player frame is built, preserving the empty-menu check.
- Address the committed semantic deck decision by its source ordinal
  on H8's trusted native draw, avoiding a full transcript clone and
  linear search in `ValidateRecordedChoice`. Other checked v111
  callers retain the old validation route.

These commits postdate the measured reports; no newer throughput gain
has been established. Reuse existing H8 tests/replays, then rerun the
one 1,024-combat workload after structural changes.

**Deeper remaining deficit:** `SemanticDeckStateV111.Transcript` is
a defensive-copy array property. The normal deck resolver also copies
and appends that growing array, and repeated checked validation
traverses it. Even with the new O(1) H8 recorded-choice lookup, other
parts of the native draw path still perform work proportional to
fight length. A persistent H8-owned deck transcript and H8-specific
trusted commit avoiding redundant full-state validation should accompany
the direct `RunState` owner/cursor migration. Explicit audit and fork
must still materialize independent complete snapshots.

## H8 persistent deck transcript (post-benchmark optimization)

The prior H8-native draw path committed a Nature choice via the checked
semantic-deck resolver. For every draw it copied the entire
`SemanticDeckStateV111.Transcript` and appended one entry, recomputed
deck/event SHA digests, validated the full historical transcript, then
cloned and searched the transcript again to verify the committed choice.
This was work growing with the total number of draws.

The current experimental branch adds `H8DeckTranscriptTrailV1` and a
version-guarded trusted commit path:

- Append one immutable choice node in constant time, retaining shared
  prefixes across subsequent deck updates and `RunState.Fork()` calls.
- Read transcript count and the latest choice directly, without
  materializing a historical array or scanning it.
- Validate the newly committed choice against the already validated
  immutable prefix. Ordinary checked v111 calls still validate all
  array-backed history.
- Reuse the exact pending source event ID in the trusted deck resolver;
  skip duplicate event serialization/hash, SHA freshness checks and
  repeated recorded-choice event hashes after H8's token/support guard.
- Preserve the public `Transcript` array for canonical serialization,
  source-event identity, legacy snapshots and explicit audits. History
  nodes and choice entries are immutable, so forks can share history
  safely while mutable run mechanics remain independently forked.

**Important:** The canonical source-style event identifier still hashes
its historical transcript when it is first produced for an H8 Nature
boundary. The implementation leaves that event identity unchanged to
preserve existing replay/history contracts. The two full mechanics
`RunState.Fork()` calls in the physical draw/automatic-turn path remain
in place because they isolate mutable RNG and suspended checkpoints.
Eliminating them requires an explicit native mutation/checkpoint owner.

This code is newer than the last measured 16.92 combats/s steady state.
No improvement claim is established until it passes the existing
`h8-runtime-smoke`, multi-boundary replay/parity and 15-worker
1,024-combat workload.

## H8 mechanics ownership: direct functional transitions

H8's player and automatic-draw hot paths now advance a session-owned
immutable `RunState` checkpoint directly:

- `H8NativePhysicalDrawEngineV1` commits a physical draw without
  making a separate whole-run fork of the input checkpoint.
- `H8NativeTurnContinuationV1` resumes the functional automatic
  pipeline from the committed draw checkpoint without an additional
  whole-run fork. The compatibility `HandDraw.Run` continues to point
  to the pre-resume immutable state.
- `PrototypeGameEngine.StepFromH8TrustedBoundary` shares ordinary
  deterministic mechanics with public `Step`, but does not create its
  defensive per-action `RunState.Fork()`. This trusted entry is only
  called after the H8 runtime checks its version token and player
  action's source-public menu.
- `PrototypeAiEnvironment.StepFromH8TrustedBoundary` reuses the exact
  cached `PrototypeAiFrame` action and payload, avoiding a second
  full legal-action scan/hashing cycle for card, potion and end-turn
  transitions. Generic public `Step` still forks.

This is an ownership optimization, not a stochastic-rule change: each
transition produces successor records and does not mutate the original
run snapshot. Public snapshot/fork/replay APIs keep their isolation
contract. The H8 replay workflow and comparison with the checked draw
route are the existing implementation gates.

No GPU performance claim is inferred from this refactor. Compare
steady-state 1,024-combat results on 15 workers against the previously
measured 16.92 combats/s after rebuilding the emulator DLL.

# H7 pilot-validation memoization: controlled A/B experiment

## Why this candidate

The completed 128-combat, 15-worker H7 A10 profile after semantic boundary
inspection optimization (emulator `5aeaab37b87efb5f43b1d69955c62b3d93ca8e1c`)
reports:

- `sub.semantic_draw_pilot_input_validate`: 77.36 GB allocation across
  4,054 draws (**19.08 MB/draw**);
- `sub.semantic_draw_pilot_normalize`: 66.32 GB allocation across
  3,994 calls (**16.36 MB/draw**, including `current.Validate()`);
- `sub.semantic_resolve_draw_card_transition`: 259.24 GB, or
  **63.95 MB/draw**, unchanged by boundary-inspection fast path.

The pilot validator has dozens of references to computed
`PendingEndTurn`, `TargetPending`, `GeneratedCardPending`,
`CostPending`, `PoolShufflePending`, etc. Some getters reconstruct
semantic events with full-state canonical hashes, and `PendingEndTurn`
projects the same mechanics/stochastic continuation repeatedly.
This research branch changes only repeated *reads within a validation
invocation*: each derived property is lazily evaluated once, at first
use. Existing validation statements, invariants, checks, exception
messages, source state, Nature law and transcript/replay code stay
unchanged.

For differential correctness checks, the original unmodified
`Validate` body is retained as `ValidateReference`. When
`STS2_H7_COMPARE_PILOT_VALIDATION=1`, every optimized successful
validation is followed by the unchanged reference validator.
**Do not enable that variable for throughput measurements.**
This flag validates acceptance by both validators; existing invalid
snapshot and stale-source regression tests cover rejection behavior.

## Independent branches

- **A (control)**: `fa06caac8b8d6b8c4208ffda48511c303bf6ce82`,
  the PR #196 boundary-inspection branch after the documented matched
  draw optimization, with all native stage profiler counters.
- **B (treatment)**: `research/h7-pilot-validation-memo-20261027`
  (record exact fetched SHA before execution).
- Keep sts2-ai PR #118's optimized-emulator+1/8 terminal replay pin
  unchanged until the new optimization passes benchmark and replay gates.

## Mandatory quick correctness tests

On PR, focused emulator `semantic-hotpath` CI runs real draw/intent
resolution, stale fingerprints, exact snapshot replay, and profiler
regressions. The `h7-draw-stage-diagnostics` workflow runs one
16-cell A10 reference-law combat source fixture per cell; the
environment sets **both**
`STS2_H7_COMPARE_PILOT_VALIDATION=1` and
`STS2_H7_COMPARE_BOUNDARY_INSPECTION=1`. These compare the optimized
validation and boundary inspection to their original implementations
on all successfully reached semantic responses.

This is a research experiment; no performance improvement is claimed
until matched A/B measurements exist. Generic or unrelated emulator
CI failures are reported separately, never treated as a gate on
isolated combat performance research.

## Paired full-replay 128-combat experiment (fish)

Requires `dotnet` 9, the existing sts2-ai virtual environment,
and your RTX 5070 with enough GPU RAM. The script builds exact
source exports under `/tmp` and never creates new worktrees or
switches your active `~/projects` branches.

```fish
cd ~/projects/sts2-ai
source .venv/bin/activate.fish

set emulator ~/projects/sts2-emulator
set before fa06caac8b8d6b8c4208ffda48511c303bf6ce82

git -C $emulator fetch origin research/h7-pilot-validation-memo-20261027
or exit 1
set after (git -C $emulator rev-parse FETCH_HEAD)
set root (mktemp -d /tmp/h7-pilot-validate-ab.XXXXXX)
echo "Output: $root"
echo "A=$before B=$after"

for mode in before after
    set rev $before
    if test $mode = after
        set rev $after
    end
    set src $root/$mode/source
    mkdir -p $src $root/$mode/profiles
    git -C $emulator archive $rev | tar -xf - -C $src
    or break
    dotnet restore $src/Sts2Emulator.sln
    or break
    dotnet build $src/Sts2Emulator.sln -c Release --no-restore -m:1
    or break
    set -gx STS2_SEMANTIC_PROFILE_DIR $root/$mode/profiles
    set -e STS2_H7_COMPARE_PILOT_VALIDATION
    set -e STS2_H7_COMPARE_BOUNDARY_INSPECTION
    python -u tools/benchmark_h7_rollouts.py \
        --emulator-dll $src/src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll \
        --emulator-bindings $src/bindings/python \
        --emulator-revision $rev \
        --output $root/$mode/h7.json \
        --episodes 128 --workers 15 \
        --actor-backend shared --actor-device cuda \
        --actor-max-batch 16 --actor-delay-ms 3 \
        --study-seed 73127 --actor-seed 31337 \
        --reference-seed 23387
    or break
    python $src/tools/summarize_semantic_perf.py \
        $root/$mode/profiles --output $root/$mode/native.json
    or break
    python $src/tools/report_draw_stage_perf.py \
        $root/$mode/native.json > $root/$mode/draw-stages.txt
    or break
end
set -e STS2_SEMANTIC_PROFILE_DIR
python $root/after/source/tools/compare_h7_boundary_perf.py \
    $root/before $root/after
echo $root
```

For source matching, the 128-combat study should reproduce all of:
`source_digest=f7e599e1154c99c59c52d85174daa7b329f456396d2f779bbd7d14304905c3b5`;
actor SHA `189d4d6a06912242af9113b72c3c28ed153fa967038adc24009c5baf0c2307e7`;
40 wins, 3,418 player decisions, 7,659 boundaries, 4,054 card draws,
and 128 full replay verifications.

**Primary metric:** MB per draw and cumulative cost for
`sub.semantic_draw_pilot_input_validate`,
`sub.semantic_draw_pilot_normalize`, and
`sub.semantic_resolve_draw_card_transition`. The normalizer includes
other work beyond validation, so do not subtract nested profiling
metrics as if they were disjoint. Also compare
`sub.semantic_draw_hash_RunState`,
`sub.semantic_draw_hash_SemanticPilotCombatV111`,
`op.semantic_resolve_draw_card`, terminal replay load and the
`op.semantic_step` costs.

After confirming identical outcomes and reduced allocations, repeat
with **no** `STS2_SEMANTIC_PROFILE_DIR` in a fresh A/B/B/A order to
estimate unbiased wall throughput. The profiling instrumentation
itself costs time. The final gate is a real H7 one-round PPO A10
training comparison with `--train-terminal-replay-every 8`, identical
model and seeds, and full evaluation replay. Avoid changing the
combined training submodule pin without that gate.

Send the four `h7.json` and `native.json` files from A and B;
the comparison script fails closed on mismatched source digests,
model signatures and draw counts.

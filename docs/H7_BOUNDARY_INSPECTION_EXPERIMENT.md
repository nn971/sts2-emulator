# H7 passive boundary inspection fast-path experiment

## Control and treatment

- **Control**: `ae370e70f5a5530d87df5749a267f24d5094b64f` (diagnostic PR #195, before any passive inspection change).
- **Treatment**: `research/h7-boundary-inspection-fastpath-20261027` (this isolated branch, pinned to the concrete SHA printed by `git rev-parse FETCH_HEAD`).
- **Frozen combined H7 baseline**: sts2-ai PR #118 still pins emulator `bfbcc394a2d6f09927d8935b52a3518e1613d934`. Do not update that gitlink until the new fast path is verified.

The only intended behavioral change is fewer expensive passive lookups while
constructing `chanceSites` and `controllerMode` in semantic JSONL
responses. For an absent event, the optimized code checks the underlying
`Combat.PendingChoice` discriminator first. For a present event, it
still calls the existing original event getter and validator. Three
frequently reused event records (pool shuffles, shortlist and costs)
are constructed at most once per JSONL response.

`SemanticChanceSitesReference` and `SemanticControllerModeReference`
retain the original implementations **exactly** for differential
testing. Set `STS2_H7_COMPARE_BOUNDARY_INSPECTION=1` to compare every
optimized response with the old implementation and fail on any discrepancy.
**Disable that variable for throughput profiling**, because running
both implementations destroys the intended saving.

## Differential correctness CI

The `h7-draw-stage-diagnostics` workflow:
- builds this branch;
- runs a real, reference-law, 16-cell A10 combat study with
  `STS2_H7_COMPARE_BOUNDARY_INSPECTION=1` across *all* semantic
  JSONL response boundaries;
- saves per-process semantic profiler counters and the stage report;
- requires the existing stable-ID/stale-state/replay regression tests
  from the focused `semantic-hotpath` workflow.

Also run tests that trigger extended source fixtures and nested
continuations (pool shuffle, generated card, random draw insertion,
cost choice, enemy action). The default 16-cell H7 study exercises the
ordinary Silent combat curriculum and cannot alone establish coverage
of every possible fixture mechanic.

## Reproducible paired fish experiment, never disturbing active branches

Requires dotnet 9, installed `sts2-ai` development dependencies and
the H7 shared CUDA actor. The `~/projects` layout is unchanged.
The benchmark constructs source exports in `/tmp`, not new worktrees.

```fish
cd ~/projects/sts2-ai
source .venv/bin/activate.fish

set emulator ~/projects/sts2-emulator
set before ae370e70f5a5530d87df5749a267f24d5094b64f
git -C $emulator fetch origin research/h7-boundary-inspection-fastpath-20261027
set after (git -C $emulator rev-parse FETCH_HEAD)
set root (mktemp -d /tmp/h7-boundary-inspection.XXXXXX)
echo "Benchmark output: $root"

for mode in before after
    set rev $before
    if test $mode = after
        set rev $after
    end
    mkdir -p $root/$mode/source $root/$mode/profiles
    git -C $emulator archive $rev | tar -xf - -C $root/$mode/source
    or break
    dotnet restore $root/$mode/source/Sts2Emulator.sln
    or break
    dotnet build $root/$mode/source/Sts2Emulator.sln -c Release --no-restore -m:1
    or break

    set dll $root/$mode/source/src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll
    set bindings $root/$mode/source/bindings/python
    set -gx STS2_SEMANTIC_PROFILE_DIR $root/$mode/profiles
    set -e STS2_H7_COMPARE_BOUNDARY_INSPECTION

    python -u tools/benchmark_h7_rollouts.py \
        --emulator-dll $dll --emulator-bindings $bindings \
        --emulator-revision $rev \
        --output $root/$mode/h7.json \
        --episodes 128 --workers 15 \
        --actor-backend shared --actor-device cuda \
        --actor-max-batch 16 --actor-delay-ms 3 \
        --study-seed 73127 --actor-seed 31337 --reference-seed 23387
    or break

    python $root/$mode/source/tools/summarize_semantic_perf.py \
        $root/$mode/profiles --output $root/$mode/native.json
    or break
    python $root/$mode/source/tools/report_draw_stage_perf.py \
        $root/$mode/native.json > $root/$mode/draw-stages.txt
    or break
end
set -e STS2_SEMANTIC_PROFILE_DIR
python $root/after/source/tools/compare_h7_boundary_perf.py \
    $root/before $root/after
echo $root
```

The expected source digest is
`f7e599e1154c99c59c52d85174daa7b329f456396d2f779bbd7d14304905c3b5`
and the expected random actor SHA is
`189d4d6a06912242af9113b72c3c28ed153fa967038adc24009c5baf0c2307e7`.
On the 128-combat reference dataset, 40 victories, 3,418 player
decisions, 7,659 total decision boundaries and 4,054 card draws were
observed before this experiment. The control and treatment should
reproduce those totals.

**Primary allocation metrics**:
`sub.semantic_draw_result_chance_sites` and
`sub.semantic_draw_result_controller_mode` per draw;
`sub.semantic_draw_hash_RunState`, number of full-run hashes,
`op.semantic_resolve_draw_card` allocations;
`sub.semantic_result_fingerprint` is unchanged and outside those
subprobes. `sub.*` counters are nested, never additive.

**Primary throughput**: `h7.json.elapsed_seconds` and
`player_decisions_per_second`. Because fine-grained profiler
measurements incur overhead, follow a positive allocation result with
a repeated A/B 128-combat test with `STS2_SEMANTIC_PROFILE_DIR` unset,
alternating execution order. The independent H7 training configuration
still needs direct measurement of the combination with 1/8 terminal
replay; this first experiment isolates *response inspection*.

Upload `before/h7.json`, `before/native.json`,
`after/h7.json`, `after/native.json` for analysis.

# H7 read-only RunState SHA-256 reuse experiment

## Motivation

Measured 128-combat A10 H7 at boundary-inspection-optimized emulator
`fa06caac8b8d6b8c4208ffda48511c303bf6ce82`:

- 458,045 full `RunState` SHA-256 computations across 4,054 draws
  (**113 actual hashes per draw**).
- Cumulative nested RunState hashing: **174.06 GB managed allocation** and
  **341.06 worker-seconds**. Nested counts overlap with parent operations.
- Overall draw requests: 312.85 GB, nested pilot validation 77.17 GB,
  pilot normalization 66.33 GB, internal transitions ~258.93 GB.
- Actual draw engine mechanics: 2.62 GB and 10.60 worker-seconds.

The prior experiment (PR #197) memoized derived pending-event *getters*
inside pilot validation and eliminated only 196 pilot SHA-256 calls
(<1%); it did not affect the 458,045 full-RunState hashes. We therefore
test direct **per-validation memoization of full RunState fingerprints**.

## Exact scope and invariants

`CanonicalJson.BeginReadOnlyRunStateHashReuse()` enables a reference-identity
dictionary for `CanonicalJson.Sha256<RunState>` **only** during the
synchronous body of `SemanticPilotCombatV111.Validate()`. It is:

- thread-local, stack-scoped and restored on exception;
- keyed by reference identity, never record structural equality;
- restricted to calls with static generic argument type `RunState`;
- shared by nested read-only validators only while the outer validation
  is still running;
- never persisted in snapshots, replay, stochastic provider state,
  pending decisions, or between card draw resolutions.

The `RunState` graph contains mutable arrays. **It is unsafe to cache
fingerprints across state transitions, requests, or mutable mechanics**.
This approach deliberately does none of those things. Every first access
still performs the unmodified canonical JSON SHA-256 computation.

With `STS2_H7_VERIFY_RUN_HASH_REUSE=1`, every hash-cache hit is checked
against an independent fresh canonical SHA-256. It throws on mismatch.
The 16-cell real A10 CI study uses this diagnostic option. It **must be
unset for performance profiling**. The opt-in stage-profiler reports
`sub.semantic_draw_hash_RunState` for actual full SHA computations and
`sub.semantic_draw_hash_RunState_cache_hit` for reused digests. The
rest of H7 and stale-boundary validation are unchanged.

## Verification gates

- Focused `semantic-rollout-hotpath` CI: exact fingerprint,
  reference-identity cache, nested scopes, exception cleanup, JSONL
  stable draw IDs, stale-fingerprint rejection and snapshot replay.
- `h7-draw-stage-diagnostics`: 16 genuine A10 combat scenario cells
  with 2 workers and each cached hash independently verified.
  The existing old-versus-new `chanceSites`/`controllerMode`
  differential check remains enabled too.
- `h7-runstate-hash-ab`: two independently compiled emulator
  revisions; matched 16-cell source/actor seeds; profiler capture
  in both runs; compare full hash counts, allocations and outcomes.

CI is smoke/correctness evidence. A full 128-combat A10 GPU benchmark
using 15 workers remains necessary before claiming a production win.

## Full 128-combat A/B (fish, no active worktree changes)

Requires existing sts2-ai venv and dotnet 9. All ephemeral builds are
exported to `/tmp`; we do not touch your active development branch.
Control is PR #196 at
`fa06caac8b8d6b8c4208ffda48511c303bf6ce82`.

```fish
cd ~/projects/sts2-ai
source .venv/bin/activate.fish

set emulator ~/projects/sts2-emulator
set before fa06caac8b8d6b8c4208ffda48511c303bf6ce82
git -C $emulator fetch origin perf/h7-readonly-runstate-hash-memo-20261027
or exit 1
set after (git -C $emulator rev-parse FETCH_HEAD)
set root (mktemp -d /tmp/h7-runhash-ab.XXXXXX)
echo "Output: $root | baseline=$before | optimized=$after"

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
    set -e STS2_H7_VERIFY_RUN_HASH_REUSE
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
end
set -e STS2_SEMANTIC_PROFILE_DIR
python $root/after/source/tools/compare_h7_boundary_perf.py \
    $root/before $root/after
echo $root
```

Require equal actor SHA, reference source digest, 40 victories,
3,418 player decisions, 7,659 boundaries and 4,054 draws; the
benchmark's `compare_h7_boundary_perf.py` rejects unequal semantic
operation counts but intentionally allows *hash counts* to decline.

**Success criteria:** lowered actual
`sub.semantic_draw_hash_RunState.count`, lower
`sub.semantic_draw_hash_RunState.allocatedBytes`, lower
`sub.semantic_draw_pilot_input_validate.allocatedBytes`, lower
`sub.semantic_draw_pilot_normalize.allocatedBytes`, and no change
to actual reference-law combat/replay results.

After observing significant allocation savings, repeat 128 combats
with the stage profiler unset in an A/B/B/A order to measure true
rollout wall-time gain and GC effects. Before changing sts2-ai PR #118's
pinned combined training setup, validate one H7 training run with 1/8
terminal replay and 100% heldout terminal checks.

## Expected outcome / stopping rule

This is a hypothesis, **not a measured speedup**. It could save much
of the repeated RunState hashing during each pilot validation, but
cannot eliminate hashes of distinct intermediate RunState instances.
If the 16-cell profile shows few cache hits or negligible allocation
improvement, record the negative result and move to a different
optimization rather than expanding unsafe cache lifetime.

## Completed paired 16-cell A10 measurement — 2026-10-27

The automated **two-emulator-build A/B** run
[GitHub Actions #40525333801](https://github.com/nn971/sts2-emulator/actions/runs/40525333801)
passed and uploaded all four H7/native reports plus the strict
comparison. Control SHA was `fa06caac8b8d`; treatment was GitHub's
PR-merge checkout `acd8abb5aad9`, combining this branch with the
same control base. Same 16 A10 scenario cells, reference-law seeds,
actor initialization and 435 player decisions, 515 draw resolutions
in each run.

| Metric | Before | After | Difference |
| --- | ---: | ---: | ---: |
| Cohort wall | 66.804 s | 51.540 s | -22.8% |
| Player decisions/s | 6.512 | 8.440 | +29.6% |
| Full draw operation allocation | 77.027 MB/draw | 53.805 MB/draw | -30.1% |
| Core draw transition allocation | 63.765 MB/draw | 40.544 MB/draw | -36.4% |
| Core draw transition time | 83.447 ms/draw | 55.269 ms/draw | -33.8% |
| Full RunState hash calls | 57,462 | 26,100 | -54.6% |
| Full RunState hash allocation | 21.911 GB | 9.955 GB | -54.6% |
| Full RunState hash time | 56.130 ms/draw | 27.084 ms/draw | -51.7% |

**Interpretation:** the optimization eliminates repeated canonical
RunState serializations within read-only validation. The allocation
decrease confirms a large real benefit; the 22.8% wall-time reduction
in one CI run remains sensitive to shared-host scheduling.

The independent 16-cell real-combat workflow with
`STS2_H7_VERIFY_RUN_HASH_REUSE=1` also passed: **every cached hash
hit was independently recomputed and compared**. The focused real
semantic stable-ID, stale-fingerprint, restore/replay, nested scope
and exception-cleanup tests passed.

### What remains before adoption

1. Run the **128-combat, 15-worker GPU A/B** documented above with
   the real shared transformer (same model SHA, source digest and
   40 wins/3418 decisions/4054 draws).
2. Repeat without opt-in profiling, in alternating A/B/B/A order, to
   establish production wall-time improvement.
3. Check `--train-terminal-replay-every 8` plus full evaluation
   in the H7 combined training branch. Do not move its existing
   pinned emulator submodule before this gate.

The large measured win is from hashing fewer times, with no cache
reuse beyond validation scope. The actual target across full H7
remains approximately 458,045 RunState hashes/128 combats prior to
this change; scale-up savings have not yet been measured.

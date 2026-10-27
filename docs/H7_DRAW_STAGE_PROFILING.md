# Diagnostic research: where H7 semantic card draws allocate memory

**Base:** optimized emulator PR #194 commit
`bfbcc394a2d6f09927d8935b52a3518e1613d934`.
**Branch:** `research/h7-draw-stage-profile-20261027`.
**Do not replace the reproducible combined H7 training pin** in AI PR #118
until this instrumented branch has produced new evidence. Profiling is
opt-in and does not change the chance law, draw-choice semantics or
snapshot-replay implementation.

## Quantitative reason for this investigation

The user's *matched* 128-combat, 15-worker, 14.66M-parameter H7 CUDA
benchmarks used the identical source digest
`f7e599e1154c99c59c52d85174daa7b329f456396d2f779bbd7d14304905c3b5`
and model SHA
`189d4d6a06912242af9113b72c3c28ed153fa967038adc24009c5baf0c2307e7`.

| Metric | Before optimization | After optimization |
| --- | ---: | ---: |
| Cohort wall | 161.22 s | 151.72 s |
| 4,054 draw operations, cumulative worker seconds | 1,016.98 | 934.08 |
| Core draw transition, cumulative worker seconds | 657.18 | 523.19 |
| Core transition allocated memory | 352.81 GB | 258.99 GB |
| Full draw operation allocated memory | 540.29 GB | 446.48 GB |
| 128 full snapshot replays, cumulative worker seconds | 752.53 | 660.94 |
| 128 full snapshot replays allocated memory | 436.82 GB | 340.79 GB |

A core transition now allocates **63.9 decimal MB per draw**; the
complete JSONL draw request still allocates **110.1 MB per draw**.
The unexplained gap between those two measurements is about **46.2MB**
per draw; no attribution has yet been measured for that gap.

## Source-code mechanism: concrete causes to test

A `semantic_resolve_draw_card` operation is layered:

1. `SemanticPilotCombatEngineV111.ResolveDrawCard` calls
   `current.Validate()`, full canonical `current.Fingerprint`
   (SHA-256 of its JSON representation), selects a physical card ID,
   and enters the internal Nature resolver.
2. `SemanticEndTurnEngineV111.Resolve` calls
   `Validate(source)`, hashes `source.Fingerprint`, and
   **deep-forks** the end-turn state:
   `Origin.Fork()`, `Run.Fork()`, `HandDraw.Fork()`, plus player
   source, intent and stochastic state. These nested source checkpoints
   contain copies of `RunState` with full `World` and `Player`.
3. `SemanticCombatDrawEngineV111.Resolve` calls another
   `Validate(state)`, hashes `state.Fingerprint`,
   does `state.Run.Fork()`, commits the chosen semantic deck index,
   resumes actual `PrototypeGameEngine.BeginSemanticCombatDraw`
   mechanics and hooks, checks the recorded physical decision,
   and validates the resulting draw state.
4. The end-turn resolver validates its resulting end-turn state;
   the pilot resolver validates the recorded semantic choice,
   publishes a history continuation, normalizes costs,
   and validates the resulting pilot state.
5. The CLI stores a forked state, calculates another full semantic
   fingerprint, builds a Nature-visible pending-event description,
   reconstructs the list of chance sites, and serializes JSON to stdout.

`CanonicalJson.Sha256` itself uses
`JsonSerializer.SerializeToElement` → recursively sorted canonical
JSON into a `MemoryStream` → UTF-8 string → UTF-8 byte array →
SHA-256. The allocation cost therefore grows with the full
serialized state, and **not just the handful of changed card indices**.

The above are **source-level observations**, not yet a measured ranking
of individual stages. In particular, the code includes nested
validations needed to enforce the replay contract; simply removing all
of them is unsafe.

## Diagnostic counters added on this branch

`semantic_perf_reset` activates a **current-thread-only** stage sink
during each JSONL `semantic_resolve_draw_card`. It records elapsed
stopwatch time and managed allocation deltas for:

- `sub.semantic_draw_pilot_input_validate`,
  `sub.semantic_draw_pilot_input_fingerprint`,
  `sub.semantic_draw_pilot_select`,
  `sub.semantic_draw_pilot_resolve`,
  `sub.semantic_draw_pilot_validate_choice`,
  `sub.semantic_draw_endturn_resolve`,
  `sub.semantic_draw_pilot_recorded_choice`,
  `sub.semantic_draw_pilot_publish`,
  `sub.semantic_draw_pilot_normalize`;
- `sub.semantic_draw_endturn_input_validate`,
  `sub.semantic_draw_endturn_input_fingerprint`,
  `sub.semantic_draw_endturn_state_fork`,
  `sub.semantic_draw_endturn_combat_draw`,
  `sub.semantic_draw_endturn_continue_hand`,
  `sub.semantic_draw_endturn_output_validate`;
- `sub.semantic_draw_combat_input_validate`,
  `sub.semantic_draw_combat_input_fingerprint`,
  `sub.semantic_draw_combat_run_fork`,
  `sub.semantic_draw_combat_select`,
  `sub.semantic_draw_combat_commit`,
  `sub.semantic_draw_combat_engine_mechanics`,
  `sub.semantic_draw_combat_recorded_choice`,
  `sub.semantic_draw_combat_publish`,
  `sub.semantic_draw_combat_output_validate`;
- `sub.semantic_draw_hash_<type>`, one per static argument type to
  `CanonicalJson.Sha256` while resolving a draw; includes canonical
  serialization, UTF-8 copies and the hash computation;
- `sub.semantic_draw_result_chance_sites` and
  `sub.semantic_draw_result_controller_mode`, scoped specifically to
  draw response materialization **outside** the inner native transition.
  Other operations' response costs use separate generic counter names.

The outer counters from PR #194 are unchanged. `sub.*` timings are
nested within `op.*`; `sub.semantic_draw_hash_<type>` is nested in
a stage that performs hashing. **Never sum overlapping metrics**.
Disable `STS2_SEMANTIC_PROFILE_DIR` for final production throughput
measurements; this branch incurs the measurement overhead of more
fine-grained probes, and is for attribution.

## Capture a 128-combat H7 source-matched profile (fish)

Use H7 AI's existing shared CUDA benchmark, not an Inspector run.
Build the diagnostic emulator in a temporary directory.

```fish
cd ~/projects/sts2-ai
source .venv/bin/activate.fish
set emulator ~/projects/sts2-emulator
set rev research/h7-draw-stage-profile-20261027
git -C $emulator fetch origin $rev
set sha (git -C $emulator rev-parse FETCH_HEAD)
set root (mktemp -d /tmp/sts2-h7-draw-stage.XXXXXX)
mkdir -p $root/source $root/profiles
git -C $emulator archive $sha | tar -xf - -C $root/source
dotnet restore $root/source/Sts2Emulator.sln
and dotnet build $root/source/Sts2Emulator.sln -c Release --no-restore -m:1
set -gx STS2_SEMANTIC_PROFILE_DIR $root/profiles

python -u tools/benchmark_h7_rollouts.py \
    --emulator-dll $root/source/src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll \
    --emulator-bindings $root/source/bindings/python \
    --emulator-revision $sha \
    --output $root/h7.json \
    --episodes 128 --workers 15 \
    --actor-backend shared --actor-device cuda \
    --actor-max-batch 16 --actor-delay-ms 3 \
    --study-seed 73127 --actor-seed 31337 --reference-seed 23387

python $root/source/tools/summarize_semantic_perf.py \
    $root/profiles --output $root/native.json
python $root/source/tools/report_draw_stage_perf.py $root/native.json \
    > $root/draw-stages.txt
set -e STS2_SEMANTIC_PROFILE_DIR
echo $root
```

Send `native.json`, `h7.json`, and `draw-stages.txt` for
analysis. Expect 4,054 draws, 3,418 player decisions and 40 victories
with the same seeds and model. The important questions are how many MB
per draw come from `EndTurn.Fork`, `Run.Fork`, the repeated
validation passes, typed JSON hashes, and the actual
`BeginSemanticCombatDraw` mechanics. The new counters will distinguish
those costs.

## Follow-on optimization policy

Use stage data to choose one targeted change. Possible safe approaches
include versioned immutable checkpoints with shared reads, caching a
canonical digest within truly immutable nodes, and reducing redundant
verifications where earlier validated provenance proves equivalence.
Any such change requires exact source-state identity, injected stale
fingerprint rejection, terminal replay equality, and a matched 128-combat
A10 source digest before performance claims. Training and native fidelity
remain independently gated.

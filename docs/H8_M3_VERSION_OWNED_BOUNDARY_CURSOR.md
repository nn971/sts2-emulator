# H8 M3: version-owned boundary classification and draw-completion cleanup

Status: experimental optimization stacked on PR #215.
This slice is motivated by the source-valid 128-combat H8 C# profile,
`h8-hotpath-profile-128.json` (15 emulator workers, 3,382 player actions,
3,981 draws, 199 intent decisions, 128 completed combats, 16 replay audits).

## Measurements that motivated this slice

All C# times are **accumulated service time across workers**, not additive
wall-clock percentages, and nested scopes overlap their parent.

| H8 stage | Calls | Accumulated seconds | Bytes |
| --- | ---: | ---: | ---: |
| H8 status projection | 7,690 | 12.89 | 4.42 GB |
| draw cursor capture | 7,562 | 6.49 | 2.68 GB |
| native draw transition | 3,908 | 15.04 | 6.74 GB |
| native draw legacy continuation | 3,908 | 8.41 | 3.46 GB |
| native end-turn legacy continuation | 1,080 | 4.12 | 1.30 GB |
| repeated full RunState.Fork | 45,470 | 1.48 | 0.81 GB |

The "legacy continuation" scope contains **actual automatic-pipeline
mechanics**, so its aggregate value is not solely removable projection
overhead. We now measure `h8.draw.resume_turn_mechanics` and
`h8.draw.next_player_check` nested within that scope.

## Current implementation

1. `H8CombatRuntimeV1` owns one cached `H8PendingKindV1` tag per
   committed boundary version (terminal, draw, intent, player-choice,
   ordinary player, unsupported). Cheap physical-hand-draw and intent
   markers are checked before invoking derived v111 pending event accessors.
   This keeps the existing external wire `kind` values and precedence for
   supported source-valid decisions.
2. Intent support and raw weights are captured once per H8 boundary;
   returned arrays are cloned, and the actual reference Chance Law remains
   event-scoped and provider-independent. The H8 journal remains the sole
   owner of reproducible explicit player and Nature choices.
3. The ordinary native draw completion checks that the next player has
   legal actions without constructing a complete legacy public frame and
   full hidden-state hash. The H8 version-owned next player boundary
   materializes the public actor frame once. The external checked v111
   continuation is unchanged.
4. The tag, intent support and draw cursor are all invalidated **only after**
   a successful committed player/Nature decision. Forked H8 sessions
   reclassify independently with distinct runtime IDs.
5. There is **no change** to native RNG, ordinary Chance Law, or learned
   Nature provider interchangeability.

Additional opt-in C# scopes now split the expensive combat initialization
into `h8.begin.native_hp`, `h8.begin.native_intents`,
`h8.begin.opening_shuffle`, `h8.begin.semantic_projection`,
`h8.begin.opening_draw_hooks` and `h8.begin.final_validation`.
`h8.intent.v111_resolution` isolates legacy intent mechanics from
reference-law event construction. These counters inform later structural
rewrites and are not themselves claimed speedups.

**Remaining legacy ownership**: the native draw path still calls
`SemanticEndTurnEngineV111.ContinueFromH8AdvancedDraw` to resume automatic
turn mechanics and return its legacy continuation projection. This PR
reduces unnecessary work at its boundary but does **not** claim a fully
new native effect execution engine. The next M4 step requires profiling
what remains in `resume_turn_mechanics`, provider commit, and legacy
bookkeeping before replacing their state ownership.

## Validation and performance test

Run all source-valid H8 tests (same gate as `h8-runtime-smoke`):

```fish
git -C ~/projects/sts2-emulator fetch origin
git -C ~/projects/sts2-emulator switch research/h8-tagged-boundary-cursor-20261028
dotnet build ~/projects/sts2-emulator/Sts2Emulator.sln -c Release -m:1
dotnet test ~/projects/sts2-emulator/Sts2Emulator.sln -c Release --no-build \
  --filter 'FullyQualifiedName~H8CombatRuntimeV1Tests|FullyQualifiedName~SemanticDeckStateV111Tests|FullyQualifiedName~CanonicalJsonTests'
```

For a controlled **unprofiled** 15-worker/128-combat CUDA comparison of
PR #215 versus this M3 branch, use the existing AI A–B–B–A harness.
It builds exact emulator Git revisions into temporary archives, preserves
your active checkout and enforces the complete source/replay/legacy parity:

```fish
git -C ~/projects/sts2-ai fetch origin
git -C ~/projects/sts2-ai switch research/h8-interleaved-emulator-abba-20261028
cd ~/projects/sts2-ai
source .venv/bin/activate.fish
python -u tools/benchmark_h8_emulator_abba.py \
  --emulator-repo ~/projects/sts2-emulator \
  --before-revision 1c7e8a4e377952af2237cb2682e25596e82324eb \
  --after-revision (git -C ~/projects/sts2-emulator rev-parse research/h8-tagged-boundary-cursor-20261028) \
  --output-root results/h8-m3-boundary-abba-a10-128 \
  --workers 15 --episodes 128 \
  --max-boundaries 1200 --replay-every 8 \
  --actor-device cuda --learner-device cuda
```

Record both individual pair speedups and drift controls. A profiler-enabled
run separately attributes savings to `h8.draw.cursor_capture`,
`h8.boundary.classify`, `h8.draw.resume_turn_mechanics`,
`h8.draw.next_player_check`, and `h8.status.project`. No 50/s
throughput prediction is supported by microprofiles alone.

**Acceptance:** exact H7/H8 trajectories, all 128 completed combats, no
missed replay audits, matching actor/model/source decisions and lower
unprofiled paired rollout time. If tests or parity fail, fix the
classification or projection semantics before adding more fast paths.

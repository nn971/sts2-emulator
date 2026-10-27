# H8 consolidated execution and staged throughput benchmark

Status: consolidated H8 research branch (stacks on PR #208 / #207 / #206 /
#205), importing PR #209's trusted semantic intent fast path.

## Consolidated contract

The H8 session retains an opaque `runtimeId + boundaryVersion` token,
a single actor/Nature decision journal, source-preserving effect order,
explicit deterministic replay, and source-selected physical card identity.
It now includes three jointly active H8 migration slices:

- Direct deterministic physical Nature draw (without the nested pilot /
  end-turn / hand-draw resolver dispatcher); legacy output projection is
  still temporarily retained for checkpoint/audit compatibility.
- Direct source-legal player `end_turn` through the real game engine and
  the fast end-turn continuation constructor.
- Trusted weighted semantic enemy intent choice, without duplicate
  full-pilot input validation; the old fingerprint is still computed
  once for intent transcript identity.

`H8CombatRuntimeV1.BeginSeeded` accepts matching `use*` toggles.
The JSONL `h8_begin_seeded` command additionally accepts four
default-true snake-case booleans:
`use_m2_automatic_draw`, `use_native_draw_transition`,
`use_native_player_end_turn`, `use_trusted_intent`.
All are copied into forked and replayed sessions. Supplying
`false` for all four selects the older M1/checked implementation
of these stages in the **same compiled binary**, permitting exact
source/choice/replay comparison.

## Benchmark now available (single-machine fish shell)

Prerequisites: Python 3.12+, .NET 9 SDK, current checkout of this branch.
The benchmark opens 15 *persistent* JSONL .NET subprocesses and has
each worker execute its shard of seeded real A10 Mawler sessions. A
fixed SHA-256-based scripted chooser selects the same semantic intent,
physical card and legal `end_turn` for both arms, avoiding learned-policy
differences. Every completed/capped/unsupported boundary is reported.

```fish
cd ~/projects/sts2-emulator
git fetch origin
git switch research/h8-m2-consolidated-benchmark-20261028
dotnet build Sts2Emulator.sln -c Release -m:1

set dll $PWD/src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll
set rev (git rev-parse HEAD)

python tools/benchmark_h8_sessions.py \
  --emulator-dll $dll \
  --emulator-bindings $PWD/bindings/python \
  --emulator-revision $rev \
  --workers 15 --episodes 128 --max-boundaries 80 \
  --replay-every 8 --order abba \
  --output results/h8-scripted-128-abba.json
```

`--order abba` executes checked/native/native/checked in four cohorts;
`--order ab` is faster for an initial smoke. The experiment rejects any
paired disagreement in status, boundary counts, type counts, scripted
physical/intent/player choices, H8 decision-journal digest **or the exact
whole-mechanics SHA-256 audit checkpoint**, and records reproducible
source hashes, DLL SHA, per-phase wall time,
supported decisions/s, terminal/unsupported/capped counts, as well as
sampled replay verification.

**Do not call a capped or unsupported session a finished combat.** In
particular, the benchmark can make progress with only the currently
supported H8 action kinds. This metric is measured **through the
external JSONL protocol**, so it includes inter-process transport and
live source-valid encounter setup; it does not isolate pure C# mechanics.

To recheck isolated draw and end-turn allocation/timing inside C#:

```fish
dotnet test Sts2Emulator.sln -c Release --no-build -m:1 \
  --filter 'FullyQualifiedName~H8CombatRuntimeV1Tests' \
  --logger 'console;verbosity=detailed'
```

## H7 reference measurement (user local 2026-10-28)

The existing H7 benchmark on emulator revision
`ec948b5b5abbb94a4ef63d362f8eefe71656987b` measured
15 processes, shared CUDA H7 inference, the **14,659,972-parameter**
Transformer and 128 source-complete replay-verified A10 combats from
16 structural cells:

| metric | H7 legacy protocol baseline |
| --- | ---: |
| Wall | 53.1311 s |
| Real combats/s | 2.4091 |
| Player decisions/s | 64.3315 |
| All boundary events/s | 144.1529 |
| GPU forward time (server sum) | 21.0041 s |
| Actor collation time (server sum) | 8.1848 s |
| Model input encoding time (worker sum) | 1.4951 s |

A separate 1-round, 128-combat PPO train run (sampled 16 terminal
replays) measured 37.809 s rollout collection, 3,478 decisions,
14.438 s server forward and 6.521 s collate; this was a different
player cohort and audit workload and should not be compared directly
to the 53.13 s fully replayed reference.

**Do not compare** H7's 2.41 *completed combats/s* against H8's
*supported scripted sessions/s*: the tasks, decision policy, curriculum,
replay workload and actor are different.

## Gating the actual H8 shared-Transformer benchmark

The new runner is **stage one: isolate emulator throughput of the
currently implemented H8 source-valid decision surface**. H8 still
lacks stable typed H7 public observations, card/potion/selection
choices, all 16 curated H5/H7 deck+encounter configurations, and
native batched transport. It cannot yet be used to honestly execute
full-size H7 policy rollouts or genuine PPO.

For stage two:

1. Implement a typed H8 public observation/action adapter preserving
   H7's `EncodedDecision` legal-action semantics. The actor must be
   able to play actual cards, target enemies, use potions and respond
   to nested selections.
2. Provide full seeded scenario/deck/relic entry for each H7 training
   cell (and later the H8 256/64 structural research curriculum).
3. Replace or route `draw_only_episode`'s legacy semantic handles
   through versioned H8 sessions while preserving **exact** source,
   stochastic/reference-law identity and player action sampling.
   The shared CUDA Transformer, size, actor seed, 15 workers, reference
   Nature and terminal replay policy must be held fixed.
4. Run at least 128 full combats for both engines, alternating
   A/B/B/A, with same model checkpoint, source digest, accepted chance
   tickets, policy log-probability checks, replay, and heldout scoring.
   Measure completed combats/s, player decisions/s, GPU queue/forward
   and batch times, encoder cost, emulator wall time, CPU utilization
   and memory, *then* enable H8 for PPO training.

Only then repin `sts2-ai`. Native Inspector parity and historical
fixture failures are independent from this research gate, but **new
H8 mechanics or replay drift is blocking**.

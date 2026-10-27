# H7 semantic-combat rollout hotpath (2026-10-27)

**Branch:** `perf/h7-semantic-rollout-hotpath-20261027`, built from
`e56779ccd86725582af580925ca8242826ba2229`, the emulator revision
used by the reference-only H7 A10 benchmarks. This is an isolated change:
do not merge it into the active unified stochastic / H8 research work
until compatibility has been reviewed.

## Changes and scope

### Paired fair public observation (one RPC)

`SemanticCombat.observe_frame()` now returns
`(public_observation, legal_actions)` from a **single**
`semantic_observe` request. It does not return hidden Nature outcomes,
physical card order, or provider state. The existing `observe()` and
`actions()` methods retain their interfaces. The common
`observe(); actions()` sequence automatically shares its exact response
without a second RPC. The one-shot cache is tied to the immutable semantic
handle and fingerprint; any intervening session operation other than another
observation invalidates it.

This eliminates approximately 3,418 redundant requests from the existing
128-combat H7 benchmark at unchanged combat semantics. The actual time saved
must be benchmarked.

### Opt-in native-server instrumentation

`semantic_perf_reset` enables/resets counters for the current .NET JSONL
process; `semantic_perf_read` returns per-operation count, cumulative elapsed
time, maximum latency and calling-thread managed allocation. It records:

- `op.semantic_begin_seeded`, `op.semantic_step`,
  `op.semantic_resolve_intent`, `op.semantic_resolve_draw_card`,
  `op.semantic_observe`, `op.semantic_save`, `op.semantic_load` and
  other semantic JSONL requests, **including reply serialization**;
- `sub.semantic_step_transition`, `sub.semantic_resolve_transition`,
  `sub.semantic_resolve_draw_card_transition`, and
  `sub.semantic_resolve_intent_transition` (C# transition logic,
  including input validation, before state-store copying);
- `sub.semantic_store_fork` (deep copy on handle storage);
- `sub.semantic_result_fingerprint` (full canonical semantic state hash);
- `sub.semantic_result_pending` (pending event / Chance Law description);
- `sub.semantic_wire_response` (JSON serialization, pipe write and flush).

`sub.*` values are **nested within** `op.*`, so adding them double
counts. The timestamps start after the request JSON was parsed and include
reply serialization but not Python's request construction, sending, JSON
parsing or Python policy work. Times are cumulative **worker wall durations**,
not exclusive CPU cycles; workers overlap.

Profiling is off by default. The server does not publish hidden state
or change game mechanics when profiling is enabled. Use only for measurement,
not ordinary reference-fidelity comparisons.

## Collect full H7 15-worker timings (fish)

The experiment can be built in `/tmp`, leaving the three existing projects
and active emulator branch untouched:

```fish
cd ~/projects/sts2-ai
source .venv/bin/activate.fish

set perf_branch perf/h7-semantic-rollout-hotpath-20261027
git -C ~/projects/sts2-emulator fetch origin $perf_branch
set perf_rev (git -C ~/projects/sts2-emulator rev-parse FETCH_HEAD)
set perf_src (mktemp -d /tmp/sts2-h7-semantic-perf.XXXXXX)
git -C ~/projects/sts2-emulator archive $perf_rev | tar -xf - -C $perf_src
dotnet restore $perf_src/Sts2Emulator.sln
dotnet build $perf_src/Sts2Emulator.sln -c Release --no-restore -m:1

set dll $perf_src/src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll
set bindings $perf_src/bindings/python
set -gx STS2_SEMANTIC_PROFILE_DIR (mktemp -d /tmp/sts2-semantic-profile.XXXXXX)

# Run the *same* H7 model and source world as baseline, with the new
# emulator revision and bindings. Full-size shared actor is optional.
python -u tools/benchmark_h7_rollouts.py \
  --emulator-dll $dll --emulator-bindings $bindings \
  --emulator-revision $perf_rev --workers 15 --episodes 128 \
  --actor-backend shared --actor-device cuda \
  --output /tmp/h7-perf-semantic-128.json

python $perf_src/tools/summarize_semantic_perf.py \
  $STS2_SEMANTIC_PROFILE_DIR \
  --output /tmp/h7-semantic-profile-summary.json
set -e STS2_SEMANTIC_PROFILE_DIR
```

The opt-in environment variable is consumed by the Python
`Session` binding. Each worker calls `semantic_perf_reset` at startup
and saves `semantic-<python_pid>-<dotnet_pid>.json` at shutdown; no
shared writes occur. The per-process summary files are preserved.

Compare to the same command with the old H5-compatible DLL/bindings,
the same actor checkpoint SHA and the same sources. The previous report
identifies `model_sha256` and `source_digest`; those and completion counts
must agree for any speedup claim. The current benchmark defaults to a
fixed seed and random-start model, so model weights are reproducible.
Use `--player-checkpoint` to pin an existing actor.

## Next, based on measured hotspots

1. If `sub.semantic_store_fork` is high, prototype structurally shared
   immutable history and assess exact-state fork independence. Do not simply
   share mutable decision arrays or skip ownership copies.
2. If `sub.semantic_result_fingerprint` is high, consider incremental
   canonical fingerprints over immutable versioned state with collision-
   resistant exact-state equivalence tests.
3. If `op.semantic_load` is high, quantify the replay cost. The current
   `SemanticPilotCombatEngineV111.Restore` replays recorded actions. This
   remains an independent mechanical verification gate; faster training
   should never require the Inspector or exact native RNG parity.
4. If stochastic resolution + Python–C# framing dominate, prototype a
   versioned engine-side reference-law continuation. **H7's current reference
   sampler uses Python's MT19937 `random.Random` initialized from an
   event-key SHA-256.** A C# replacement must either match its exact
   event-key distribution and replay transcript or introduce a separately
   versioned law with its own validation. Do not silently change H7's
   sampling distribution to gain speed.

The existing general emulator microbenchmark does not measure this exact
H7 semantic loop. This workflow supplements it with the real training
boundary path and keeps native Inspector checks independent of training.

## Measured full H7 A10 profile (128 combats, 15 workers)

**Source:** locally generated `h7-perf-semantic-128.json` and
`h7-semantic-profile-summary.json`, supplied 2026-10-27. Revision
`ca458eda7aeddff8b15dd7925dbac14f9547f443`,
full 14,659,972-parameter H7 actor, shared CUDA, 16 A10 training cells,
3,418 player decisions, 4,054 draw resolutions, 187 intent resolutions,
40 victories, 128 completed combats. Reference Chance Law. End-to-end
rollout: **158.998 s / 21.497 player decisions/s**.

Native JSONL profiling (cumulative wall time across 15 overlapping
workers; total worker episode time 2,195.18 worker-seconds):

| Operation | Calls | Cumulative seconds | Avg latency | Managed allocation |
| --- | ---: | ---: | ---: | ---: |
| `semantic_resolve_draw_card` | 4,054 | 999.09 | 246 ms | 540.29 GB |
| `semantic_load` | 128 | 739.40 | 5,777 ms | 436.82 GB |
| `semantic_step` | 3,418 | 247.35 | 72 ms | 113.20 GB |
| `semantic_resolve_intent` | 187 | 42.37 | 227 ms | 20.27 GB |
| `semantic_observe` | 3,418 | 16.17 | 4.7 ms | 2.70 GB |
| `sub.semantic_store_fork` | 7,915 | 9.21 | 1.16 ms | 5.27 GB |

All top-level semantic JSONL operations allocated **1.12 TB in aggregate**
(across the run, *not* peak memory). The 4,054 draw operations contain
645.43 seconds in the measured internal C# draw transition itself.
Nested `sub.*` counters must never be added to `op.*` totals.

### Revised priorities supported by evidence

1. **Terminal full-history replay** consumes 33.7% of cumulative worker
   episode time. The existing `semantic_load` must remain an independent
   full mechanical replay. For routine AI training, consider separately
   changing the `sts2-ai` episode contract to audit a deterministic
   subset of terminals while maintaining full replays in acceptance tests.
   **Never claim `terminal_replay_verified=true` if replay was skipped.**
   Altering the emulator loader silently would undermine the test.
2. **Draw resolution** consumes 45.5% of cumulative worker episode time
   and produces the majority of non-replay allocation. The current
   stable-ID `ResolveDrawCard` wrapper performs an invariant validation
   and full SHA-256 before dispatching to a private resolver that repeated
   the same checks. A subsequent isolated commit removes the duplicate
   check while preserving validation at the public entry and raw numeric
   `Resolve` entry. This change awaits a paired full-size benchmark before
   claiming a speedup.
3. **Store forks (0.4%)** and **public observations (0.7%)** are much
   smaller, and should not be the next large refactor. History sharing
   and caching fingerprints still need independent profiling evidence
   and exact-state semantics tests before invasive implementation.
4. The 128-combat baseline with shared CUDA was about 157.3s. This
   profile-instrumented revision took 159.0s. The difference is too small
   and uncontrolled to infer a performance regression or improvement.

No Inspector fidelity or native RNG parity gates are required for reference
training. Full-snapshot semantic replay remains an explicit, measurable
independent contract, and changing the replay sampling policy belongs in the
consumer with provenance.

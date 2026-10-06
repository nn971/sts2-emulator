# Parity testing and replay

## Goal

Turn the live game into a source of **versioned semantic evidence**.

## Trace model

A trace is JSON Lines (`.jsonl`) with one header followed by transition records.

Header example:

```json
{"type":"header","trace_schema":"0.1","game_build":"0.111.0","bridge_version":"dev","run_seed":"42"}
```

Transition example:

```json
{
  "type":"transition",
  "decision_index":17,
  "phase":"CardReward",
  "action":{"kind":"choose_card","payload":{"index":1}},
  "before":{"hash":"...","state":{}},
  "after":{"hash":"...","state":{}},
  "rng_before":{"streams":[]},
  "rng_after":{"streams":[]}
}
```

The concrete schema lives in `schemas/trace.schema.json` and will evolve.

## Workflow

### 1. Capture reference trace

A live-game instrumentation mod records canonical snapshots at decision boundaries.

### 2. Replay in emulator

The replay runner reconstructs the initial canonical state and feeds the same normalized actions to the emulator.

### 3. Compare after every step

Compare:

- canonical state hash;
- legal-action set;
- RNG fingerprints;
- optionally selected canonical events.

### 4. Stop at first divergence

Produce a compact report with the first differing path.

`tools/trace_diff.py` already compares two trace files and reports the first structural difference. It is intentionally independent of future game-rule code.

## Differential fuzzing

Once the bridge is usable, continuously generate traces using policies such as:

- uniformly random legal action;
- weighted random action emphasizing rare phases;
- mutation of existing traces;
- targeted scenario policies (shops, events, potion-heavy combats, etc.);
- action enumeration from saved states where the reference bridge can restore/fork safely.

The corpus should grow toward semantic diversity, not only number of runs.

## Corpus organization

Suggested local layout:

```text
data/traces/
└── game-0.111.0/
    ├── manifest.json
    ├── random/
    ├── targeted/
    └── regressions/
```

Large corpora should eventually live in artifact storage rather than Git. Git should keep small minimized regression traces.

## Minimization

When a long trace fails, preserve the smallest prefix that still reaches the first divergence. If safe state restoration becomes available, also minimize irrelevant prior choices while maintaining the same failing state.

## CI tiers

- **PR:** unit tests + small curated parity traces + microbench smoke test.
- **main/nightly:** larger parity corpus + fuzz seeds + benchmark regression report.
- **release:** full version-pinned corpus and reproducible performance report.

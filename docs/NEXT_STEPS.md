# Immediate next steps

The next work should focus on evidence acquisition, not breadth of guessed mechanics.

## Sprint 1 — choose and pin the reference build

1. Record the exact installed STS2 version/build.
2. Record the mod-loader/toolchain versions used for instrumentation.
3. Create `reference-builds/<build>/manifest.json` locally or in a small redistributable manifest.
4. Freeze trace schema `0.1` for the first experiment, accepting that it will evolve.

## Sprint 2 — inspect prior art before coding the bridge

Study, side by side:

- CombatSolver's snapshot/fork/RNG boundaries;
- `sts2-simulator`'s live bridge and parity replay protocol;
- Zamiell's C# NativeAOT emulator/binding architecture.

Write an ADR for what we reuse conceptually and what we deliberately redesign.

## Sprint 3 — minimal native recorder

Implement passive recording for one decision class at a time:

1. run metadata + seed;
2. map choice;
3. one combat state/action;
4. card reward.

Do not attempt full coverage yet.

The output must be inspectable JSONL and stable across two recordings of the same deterministic scenario.

## Sprint 4 — RNG inventory

Build a table:

| Stream | Native owner/type | Init point | Full state recoverable? | First parity fixture |
|---|---|---|---|---|
| TBD | | | | |

Recover exact stream semantics before implementing a PRNG codec in the emulator.

## Sprint 5 — first emulator transition

Choose the smallest nontrivial run-level transition whose native trace is fully understood—likely a map/reward choice rather than combat.

Implement it in `Sts2Emulator.Core`, then make replay pass exactly.

The rule for early development is:

> one audited transition end-to-end is more valuable than fifty approximate cards.

## Sprint 6 — parity harness automation

Add a command roughly equivalent to:

```text
sts2sim replay reference.jsonl
```

It should stop on first divergence and print canonical state/RNG/legal-action differences.

## Sprint 7 — establish a performance baseline

Only after several genuine mechanics exist, record:

- step throughput by phase;
- fork throughput;
- canonical hash cost;
- allocations and memory.

Then optimize the largest measured bottleneck without changing parity fixtures.

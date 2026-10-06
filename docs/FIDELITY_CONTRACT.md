# Fidelity contract

This document defines the standard required before we call a subsystem “trustworthy.”

## 1. Version pinning

Every parity artifact records:

- exact STS2 game version/build;
- reference bridge version;
- emulator git revision/version;
- trace schema version;
- static-data snapshot/version, if any.

A parity result without this provenance is informational only.

## 2. Canonical state equality

We compare **canonical semantic state**, not object identity or UI state.

Canonical state should eventually include at least:

- player HP/max HP, gold, act/floor/current room;
- deck with persistent per-card instance state;
- relics and mutable relic counters/state;
- potions and slots;
- map graph/current node and relevant generated room state;
- reward/shop/event/rest state when active;
- combat creatures, piles, powers, counters, history needed for continuation;
- encounter/event/pool history that changes future generation;
- all RNG streams required for exact continuation;
- any hidden persistent flags that affect future legality or outcomes.

Presentation-only data is excluded by explicit rule, never by accident.

## 3. Legal-action equality

At every decision boundary, the reference and emulator should expose equivalent legal choices after canonical normalization.

This catches bugs that state snapshots alone may miss, such as an event option incorrectly enabled or a target selection omitted.

## 4. RNG equality

Matching distributions are insufficient for parity mode. For audited streams we require equivalent continuation state after corresponding actions.

The representation may differ internally. The trace should provide a canonical stream fingerprint or serialized state that makes divergence visible.

## 5. Ordering semantics

Hook/trigger ordering is part of game semantics. A correct final HP total does not validate an incorrect sequence if the sequence can affect later state.

Tests should capture relevant ordering through canonical events or final hidden state whenever practical.

## 6. First-divergence discipline

When a trace diverges, debugging focuses on the **first canonical difference**, not the final run outcome. Later differences are usually downstream symptoms.

A parity bug report should include:

1. smallest known reference trace prefix that reproduces the divergence;
2. first decision index with disagreement;
3. path to first differing field;
4. reference value and emulator value;
5. RNG fingerprints;
6. action taken;
7. game/emulator versions.

## 7. Unsupported behavior

Unsupported mechanics must fail loudly or be tagged as unsupported. Silent fallback approximations are prohibited in parity mode.

Approximate models may exist later for search acceleration, but they live behind an explicitly different interface and can always be rescored by the exact engine.

## 8. Confidence labels

Suggested labels for subsystems:

- **Unimplemented** — no semantics claimed.
- **Implemented** — code exists, limited validation.
- **Regression-tested** — deterministic unit/property tests exist.
- **Trace-validated** — observed native traces match.
- **Fuzz-parity** — randomized differential testing has substantial coverage.
- **Milestone-grade** — included in complete-run corpus with no unexplained divergence.

This is more informative than a single coverage percentage.

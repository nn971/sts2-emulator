# Contributing

## Prime rule

A new mechanic is valuable when it improves **audited semantic coverage**, not merely when it increases a count.

For parity-critical changes, please include as many of the following as applicable:

- game build/reference version;
- source/behavioral reasoning for the rule;
- unit/property test;
- native trace or minimized parity fixture;
- first-divergence fix evidence;
- performance impact if the path is hot.

## Do not

- copy decompiled source text into the repository;
- add silent approximations to parity mode;
- let Python/research wrappers mutate canonical rules;
- expose hidden RNG in a “fair” observation by accident;
- optimize by weakening the canonical state/hash without proof.

## Style

Keep the engine explicit and boring. Prefer small deterministic functions and visible state transitions over clever global services.

## Commit categories

Suggested prefixes:

- `engine:` canonical game semantics/state
- `rng:` RNG capture/codec/stream logic
- `parity:` bridge/replay/diff/corpus
- `perf:` behavior-preserving optimization
- `trace:` schema/protocol changes
- `research:` non-canonical experiments
- `docs:` documentation/ADR

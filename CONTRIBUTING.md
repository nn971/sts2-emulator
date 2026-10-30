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

## Stochastic provider changes: mandatory architecture review

Before adding or changing RNG, Nature, Chance Law, draw order, controlled
continuations, or H15 search boundaries, read
[the canonical stochastic ownership contract](docs/UNIFIED_STOCHASTIC_ARCHITECTURE.md),
[ADR 0004](docs/adr/0004-provider-owned-stochastic-state-and-chance-law.md),
and [ADR 0005: H15 duplication retrospective](docs/adr/0005-h15-reference-nature-reuse-retrospective.md).
Inspect the existing `StochasticSiteRegistryV111`, `ChanceLawV111`,
`ReferenceNatureV111`, and related atomic session implementations
**before** proposing a new sampler or mechanics resolver.

A PR introducing a new stochastic component must name the specific
capability absent from the existing owners. Prefer reusing shared
`ReferenceNature` sampling, typed continuation replay, and the canonical
Chance Law identity/provenance validator. Keep fixed-fixture conditioning,
authentic native-run posterior conditioning, and local
`SOURCE_VERIFIED` chance laws distinct.

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

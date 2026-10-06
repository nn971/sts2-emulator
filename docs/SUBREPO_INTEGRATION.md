# Using `sts2-emulator` as an AI-project subrepository

## Intended relationship

The emulator should be usable as a standalone repository and as a pinned dependency of a larger research repository.

A future parent project might look like:

```text
sts2-ai/
├── emulator/              # git submodule/subtree -> sts2-emulator
├── search/                # beam/MCTS/tree-search algorithms
├── training/              # policy/value/model training
├── datasets/              # search/training corpora and manifests
├── strategy-db/           # persistent evaluated states / experience graph
├── agents/                # decision policies
├── experiments/           # configs, sweeps, evaluation
└── tools/
```

The exact parent name is deliberately unspecified. This repository should never require the parent to exist.

## Recommended Git integration

For a separately versioned emulator, a Git submodule is the cleanest starting point:

```fish
git submodule add <sts2-emulator-repo-url> emulator
git submodule update --init --recursive
```

The parent commit then pins an exact emulator revision, which is valuable for reproducible experiments.

A subtree is also reasonable if simpler cloning is preferred, but it weakens the clean version boundary slightly.

## Dependency direction

The dependency graph must remain one-way:

```text
sts2-emulator  <---  future AI project
```

The emulator must never import the parent's training/search code.

This keeps the emulator independently testable and prevents strategy assumptions from leaking into mechanics.

## What the parent may depend on

The parent may depend on:

- canonical state/action schemas;
- deterministic stepping;
- state fork/clone semantics;
- batch stepping;
- exact-state hashes;
- optional player-visible projections;
- native trace/replay tooling for validation;
- stable language bindings.

The parent should avoid depending on internal concrete classes when a public API is available.

## Versioning contract

Every experiment should record at least:

```text
game_build
emulator_commit
emulator_schema_version
binding_version
information_policy_version   # if observations are used
```

Search results and training data should be invalidated or explicitly migrated when a semantic correction changes canonical transitions.

## Where strategic caches belong

Persistent search trees, strategic experience graphs, policy/value targets, and training replay buffers belong in the parent project.

The emulator may provide generic mechanisms useful to them—stable hashes, snapshots, branch serialization—but should not prescribe how strategic evidence is stored or learned from.

## Bindings

The preferred long-term arrangement is:

```text
C# canonical engine
    ↓
NativeAOT/C ABI or similarly stable batch API
    ↓
Python/Rust/etc. consumer
```

The binding layer may optimize batching and memory transfer. It must not reimplement rules.

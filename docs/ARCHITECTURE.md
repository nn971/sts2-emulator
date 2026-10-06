# Architecture

## Core design

The repository separates **native truth**, **exact simulation**, and **consumer interfaces**.

```text
┌──────────────────────────────┐
│ Real Slay the Spire 2 build  │
│ native mechanics + native RNG│
└──────────────┬───────────────┘
               │ instrumentation / reference bridge
               ▼
┌──────────────────────────────┐
│ Native reference oracle      │
│ canonical snapshots + traces │
└──────────────┬───────────────┘
               │ differential replay
               ▼
┌──────────────────────────────┐
│ Exact fast emulator          │
│ C# deterministic core        │
│ explicit RNG + cheap forks   │
└──────────────┬───────────────┘
               │ stable versioned API / bindings
               ▼
┌──────────────────────────────┐
│ External consumers           │
│ AI/search/testing/analysis   │
└──────────────────────────────┘
```

The native game is the semantic reference. This repository owns the emulator and reference/parity tooling. External consumers never own parity-critical rules.

## Why C# for the canonical emulator

STS2 is implemented in C#, so a C# parity-critical core offers:

- close mapping between audited native behavior and emulator control flow;
- lower semantic translation cost than a rules rewrite in a distant language;
- direct access to .NET performance tooling;
- NativeAOT as a future embedding option;
- clean thin bindings to Python/Rust/etc. without moving mechanics into those layers.

## Public semantic API

The engine should converge on a small contract:

```text
GetLegalActions(S) -> [a]
Step(S, a)         -> S'
Fork(S)            -> independent S_copy
Hash(S)            -> exact-state fingerprint
Serialize(S)       -> canonical snapshot
Load(snapshot)     -> S
```

High-throughput consumers also need batch variants, but batch operations must preserve identical semantics.

`Step` is deterministic because every continuation-relevant RNG stream belongs to `S`.

## Exact state vs projections

The exact engine state includes hidden information needed for continuation. The emulator may additionally expose versioned projections such as player-visible state:

```text
O = Project(S, information_policy)
```

Projection APIs are convenience/consumer interfaces. They never replace exact state internally.

## Reference oracle

The native bridge captures decision-boundary snapshots rather than rendered frames. Each transition should contain:

- game build/version;
- trace/schema version;
- run identity/seed;
- decision index and phase;
- canonical state before the action;
- complete legal-action set or digest;
- selected action;
- canonical state after the action;
- RNG stream fingerprints/state before and after where available;
- provenance/diagnostics.

The oracle can be relatively slow. Its job is truth generation and differential verification.

## Exact emulator

The emulator should:

- omit UI, animation, audio, and render objects;
- keep continuation-relevant state explicit;
- make mutation ownership obvious;
- support branch-efficient state forks;
- keep RNG branch-local;
- expose canonical serialization independent of hot-path memory layout;
- permit parallel independent simulations;
- provide clear unsupported-mechanic failures;
- remain reproducible under release builds.

## State storage strategy

The scaffold begins with simple records and explicit forks. This favors auditability over premature optimization.

After representative parity traces exist, profile and consider:

- structural sharing;
- copy-on-write arenas;
- compact value types;
- pooled buffers;
- persistent collections;
- specialized pile/deck storage;
- compact instance IDs;
- incremental exact-state hashes.

Every storage optimization is validated through existing canonical parity fixtures.

## Consumer boundary

External consumers should use stable APIs/bindings rather than emulator internals. In particular:

- no game rule may exist only in a Python wrapper;
- no AI/search dependency may enter the emulator core;
- strategic caches belong outside this repository;
- consumer-specific tensor/graph representations are derived from exported state.

See [SUBREPO_INTEGRATION.md](SUBREPO_INTEGRATION.md).

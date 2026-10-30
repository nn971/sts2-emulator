# RNG model

> **Desired implementation:** [Interchangeable native RNG / controlled Nature architecture](DESIRED_RNG_IMPLEMENTATION.md) (partially implemented; see the [migration matrix](DESIRED_RNG_MIGRATION_STATUS.md)). It separates ordered mechanical random-choice boundaries from the provider selecting the outcome, while preserving native seeded replay as its own fidelity gate.

The [Unified stochastic architecture](UNIFIED_STOCHASTIC_ARCHITECTURE.md) is the canonical ownership contract; the [staged roadmap](STOCHASTIC_ARCHITECTURE_ROADMAP.md) records Chance Law and ReferenceNature acceptance.

Native sampling remains on its source provider path. Chance Law is a separate read-only query over a declared pre-outcome support/constraint state, not a runtime sampling dependency.

## Principle

Provider-owned RNG and stochastic realizations/constraints are part of the **complete simulation state**, not global services. Mechanics owns physical objects and deterministic consequences; the active provider owns private stream state, committed outcomes or unresolved constraints.

Conceptually, each random stream is a deterministic transition:

```text
(result, R') = Next(R, request)
```

and auto-resolving seeded execution becomes deterministic:

```text
S' = Step(S, action)
```

because the complete state contains the active provider's private state. Nature mode instead suspends at a declared stochastic boundary until a legal choice is supplied.

## Multiple streams

STS2 uses multiple context-specific RNG streams. The pinned **v0.111.0** native source establishes **12 eager run-level streams, three eager per-player streams**, and additional context-created map, event, encounter and monster RNGs. Their names, exact xxHash64 → SplitMix64 → xoshiro256** initialization, concrete source paths, and native capture evidence are documented in [v0.111.0 RNG inventory and fidelity matrix](reference-builds/v0.111.0-native-rng-fidelity-matrix.md).

The remaining work is to match the **consumer-to-stream mapping and call ordering** of every relevant mechanic, then compare exact seeded native/emulator replay. The six-stream SplitMix64 `PrototypeRng` remains a separately versioned approximation; it is not the pinned native RNG codec.

The scaffold therefore represents a stream generically as:

- stable stream ID;
- codec/version identifying how the opaque state is interpreted;
- opaque state bytes;
- optional call count for diagnostics.

The native v0.111.0 provider now implements the audited source RNG codec explicitly; these generic stream fields describe transport and snapshot ownership, **not** an absence of an implemented native algorithm. The retained six-stream `PrototypeRng` is a separate approximation, and exact call-site/stream parity is still incomplete.

## Forking

If state `S` forks into branches A and B, each branch gets independent logical RNG state:

```text
           R0
          /  \
       A:R0  B:R0
        |      |
      Next   Next
        |      |
       RA      RB
```

Advancing A must never mutate B or the live game.

An optimized implementation may share immutable bytes/state until first mutation, but observable branch semantics must match independent copies.

## Lazy native RNG materialization

CombatSolver demonstrates an attractive pattern: store immutable full RNG state in the prediction state, materialize a mutable native RNG only when a random call is required, then recapture state before branch sharing. We should study and adapt that idea where compatible with whole-run semantics.

## Parity requirements

For each audited stream, tests should establish:

1. same initialization from equivalent run state;
2. same random result for the same request sequence;
3. same post-call stream fingerprint;
4. same stream chosen for each game operation;
5. same lazy/eager consumption timing;
6. independence across forks;
7. no accidental RNG consumption by debug/log/serialization code.

## Exact state vs player-visible projection

The exact emulator must retain RNG state because it is continuation-relevant. A player-visible projection generally excludes hidden RNG internals.

```text
ExactState = visible state + hidden continuation state + RNG
PlayerProjection = information legitimately visible from the game interface
```

The projection boundary belongs to the public API so downstream consumers can avoid accidental hidden-state leakage while the emulator still replays exactly.

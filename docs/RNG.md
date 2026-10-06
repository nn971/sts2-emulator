# RNG model

## Principle

RNG is part of the state, not a global service.

Conceptually, each random stream is a deterministic transition:

```text
(result, R') = Next(R, request)
```

and the whole game transition becomes deterministic:

```text
S' = Step(S, action)
```

because `S` contains the RNG bundle.

## Multiple streams

STS2 uses multiple context-specific RNG streams. The exact inventory, initialization rules, lazy-materialization behavior, and call ordering must be recovered from the pinned game build and validated with native traces.

The scaffold therefore represents a stream generically as:

- stable stream ID;
- codec/version identifying how the opaque state is interpreted;
- opaque state bytes;
- optional call count for diagnostics.

We intentionally avoid hard-coding a guessed PRNG algorithm into the starter repo.

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

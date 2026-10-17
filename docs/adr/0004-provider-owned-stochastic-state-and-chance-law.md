# ADR 0004: Provider-owned stochastic state and optional Chance Law

**Status:** Accepted target design, 2026-10-16 (implementation partial).

## Context

The source game commits some hidden outcomes early (a shuffled pile), while Nature must be able to choose individual unresolved cards later. Persisting the native permutation inside the mechanics engine forces Nature to mimic native commitment. Conversely, allowing two independently writable draw-order representations creates divergence. Explicit `p` is needed for Nature but not for native source sampling.

## Decision

The engine owns the simulation lifecycle and hidden mechanical history; the **active stochastic provider** owns private RNG streams, committed stochastic realizations or unresolved constraints. The engine communicates physical membership and forced placement operations through one mechanic/provider contract. Complete snapshots/forks include both states.

A **read-only Chance Law** query computes outcome-independent conditional `p` given a declared information/constraint state. It serves Nature, a seeded dumb `ReferenceNature` test policy, and offline analysis. **Normal Native RNG execution and the native game bridge never depend on Chance Law.** The native provider samples using pinned source procedures; the native inspector validates it separately.

ReferenceNature samples according to the reported law and executes the same mechanics. Independent small-state enumeration verifies the law rather than allowing self-confirmation by the sampler. Source-verified `p` / unbiased importance-correction claims remain a separate N5 gate.

## Consequences

The semantic deck can represent committed native order and constrained unresolved Nature order without double randomization. Engine and provider state must be forked/serialized atomically. Existing versioned APIs remain compatible; migration begins by auditing `CombatState.DrawPile` versus `SemanticDeckStateV111`. See [canonical architecture](../UNIFIED_STOCHASTIC_ARCHITECTURE.md) and [roadmap](../STOCHASTIC_ARCHITECTURE_ROADMAP.md).

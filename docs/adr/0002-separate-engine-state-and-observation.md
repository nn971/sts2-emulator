# ADR 0002: Separate exact engine state from consumer projections

- Status: Accepted for scaffold
- Date: 2026-10-06

## Decision

The emulator stores all continuation-relevant hidden state, including RNG. Player-visible or other consumer-specific observations are explicit, versioned projections of exact state.

## Reason

Exact replay and forked simulation require hidden state. Downstream consumers may require restricted/player-visible information and must not force the canonical engine to discard continuation-relevant data.

## Consequence

The exact-state API and projection APIs are distinct. Dataset/search/training code in a parent repository should record which projection/information policy it used.

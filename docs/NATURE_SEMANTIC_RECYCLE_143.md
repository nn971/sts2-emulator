# Nature #143 — source-timed semantic discard recycle

**Schema:** `sts2-v111-semantic-deck-v1` inside the separately versioned
`sts2-v111-semantic-real-combat-draw-v1` conditioned segment.
**Active code:** `PrototypeGameEngine.DrawCards`,
`SemanticDeckEngineV111.RecycleNature`.

## When the source mechanic reshuffles

A card draw processes a card and its `CardDrawn` hooks *before* checking
whether the next card draw needs the empty pile refilled. When a later
draw observes an empty draw pile and nonempty discard:

1. The combat engine validates the physical discard cards, active shuffle
   hooks and supported source conditions.
2. The game moves the entire discard into the draw pile, clears the discard,
   and increments the semantic provider's shuffle epoch.
3. Instead of issuing one Nature decision for every source-time
   Fisher–Yates swap, the provider keeps the new epoch **unresolved**.
4. When a card-effect draw crosses the recycle and an after-shuffle relic
   inserts a generated card, the draw frame suspends on the existing semantic
   insertion event. After the position is selected, the same `DrawCards` loop
   continues and asks Nature for the first physically required card instance;
   each subsequent draw triggers its own legal identity ticket and normal
   `CardDrawn` hooks.
5. Under the controlled provider no shadow native RNG step is consumed.
   Exact stream call count/seed parity remain obligations of the separate
   native provider path, whose code is unchanged.

This is one source-time shuffle reset and a series of later semantic
reveals, **not two overlapping Nature randomization layers**.

## Perfect Fit

Source `ApplyPerfectFitShuffleOrder` partitions the shuffled discard so
that Perfect Fit cards are drawn first, preserving their shuffled order
among themselves. The Nature deck stores **two coupled-but-independent
unresolved groups**: top slots eligible only from the Perfect Fit pool,
and subsequent slots eligible only from the other cards. This is the
exact structural support of source shuffle + deterministic Perfect Fit
reordering; any uniform-distribution statement is a *synthetic test
assumption* until the source-law audit.

The physical `CombatState.DrawPile` retains concrete member IDs for
mechanical identity/membership checks. Its internal order is not used to
choose semantic Nature outcomes.

## Explicit remaining hooks

An active after-shuffle relic inserts its generated card at a random draw-pile
position. The card-effect draw continuation now reuses the semantic insertion
event and updates the provider deck before resuming the same draw. Other
recycle contexts without that saved card-effect frame still fail closed at
`combat.draw.recycle` for after-shuffle insertion.

Stratagem can suspend for a player selection *after* a shuffle and before
the resumed draw. That choice also requires its own nested continuation;
it currently fails atomically:

`UNSUPPORTED_SEMANTIC_DECK site=combat.draw.recycle operation=stratagem_player_selection`

The real-semantic bridge now includes limited card-effect draw continuations,
but is not a complete opening-hand/turn/card-effect/JSONL implementation.
Snecko Eye, Slither, Stratagem selection, nested `CardDrawn` decisions,
split-pool unknown-position deck mutation, native seeded parity, source
probability and original-game importance weights remain separate future gates.

## Tests

Integration tests execute the **real `DrawCards` loop** over a conditioned
two-card draw pile and a populated discard. They establish card-hook
counter order, exact instance identity, interrupted per-card Nature
decisions, untouched native RNG state, an epoch transition, snapshot
provenance replay, and Perfect Fit priority constraints. Pure provider
tests verify partition support, event chronology, fork serialization
and rejection of malformed/unsupported recycle requests.

The frozen Silent `main` and legacy controlled Fisher–Yates fixture
are unchanged.

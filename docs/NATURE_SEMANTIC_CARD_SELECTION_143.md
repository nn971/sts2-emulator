# Nature #143 — semantic card-selection deck movements

**PR:** #164; **versioned fixture envelope:**
`sts2-v111-semantic-card-selection-v1`.
**Source mechanic:** `PrototypeGameEngine.ApplyCardSelection` and
`ResolvePendingChoice` invoked by the real
`PrototypeAiEnvironment.Step(select_cards)`.

## Source-time positional semantics

The game's physical `CombatState.DrawPile` stores top at the
**last** array element. The semantic provider stores its positional
constraints **top-first**. When an ordinary source `select_cards`
resolution with `MoveToDrawTop` takes known physical card instances
from hand, discard or exhaust and appends them to the physical draw
pile, the engine now also inserts their IDs at the semantic **top**
using `SemanticDeckEngineV111.PlaceTop` in the same selection order.
If the choice appends [a, b] to the physical pile, its semantic
top-first constraints start [b, a, ...].

The existing unresolved card pools, peeked/known positions, shuffle
epoch and historic Nature choices are preserved. The selected card
identities are already known because they came from visible zones.
No new Nature card-choice ticket is issued at the placement site.
The next actual `DrawCards` consumes known-top cards without
re-randomization; subsequent unresolved draws receive their ordinary
per-card Nature choices.

A source selection that **removes a card from the draw pile**
(`MoveToHand`, `MoveToDiscard` or `MoveToExhaust`) preserves
the provider's positional state only if the selected instance's
position was already fixed by a prior semantic reveal/peek/scry.
It removes that exact known slot by `RemoveKnown`. Selecting an
identity still inside an unresolved pool requires a distinct
random-position continuation and currently fails explicitly before
publishing a child.

Before either supported mutation the original engine validates
provider/physical draw-pile membership. Removing all unselected
hidden cards remains unsupported. The source mechanics still update
all other physical card zones and continuation events normally.

## Action and replay entry

`SemanticCardSelectionEngineV111.Step` accepts a **source-shaped
pending `select_cards` checkpoint** already obtained after a card,
potion or other source effect has created the choice. It verifies the
supported source selection and pile transition, resolves the legal action
through the original AI action engine, and validates the exact resulting
provider constraints and physical card zones. A supported queued source
continuation may complete after the selection; unsupported outer choices,
sequential prompts and draw-on-completion choices fail closed.

The resulting object contains the complete original pending choice,
legal action ID, selected card IDs and real post-resolution RunState.
`Restore` executes that same legal source action again, validating
its hidden-state commitments and the whole replayed snapshot.

Focused tests prepare genuine Silent combat card instances and a
source-shaped `select_cards` checkpoint to cover:
- Multi-card `MoveToDrawTop` source ordering.
- The next genuine end-turn hand draw consuming a forced known top
  before requesting an unresolved identity.
- A peek-revealed draw-pile instance moved to hand while other hidden
  positions retain their constraints.
- Rejected unknown-position removals and tampered semantic snapshots.
- Deterministic independent 15-worker source-selection + end-turn replay.

## Opt-in semantic player-choice bridge

The semantic JSONL session now exposes one source action that creates a real
`select_cards` prompt: an opt-in seeded fixture substitutes Silent's Survivor
into the first opening-hand slot. Its block and card-play hooks run through the
ordinary engine, then the session publishes the source candidate IDs, legal
selection action IDs and a stable `player_card_selection` event. The ordinary
fair observation and legal-action list remain the player's interface; the
semantic event adds identity and support metadata for replay.

Selecting a legal action resumes the original `ResolvePendingChoice` path,
including its saved source-card and queued-operation continuation. The choice
is recorded as `semantic_card_selection` in the existing action transcript,
and save/load replays the play plus selection from entry provenance. Choices
that do not move the hidden draw pile preserve its semantic state; known-top
placements and previously revealed draw-pile removals use the existing deck
projection rules above.

This is deliberately a fixture loadout integration, not evidence that Survivor
was acquired through the native run route. Outer-choice continuations,
sequential optional prompts, draw-on-completion prompts, hidden-position
removals and any continuation that reaches an unsupported random site still
fail closed. Conditional probabilities remain `UNVERIFIED`.

## Remaining integration gates

This implements the **real choice-resolution mechanic**, not yet
general source-authentic loadout acquisition or every parent card/potion
continuation that creates a `select_cards` prompt. It does not yet support
scry discard/reordering, partial random-position removals, card-effect draws,
effects that return the hand and reshuffle, or random insertions into the draw
pile. Those transitions require dedicated stochastic and source-timed
continuations, with provider membership verified at each mutation site.

This opt-in capability is separate from legacy controlled
Fisher–Yates swap tickets and from the frozen four-Act Silent
`main`. Source probability law is still **UNVERIFIED**.

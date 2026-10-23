# Nature #143 — semantic card-selection deck movements

**PR:** #164; **versioned fixture envelope:**
`sts2-v111-semantic-card-selection-v1`; sequential actions use the existing
`sts2-v111-semantic-card-selection-choice-v2` event.
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
(`MoveToHand`, `MoveToDiscard` or `MoveToExhaust`) removes a known slot
directly. It can also remove an unresolved identity without exposing its
position when that identity's shared pool occupies one contiguous run of
positions. The remaining cards in that run stay exchangeable, so deleting
any one slot represents the same remaining constraints. If a known inserted
card still splits that shared pool into multiple runs after the other
selected known cards are removed, choosing which run lost the removed
identity requires a latent-position continuation and currently fails closed.
New snapshots label these source operations
`remove_known_draw_card` or `remove_unresolved_draw_card_identity`.

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
continuation may complete after the selection. Sequential selection is
supported for the exact turn-start Gambling Chip continuation and Gambler's
Brew's potion-use completion; other sequential or nested choices fail closed.
Gambler's Brew's completed selection resumes its source draw for the selected
count through the existing semantic draw owner.

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
- Contiguous unresolved-pool removals, rejected split-pool removals and
  tampered semantic snapshots.
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
placements and supported known or contiguous-pool draw-pile removals use the
existing deck projection rules above.

The persistent semantic pilot also admits Neow's Fury's optional
discard-pile-to-hand selection and Neow's Abundance's exact three upgraded
Power offer through the existing card-selection and pool-shuffle owners.
Gambler's Brew uses the sequential event for one-at-a-time hand discards, then
the semantic draw owner for the selected count. Seeker Strike now admits its
exact physical source card and three-card draw-pile shortlist through the
existing full-pool shuffle and card-selection events. Discovery's optional
three-card generated offer now uses its exact source card and existing
ChoicePool selection event. Splash's optional three-Attack offer now uses its
exact source card and source-upgrade state with the same owners. Anointed's exact Rare shuffle likewise uses its
physical source-card identity without a fixture marker. Each path retains its
source continuation and hidden draw-pile checks; Native acquisition and
cursor parity remain separate evidence.

Secret Technique, Secret Weapon, Purity, and Scrawl now enter their ordinary
source card-selection prompt through the same event and replay owner. The
draw-pile choices retain the existing known or contiguous-pool removal checks.
Thinking Ahead resumes its two-card draw and `MoveToDrawTop` choice through the
existing draw and selection owners.

The Survivor entry is deliberately a fixture loadout integration, not evidence
that Survivor was acquired through the native run route. Other outer-choice continuations,
sequential optional prompts, draw-on-completion prompts, hidden-position
removals and any continuation that reaches an unsupported random site still
fail closed. Conditional probabilities remain `UNVERIFIED`.

## Remaining integration gates

This implements the **real choice-resolution mechanic**, not yet
general source-authentic loadout acquisition or every parent card/potion
continuation that creates a `select_cards` prompt. It does not yet support
scry discard/reordering, removals from pools split around known anchors,
general draw effects beyond the supported direct-draw and draw-then-discard
Silent cards and Gambler's Brew documented in
[`NATURE_SEMANTIC_EFFECT_DRAWS_143.md`](NATURE_SEMANTIC_EFFECT_DRAWS_143.md),
effects that return the hand and reshuffle, or random insertions into the draw
pile. Those transitions require dedicated stochastic and source-timed
continuations, with provider membership verified at each mutation site.

This opt-in capability is separate from legacy controlled
Fisher–Yates swap tickets and from the frozen four-Act Silent
`main`. Source probability law is still **UNVERIFIED**.

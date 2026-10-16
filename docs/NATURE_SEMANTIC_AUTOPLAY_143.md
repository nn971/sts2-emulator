# Semantic random-card auto-play continuation

## Supported operation

The semantic pilot now supports `combat.autoplay.random_card.select` when a
player action enters `AutoPlayRandomCardsFromZone`. It shares the serializable
card-shortlist state and publishes a `SemanticCardShortlistEventV111` with one
selection required. Nature selects a physical card instance from the ordered
eligible source pile. The pending event remains outside the player's legal
action menu.

The source cards currently routed through this player-action path are:

- **Beat Down** selects an Attack from the discard pile.
- **Catastrophe** selects from the draw pile, falling back to that pile when
  no playable card is eligible.

The semantic pilot's existing `fixture_selection_card_id` can replace the
first opening-hand card with either source card for a reproducible fixture.
This changes fixture loadout only; it does not claim native acquisition
history.

The engine saves the active auto-play operation, queued suffix, card-play
series, and enclosing choice/event continuation. After Nature selects an
instance, the engine removes it from its source pile, queues the same
`AutoPlayCombatCard` operation, and resumes the saved operation kernel. For a
Catastrophe card selected from the draw pile, its identity is also removed
from the semantic deck before physical pile movement. The next repetition
recomputes its eligible pool after the prior auto-play has resolved.

When a controlled source pool has one card, the engine selects it directly
and synchronizes the semantic deck for draw-pile removal. No Nature event is
needed when support is a singleton.

## Limits and verification

Only the player-action continuation is supported. A multi-candidate random
auto-play reached from an automatic-pipeline continuation still fails closed;
this does not add end-turn or other automatic-stage resumption. Auto-played
cards can also reach other controlled chance sites whose coverage remains
separate. Native stream assignment, seeded native cursor parity, and the
conditional source law remain unverified.

The Release solution build succeeded with zero warnings and errors for this
change. Tests and mechanics regressions were not run, so controlled mechanics
remain unverified. See the [desired RNG migration row](DESIRED_RNG_MIGRATION_STATUS.md)
for the current site status.

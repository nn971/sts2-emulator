# Semantic random draw-pile replay selection

## Supported operation

The semantic pilot supports `combat.draw_pile.random_replay_card.select` when
`EmpowerRandomDrawCardReplay` is reached from a player action or automatic
pipeline. Hidden Gem (`proto.colorless.hidden_gem`) is enabled in this route.
It uses the existing serializable card-shortlist continuation and emits a
`SemanticCardShortlistEventV111` with the draw-pile instance IDs in source
order.

Set `fixture_selection_card_id="proto.colorless.hidden_gem"` to replace the
first opening-hand card with Hidden Gem in a reproducible semantic-pilot
fixture. This changes the fixture loadout and does not claim native card
acquisition history.

Eligibility matches the source effect: cards must not be unplayable, Curse or
Status cards, already replayed, or enchanted with Spiral or Glam. If the
eligible pool contains Attacks, Skills or Powers, only that preferred ordered
subset is offered; otherwise the complete eligible pool is used. Nature picks
one physical card instance. The engine adds the effect amount to that card's
`ReplayCount` and resumes the saved operation suffix. The card remains in its
draw-pile position, so the semantic deck order is unchanged.

Singleton support is applied directly without an external Nature choice.

## Limits and verification

The saved automatic-pipeline cursor remains attached while the shortlist and
any nested shortlist choices resolve, then resumes after the operation
finishes. Any later nested chance operation must have its own continuation
support. Native stream assignment, seeded cursor parity, and the conditional
source law remain unverified.

The Release solution build succeeded with zero warnings and errors for this
change. Tests and mechanics regressions were not run, so controlled mechanics
remain unverified. See the [desired RNG migration row](DESIRED_RNG_MIGRATION_STATUS.md)
for the current site status.

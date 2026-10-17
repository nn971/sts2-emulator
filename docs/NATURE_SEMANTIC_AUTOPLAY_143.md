# Semantic random-card auto-play continuation

## Supported operation

The semantic pilot supports `combat.autoplay.random_card.select` when a
player action or automatic pipeline enters `AutoPlayRandomCardsFromZone`. It
shares the serializable card-shortlist state and publishes a
`SemanticCardShortlistEventV111` with one selection required. Nature selects a
physical card instance from the ordered eligible source pile. The pending
event remains outside the player's legal action menu.

The current semantic fixture route uses these source cards:

- **Beat Down** selects an Attack from the discard pile.
- **Catastrophe** selects from the draw pile, falling back to that pile when
  no playable card is eligible.

The semantic pilot's existing `fixture_selection_card_id` can replace the
first opening-hand card with either source card for a reproducible fixture.
This changes fixture loadout only; it does not claim native acquisition
history.

The engine saves the active auto-play operation, queued suffix, card-play
series, and enclosing choice/event continuation. Automatic stages retain their
pipeline cursor alongside that continuation; nested shortlist choices keep the
same cursor until the operation finishes. After Nature selects an instance,
the engine removes it from its source pile, queues the same
`AutoPlayCombatCard` operation, and resumes the saved operation kernel. For a
Catastrophe card selected from the draw pile, its identity is also removed
from the semantic deck before physical pile movement. The next repetition
recomputes its eligible pool after the prior auto-play has resolved.

When a controlled source pool has one card, the engine selects it directly
and synchronizes the semantic deck for draw-pile removal. No Nature event is
needed when support is a singleton.

## Distilled Chaos top-draw continuation

The semantic pilot also admits manual **Distilled Chaos** use. Its
`AutoPlayTopDrawCards` operation consumes known top identities directly and
suspends on an unresolved Nature top card through the existing semantic draw
event. The continuation saves the remaining count, already staged physical
instances, and queued effects. After Nature selects an identity, the ordinary
operation resumes, stages the rest of the requested cards, then queues the
same `AutoPlayCombatCard` operations. This is a card autoplay, not a hand
draw: it does not dispatch `CardDrawn` hooks. ReferenceNature and controlled
resolvers use the existing conditional draw law; its equal remaining-pool
weights remain `UNVERIFIED`.

This boundary supports manual Distilled Chaos and Mayhem's automatic
`AutoPrePlay` trigger. Mayhem resumes its saved pipeline after the event and
preserves History Course's post-dispatch action. An empty draw pile with an
empty discard produces no autoplay. A required recycle creates a deferred
Nature shuffle and continues through the same top-card event. After-shuffle
relics that insert generated cards resolve through the existing controlled
insertion continuation. A seeded semantic fixture can include Distilled Chaos with
`fixture_top_autoplay_potion_id`.

## Limits and verification

The continuation resumes its saved automatic stage after all selected
auto-play operations and nested choices finish. Semantic draw and draw-cost
choices reached inside a Mayhem-autoplayed card preserve its card-play frame
and pipeline cursor through the draw hooks. Nested Nature draws reuse the
semantic recycle and after-shuffle insertion path in `DrawCards`; the legacy
recycle ticket without a semantic deck remains unsupported. Other unrelated
controlled chance sites remain separate. Native stream assignment, seeded
native cursor parity, and the conditional source law remain unverified.

The Release solution build succeeded with zero warnings and errors for this
change. Tests and mechanics regressions were not run, so controlled mechanics
remain unverified. See the [desired RNG migration row](DESIRED_RNG_MIGRATION_STATUS.md)
for the current site status.

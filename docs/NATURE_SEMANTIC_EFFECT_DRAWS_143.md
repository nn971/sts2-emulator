# Nature #143 — card-effect draw continuation

The semantic playable pilot now suspends a Nature draw that occurs inside a
card's queued effects. The current action adapter admits **Backflip, Quick
Slash, Adrenaline and Escape Plan** for this path. Their existing card effects
run through `PrototypeGameEngine`; the physical card is selected from the
current draw support before it enters the hand, and the source draw hook runs
once after the identity is committed.

## Saved continuation

`PendingCombatChoiceState.ControlledSemanticDraw` stores the remaining draw
count, the original `FromHandDraw` flag, event depth and drawn-card keyword
override. Its outer choice continuation keeps the source card, queued effect
suffix, card-play completion event and play-series state. Resolving one typed
`semantic_draw` event commits that card identity, continues the same
`DrawCards` invocation, and either publishes the next draw event or resumes
the original effect queue. Snapshot restoration replays each draw event in
order from the player action history.

The internal choice is valid only with a pending Nature draw at the top of the
physical and semantic draw piles. Combat and semantic-state invariants reject
other controlled-choice metadata, missing supports, and conflicting pilot
boundaries. The event uses the existing semantic draw schema and reports
`probabilityStatus=UNVERIFIED`.

## Current limits

This covers direct draws whose draw hooks do not open another choice. Draws
that trigger randomized drawn-card costs, pile recycling, Stratagem selection,
random post-shuffle card insertion or a nested `CardDrawn` choice still fail
closed until those continuation stages compose with the saved draw frame.
Cards that draw and then prompt for a follow-up selection also need nested
choice resolution to resume their outer effect continuation. Native source
probability laws remain unverified.

The player-action fixture and card allowlists remain opt-in; this continuation
does not broaden source-authentic loadout acquisition or the frozen all-Act
Silent entry point.

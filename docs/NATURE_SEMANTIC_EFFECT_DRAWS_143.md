# Nature #143 — card-effect draw continuation

The semantic playable pilot now suspends a Nature draw that occurs inside a
card's queued effects. Direct-draw actions currently include **Backflip, Quick
Slash, Adrenaline, Escape Plan, Expertise, Reflex, Restlessness and Thinking
Ahead**. It also
supports **Acrobatics, Dagger Throw and Prepared**, which open an ordinary
`select_cards` prompt after their draw. Their existing card effects run through
`PrototypeGameEngine`; the physical card is selected from the current draw
support before it enters the hand, and the source draw hook runs once after
the identity is committed.

## Saved continuation

`PendingCombatChoiceState.ControlledSemanticDraw` stores the remaining draw
count, the original `FromHandDraw` flag, event depth and drawn-card keyword
override. Its outer choice continuation keeps the source card, queued effect
suffix, card-play completion event and play-series state. Resolving one typed
`semantic_draw` event commits that card identity, continues the same
`DrawCards` invocation, and either publishes the next draw event or resumes
the original effect queue. Snapshot restoration replays each draw event in
order from the player action history.

When the resumed effect queue reaches one of the supported post-draw discard
choices, the engine publishes its usual semantic card-selection event. The
player's legal `select_cards` action then resumes the original source choice
and completes the played card. If that queued effect defeated the final enemy,
the choice can finish at the genuine reward boundary and records the reward RNG
audit from the pre-reward state. If a draw or discard hook creates another
unsupported nested boundary, the guarded action fails closed.

Standard choices opened by a `CardDrawn` subscriber now compose with this
frame. The pending choice retains the remainder of its event dispatch; an
outer queued `DrawCards` operation retains the unprocessed draw count, delayed
keyword updates, source effect suffix and any enclosing event continuation.
After the choice and event chain finish, the remaining draw resumes once.

Draw-time randomized energy costs now use the existing typed semantic cost
event for Snecko-style relic costs and Slither overrides. The selected card
identity is committed first, then cost choices resolve before `CardDrawn`;
the remaining draw and source effect queue stay saved across both stages.
Drawn-card keyword overrides are applied after the entire draw operation and
its draw hooks complete, including when a cost event interrupts the operation.

The internal choice is valid only with a pending Nature draw at the top of the
physical and semantic draw piles. Combat and semantic-state invariants reject
other controlled-choice metadata, missing supports, and conflicting pilot
boundaries. The event uses the existing semantic draw schema and reports
`probabilityStatus=UNVERIFIED`.

## Current limits

The saved draw frame crosses a nonempty-discard recycle through the existing
`RecycleNature` epoch and preserves the Perfect Fit top-pool constraint.
When an after-shuffle relic inserts a generated card, the existing semantic
draw-insertion event suspends the frame and resumes it after the chosen
position is committed. Stratagem reuses the existing semantic `select_cards`
event before the remaining draw resumes. Standard serialized `PendingChoice`
events raised by `CardDrawn` hooks compose, including repeated nested choices.
Knowledge Demon choices and nested choices in standalone or automatic
draw-pipeline frames remain outside this card-effect continuation. The
automatic hand-draw continuation separately supports after-shuffle insertion;
other draw-pipeline recycle contexts remain outside the saved frames. Native
source probability laws remain unverified.

The player-action fixture and card allowlists remain opt-in; this continuation
does not broaden source-authentic loadout acquisition or the frozen all-Act
Silent entry point.

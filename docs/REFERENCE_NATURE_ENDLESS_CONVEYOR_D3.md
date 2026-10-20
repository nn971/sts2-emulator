# ReferenceNature Endless Conveyor law and session contract

This contract covers weighted dish rolls during configured Event-node entry or
an active Endless Conveyor continuation, the first-dish Observe Chef upgrade,
Spicy Snappy's random upgrade, and Fried Eel's Colorless-card draw. Ordinary
event selection remains on its existing provider. Entry coverage is available
only when that selection resolves to Endless Conveyor. An active continuation
must be a current legal action that reaches the described stochastic boundary.
It also covers the uniform potion model request made by a legal Suspicious
Condiment Grab. That action commits the queued potion and Nature cursor
atomically; resolving or skipping the potion and rolling the next dish remain a
separate player action and session.

## Dish law

`ChanceLawEngineV111.QueryNativeEndlessConveyorDishEntry` accepts a stable
action ID. In map-choice, it must identify a legal Event-node action. In an
active event, it must identify a legal action whose ordinary continuation
reaches one weighted dish ticket. The query probes the action on a fork,
captures the typed unit-float request and pre-draw event/player state, then
builds the law without using the sampled dish outcome. The source run is
unchanged. The initial roll is ordinal zero; later ordinals come from the
captured Native event cursor, or are checked against `ordinal` when supplied.
A map action that enters another event, an action with no weighted roll, and
the forced fifth Seapunk Salad have no dish law.

The support and integer weights follow the source-pinned candidate order:

| Dish | Weight | Eligibility |
| --- | ---: | --- |
| Caviar | 6 | Always |
| Spicy Snappy | 3 | Always |
| Jelly Liver | 3 | Always |
| Fried Eel | 3 | Always |
| Suspicious Condiment | 3 | At least one potion slot is empty |
| Clam Roll | 6 | Player is below max HP |

Golden Fysh is absent on the first roll and can enter later supports. The
forced fifth Seapunk Salad is draw-free. The previous dish, dish count, HP,
max HP, empty potion slots, action, pre-roll Native cursor, ordered weighted
support, and typed request bind the conditioning fingerprint. Outcome IDs are
`dish:<id>`; the listed integers are the source ticket-category weights. The
law maps each rounded Native unit float through the source's float multiply
and sequential ticket subtraction, then counts each dish's exact preimages
on the 53-bit `NextDouble` grid. Those mapping weights sum to `2^53` and are
`SOURCE_VERIFIED` for the declared ordered support. Candidate eligibility and
completeness, stream attribution, seed frequencies, and Native cursor parity
remain separate.

## Card index laws

`QueryNativeEndlessConveyorUpgrade` handles two boundaries: first-dish Observe
and a legal Grab on Spicy Snappy. Both use the deck-order
`CanSelectEventDeckCard(Upgrade)` filter, preserve physical card-instance
identities, and bind exact Native bounded-index grid masses to the persisted
`ENDLESS_CONVEYOR` cursor and request ordinal. Observe upgrades one eligible
card and leaves the event. Spicy Snappy upgrades one eligible card before the
next dish ticket. An empty eligible pool skips the index draw; a singleton
pool still calls the Native index boundary. Outcome IDs pair each card
instance with its source index.

`QueryNativeEndlessConveyorColorlessCard` handles a legal Fried Eel Grab. Its
ordered support is the implemented, solo-safe Colorless catalog used by the
mechanics. The outcomes pair each card model with its source index and use
the exact preimage counts for Native `NextInt(supportCount)` on the 53-bit
`NextDouble` grid, including binary64 multiplication rounding. This
bounded-index transform is `SOURCE_VERIFIED` on the declared support. The
current pool excludes multiplayer-only and mechanics-unimplemented catalog
entries, so native pool completeness and source-order fidelity remain open.

## Suspicious Condiment potion model

The legal Grab action uses `potion.uniform_model.index_draw` on the `reward`
stream, with the source-ordered `NativePotionPoolOrder` and owner
`event:endless_conveyor:dish:<dish-count>:potion`. Its request ordinal follows
the saved legacy `reward` stream call count. The Chance Law gives equal weights
to the complete ordered emulator pool and fingerprints the owner, ordinal, and
pool; equal weighting and Native pool completeness remain `UNVERIFIED`.

The Endless Conveyor session covers this one index request only. Replay checks
that the ordinary action queues the Nature-selected potion, retains the pending
next-dish roll, preserves potion slots and gold mechanics, and leaves both the
legacy stream and Native event cursor unchanged. After the player takes or
skips the potion, a separate session handles the deferred dish ticket.

The player-visible event projection may expose these ordered supports only
through the Nature choice view. It does not expose deck order, card pools, or
the Native cursor in the ordinary event projection.

## Atomic event-specific continuation

`ReferenceNatureEndlessConveyorSessionV111` exposes these boundaries through
the JSONL and Python session APIs. Begin requires a current legal action. A
map-entry session samples one dish ticket. A first-dish Observe session
samples one upgrade index. A Spicy Snappy Grab with an eligible card samples
the upgrade index followed by the next weighted dish ticket; with an empty
upgrade pool it samples only the ticket. A Fried Eel Grab samples the
Colorless-card index followed by the next weighted dish ticket.
For Suspicious Condiment, the legal Grab samples its uniform potion model
index and commits the pending potion reward before the player takes or skips
it. That later action can then be handled by a separate next-dish session.

For multi-draw Grabs, the session samples the typed index and float laws in
request order, keeps the playable run at its origin while the prefix is
pending, then replays the complete ordinary action with both requests scripted.
It verifies the selected upgrade or appended Colorless card and the resulting
dish. The event mechanics and ReferenceNature cursor commit together. The
Native `ENDLESS_CONVEYOR` cursor remains at its pre-action position. The same
atomic commit applies to single-boundary Observe, dish, and Condiment-potion
sessions. Fork, save/restore, replay validation, release, capability coverage,
and the Nature-only pending choice view are supported.

Jelly Liver retains the scripted marker while its grab prompt is pending.
Suspicious Condiment's Nature Grab also retains the marker while its potion
prompt is pending; after the player takes or skips the potion, the separate
Nature dish session clears it when the deferred roll completes. An ordinary
Native Observe clears the marker after the upgrade action; a scripted Nature
Observe retains it while preserving the Native cursor.

## Limits

Each session covers at most one next-dish ticket, or the Suspicious Condiment
potion index at its queued-reward boundary. A ticket session can include one
preceding Spicy Snappy upgrade or Fried Eel Colorless-card index in the same
Grab action. Other random requests in the action keep their existing
providers. The forced fifth Seapunk Salad is draw-free. The pinned event source
audit remains authoritative for dish effects and roll timing.

Observe covers only the first dish and requires a nonempty upgradable-card
support. Empty support and later-page Observe actions are outside this Nature
boundary. Fried Eel currently uses the restricted implemented solo-safe
Colorless catalog. Native seeded-result parity and exact source pool fidelity
remain open. The upgrade and Colorless card-index transforms are
`SOURCE_VERIFIED` on their declared supports. Dish-ticket mapping weights are
`SOURCE_VERIFIED` for the declared weighted support; candidate-pool fidelity,
stream attribution, seed frequencies, and cursor parity remain open. Suspicious
Condiment's full-pool potion model remains `UNVERIFIED`.

Actions that do not produce one of the covered draws, or that trigger the
forced fifth dish, are unsupported as Nature choices. Ordinary event
mechanics continue through their existing providers at those boundaries.

# Native event-context stochastic contract

This contract describes the event-local RNG lifecycle shared by migration
rows whose owner names `context.event.*` and that link this document. The
migration matrix remains the site index and retains each row's exact owner,
ordered support, occurrence, and source-audit status. A small number of rows,
including Hungry for Mushrooms, Tinker Time, Battleworn Dummy, Endless Conveyor,
Fake Merchant, the Doors Light shuffle, Reflections, Tablet of Truth, Trial,
and Trash Heap rewards, have narrower source and/or
ReferenceNature contracts linked from their own rows.

## Provider and private state

For configured Native runs,
[`NativeEventContextRngV111`](../src/Sts2Emulator.Core/NativeEventContextRngV111.cs)
creates the `EventModel.Begin`-compatible stream. Index, bounded-integer,
unit-float, and shuffle helpers advance that event stream and write its new
snapshot back to `EventState.NativeEventRng`. When no replacement provider
supplies a logical ordinal, index, bounded-integer, and unit-float requests
use the event stream's current call count; scripted scopes retain their
logical prefix ordinal while leaving the Native cursor unchanged. Legacy index,
bounded-integer, and shuffle
calls retain the prototype `event` stream; legacy integer substitutes for
Native event unit floats are identified in their migration rows. The event
instance, page/action stage, prior event effects, and the current ordered
eligible support constrain each occurrence; the per-site matrix row records
which of these inputs is active for that site. Pool identities, ordering, and
stream state are private generation constraints.

The helpers do not turn a primitive request into a complete semantic law.
An event's ordered candidates can depend on its page, the player's deck or
inventory, prior selections, and survival or eligibility gates. The row's
source audit remains authoritative for those details. A missing native event
stream can only be reconstructed at the untouched entry page; progressed
events reject reconstruction rather than replaying an unknown prefix.

## Audience and resolver boundary

The player-visible event view contains the current event page, legal choices,
and committed effect or reward. It omits candidate-pool order, seed, and RNG
cursor. The event's persisted stream snapshot and committed stochastic
values belong to the privileged state; they are not Nature observations.
There is no family-wide Nature-view Chance Law or atomic ReferenceNature
session for the rows linked here. Site-specific sessions for Endless
Conveyor, Hungry for Mushrooms, Tinker Time, Battleworn Dummy, Fake Merchant,
the Doors Light shuffle, Reflections, Tablet of Truth, Trial, and Trash Heap
rewards remain limited to the exact event boundaries
documented in their contracts. Existing controlled event-choice and
Ancient-offer sessions do not implicitly cover an event merely because it
uses this RNG helper.

Endless Conveyor exposes read-only weighted dish laws, a first-dish Observe
upgrade law, a Spicy Snappy upgrade law, and a Fried Eel Colorless-card law.
Dish queries condition on the ordinary event-selection result and pre-draw
weights without inspecting the sampled float outcome; card queries bind their
ordered `choose_index` supports to the persisted event cursor. The existing
event-specific session can sample a Spicy upgrade or Fried Eel card index
before the next dish ticket, then replay both typed requests in one atomic
continuation ([Endless Conveyor contract](REFERENCE_NATURE_ENDLESS_CONVEYOR_D3.md)).

Reflections exposes read-only conditional laws for its downgrade and upgrade
card-pick sites. The query rebuilds the projected deck and remaining ordered
physical-card support from the complete prior index prefix, including the
eligibility changes caused by prior downgrades. Its index outcomes use exact
Native `NextInt(supportCount)` preimage counts on the uniform 53-bit
`NextDouble` grid, including binary64 multiplication rounding. The atomic
`ReferenceNatureGloryReflectionsSessionV111` uses these laws as an ordered
prefix, then replays the ordinary `touch_mirror` action once all picks are
resolved and commits its mechanic result with the Nature cursor. The Native
event cursor remains at entry under scripted replay. The session is limited to
the pending configured action when at least one card-pick draw exists.

Pinned Native event gold index formulas for Dense Vegetation, Whispering
Hollow, both Sunken Statues, Luminous Choir, Sunken Treasury, Punch Off, and
This or That expose read-only conditional laws. A caller supplies `event_id`
as a declared branch condition; the query validates that the event belongs to
the configured route and constructs the typed pre-draw request without
running event selection or inspecting a Native realization, seed, or cursor.
It counts exact `NextInt` preimages and aggregates source indices that map to
the same gold value. Lost Wisp and Jungle Maze Adventure likewise expose their three
unit-float formulas, counting 53-bit `NextDouble`-grid preimages after
`NextFloat` rounding, source float arithmetic, and integer truncation. These
declared Native transforms are `SOURCE_VERIFIED` under the uniform-grid input
model. These are conditional gold mappings, not a complete Event-node law or
an atomic event-entry session. Event-pool completeness, Native event seed
frequencies, stream attribution, and cursor parity remain open. Legacy
integer fallbacks and adjacent unsupported source alternatives remain
`UNVERIFIED`.

Stone of All Time's `push_ticket` and `lift_ticket` each have a read-only
one-outcome law at the legal active-event choice boundary. The query conditions
on the requested `push` or `lift` action and confirms that it is currently
legal; all 100 bounded indices map to `no_effect`. This mapping is
`SOURCE_VERIFIED` from the pinned resolver, which discards the selected index.
The law does not inspect the saved Native cursor and does not create a
ReferenceNature event session: the ordinary event action continues to own the
source cursor draw. Legacy fallback cursor and result behavior remain outside
this claim.

The resolver is the event-specific page-generation or choice-effect method.
It applies the selected primitive result synchronously, commits the resulting
page/effect into
[`EventMechanicsStateV111`](../src/Sts2Emulator.Core/EventMechanicsStateV111.cs),
and carries the event provider state through
[`EventStochasticStateV111`](../src/Sts2Emulator.Core/EventStochasticStateV111.cs).
These helpers do not themselves save a pending Nature request or a replayable
mechanic continuation. Site-specific sessions keep their scripted transcript
at the owning event boundary, then invoke the existing source-owned event
effect once.

## Provenance and unsupported handling

The Reflections downgrade and upgrade, Infested Automaton card-pick, Endless
Conveyor card-index, and pinned Native generated-gold index laws mark their
bounded-index transform `SOURCE_VERIFIED` on the declared support. Lost Wisp
and Jungle Maze Adventure gold laws mark their unit-float-to-integer mappings
`SOURCE_VERIFIED` on the three declared formulas. Infested Automaton's pool remains restricted to
supported cards, and Endless Conveyor's Colorless pool remains restricted to
implemented solo-safe models. A pinned event RNG seed
derivation and a persisted cursor establish provider ownership, not seed
frequencies, source-pool completeness, or call-order parity. Other remaining
linked rows stay **UNVERIFIED**; no event helper match or source-shaped
candidate pool is by itself evidence of calibrated `p`.

The current Native event path may continue to resolve a source-owned event
with its configured provider. Rows without a linked site-specific contract
remain unsupported for a controlled Nature request and must fail closed; they
must not silently fall back to a different event or prototype stream.
Acceptance requires site-specific pre-outcome support and constraint
fingerprints, a declared law and provenance, an ordinary event resolver
replay, and an atomic event/run snapshot boundary.

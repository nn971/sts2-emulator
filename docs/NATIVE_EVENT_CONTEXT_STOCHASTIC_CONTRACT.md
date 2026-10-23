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
ordered `choose_index` supports to the request stream and ordinal. Native
queries do not inspect the saved event cursor, while legacy fallback queries
derive the request ordinal from the saved `event` call count. The existing
event-specific session can sample a Spicy upgrade or Fried Eel card index
before the next dish ticket, then replay both typed requests in one atomic
continuation ([Endless Conveyor contract](REFERENCE_NATURE_ENDLESS_CONVEYOR_D3.md)).

Reflections exposes read-only conditional laws for its downgrade and upgrade
card-pick sites. The query rebuilds the projected deck and remaining ordered
physical-card support from the complete prior index prefix, including the
eligibility changes caused by prior downgrades. Configured Native index
outcomes use exact `NextInt(supportCount)` preimage counts on the uniform
53-bit `NextDouble` grid, including binary64 multiplication rounding. The
atomic `ReferenceNatureGloryReflectionsSessionV111` uses these laws as an
ordered prefix, then replays the ordinary `touch_mirror` action once all picks
are resolved and commits its mechanic result with the Nature cursor. Native
replay preserves the event cursor. Native-regional runs without
`NativeRunRng` use the saved legacy `event` call count plus the prefix length
for typed request ordinals, with equal-index `UNVERIFIED` laws; the selected
legacy calls remain unadvanced. The session is limited to the pending action
when at least one card-pick draw exists.

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
model. `ReferenceNatureNativeEventGoldSessionV111` consumes the supported index and
float mappings while replaying configured Event-node entry for the mapped
source events. The same atomic session also consumes Stone of All Time's
occupied-potion-slot draw and The Future of Potions' ordered card-type draws,
using their existing conditional laws. It suspends only at those declared
entry requests; event selection and other requests keep their existing
providers. See the
[Native event-entry session contract](REFERENCE_NATURE_NATIVE_EVENT_GOLD_D3.md).
This does not establish complete event-pool support, Native event seed
frequencies, stream attribution, or cursor parity. Legacy integer fallbacks
and adjacent unsupported source alternatives remain `UNVERIFIED`.

When the Native entry session chooses Stone of All Time's potion slot, the
existing push/lift continuation remains usable. Its next event-stream request
keeps source ordinal 1, and the existing event-choice Nature session can
control that ticket. A later ordinary Native draw clears the scripted entry
marker after advancing the context stream; Native cursor parity remains open.

Stone of All Time's `push_ticket` and `lift_ticket` each have a read-only
one-outcome law at the legal active-event choice boundary. On Native runs both
tickets are at event-stream ordinal 1 because the entry potion-slot draw owns
ordinal 0. On the retained legacy route the query binds the caller-supplied
ordinal as source-call metadata without reading the current `event` stream
call count. The query conditions on the requested action and confirms that it
is currently legal; all 100 bounded indices map to `no_effect`. This mapping
is `SOURCE_VERIFIED` from the pinned resolver, which discards the selected
index. Replay matches the typed request to its saved boundary and ordered
prefix. Neither route's RNG state is advanced by the query.

Both Native tickets use the existing atomic controlled event-choice session.
Lift suspends from the untouched pre-choice state, then replays the ordinary
lift action after Nature selects `no_effect`; potion removal, Max HP, and the
terminal page commit together. Push first applies its HP loss and offers the
source deck-card choice. The session records that player choice, then samples
the ticket and commits the terminal page without replaying the choice. A lethal
HP loss ends the run before the source ticket. Native controlled outcomes
leave the event cursor at its post-entry-slot value. The same session also
admits the retained legacy fallback under its `event` stream request; it does
not advance that cursor either. Neither route claims seeded cursor parity.

The entry-time `potion_slot.index_draw` also has a pre-entry conditional law.
The caller supplies the legal Event-node action and explicitly declares Stone
of All Time as the event branch; the query checks route eligibility and uses
the ordered occupied potion-slot support. Native uses exact `NextInt` grid
masses and is `SOURCE_VERIFIED`; the legacy ReferenceNature route uses equal
weights and remains `UNVERIFIED`. Native ordinal is 0; the legacy request
ordinal follows the event-selection ticket but is supplied explicitly rather
than derived from the saved `event` stream call count. The query does not
inspect the later event-selection result or create an entry session. Event
selection, seed frequencies, stream attribution, and Native cursor parity
remain separate claims.

The Future of Potions `card_type.index_draw` has one pre-entry law per owned
potion, ordered by slot and conditioned on that explicit event branch. Each
support is the source-shaped Attack/Skill pair for a Common potion or
Attack/Skill/Power trio for an Uncommon or Rare potion. The query binds the
prior request prefix and counts exact Native `NextInt` preimages on the
declared support; it neither resolves Event selection nor reads a sampled
card-type result. The query does not create a session. The existing atomic
Native event-entry session scripts and replays these ordered offers on the
configured Native and captured Native-regional legacy routes; see the
[entry session contract](REFERENCE_NATURE_NATIVE_EVENT_GOLD_D3.md#boundary).

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

Configured Native Reflections downgrade and upgrade, configured Native
Infested Automaton card-pick, configured Native Endless Conveyor card-index,
and pinned Native generated-gold index laws mark their bounded-index
transform `SOURCE_VERIFIED` on the declared support. Lost Wisp
and Jungle Maze Adventure gold laws mark their unit-float-to-integer mappings
`SOURCE_VERIFIED` on the three declared formulas. The Trial's three-case
bounded-index transform is `SOURCE_VERIFIED`; its Chance Law query does not
inspect the event cursor, while atomic-session admission separately requires
the untouched entry cursor for configured Native replay. Native-regional
fallback without `NativeRunRng` derives its typed request ordinal from the
saved legacy `event` call count, uses equal-index `UNVERIFIED` weights, and
leaves the selected request unadvanced without creating a Native cursor or
provider marker. Stone of All Time's two ignored ticket
results map to a single `no_effect` outcome, and its potion-slot index is
source-verified on the occupied-slot support. Future of Potions card-type
indices are source-verified for each declared rarity-derived support. Infested
Automaton's pool remains restricted to supported cards, and Endless
Conveyor's Colorless pool remains restricted to implemented solo-safe models.
A source-ordered Trash Heap card or relic pool has exact bounded-index
preimages. Configured Native queries do not read the Native event cursor;
Native-regional fallback queries read the legacy `event` call count only to
derive the typed request ordinal. The atomic session validates its replay
origin before applying the choice.
Native-regional Infested Automaton sessions also accept the legacy event
stream fallback when `NativeRunRng` is absent. Its law assigns equal weights
to the saved candidate support and remains `UNVERIFIED`; replay leaves the
Nature-selected legacy request unadvanced. The JSONL card-pick query still
uses semantic choice-draw ordinal 0; the returned law carries the saved
legacy request ordinal.
Tablet of Truth also counts exact bounded-index preimages for each ordered
eligible physical-card support without using the run seed or saved cursor in
the configured Native law; its page-by-page scripted session preserves the
cursor captured at that page's boundary. Native-regional fallback derives its
typed request ordinal from the saved legacy `event` call count, uses
equal-index `UNVERIFIED` weights, and leaves the selected request unadvanced
without creating a Native cursor or provider marker.
Doors of Light and Dark, Fragrant Mushroom, both Tinker Time shuffle stages,
and Battleworn Dummy setting 2 also admit Native-regional fallback without
`NativeRunRng`. Their shuffle laws use equal-index `UNVERIFIED` weights, derive
typed request ordinals from the saved legacy `event` call count plus the swap
offset, and leave selected calls unadvanced without creating a Native event
cursor or provider marker.
A pinned event RNG seed
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

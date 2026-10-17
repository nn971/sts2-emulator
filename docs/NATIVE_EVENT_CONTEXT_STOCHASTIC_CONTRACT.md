# Native event-context stochastic contract

This contract describes the event-local RNG lifecycle shared by migration
rows whose owner names `context.event.*` and that link this document. The
migration matrix remains the site index and retains each row's exact owner,
ordered support, occurrence, and source-audit status. A small number of rows,
including Tinker Time, the Doors Light shuffle, Tablet of Truth, Trial, and
Trash Heap rewards, have narrower source and/or ReferenceNature contracts
linked from their own rows.

## Provider and private state

For configured Native runs,
[`NativeEventContextRngV111`](../src/Sts2Emulator.Core/NativeEventContextRngV111.cs)
creates the `EventModel.Begin`-compatible stream. Index, bounded-integer,
unit-float, and shuffle helpers advance that event stream and write its new
snapshot back to `EventState.NativeEventRng`. Legacy index, bounded-integer, and shuffle
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
session for the rows linked here. Site-specific sessions such as Tinker Time,
the Doors Light shuffle, Tablet of Truth, Trial, and Trash Heap rewards remain
limited to the exact event boundaries documented in their contracts. Existing
controlled event-choice and Ancient-offer sessions do not implicitly cover an
event merely because it uses this RNG helper.

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

Every linked row still marks its probability law **UNVERIFIED**. A pinned
event RNG seed derivation and a persisted cursor establish provider ownership,
not conditional probabilities or call-order parity. No event helper match or
source-shaped candidate pool is evidence of calibrated `p`.

The current Native event path may continue to resolve a source-owned event
with its configured provider. Rows without a linked site-specific contract
remain unsupported for a controlled Nature request and must fail closed; they
must not silently fall back to a different event or prototype stream.
Acceptance requires site-specific pre-outcome support and constraint
fingerprints, a declared law and provenance, an ordinary event resolver
replay, and an atomic event/run snapshot boundary.

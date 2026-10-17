# ReferenceNature Punch Off fight-entry contract

`ReferenceNaturePunchOffFightSessionV111` controls only the two construct HP
offsets when the configured Act 1 Punch Off event enters its fight.

## Boundary and support

Begin on the uncommitted Fight page of the live configured Punch Off event and
provide the stable ID of its legal `fight` action. A Native-provider probe
must observe exactly two requests at
`event.punch_off.construct_hp_offset.index_draw`, in construct order. Each
request must be a bounded integer with support 0 through 7, ordinal 0, and
owner `event:punch_off:construct_combat_id:<CombatId>`. The request stream must
be the persisted `context.monster.<CombatId>` stream for that creature. The
chance law presents the corresponding 2–9 HP reductions as `hp_offset:`
outcomes with equal weights. The mapping `NextInt(8) = (int)(NextDouble() * 8)`
is source-audited, and the 53-bit `NextDouble` grid divides into eight bins of
exactly `2^50` values each, so this bounded-index law is **SOURCE_VERIFIED**
under the uniform-grid input model. This does not verify run-seed frequencies,
monster-context seed derivation, or seeded result/cursor parity.

Queries require the complete preceding offset-index prefix and replay the
fight action on a fork to recheck current eligibility and request ownership.
Legacy fights, other events, delayed mechanics boundaries, changed request
shape, and any state that cannot produce exactly two matching monster-local
requests fail closed.

## Atomic replay

The run stays private until both offsets are sampled. Replay installs the
selective index provider for only the two Punch Off HP-offset sites. Other
requests during combat entry go through the stream's existing Native or
legacy provider. The ordinary `fight` action applies the reductions and
starts combat; no event or combat hooks are replayed separately.

Replay verifies both resulting enemy HP values and opening moves, the two
request identities and selected indices, the unchanged monster-local
cursors, the run-stream cursors observed by the Native probe, and the event
cursor. The combat state and ReferenceNature cursor commit together. A later
Punch Off reward session is a separate boundary after the player wins.

The contract does not claim exact Native monster context derivation or seeded
result/cursor parity. Those require the separate fidelity work tracked by the
migration matrix.

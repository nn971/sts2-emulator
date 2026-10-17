# ReferenceNature native event shuffle contract

This contract records the first source-independent ReferenceNature session
for a full Native event-local Fisher–Yates shuffle. Its initial supported
resolver is the Light option in Doors of Light and Dark.

## Boundary

The session accepts a configured v0.111 run at the pending Doors of Light and
Dark initial page when `light` is legal and at least two deck instances can be
upgraded. It uses the existing event resolver's eligible-card order, event
stream, owner ID, and shuffle site. A pool of zero or one card performs no
shuffle draw and has no Chance Law session. The deterministic Dark option is
outside this boundary.

## Laws and replay

Each pending law represents one descending Fisher–Yates swap. Its bounded
integer support is `0..(candidate count - ordinal - 1)`, and its conditioning
fingerprint includes the ordered eligible card identities and every preceding
swap index. Outcomes use stable `swap_index:<index>` identifiers and equal
integer weights. The probability status stays `UNVERIFIED`.

`ReferenceNatureNativeEventShuffleSessionV111` stores the original run and
sampler cursor while outcomes are pending. Once every swap has been sampled,
it replays the ordinary `light` event action through the existing scripted
Native event-index resolver. The run result and ReferenceNature cursor commit
together. The event's Native stream remains at its entry cursor and the saved
`scripted-v111` marker is validated against the terminal Light result page.
Fork, status, save, and restore preserve and revalidate the complete transcript.

## Limits

This does not establish source probability calibration, live-game parity, or
Native seeded result parity. It does not add event-specific shuffle mechanics:
future event shuffles can use the same session only when their existing
resolver's conditioning plan and action replay are explicitly admitted. The
Doors source audit remains the authority for its modeled eligibility and
event flow.

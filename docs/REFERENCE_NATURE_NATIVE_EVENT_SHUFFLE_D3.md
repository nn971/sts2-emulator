# ReferenceNature native event shuffle contract

This contract records the first source-independent ReferenceNature session
for a full Native event-local Fisher–Yates shuffle. The supported resolvers
are the Light option in Doors of Light and Dark, Fragrant Mushroom's eligible
card shuffle, and Tinker Time's type and rider offer stages.

## Boundary

The session accepts a configured v0.111 run at the pending Doors of Light and
Dark initial page when `light` is legal and at least two deck instances can be
upgraded. It accepts Fragrant Mushroom's initial choice only when the player
survives its 15 HP cost and at least two deck instances are eligible. It also
accepts Tinker Time's initial `choose_card_type` action and, after that
scripted Nature result, one of the offered type choices on the next page. The
Tinker rider session replays that type choice, which both selects the type and
creates its rider offers. It uses the existing event resolver's candidate
order, event stream, owner ID, and shuffle site. A pool of zero or one
candidate performs no shuffle draw and has no Chance Law session. The
deterministic Doors Dark option and later player choice of a Tinker rider are
outside this boundary.

## Laws and replay

Each pending law represents one descending Fisher–Yates swap. Its bounded
integer support is `0..(candidate count - ordinal - 1)`, and its conditioning
fingerprint includes the ordered eligible card identities and every preceding
swap index. Outcomes use stable `swap_index:<index>` identifiers and equal
integer weights. The probability status stays `UNVERIFIED`.

The Fragrant Mushroom, Tinker type, and Tinker rider shuffle sites have
separate laws. A rider law is
conditioned on the prior scripted type shuffle and the currently offered type;
an ordinary Native Tinker offer cannot be treated as the missing Nature prefix.

`ReferenceNatureNativeEventShuffleSessionV111` stores the original run and
sampler cursor while outcomes are pending. Once every swap has been sampled,
it replays the ordinary event action through the existing scripted Native
event-index resolver. The run result and ReferenceNature cursor commit
together. The event's Native stream remains at its entry cursor and the saved
`scripted-v111` marker is validated against the terminal Light result page or
Tinker offer page. The stream stays at entry across the two scripted Tinker
stages. Fork, status, save, and restore preserve and revalidate the transcript.

## Limits

This does not establish source probability calibration, live-game parity, or
Native seeded result parity. It does not add event-specific shuffle mechanics:
future event shuffles can use the same session only when their existing
resolver's conditioning plan and action replay are explicitly admitted. The
Doors and Tinker source audits remain authoritative for their modeled
eligibility and event flow.

# ReferenceNature native event shuffle contract

This contract records the first source-independent ReferenceNature session
for a full Native event-local Fisher–Yates shuffle. The supported resolvers
are the Light option in Doors of Light and Dark, Fragrant Mushroom's eligible
card shuffle, Tinker Time's type and rider offer stages, and Battleworn
Dummy's setting 2 upgrade shuffle. It also covers Fake Merchant's six-of-nine
relic stock shuffle when a configured Event-node action's ordinary event
selection resolves to Fake Merchant.

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

Battleworn accepts a legal combat action only from a setting 2 fight where
that action immediately defeats the remaining enemies and completes the event
with victory. The session samples only the upgrade-card shuffle. During replay,
other combat index requests keep the provider associated with their stream
codec, while the Battleworn event shuffle uses the scripted Nature prefix.

Fake Merchant accepts a legal Event-node action from the map-choice phase.
The ordinary weighted event-selection request is replayed through its existing
provider; admission requires that it select Fake Merchant. The session then
samples only the six-of-nine stock-relic shuffle. During replay, other index
requests use the existing provider associated with their stream codec, while
the Fake Merchant stock shuffle uses the scripted Nature prefix. A map action
that selects another event is outside this session boundary.

## Laws and replay

The player-visible event projection exposes the resulting page, offers, and
effects. The Nature-only choice view exposes the pending conditional shuffle
law; seeds, Native cursor state, and the unrevealed pool order remain in the
privileged state.

Each pending law represents one descending Fisher–Yates swap. Its bounded
integer support is `0..(candidate count - ordinal - 1)`, and its conditioning
fingerprint includes the ordered eligible card identities and every preceding
swap index. Outcomes use stable `swap_index:<index>` identifiers and equal
integer weights. The probability status stays `UNVERIFIED`.

The Fragrant Mushroom, Tinker type, Tinker rider, and Fake Merchant stock
shuffle sites have separate laws. A rider law is conditioned on the prior
scripted type shuffle and the currently offered type; an ordinary Native
Tinker offer cannot be treated as the missing Nature prefix. Fake Merchant's
law is conditioned on the legal map action and the ordinary event-selection
result being Fake Merchant, then on the ordered nine-relic stock pool and
preceding swap indices.

`ReferenceNatureNativeEventShuffleSessionV111` stores the original run and
sampler cursor while outcomes are pending. Once every swap has been sampled,
it replays the ordinary event action through the existing scripted Native
event-index resolver. The run result and ReferenceNature cursor commit
together. The event's Native stream remains at its entry cursor and the saved
`scripted-v111` marker is validated against the terminal Light result page,
Tinker offer page, Battleworn victory page, or Fake Merchant stock page. The
stream stays at entry across the two scripted Tinker stages. Fork, status,
save, and restore preserve and revalidate the transcript.

## Limits

This does not establish source probability calibration, live-game parity, or
Native seeded result parity. It does not add event-specific shuffle mechanics:
future event shuffles can use the same session only when their existing
resolver's conditioning plan and action replay are explicitly admitted. The
Doors, Tinker, Battleworn, and Fake Merchant source audits remain authoritative
for their modeled eligibility and event flow. Battleworn's session covers a
winning combat action boundary; it does not create a multi-action combat
continuation. Fake Merchant's session does not control or reinterpret the
ordinary weighted event-selection law; its availability depends on that
provider's result for the selected Event node.

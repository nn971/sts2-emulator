# ReferenceNature Native shuffle contract

This contract records source-independent ReferenceNature sessions for a full
Native Fisher–Yates shuffle. The event resolvers
are the Light option in Doors of Light and Dark, Fragrant Mushroom's eligible
card shuffle, Tinker Time's type and rider offer stages, and Battleworn
Dummy's setting 2 upgrade shuffle. It also covers Fake Merchant's six-of-nine
relic stock shuffle when a configured Event-node action's ordinary event
selection resolves to Fake Merchant, and War Historian Repy's relic offer
when its initial chest action is selected in configured native regional mode.
The run-stream resolver covers one War Hammer after an action immediately
wins an ordinary Elite fight.

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

War Hammer accepts an action only when one active War Hammer is present in an
ordinary Elite combat and the action wins immediately. The session captures
the emulator's ordered eligible-card identities and the `run.niche` cursor
immediately before the relic shuffle. It rejects actions that perform another
`run.niche` draw after that shuffle, so replay can leave the run stream at its
captured pre-shuffle cursor. Multiple hammers, a delayed victory, and pools of
zero or one eligible card remain outside this continuation.

Repy accepts only the page-zero `unlock_chest` action, before any chest reward
is generated. It reuses the source-shaped ordered unowned relic pool and
captures the `player.rewards` cursor before the shuffle. Admission probes the
ordinary action and requires exactly one matching relic shuffle followed by
two potion draws. The session conditions both potion index laws on the full
relic-swap prefix and any earlier potion index. The player-stream assignment
remains a source hypothesis, and the event has no Native event RNG context at
this boundary.

## Laws and replay

The player-visible event projection exposes the resulting page, offers, and
effects. The Nature-only choice view exposes the pending conditional shuffle
law; seeds, Native cursor state, and the unrevealed pool order remain in the
privileged state.

Each pending shuffle law represents one descending Fisher–Yates swap. Its
bounded integer support is `0..(candidate count - ordinal - 1)`, and its
conditioning fingerprint includes the ordered eligible identities and every
preceding swap index. Outcomes use stable `swap_index:<index>` identifiers
and exact integer weights. Source-backed Native shuffle laws use each index's
exact preimage count on the 53-bit `NextDouble` grid, including the
binary64-multiplication rounding boundary, and are `SOURCE_VERIFIED` under the
uniform-grid input model. Power-of-two bounds have equal weights; other bounds
can have unequal weights. This status does not establish stream
attribution, seed frequencies, or seeded parity. Repy's two potion laws expose
indices into the configured potion pool, use stable potion-and-index
identities, and include the full preceding shuffle and potion prefix; they
remain `UNVERIFIED`.

The Fragrant Mushroom, Tinker type, Tinker rider, and Fake Merchant stock
shuffle sites have separate laws. A rider law is conditioned on the prior
scripted type shuffle and the currently offered type; an ordinary Native
Tinker offer cannot be treated as the missing Nature prefix. Fake Merchant's
law is conditioned on the legal map action and the ordinary event-selection
result being Fake Merchant, then on the ordered nine-relic stock pool and
preceding swap indices.
Repy's law is conditioned on its initial chest action, ordered unowned relic
pool, captured `player.rewards` cursor, and preceding swap indices. Its source
stream attribution remains a hypothesis; its bounded-index transform law is
`SOURCE_VERIFIED` under the uniform-grid model. Its two potion-index laws use
the full relic-swap prefix and preceding potion indices; the potion picks
remain independent with replacement in the modeled support, and their source
probability provenance remains `UNVERIFIED`.

`ReferenceNatureNativeEventShuffleSessionV111` stores the original run and
sampler cursor while outcomes are pending. Once every swap has been sampled,
it replays the ordinary event action through the existing scripted Native
event-index resolver. The run result and ReferenceNature cursor commit
together. For event-context shuffles, the Native event stream remains at its
entry cursor and the saved `scripted-v111` marker is validated against the
terminal Light result page, Tinker offer page, Battleworn victory page, or
Fake Merchant stock page. The stream stays at entry across the two scripted
Tinker stages. Fork, status, save, and restore preserve and revalidate the
transcript.

For War Hammer, replay scripts only its `run.niche` Fisher–Yates requests and
leaves that stream at the captured pre-shuffle cursor. Other combat requests
retain their Native providers. War Hammer uses the emulator's current
CardId-plus-instance-ID ordering proxy; the source ModelId ordering is not
established by this law or session.

For Repy, replay scripts the relic Fisher–Yates requests and both potion
index requests as one typed prefix. The `player.rewards` cursor remains at its
captured entry state, while request ordinals preserve the source call order
across the scripted shuffle and potion picks. The event RNG field remains
absent and no `scripted-v111` event-context marker is added. This models the
emulator's current stream hypothesis; it does not establish Native source
ownership or seeded parity.

## Limits

This does not establish source probability calibration, live-game parity, or
Native seeded result parity. It does not add shuffle mechanics: future sites
can use the same session only when their existing
resolver's conditioning plan and action replay are explicitly admitted. The
Doors, Tinker, Battleworn, Fake Merchant, and War Hammer source audits remain
authoritative for their modeled eligibility and flow. Battleworn's session covers a
winning combat action boundary; it does not create a multi-action combat
continuation. Fake Merchant's session does not control or reinterpret the
ordinary weighted event-selection law; its availability depends on that
provider's result for the selected Event node.

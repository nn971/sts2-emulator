# ReferenceNature Native shuffle contract

This contract records source-independent ReferenceNature sessions for Native
Fisher–Yates shuffles and admitted combat-victory index hooks. In addition to
configured Native event streams, Doors of Light and Dark, Fragrant Mushroom,
both Tinker Time shuffle stages, and Battleworn Dummy setting 2 admit the
Native-regional legacy `event` stream when `NativeRunRng` is absent. The event resolvers
are the Light option in Doors of Light and Dark, Fragrant Mushroom's eligible
card shuffle, Tinker Time's type and rider offer stages, and Battleworn
Dummy's setting 2 upgrade shuffle. It also covers Fake Merchant's six-of-nine
relic stock shuffle when a configured Event-node action's ordinary event
selection resolves to Fake Merchant, and War Historian Repy's relic offer
when its initial chest action is selected in configured native regional mode.
The run-stream resolvers cover one War Hammer after an action immediately
wins an ordinary Elite fight and Fishing Rod's triggered combat-end upgrade
indices after an ordinary combat victory. The same saved replay also covers
Mad Science Improvement's legacy combat-stream indices after an immediate
combat victory, and Pael's Tooth's legacy reward-stream indices during its
post-combat card return.

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

Fishing Rod accepts a configured ordinary Combat-room action only when it
wins immediately and at least one non-melted Rod reaches its every-third-
combat upgrade draw. The session captures the typed request sequence from the
ordinary victory action, then recomputes each later request after applying
earlier Nature-selected upgrades. Empty candidate pools remain draw-free; the
session rejects any unexplained `run.niche` calls in the same action.

Improvement accepts a legal combat action only when it wins immediately and
the Improvement Power reaches at least one eligible-card draw. Its legacy
`combat` support is source-ordered and shrinks after each without-replacement
pick. The session recaptures later supports after earlier indices are applied
and rejects other `combat`-stream draws at the same boundary.

Pael's Tooth accepts a legal combat action only when it wins immediately and
at least one non-melted Tooth has saved cards to return. It scripts one
`reward` index per nonempty Tooth in relic-slot order, using each queue's
source-ordered physical card instances. It rejects unrelated `reward` draws
before or between those selections. Requests after the final Tooth selection,
including ordinary reward generation, keep their legacy provider and are
preserved by the replay.

## Laws and replay

The player-visible event projection exposes the resulting page, offers, and
effects. The Nature-only choice view exposes the pending conditional shuffle
law; seeds, Native cursor state, and the unrevealed pool order remain in the
privileged state.

Each pending shuffle law represents one descending Fisher–Yates swap. Its
bounded integer support is `0..(candidate count - ordinal - 1)`, and its
conditioning fingerprint includes the ordered eligible identities and every
preceding swap index. Outcomes use stable `swap_index:<index>` identifiers.
Configured Native shuffle laws use exact integer weights: each index's
exact preimage count on the 53-bit `NextDouble` grid, including the
binary64-multiplication rounding boundary, and are `SOURCE_VERIFIED` under the
uniform-grid input model. Power-of-two bounds have equal weights; other bounds
can have unequal weights. The supported Native-regional fallback laws use
equal weights over the saved legacy `event` request support and remain
`UNVERIFIED`; their typed ordinal is the saved event call count plus the swap
offset. These statuses do not establish stream
attribution, seed frequencies, or seeded parity. Repy's two potion laws expose
indices into the configured potion pool, use stable potion-and-index
identities, and include the full preceding shuffle and potion prefix. Their
bounded-index transform is `SOURCE_VERIFIED` on that declared support; the
`player.rewards` stream assignment remains a hypothesis.

Fishing Rod's pending law is the exact Native `NextInt` mass over the current
captured request's index support. Its outcome identifies an index; the
ordinary combat-end hook maps that index to the eligible deck card instance.
The transform is `SOURCE_VERIFIED` for the declared support, while relic
trigger timing, complete card eligibility, and Native owner calibration stay
separate.

Improvement's pending law is the generic uniform-index model for its saved
legacy `combat` request and remains `UNVERIFIED`. The ordinary victory hook
maps each index to one eligible physical card and removes that card from the
next request's support.

Pael's Tooth's pending law is the generic uniform-index model for its saved
legacy `reward` request and remains `UNVERIFIED`. The direct query and session
reconstruct the current queue from the selected relic slot and saved-card
identity hash; the law does not establish source probability or Native stream
ownership.

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
remain independent with replacement in the configured support and use the
same `SOURCE_VERIFIED` bounded-index transform. These statuses do not verify
stream attribution, seed frequencies, or seeded parity.

`ReferenceNatureNativeEventShuffleSessionV111` stores the original run and
sampler cursor while outcomes are pending. Once every swap has been sampled,
it replays the ordinary event action through the existing scripted Native
event-index resolver, or through a controlled legacy request prefix for the
supported fallback sites. Fallback replay leaves the selected `event` calls
unadvanced and creates no Native event cursor or provider marker. The run
result and ReferenceNature cursor commit together. For configured event-context shuffles, the Native event stream remains at its
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

For Fishing Rod, replay scripts each captured `run.niche` index request and
leaves that stream at its captured entry cursor. Earlier upgrades are applied
by the ordinary victory hook before later Rod supports are recaptured. The
session admits only an immediate ordinary-combat victory with no additional
`run.niche` calls; it does not establish source candidate-pool fidelity or
Native cursor parity.

For Improvement, replay scripts each captured `combat` index request and
leaves the legacy stream at its entry cursor. The hook's without-replacement
support is recaptured after each prior selection; the ordinary victory action
then applies all selected upgrades before the result and Nature cursor commit.

For Pael's Tooth, replay scripts each captured `reward` index request and
does not advance the legacy stream for those scripted selections. It commits
returned cards, relic saved-card queues, any later ordinary reward mechanics,
and the Nature cursor together. Additional `reward` draws after the final
Tooth return remain legacy draws and advance the legacy cursor; those draws do
not enter the Tooth Nature prefix.

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
Doors, Tinker, Battleworn, Fake Merchant, War Hammer, Fishing Rod,
Improvement, and Pael's Tooth source audits remain authoritative for their modeled eligibility
and flow. Battleworn's session covers a
winning combat action boundary; it does not create a multi-action combat
continuation. Fishing Rod, Improvement, and Pael's Tooth use their existing ordinary
victory hooks and do not establish source trigger or pool fidelity. Fake Merchant's session does not control or reinterpret the
ordinary weighted event-selection law; its availability depends on that
provider's result for the selected Event node.

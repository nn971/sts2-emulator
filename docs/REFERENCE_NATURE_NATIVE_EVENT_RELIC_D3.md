# Native event relic ReferenceNature continuation

**Snapshot:** 2026-10-21. **Status:** implemented for supported configured Native event-choice relic draws, Neow Small Capsule and Large Capsule pickups, Neow's Bones relic offers, Punch Off's `nab` relic offer, Round Tea Party, Trial, Battleworn Dummy setting-three victory, and Pael's Wing; identity-pool fidelity and Native cursor parity remain open.

## Boundary

`ReferenceNatureNativeEventRelicSessionV111` starts from a currently legal
event choice on a configured Native route. It supports choices whose effects
include one or more `GainRandomRelic` operations, and choices that acquire Toy
Box and then create its five Wax relic rewards. Each expected relic pull
contributes one rarity request and, when the remaining bag has an eligible
identity, one identity request. The ordinary event choice, relic effects,
completed reward, and ReferenceNature cursor form one saved boundary.

Two event-specific branches also use direct relic draws outside the event
effect list: Round Tea Party's `continue_fight` draw on page one, and Trial's
two draws after `merchant_guilty` on page two. They use the same replay
boundary when the current action is legal.

The same session also accepts the legal Native `take_reward_relic` action for
Large Capsule when it is offered by Neow's Bones. It captures the two
`GainRandomRelic` effects that run after the Neow relic is acquired, including
both rarity and eligible identity requests, and commits the selected relic
and its follow-up rewards with the Nature cursor. If the bag has no eligible
identity, the session resolves the rarity request and follows the source's
no-relic result without an identity request.

When Neow's Bones itself is selected from the opening offers or a Neow relic
reward, the session also captures its `OfferNeowsBonesRelics` follow-up. That
emulator path constructs the ordered eligible relic pool after adding Bones,
then selects two distinct relics by a full descending Fisher–Yates shuffle on
the legacy `reward` stream. The session records each conditional bounded
integer swap, rebuilds the pool and owner from the saved origin, and commits
the ordinary Neow result with the Nature cursor after replay. This request
uses `run.random_pool_distinct.shuffle_swap_index`; its equal-index law on
`reward` remains `UNVERIFIED` and makes no claim of Native source or cursor
parity.

The session also captures Neow's Small Capsule when selected from the opening
Neow options or from a Neow relic reward. Its `OfferRandomRelic` follow-up uses
one `unowned_relic_selection` bounded index on `player.rewards`; the request
owner fingerprints the configured relic pool after excluding the acquired
Small Capsule and all previously owned relics. The selected event choice or
reward and the Nature cursor commit together. Neow pickup effects other than
the documented Bones and Small Capsule paths remain outside this boundary.

The same session captures configured Native Punch Off's legal page-zero `nab`
choice. After appending its Injury, the ordinary action creates the one-relic
reward with `unowned_relic_selection` on `player.rewards`; the owner identifies
the event and fingerprints the ordered configured relic pool after excluding
owned relics. Replay validates this support and commits the event result and
Nature cursor together. The bounded-index transform is `SOURCE_VERIFIED` on
the declared request. Other Punch Off choices and its later combat reward are
outside this boundary.

The session also accepts a legal combat action during Battleworn Dummy's
setting-three fight. Replay must finish the combat as a victory and reach its
single relic reward draw; unsupported stochastic requests encountered earlier
in the action fail closed. The action, victory reward, event result, and Nature
cursor commit together. Setting one and setting two, multi-action fights, and
draw-free or losing combat actions are outside this boundary.

For Pael's Wing, the session accepts the current legal
`sacrifice_reward_card` or `sacrifice_reward_card_group` action when it
advances the Wing's saved sacrifice count to an even number. It captures the
resulting relic draw and commits the sacrifice and acquisition with the Nature
cursor. Other reward actions remain outside this boundary.

Event selection happens before this boundary and keeps its current provider.
The session rejects unsupported event choices and Neow pickups, Battleworn
actions outside the setting-three victory boundary, Pael's Wing actions that
do not draw a relic, and stochastic sites outside the declared request set.
Event choices with another fixed relic effect are excluded because their effect
ordering changes the live owned relic support; the exact Small Capsule pickup
is admitted with its post-acquisition support reconstructed from the selected
relic. Until all expected requests resolve, its published run remains at the
projected origin. On completion it commits the event result and Nature cursor
together.

## Law and replay

The session uses `QueryNativeRelicRarity` for each
`relic_grab_bag.rarity.unit_float` request. It maps the selected rarity back to
a representative source float within that threshold interval. The Native
threshold transform is `SOURCE_VERIFIED` for the declared `player.rewards`
request.

For each `treasure.relic_grab_bag.model.index_draw`, replay rebuilds the
canonical remaining relic bag from the origin and prior selected identities.
It validates the requested rarity, fallback tier, sorted candidate
identities, owner hash, and support before querying the exact Native
bounded-index law. The sorted Nature set deliberately omits Native queue
order. The bounded transform is `SOURCE_VERIFIED`; candidate-pool completeness
and the conditional Native identity distribution over its hidden queue remain
`UNVERIFIED`.

The Small Capsule request uses the exact Native bounded-index transform for
`unowned_relic_selection` on the declared ordered pool. Its support follows the
emulator's configured relic pool after the acquired relic is added; source-pool
completeness and Native cursor parity remain separate evidence.

The transcript follows actual source request order. An action that also
reaches another stochastic site fails closed rather than partially
committing a relic-only prefix. Each `ResolveNext()` samples the next law and
maps it to the typed Native float or index provider value, then replays the
action from its saved origin. Save/restore recomputes the complete prefix.
Forks keep independent snapshots. JSONL and Python expose begin, status,
resolve, fork, save, load, release, and the committed playable run.

## Scope limits

The session does not cover legacy event rewards, choices with additional fixed
relic effects, other Neow relic-pickup effects, other Punch Off choices or its
later combat reward, Battleworn settings one or two, or choices/actions that
request a non-relic random operation. The Neow's Bones offer shuffle remains a
legacy `reward`-stream approximation and does not establish Native source or
cursor parity. The session does not establish source-pool completeness,
hidden queue identity probabilities, event-selection parity, seeded Native
result parity, or Native cursor parity. The Native reference mod and Fidelity
Inspector remain needed for those source-evidence tasks.

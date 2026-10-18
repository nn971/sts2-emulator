# Native event relic ReferenceNature continuation

**Snapshot:** 2026-10-19. **Status:** implemented for supported configured Native event-choice relic draws; identity-pool fidelity and Native cursor parity remain open.

## Boundary

`ReferenceNatureNativeEventRelicSessionV111` starts from a currently legal
event choice on a configured Native route. It supports choices whose effects
include one or more `GainRandomRelic` operations, and choices that acquire Toy
Box and then create its five Wax relic rewards. Each expected relic pull
contributes one rarity request and one identity request. The ordinary event
choice, relic effects, completed reward, and ReferenceNature cursor form one
saved boundary.

The same session also accepts the legal Native `take_reward_relic` action for
Large Capsule when it is offered by Neow's Bones. It captures the two
`GainRandomRelic` effects that run after the Neow relic is acquired, including
both rarity and identity requests, and commits the selected relic and its
follow-up rewards with the Nature cursor.

Event selection happens before this boundary and keeps its current provider.
The session rejects another event, route, choice, Neow pickup, or stochastic
site outside the declared relic rarity and relic model requests. Event choices
with another fixed relic effect are excluded because their effect ordering
changes the live owned relic support. Other Neow pickup triggers remain outside
this boundary. Until all expected requests resolve, its published run remains
at the projected origin. On completion it commits the event result and Nature
cursor together.

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

The transcript follows actual source request order. A mixed event choice that
also reaches another stochastic site fails closed rather than partially
committing a relic-only prefix. Each `ResolveNext()` samples the next law and
maps it to the typed Native float or index provider value, then replays the
action from its saved origin. Save/restore recomputes the complete prefix.
Forks keep independent snapshots. JSONL and Python expose begin, status,
resolve, fork, save, load, release, and the committed playable run.

## Scope limits

The session does not cover legacy event rewards, choices with additional fixed
relic effects, other Neow relic-pickup effects, or choices that request a
non-relic random operation. It does not establish source-pool completeness,
hidden queue identity probabilities, event-selection parity, seeded Native
result parity, or Native cursor parity. The Native reference mod and Fidelity
Inspector remain needed for those source-evidence tasks.

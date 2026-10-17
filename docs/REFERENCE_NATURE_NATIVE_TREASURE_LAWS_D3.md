# Native treasure relic Chance Law (D3 query contract)

This contract adds a read-only law query for a configured native treasure
room's relic-grab-bag draw. It extends the versioned `chance_law_query`
coverage list; it does not add a resumable treasure-entry session or change
the player observation schema.

## Boundary and law

`treasure.relic_grab_bag.draw` is available at `MapChoice` when a configured
native Treasure node is reachable, the next Treasure entry will not be skipped
by Silver Crucible, and the run has its persistent relic bag. The query uses
the remaining relic identities by rarity, after current ownership filtering.
It deliberately sorts those identities and never returns the private order of
the already-shuffled queues.

The law combines the relic-rarity draw's nominal 50/33/17 Common/Uncommon/Rare
partition with the conditional equal support of each still-hidden queue
permutation. It follows `PrototypeNativeRelicGrabBag.Pull`: Common falls back
to Uncommon and then Rare, Uncommon falls back to Rare, and Rare has no
fallback. Shop relics are not a treasure-room outcome. If all eligible queues
are empty, `no_relic` is the outcome.

The query and mechanics share the rarity weights, fallback-tier sequence, and
queue eligibility helper in `PrototypeNativeRelicGrabBag`. That helper applies
ownership filtering to both reward and merchant draws, and merchant-only
exclusions only to merchant draws. The query sorts identities after applying
the reward filters, so its fingerprint is independent of private queue order.

The query conditions on the currently remaining identities, not their private
queue order. This models the distribution over a uniform hidden permutation
after prior relic outcomes have removed known identities. The query does not
read or mutate the Native RNG cursor or any queue order. The law remains
`UNVERIFIED` against original-game probabilities and shuffle/cursor behavior.

## Projection and unsupported scope

Nature receives stable relic outcome IDs, support, and conditioning
fingerprints. The ordinary player view receives the relic only after normal
treasure mechanics commits it; it never receives the remaining queue order or
RNG state. The query has no resolver: native treasure entry still owns the
rarity roll and queue pull, and a controlled Treasure-entry continuation is
unsupported. Skipped chests, non-native profiles, missing relic bags, and
unavailable Treasure nodes fail closed.

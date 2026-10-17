# Native treasure relic Chance Law and session (D3 contract)

This contract covers read-only laws and an opt-in atomic Nature continuation
for configured native Treasure entry. It extends versioned
`chance_law_query` and `stochasticSessionCoverage`; the player observation
schema is unchanged.

## Boundary and law

`treasure.relic_grab_bag.draw` remains a compact pre-entry query at `MapChoice`
when a configured native Treasure node is reachable, the next entry will not
be skipped by Silver Crucible, and the run has its persistent relic bag. Its
outcomes combine rarity and identity into a final relic result or `no_relic`.
The query uses the remaining identities by rarity after ownership filtering;
it sorts them and never returns the private order of the shuffled queues.

The granular rarity query uses `site_id: relic_grab_bag.rarity.unit_float`,
the exact `owner_id: relic_grab_bag:reward`, and the typed request ordinal. It
reports a `no_relic` outcome when a rarity branch has no eligible result after
the source fallback chain; the rarity float remains consumed during replay.
The granular identity query uses
`site_id: treasure.relic_grab_bag.model.index_draw`, the selected `rarity`, and
the sorted `relic_candidate_ids` supplied to Nature.

`ReferenceNatureTreasureSessionV111` begins from a current legal Treasure-node
action at that same boundary. It replays the actual map action under the mixed
typed provider, first sampling `relic_grab_bag.rarity.unit_float`, then
`treasure.relic_grab_bag.model.index_draw` when a legal relic remains. The
identity law is conditional on the sampled rarity and source fallback plan.
The replay applies the selected identity through `PlanPull`/`PullSelected` and
commits the resulting Reward or completed room with the ReferenceNature cursor
atomically. The session supports fork, save/restore, status, resolve and
release through JSONL and Python. Its Nature origin sorts each rarity queue so
Native's committed hidden order is not included in the session snapshot.

The law combines the relic-rarity draw's nominal 50/33/17 Common/Uncommon/Rare
partition with the conditional equal support of each still-hidden queue
permutation. It follows `PrototypeNativeRelicGrabBag.Pull`: Common falls back
to Uncommon and then Rare, Uncommon falls back to Rare, and Rare has no
fallback. Shop relics are not a treasure-room outcome. If all eligible queues
are empty, `no_relic` is the outcome.

The query and mechanics share the rarity weights, fallback-tier sequence,
`PlanPull`, and queue eligibility helper in `PrototypeNativeRelicGrabBag`. That
helper applies ownership filtering to both reward and merchant draws, and
merchant-only exclusions only to merchant draws. The session's sorted support
and identity law fingerprints are independent of private queue order.

The queries and session condition on currently remaining identities, not their
private queue order. This models a uniform hidden permutation after prior
relic outcomes have removed known identities. Queries do not read or mutate
the Native RNG cursor or queue order. The session does not advance that Native
cursor. The laws remain `UNVERIFIED` against original-game probabilities,
float-edge masses, and shuffle/cursor behavior.

## Projection and unsupported scope

Nature receives stable outcome IDs, sorted support, and conditioning
fingerprints. The ordinary player view receives the relic only after the
normal Treasure mechanics commits it; it never receives remaining queue order
or RNG state. Ordinary Native execution keeps its source rarity roll and
queue-front pull. The Nature session requires a committed relic bag; bag
construction, skipped chests, non-native profiles, missing bags and
unavailable Treasure nodes fail closed. Native cursor parity, later Treasure
draws after purchases, and source probability calibration remain open.

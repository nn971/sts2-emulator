# Native treasure relic Chance Law and session (D3 contract)

This contract covers read-only laws and an opt-in atomic Nature continuation
for configured native Treasure entry. It extends versioned
`chance_law_query` and `stochasticSessionCoverage`; the player observation
schema is unchanged.

## Boundary and law

`treasure.relic_grab_bag.draw` remains a compact pre-entry query at `MapChoice`
when a configured native Treasure node is reachable, the next entry will not
be skipped by Silver Crucible. Its outcomes combine rarity and identity into a
final relic result or `no_relic`. If no relic bag is stored yet, the query
builds sorted eligible rarity sets from the same source catalog and ownership
filter used by bag construction. It never returns or requires the private
order of shuffled queues.

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
release through JSONL and Python. Its Nature origin sorts stored queues, or
constructs sorted eligible rarity sets when the source run has no bag, so
Native's committed hidden order is never included in the session snapshot.
Residual identity sets carry an optional `NatureUnordered` snapshot tag;
Native bag snapshots retain their current shape. A snapshot carrying this tag
uses `sts2-exact-snapshot-v3-nature-relic-bag-order`. Ordinary queue-order pulls
reject Nature-owned sets until a matching controlled Chance Law continuation
selects an identity.

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

Constructing the sorted Nature pools does not replay Native's run-start queue
shuffles. Sequential conditional identity sampling represents each supported
pull from the hidden uniformly shuffled queue without exposing or committing
its order. Continued relic-bag pulls require a provider-owned residual-set
continuation; ordinary Native execution must retain its source-created order.

## Projection and unsupported scope

Nature receives stable outcome IDs, sorted support, and conditioning
fingerprints. The ordinary player view receives the relic only after the
normal Treasure mechanics commits it; it never receives remaining queue order
or RNG state. Ordinary Native execution keeps its source rarity roll and
queue-front pull. The session can construct Nature's eligible pools without a
committed Native bag. Skipped chests, non-native profiles, and unavailable
Treasure nodes fail closed. Native queue-shuffle cursor parity, continued bag
draws after the session, later Treasure draws after purchases, and source
probability calibration remain open.

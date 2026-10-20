# Shovel ReferenceNature continuation

**Snapshot:** 2026-10-21. **Status:** implemented for one legal Native or
legacy Rest `dig` action; Native source-pool fidelity and cursor parity remain
open.

## Boundary

`ReferenceNatureNativeShovelSessionV111` starts from a legal Rest `dig` action
with Shovel. On configured Native routes it replays that ordinary action and
captures the relic-bag rarity float followed by the relic identity index.
Those requests, the resulting Reward, and the ReferenceNature sampler cursor
form one saved boundary. Until both requests resolve, the published run
remains at its projected origin. The completed Reward and Nature cursor are
committed together.

On legacy routes the same session captures the single eligible-relic index
from `PickUnownedEventRelic`, then commits the resulting Reward and Nature
cursor together. Its candidate labels and `rest:shovel` pool fingerprint are
checked against the current unowned prototype relic list. This reuses the
existing shovel boundary, save format, and CLI lifecycle rather than adding a
second session type.

The Native route uses `relic_grab_bag.rarity.unit_float` and
`treasure.relic_grab_bag.model.index_draw` on `player.rewards`. It rejects
other phases, routes, actions, or stochastic sites. The Native relic queue is
projected to canonical `NatureUnordered` rarity sets; selected identities are
applied through the existing `PlanPull` and `PullSelected` helpers. The legacy
route uses `relic.unowned_random_selection.index_draw` on `event` and checks
its ordered support and owner against the ordinary shovel mechanics.

## Law and replay

The rarity request uses the shared exact Native float-threshold Chance Law.
Its 50/33/17 transform is `SOURCE_VERIFIED` for the declared request. The
identity request uses the exact Native bounded-index transform on the current
sorted fallback support. The session recomputes that support from the origin
and prior selected identities, then validates the rarity, fallback tier,
support, and request owner before accepting the choice.

The legacy index law uses equal weights over the validated prototype pool and
remains `UNVERIFIED`.

Each `ResolveNext()` samples one request and replays the full ordinary Rest
action from its saved origin with the typed decision prefix. Save/restore
recomputes that prefix, and forks keep independent snapshots. JSONL and Python
expose begin, status, resolve, fork, save, load, release, and the committed
playable run through `begin_reference_nature_native_shovel()`.

## Scope limits

The session handles one Shovel dig per action. Other relic acquisitions retain
their existing event, merchant, Treasure, or reward boundaries. The session
does not claim complete source relic eligibility, hidden queue identity
probabilities, source call-order completeness, seeded Native result parity,
or Native cursor parity. The Native reference mod and Fidelity Inspector are
still required for those source-evidence checks.

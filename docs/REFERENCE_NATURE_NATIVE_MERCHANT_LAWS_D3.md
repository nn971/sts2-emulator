# Native merchant Chance Laws and session (D3 contract)

This contract defines read-only Chance Law queries and an atomic
ReferenceNature session for configured Native Shop entry, the Native-regional
legacy-RNG Shop fallback, offer and price outcomes, the Alchemical Coffer
potion-generation effect, and registered Native player reward-index draws
reached from Shop purchases. It also continues Toy Box's five configured
Native relic-bag pulls when Toy Box is bought. It uses the
versioned `chance_law_query` coverage and
`ReferenceNatureMerchantSessionV111`; player observation fields stay unchanged.

## Boundary and inputs

Configured Native merchant offer and price queries require a run at
`MapChoice`, a native run RNG bundle, native regional systems, and at least
one currently available Shop node. They describe the merchant boundary
conditional on entering that Shop. The session also accepts a legal
Native-regional Shop-entry action when the run has no `NativeRunRng` bundle;
that retained fallback is replayed from the action checkpoint using its
legacy `shop` and `reward` index requests.
For existing offer and price laws, `ordinal` is the zero-based occurrence
within the named offer group. Card-rarity and card-model merchant laws require
`card_type`; a card-model query also requires the preceding rarity result.
Potion-rarity laws use offer ordinals 0–2; a potion-model query requires the
preceding rarity and exactly one unique `excluded_potion_ids` entry for each
preceding potion offer. These values are explicit conditional inputs and are
fingerprinted; the query never infers a selected result from Native RNG state.

The shared `card_rarity.roll.unit_float` site also has a standalone query mode
for any source. It requires the caller to declare source, current offset,
Ascension, owner ID, and stream ordinal; it returns the raw rarity law before
available-pool fallback and does not inspect the run or Native RNG state.

`potion_factory.rarity_roll.unit_float` also has a cursor-blind standalone
query mode. It accepts a typed request with caller-declared owner and one of
the supported Native streams (`run.combat_potion_generation`,
`player.rewards`, or `player.shops`) and uses the same exact inclusive
threshold masses as the merchant and Coffer paths.

`relic_grab_bag.rarity.unit_float` has a standalone raw-rarity query for a
caller-declared `player.rewards` request. It counts exact source-rounded
`NextFloat()` inputs at the strict 0.50 and 0.83 thresholds without reading the
relic queues; Treasure's existing identity-mixture law remains a separate
query.

For Shop generation, the session begins from a current legal `choose_map_node`
action for a configured Shop. For purchases, it begins from any current legal
configured Shop purchase action or Native-regional Shop purchase when the run
has no `NativeRunRng`. It reruns the ordinary action from its checkpoint under
the mixed typed provider replay. Each suspension is
conditioned on the exact source request and the already sampled prefix. It
commits the completed action and run-scoped
Nature cursor together after the source mechanics finish. Its operations are
`reference_nature_merchant_begin`,
`status`, `resolve_next`, `fork`, `save`, `load`, and `release_many`.

The Nature-only `shop.merchant.relic_model.index_draw` boundary uses the
source stream `ordinal`, `offer_ordinal` (0–2), selected `rarity`, sorted
`relic_candidate_ids` from the pending request, and `excluded_relic_ids` from
earlier offers in the same shop. The query removes those earlier selections
from the run's hidden bag sets, reuses the source fallback plan, and verifies
that the canonical support matches. It does not reveal the bag's committed
queue order.

Buying Alchemical Coffer uses the existing mixed typed session for four
source-ordered PotionFactory picks. Each pick first samples a rarity float,
then selects a model index from the remaining models of that rarity. The
rarity law counts the exact rounded Native `NextFloat` grid at the inclusive
0.1 and 0.35 thresholds; each model law counts exact Native bounded-index
preimages on the source-ordered remaining pool. The prefix records the four
rarities and selected potion IDs through request replay, so no duplicate
purchase-session type is needed. These transforms are `SOURCE_VERIFIED` on
their declared source and support; candidate-pool completeness, seed
frequencies, stream attribution, and Native cursor parity remain separate.

Configured Shop purchases can also suspend at registered Native
`player.rewards` model-index sites for random Rare-card acquisition,
random-potion acquisition, and potion-slot filling.
These use the declared request owner, ordinal, and support count for the exact
Native bounded-index law. The ordinary purchase replay verifies the full
request prefix and ordered candidate labels. This establishes the index
transform only; pool fidelity, trigger completeness, and Native stream
attribution remain unverified.

Buying Toy Box composes five rarity and identity request pairs into the same
purchase session. Configured Native purchases use
`relic_grab_bag.rarity.unit_float` with exact rounded Native `NextFloat()`
threshold masses. Native-regional fallback purchases without a Native RNG use
the retained `relic_grab_bag.rarity.ticket` site with equal weights over its
100 indices. Both routes use `treasure.relic_grab_bag.model.index_draw` with
the sorted remaining support captured from the Nature-owned unordered bag and
condition each identity on the preceding pulls; identity weights remain
`UNVERIFIED`. Replay checks every request, owner, and ordered label prefix
before committing the purchase and all five rewards with the Nature cursor.
The Native reward cursor remains unchanged.

The same session also resolves every other registered index-valued request
whose SiteId is in `StochasticSiteRegistryV111.RunActionShopPurchaseSiteIds`.
It validates that the saved action is a current legal configured or
Native-regional Shop purchase, that the request stream is permitted for its
registered site, and
that the request has one distinct, nonempty label per supported index. The
stream allowlist is shared with controlled run-action continuation validation.
The generic typed-index law fingerprints the provider-independent mechanics
boundary, action identity, exact request, ordered labels, and prior decision
prefix. Canonical Native streams receive exact bounded-index masses;
noncanonical streams retain `UNVERIFIED` uniform-index weights. The registry
is the sole site list. This adds a serialized resolution path for registered
purchase indices without certifying the source trigger, candidate pool, or
stream assignment.

## Conditioned support by site

| Site | Conditioned support |
| --- | --- |
| `shop.merchant.sale_slot.index_draw` | Five character-card slots, with exact Native bounded-index grid masses |
| `card_rarity.roll.unit_float` | Rare, Uncommon, Common, using the current rarity offset and Ascension shop thresholds; merchant branch counts exact rounded Native `NextFloat` grid masses |
| `shop.merchant.card_model.index_draw` | Ordered native Silent card models matching card type and supplied rarity, with exact Native bounded-index grid masses |
| `shop.colorless_card_model.index_draw` | Ordered implemented single-player Colorless models filtered by configured unlocked epochs; ordinal 0 is Uncommon and ordinal 1 is Rare, with exact Native bounded-index grid masses |
| `potion_factory.rarity_roll.unit_float` | Common, Uncommon, Rare using the PotionFactory's inclusive float thresholds and exact rounded Native `NextFloat` grid masses; standalone law queries bind the declared supported stream and owner |
| `potion_factory.model_within_rarity.index_draw` | Ordered potion models in the requested tier after supplied exclusions, with exact Native bounded-index grid masses on the merchant branch |
| `relic_grab_bag.rarity.unit_float` | Common, Uncommon, Rare using the grab bag's float thresholds and exact rounded Native `NextFloat` grid masses; the third merchant relic offer is forced Shop rarity and has no draw; configured Native Toy Box purchases use this draw for each wax relic |
| `shop.merchant.relic_model.index_draw` | Eligible relic identities from the first nonempty fallback tier after the selected rarity, with earlier same-shop selections removed and queue order hidden |
| `treasure.relic_grab_bag.model.index_draw` | Toy Box purchase on configured Native and Native-regional fallback routes: sorted eligible identities remaining after prior wax pulls; equal identity weights are `UNVERIFIED` |
| `shop.merchant.card_price_modifier.unit_float` | Exact Native grid masses for the rounded card price under the 0.95–1.05 modifier; `card_id`, `colorless_offer`, and optional `sale_reprice` identify the source branch |
| `shop.merchant.potion_price_modifier.unit_float` | Exact Native grid masses for the rounded potion price under the 0.95–1.05 modifier for the supplied `potion_id` |
| `shop.merchant.relic_price_modifier.unit_float` | Exact Native grid masses for the rounded relic price under the 0.85–1.15 modifier for the supplied `relic_id` |
| `shop.merchant.card_upgrade_roll.unit_float` | One `no_effect` outcome, `SOURCE_VERIFIED`: every source float is mechanically equivalent because the merchant upgrade chance is negative |

The sale-slot, merchant card-model, Colorless card-model, and merchant
potion-model index laws count the exact preimages of each result under the
pinned Native `NextInt(maxExclusive)` transform: a uniform 53-bit `NextDouble`
grid, binary64 multiplication, then truncation. These laws report
`SOURCE_VERIFIED` for that transform on the declared support. The merchant
card, potion, and relic rarity laws also count exact preimages of Native
`NextFloat()` after binary32 rounding and the source's strict or inclusive
threshold comparisons. The mechanically degenerate merchant upgrade law is
`SOURCE_VERIFIED` because every input produces `no_effect`. These claims do not
certify that a restricted candidate pool is complete, establish Native
stream assignment or seed frequencies, or prove seeded cursor parity. The
hidden relic queue identity law remains `UNVERIFIED` because its queue order
is private. Card and relic rarity laws derive the source float thresholds from
their integer basis-point values; potion rarity uses the source float
constants directly. Each reports exact rounded-grid preimage weights. Merchant
price laws map every rounded unit float through the existing source-shaped
modifier and integer-rounding functions, then count each resulting price's
exact 53-bit `NextDouble` preimages. The reported integer weights sum to
`2^53` for the declared item and price branch. Native stream assignment,
seed frequencies, and cursor parity remain open. Read-only queries do not
mutate the run or provider state.

## S0 projection, resolver, provenance, and unsupported boundary

The ordinary player projection contains the committed Shop offers, prices, and
resulting purchase mechanics. Hidden candidate pools, relic queue order, typed
request details, and RNG stream state remain private. At a Nature boundary, the
current Chance Law exposes only the request support conditioned on declared
branch inputs and the saved decision prefix; this contract adds no privileged
inspector projection.

`ReferenceNatureMerchantSessionV111` resolves configured Native Shop entry,
Native-regional legacy-RNG Shop entry, and supported configured purchase
boundaries by replaying the ordinary source action from its saved state
through the ordered request prefix. It commits the completed mechanics result
and Nature cursor together. A pending request resumes at its current source
boundary, so prior shop generation and purchase effects are not applied
twice. The fallback includes the sale slot, card rarity/model and upgrade
ticket, Colorless-card models, weighted potion rarity/models, relic rarity and
Nature-only hidden relic identity, and the three integer-percent price
requests. Those legacy request-index laws remain `UNVERIFIED`.

The declared Native bounded-index and float-grid transforms are
`SOURCE_VERIFIED` for their listed supports. That status does not establish
candidate-pool completeness, stream attribution, seed frequencies, or Native
cursor parity. Legacy merchant percentage-bin requests and hidden relic queue
identity retain their separate `UNVERIFIED` status.

Native queue construction and queue shuffling, unregistered stochastic Shop
hooks, and later Treasure draws outside the supported Toy Box purchase remain
outside the merchant session.
Alchemical Coffer's four rarity-float/model-index pairs and configured Native
Toy Box's five rarity-float/relic-identity pairs are supported purchase effects;
Native-regional fallback Alchemical Coffer purchases use four legacy
rarity-ticket/model-index pairs, with source-ordered residual potion supports
and `UNVERIFIED` equal-index weights. Native-regional fallback Shop entry also
validates each of its three legacy weighted-potion offer pairs. Native-regional
fallback Toy Box purchases use rarity tickets instead; the registered
index-only purchase sites described above are also supported on configured
and Native-regional legal Shop purchases.
Ordinary non-Native legacy Shop-node generation continues to use the run-action
boundary. The Native-regional fallback session uses equal request-index
weights for its captured legacy `shop` and `reward` requests; this controls the
emulator request prefix without certifying Native RNG behavior or legacy
cursor advancement.

## Unsupported scope

The session replays the full Shop-entry or supported purchase mechanics under
its provider scope,
but it does not reproduce Native source cursor advancement. To keep the
committed Native queue order private, it stores a canonicalized Nature-owned
projection of the existing persistent relic bag in its origin and resulting
run; the original queue order never enters the session snapshot. If the source
has no bag, the projection creates canonical eligible rarity sets without
sampling Native queue order. The projection carries a `NatureUnordered`
order-mode discriminator in the stochastic owner. New exact snapshots
serialize that owner in the v4 simulation envelope; the loader still accepts
the earlier v3 Nature-bag snapshot schema. Ordinary queue-order pulls reject a
Nature-owned set unless a controlled Chance Law replay selects an identity.
Native bag construction and queue shuffling remain outside this session, and
the completeness of the modeled eligible sets remains unverified. Unregistered
stochastic Shop hooks, later Treasure draws outside Toy Box, and Native cursor
parity remain unsupported. Alchemical Coffer, Toy Box, and the registered index-only purchase
sites are covered as described above; other purchase effects remain unsupported
unless they reach a registered continuation boundary. Ordinary non-Native
legacy Shop-node generation continues to use its existing run-action contract.
The Native-regional fallback is now included in the atomic merchant session. Its
price requests retain the discrete integer-percentage approximations to the
Native unit-float price sites, and its 10,000-way card-upgrade ticket is
mechanically inert for merchant cards. Read-only Chance Law queries expose
eleven request indices for card and potion prices, thirty-one for relic prices,
and 10,000 for the upgrade ticket. All fallback requests use equal
request-index weights and remain `UNVERIFIED`; the price bins approximate the
Native float paths, and the session does not certify legacy cursor behavior.
For `chance_law_query`, callers declare the request `owner_id`, `ordinal`, and
the fixed `support_count` (11, 31, or 10,000) for the selected site.

## Python binding

`State.query_chance_law` accepts `card_type`, `rarity`, and
`excluded_potion_ids` for merchant model queries; the Nature-only relic model
law also accepts `offer_ordinal`, `relic_candidate_ids`, and
`excluded_relic_ids`. Price laws accept `card_id`, `potion_id`, `relic_id`,
`colorless_offer`, and `sale_reprice`. The no-effect float law accepts its
typed `owner_id`. These arguments affect only the returned law and are not
submitted as player actions. `State.begin_reference_nature_merchant(action_id,
nature_seed=...)` starts the session from a legal Native-regional Shop entry
(including the legacy-RNG fallback), configured purchase, or Native-regional
Shop purchase; its Python object exposes status, fork, save/load, release, and
the committed playable state.

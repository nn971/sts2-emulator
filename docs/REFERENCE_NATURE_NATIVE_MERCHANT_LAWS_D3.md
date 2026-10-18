# Native merchant Chance Laws and session (D3 contract)

This contract defines read-only Chance Law queries and an atomic
ReferenceNature session for configured Native Shop entry, offer and price
outcomes, and the Alchemical Coffer potion-generation effect. It uses the
versioned `chance_law_query` coverage and
`ReferenceNatureMerchantSessionV111`; player observation fields stay unchanged.

## Boundary and inputs

Merchant offer and price queries require a run at `MapChoice`, a native run
RNG bundle, native regional systems, and at least one currently available Shop
node. They describe the merchant boundary conditional on entering that Shop.
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

For Shop generation, the session begins from a current legal `choose_map_node`
action for a configured Shop. For the Alchemical Coffer effect, it begins from
the current legal `buy_relic` action for an unsold Coffer offer. In both cases
it reruns the ordinary action from its checkpoint under the mixed typed
provider replay. Each suspension is conditioned on the exact source request
and the already sampled prefix. It commits the completed action and run-scoped
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

| Site | Conditioned support |
| --- | --- |
| `shop.merchant.sale_slot.index_draw` | Five character-card slots, with exact Native bounded-index grid masses |
| `card_rarity.roll.unit_float` | Rare, Uncommon, Common, using the current rarity offset and Ascension shop thresholds; merchant branch counts exact rounded Native `NextFloat` grid masses |
| `shop.merchant.card_model.index_draw` | Ordered native Silent card models matching card type and supplied rarity, with exact Native bounded-index grid masses |
| `shop.colorless_card_model.index_draw` | Ordered implemented single-player Colorless models; ordinal 0 is Uncommon and ordinal 1 is Rare, with exact Native bounded-index grid masses |
| `potion_factory.rarity_roll.unit_float` | Common, Uncommon, Rare using the PotionFactory's inclusive float thresholds and exact rounded Native `NextFloat` grid masses |
| `potion_factory.model_within_rarity.index_draw` | Ordered potion models in the requested tier after supplied exclusions, with exact Native bounded-index grid masses on the merchant branch |
| `relic_grab_bag.rarity.unit_float` | Common, Uncommon, Rare using the grab bag's float thresholds and exact rounded Native `NextFloat` grid masses; the third merchant relic offer is forced Shop rarity and has no draw |
| `shop.merchant.relic_model.index_draw` | Eligible relic identities from the first nonempty fallback tier after the selected rarity, with earlier same-shop selections removed and queue order hidden |
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

## Unsupported scope

The session replays the full Shop-entry or supported purchase mechanics under
its provider scope,
but it does not reproduce Native source cursor advancement. To keep the
committed Native queue order private, it stores a canonicalized Nature-owned
projection of the existing persistent relic bag in its origin and resulting
run; the original queue order never enters the session snapshot. If the source
has no bag, the projection creates canonical eligible rarity sets without
sampling Native queue order. This projection carries an optional
`NatureUnordered` snapshot tag; Native bags omit it and retain their existing
shape. A snapshot carrying the tag uses
`sts2-exact-snapshot-v3-nature-relic-bag-order`, advertised separately in
`hello`. Ordinary queue-order pulls reject a
Nature-owned set unless a controlled Chance Law replay selects an identity.
Native bag construction and queue shuffling remain outside this session, and
the completeness of the modeled eligible sets remains unverified. Other
stochastic purchase effects, later Treasure draws, and Native cursor parity
remain unsupported. Legacy Shop-node generation continues to use its existing
run-action contract. The four legacy `shop.merchant.*` rows linked from the
migration matrix remain separate: three price rows use discrete integer
percentage bins that approximate the Native unit-float price sites, and the
legacy card-upgrade row consumes a 10,000-way ticket even though the generated
merchant card cannot be upgraded. These legacy requests do not enter the
configured Native merchant session, and the Native no-effect float law does
not certify their legacy cursor behavior. A ReferenceNature request for those
legacy boundaries remains unsupported and must fail closed until a matching
legacy merchant continuation exists.

## Python binding

`State.query_chance_law` accepts `card_type`, `rarity`, and
`excluded_potion_ids` for merchant model queries; the Nature-only relic model
law also accepts `offer_ordinal`, `relic_candidate_ids`, and
`excluded_relic_ids`. Price laws accept `card_id`, `potion_id`, `relic_id`,
`colorless_offer`, and `sale_reprice`. The no-effect float law accepts its
typed `owner_id`. These arguments affect only the returned law and are not
submitted as player actions. `State.begin_reference_nature_merchant(action_id,
nature_seed=...)` starts the session from a legal configured Shop entry or
Alchemical Coffer purchase; its Python object exposes status, fork, save/load,
release, and the committed playable state.

# Native merchant Chance Laws and session (D3 contract)

This contract defines read-only Chance Law queries and an atomic
ReferenceNature session for offer and price outcomes in the configured native
merchant generator. It uses the versioned `chance_law_query` coverage and
`ReferenceNatureMerchantSessionV111`; player observation fields stay unchanged.

## Boundary and inputs

Queries require a run at `MapChoice`, a native run RNG bundle, native regional
systems, and at least one currently available Shop node. The query describes
the merchant boundary conditional on entering that Shop. For existing offer
and price laws, `ordinal` is the zero-based occurrence within the named offer
group. Card-rarity and
card-model laws require `card_type`; a card-model query also requires the
preceding rarity result. Potion-rarity laws use offer ordinals 0–2; a
potion-model query requires the preceding rarity and exactly one unique
`excluded_potion_ids` entry for each preceding potion offer. These values are
explicit conditional inputs and are fingerprinted; the query never infers a
selected result from Native RNG state.

The session begins from a current legal `choose_map_node` action for a
configured Shop. It reruns the normal map-entry and `StartShop` mechanics from
the pre-entry checkpoint under the mixed typed provider replay. Each suspension
is conditioned on the exact source request and the already sampled prefix. It
commits the generated shop and run-scoped Nature cursor together after the
source mechanics finish. Its operations are `reference_nature_merchant_begin`,
`status`, `resolve_next`, `fork`, `save`, `load`, and `release_many`.

The Nature-only `shop.merchant.relic_model.index_draw` boundary uses the
source stream `ordinal`, `offer_ordinal` (0–2), selected `rarity`, sorted
`relic_candidate_ids` from the pending request, and `excluded_relic_ids` from
earlier offers in the same shop. The query removes those earlier selections
from the run's hidden bag sets, reuses the source fallback plan, and verifies
that the canonical support matches. It does not reveal the bag's committed
queue order.

| Site | Conditioned support |
| --- | --- |
| `shop.merchant.sale_slot.index_draw` | Five character-card slots, equal weight |
| `card_rarity.roll.unit_float` | Rare, Uncommon, Common, using the current act-independent rarity offset and Ascension shop thresholds |
| `shop.merchant.card_model.index_draw` | Ordered native Silent card models matching card type and supplied rarity |
| `shop.colorless_card_model.index_draw` | Ordered implemented single-player Colorless models; ordinal 0 is Uncommon and ordinal 1 is Rare |
| `potion_factory.rarity_roll.unit_float` | Common, Uncommon, Rare at the PotionFactory's 65/25/10 nominal partition |
| `potion_factory.model_within_rarity.index_draw` | Ordered potion models in the requested tier after supplied exclusions |
| `relic_grab_bag.rarity.unit_float` | Common, Uncommon, Rare at the grab bag's 50/33/17 nominal partition; the third merchant relic offer is forced Shop rarity and has no draw |
| `shop.merchant.relic_model.index_draw` | Eligible relic identities from the first nonempty fallback tier after the selected rarity, with earlier same-shop selections removed and queue order hidden |
| `shop.merchant.card_price_modifier.unit_float` | Rounded card price under the 0.95–1.05 modifier; `card_id`, `colorless_offer`, and optional `sale_reprice` identify the source branch |
| `shop.merchant.potion_price_modifier.unit_float` | Rounded potion price under the 0.95–1.05 modifier for the supplied `potion_id` |
| `shop.merchant.relic_price_modifier.unit_float` | Rounded relic price under the 0.85–1.15 modifier for the supplied `relic_id` |
| `shop.merchant.card_upgrade_roll.unit_float` | One `no_effect` outcome: every source float is mechanically equivalent because the merchant upgrade chance is negative |

The index laws use equal integer weights over the same ordered candidate pools
used by the generator. Threshold laws use the source-shaped nominal
basis-point partitions. Price laws map intervals of the nominal modifier
range through the source rounding rule; they do not model the exact finite
distribution of native float values. The no-effect upgrade law is degenerate
because the float cannot change the generated card; its representative is
only a mechanics-preserving replay value. Every law remains `UNVERIFIED`:
these structures do not establish original conditional probabilities,
float-edge calibration, or native cursor parity. Read-only queries do not
mutate the run or provider state.

## Unsupported scope

The session replays the full Shop-entry mechanics under its provider scope,
but it does not reproduce Native source cursor advancement. To keep the
committed Native queue order private, it stores a canonicalized Nature-owned
projection of the existing persistent relic bag in its origin and resulting
run; the original queue order never enters the session snapshot. This
projection carries an optional `NatureUnordered` snapshot tag; Native bags
omit it and retain their existing shape. A snapshot carrying the tag uses
`sts2-exact-snapshot-v3-nature-relic-bag-order`, advertised separately in
`hello`. Ordinary queue-order pulls reject a
Nature-owned set unless a controlled Chance Law replay selects an identity.
The boundary
requires that bag to exist, so Native bag construction is outside this
session. Stochastic consequences of merchant purchases, later Treasure draws,
exact native float-edge probabilities, and Native cursor parity remain
unsupported. Legacy Shop-node generation continues to use its existing
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
nature_seed=...)` starts the session; its Python object exposes status, fork,
save/load, release, and the committed playable state.

# Native merchant Chance Laws (D3 query contract)

This contract adds read-only Chance Law queries for offer and price outcomes
in the configured native merchant generator. It extends the existing versioned
`chance_law_query` coverage list; it does not add a resumable merchant session
or change the player observation schema.

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

The index laws use equal integer weights over the same ordered candidate pools
used by the generator. Threshold laws use the source-shaped nominal
basis-point partitions. Price laws map intervals of the nominal modifier
range through the source rounding rule; they do not model the exact finite
distribution of native float values. Every response remains `UNVERIFIED`:
these queries do not establish original conditional probabilities,
float-edge calibration, or native cursor parity. Querying does not mutate the
run or its provider state.

## Unsupported scope

Source call/cursor replay, the full native shop-entry continuation, and
stochastic consequences of merchant purchases remain unsupported by this
contract. Configured `StartShop` now resolves default and explicitly passed
native index/unit-float providers through `StochasticProviderScopeV111`, whose
logical stream ordinals advance independently when a replacement provider
does not advance Native RNG. A mixed replay can now suspend at the
Nature-only merchant relic identity boundary and `PullSelected` applies its
chosen identity through the shared bag mechanics, but no serialized merchant
session consumes this path. Native shop generation remains unsupported in
the existing controlled run-action session. Legacy Shop-node generation
continues to use its existing run-action contract.

## Python binding

`State.query_chance_law` accepts `card_type`, `rarity`, and
`excluded_potion_ids` for merchant model queries; the Nature-only relic model
law also accepts `offer_ordinal`, `relic_candidate_ids`, and
`excluded_relic_ids`. Price laws accept `card_id`, `potion_id`, `relic_id`,
`colorless_offer`, and `sale_reprice`. These arguments affect only the
returned law and are not submitted as player actions.

# Native merchant Chance Laws (D3 query contract)

This contract adds read-only Chance Law queries for discrete outcomes in the
configured native merchant generator. It extends the existing versioned
`chance_law_query` coverage list; it does not add a resumable merchant session
or change the player observation schema.

## Boundary and inputs

Queries require a run at `MapChoice`, a native run RNG bundle, native regional
systems, and at least one currently available Shop node. The query describes
the merchant boundary conditional on entering that Shop. `ordinal` is the
zero-based occurrence within the named offer group. Card-rarity and
card-model laws require `card_type`; a card-model query also requires the
preceding rarity result. Potion-rarity laws use offer ordinals 0–2; a
potion-model query requires the preceding rarity and exactly one unique
`excluded_potion_ids` entry for each preceding potion offer. These values are
explicit conditional inputs and are fingerprinted; the query never infers a
selected result from Native RNG state.

| Site | Conditioned support |
| --- | --- |
| `shop.merchant.sale_slot.index_draw` | Five character-card slots, equal weight |
| `card_rarity.roll.unit_float` | Rare, Uncommon, Common, using the current act-independent rarity offset and Ascension shop thresholds |
| `shop.merchant.card_model.index_draw` | Ordered native Silent card models matching card type and supplied rarity |
| `shop.colorless_card_model.index_draw` | Ordered implemented single-player Colorless models; ordinal 0 is Uncommon and ordinal 1 is Rare |
| `potion_factory.rarity_roll.unit_float` | Common, Uncommon, Rare at the PotionFactory's 65/25/10 nominal partition |
| `potion_factory.model_within_rarity.index_draw` | Ordered potion models in the requested tier after supplied exclusions |
| `relic_grab_bag.rarity.unit_float` | Common, Uncommon, Rare at the grab bag's 50/33/17 nominal partition; the third merchant relic offer is forced Shop rarity and has no draw |

The index laws use equal integer weights over the same ordered candidate pools
used by the generator. Threshold laws use the source-shaped nominal
basis-point partitions. Every response remains `UNVERIFIED`: these queries do
not establish original conditional probabilities, float-edge calibration, or
native cursor parity. Querying does not mutate the run or its provider state.

## Unsupported scope

Merchant continuous price modifiers, source call/cursor replay, the full
native shop-entry continuation, and stochastic consequences of merchant
purchases remain unsupported by this contract. Native shop generation still
fails closed in controlled run-action sessions. Legacy Shop-node generation
continues to use its existing run-action contract.

## Python binding

`State.query_chance_law` accepts `card_type`, `rarity`, and
`excluded_potion_ids` for these conditional queries. These arguments affect
only the returned law and are not submitted as player actions.

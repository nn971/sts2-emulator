# Native combat reward ReferenceNature session

`ReferenceNatureCombatRewardSessionV111` is an opt-in atomic continuation for
the complete Native combat victory and reward-generation boundary. Begin with
a current legal combat action in a configured Native Combat, Elite, or Boss
room. The action must immediately win and enter `RunPhase.Reward` under the
ordinary engine. The session replays that action from its original checkpoint
for each selected prefix and publishes the resulting reward state only after
all captured requests have been resolved. While pending, the returned run
remains at the pre-action checkpoint and retains its previously committed
ReferenceNature cursor.

The session filters the shared typed provider scope to these Native reward
sites. Requests outside the list retain their ordinary providers.

| Site | Conditioned choice |
| --- | --- |
| `reward.potion_odds.unit_float` | Offered or not offered from the persisted pity value and Elite bonus; forced rewards bypass this draw |
| `reward.combat_gold.index_draw` | Inclusive Native gold amount for the room and Ascension |
| `potion_factory.rarity_roll.unit_float` | Potion reward rarity |
| `potion_factory.model_within_rarity.index_draw` | Source-ordered potion identity within the selected rarity |
| `card_rarity.roll.unit_float` | Raw encounter-card rarity before available-tier fallback, conditioned on the current rarity offset |
| `card_rarity.encounter_model.index_draw` | Card identity within the request's remaining rarity support |
| `card_rarity.upgrade_roll.unit_float` | Raw inclusive card-upgrade comparison flag |
| `relic_grab_bag.rarity.unit_float` | Native relic rarity |
| `treasure.relic_grab_bag.model.index_draw` | Identity from the selected relic-bag fallback tier after prior reward pulls |
| `reward.boss_relic_offer.shuffle_swap_index` | Each bounded swap while constructing a Boss relic offer |
| `reward.lava_rock.relic_offer.shuffle_swap_index` | Each bounded swap while constructing Lava Rock's additional relic offers |

The bounded-index and unit-float laws use exact Native transform masses and
report `SOURCE_VERIFIED` for those declared transforms. That status does not
establish source-pool completeness, stream attribution, seeded result
frequencies, or Native cursor parity. The hidden relic-identity law remains
`UNVERIFIED`; it samples the canonical remaining identity support while
replaying the existing rarity fallback and bag-removal mechanics. Every
selected float is mapped to a representative that preserves the sampled
mechanic outcome.

JSONL operations are `reference_nature_combat_reward_begin`, `status`,
`resolve_next`, `fork`, `save`, `load`, and `release_many`. Begin accepts a
`state_handle`, a current legal winning `action_id`, and an optional unsigned
64-bit `nature_seed`. Resolve and fork use
`reference_nature_combat_reward_handle`; resolve also requires the current
`fingerprint`. A state handle and `reward` are returned only after completion.
The pending Nature view uses `nature_choice_observe` with
`session_kind: "combat_reward"`.

The session is advertised by `ReferenceNatureCombatRewardSessionSchemaId` and
`ReferenceNatureCombatRewardSiteIds` in the validated stochastic session
coverage. `ReferenceNatureRunCursorV111` commits the sampled cursor together
with the completed reward state. The session does not cover legacy reward
generation, victory actions that do not immediately produce a complete
reward, or Native queue-shuffle construction before the combat boundary.

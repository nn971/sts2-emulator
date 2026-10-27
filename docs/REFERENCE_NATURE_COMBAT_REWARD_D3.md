# Combat reward ReferenceNature session

`ReferenceNatureCombatRewardSessionV111` is an opt-in atomic continuation for
combat victory and reward generation. Begin with a current legal combat
action in a Combat, Elite, or Boss room. The action must immediately win and
enter `RunPhase.Reward` under the ordinary engine. The session replays that
action from its original checkpoint for each selected prefix and publishes
the resulting reward state only after the controlled prefix has resolved.
While pending, the returned run remains at the pre-action checkpoint and
retains its previously committed ReferenceNature cursor.

The session filters the shared typed provider scope to the Native reward
sites below. Requests outside both site lists retain their ordinary
providers.

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

The source-independent [combat reward acceptance test](../tests/Sts2Emulator.Core.Tests/StochasticCombatRewardAcceptanceTests.cs)
replays legacy and configured Native immediate victories, saves and restores a
pending prefix, exercises both potion-presence outcomes, and checks selected
potion, gold, card, rarity, and upgrade results against the committed reward.
It also verifies that replay leaves each source provider cursor unchanged.
The Native card-model law uses semantic `card_model:<id>` outcomes; the mapper
checks the saved candidate order before translating one back to its typed
index. Potion pity uses the source's strict float comparison, while card-
upgrade mapping uses its inclusive comparison. These checks cover the
emulator continuation and declared transforms; installed-game pool fidelity,
stream attribution, and cursor parity remain separate.

When `NativeRunRng` is absent, the same session also captures these registered
legacy reward-stream index requests:

| Site | Conditioned choice |
| --- | --- |
| `reward.potion_odds.ticket` | Legacy pity ticket |
| `reward.combat_gold.index_draw` | Legacy bounded gold amount |
| `reward.random_potion.drop.ticket` | Potion offered/not offered |
| `reward.random_potion.model` | Legacy reward-potion pool index |
| `reward.elite_relic.model` | Legacy Elite relic pool index |
| `reward.card_rarity.ticket` | Legacy reward-card rarity ticket |
| `reward.card_model.index_draw` | Legacy reward-card pool index |
| `card_rarity.roll.ticket` | Legacy encounter-card rarity ticket |
| `card_rarity.upgrade_roll.ticket` | Legacy card-upgrade ticket |
| `potion.rarity_roll.ticket` | Legacy potion rarity ticket |
| `potion.model_within_rarity.index_draw` | Legacy rarity-filtered potion index |
| `potion.uniform_model.index_draw` | Legacy full-pool potion index |
| `relic_grab_bag.rarity.ticket` | Legacy relic rarity ticket |
| `treasure.relic_grab_bag.model.index_draw` | Legacy combat reward relic identity index |
| `reward.boss_relic_offer.shuffle_swap_index` | Legacy Boss relic offer swap |
| `reward.lava_rock.relic_offer.shuffle_swap_index` | Legacy Lava Rock offer swap |

These retained requests use equal-index `UNVERIFIED` laws bound to the typed
request, candidate labels, active action, and ordered replay prefix. They do
not establish the legacy stream's probability mapping or Native parity. When
the replay-validated label vector is complete and unique, its labels become
the ordered Chance Law outcome IDs without changing those weights; absent,
duplicate, numeric-range, and oversized vectors retain index outcomes. Other
stochastic requests retain their existing providers and are not controlled by
this session. In a Native regional run with no `NativeRunRng`, the session
canonicalizes the relic bag and controls the retained rarity ticket and
identity index on `reward`; the rest of the legacy run's RNG ownership stays
unchanged.

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
with the completed reward state. The session does not cover unregistered
stochastic reward hooks, victory actions that do not immediately produce a
complete reward, or Native queue-shuffle construction before the combat
boundary.

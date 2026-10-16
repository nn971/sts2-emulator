# Desired RNG implementation — migration matrix (D0–D5 partial)

**Baseline:** `agent/v111-hive-glory-pregenerated-encounter-queues-v20`
(pinned native v0.111.0 code). This work is stacked on that emulator
integration branch, **not** the lightweight inspector foundation branch.

**Modes and status.** `native-v111` chooses outcomes from the existing
source-audited xoshiro stream and consumes its cursor. `scripted-v111`
matches a complete, ordered request/outcome transcript and makes no native
draw. `controlled-v111` validates an external index at a single request
site, also without consuming native RNG. Scripted and controlled providers
started as D1 components. Isolated **serializable D3 HP → intent continuations**
and an opt-in D4 JSONL/Python adapter now exist. Restricted semantic rollouts
can reach supported combat terminals, but there is **not yet** a general
whole-run controlled continuation or a trained Nature-policy API. No `p/q`
probability or original conditional law is claimed. No consumer-wide profile
switch has occurred; the legacy prototype profile and default v111 protocols
remain available. Distinguished Cape's curse-acquisition site now uses an
explicit `legacy-prototype-v0` adapter under the retained six-stream profile.
It preserves the legacy modulo sampler and does not claim native-v111 parity.

The matrix separates call-site mechanics and reference-law status. “Native
parity” means a seeded emulator cursor/result regression; it does not imply a
live Inspector comparison. “Controlled mechanics” records existing focused
evidence, not a build-only check. `No` in the law column means no calibrated
reference distribution is claimed.

| Stable call-site ID | Owner / stream | Source audit | Native v111 implemented | Native parity tested | Controlled implemented | Controlled mechanics tested | Reference law calibrated |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `run.encounter.overgrowth_boss.index_draw` | run + ordered Act 1 boss pool / legacy `combat` | Yes: fixed ordered pool and one bounded choice | No: legacy typed index provider only | No | No | No | No |
| `combat.initial_unique_hp` | creature CombatId / native `run.niche`, legacy `combat` | Yes: native initial and attached summons; legacy initial, death, and ordinary summons | Yes: native unique-pool draw and shared legacy typed index provider | Yes: seeded result/hash/cursor for native entry | Yes: initial entry only | Yes | No |
| `combat.tough_egg.hatchling_hp` | Tough Egg CombatId / native `run.niche`, legacy `combat` | Yes, direct hatch range and call timing | Yes: typed native hatch and legacy transform index providers | Yes: existing seeded hatch oracle | No | No | No |
| `combat.committed_intent.index_draw` | creature CombatId / native `run.monster_ai`, legacy `combat` | Yes, supported moves | Yes: native subset and legacy typed bounded-integer provider | Yes: seeded result/hash/cursor | Yes: isolated entry, source route and automatic turn continuation | Yes | No |
| `combat.encounter.formation.index_draw` | encounter entry + floor / native `context.encounter.*`, legacy `combat` | Partial: supported source resolvers and opening rotation; singleton calls preserved | Yes: native and legacy typed providers across callback and distinct-pool resolvers | Yes: existing seeded result/cursor regressions | No | No | No |
| `combat.encounter.overgrowth_elite.index_draw` | act/floor + eligible encounter pool / legacy `combat` | Partial: previous-elite exclusion and ordered fallback pool | No: legacy typed index provider only | No | No: fails closed pending an encounter-selection continuation | No | No |
| `combat.encounter.overgrowth_weak.index_draw` | act/floor + remaining weak encounter pool / legacy `combat` | Partial: remaining encounter order and exhaustion boundary | No: legacy typed index provider only | No | No: fails closed pending an encounter-selection continuation | No | No |
| `combat.encounter.overgrowth_normal.index_draw` | act/floor + eligible normal encounter pool / legacy `combat` | Partial: previous-encounter filter and ordered fallback pool | No: legacy typed index provider only | No | No: fails closed pending an encounter-selection continuation | No | No |
| `combat.encounter.weighted_selection.ticket` | ordered eligible encounters / legacy `combat` | Partial: ordered weights and bounded ticket interval | No: legacy typed bounded-integer provider only | No | No: fails closed pending an encounter-selection continuation | No | No |
| `event.orobas.relic_offer.index_draw` | Orobas event / `context.event.OROBAS.player_0` | Yes: three ordered eligible pools; singleton fallback still consumes | Yes: typed provider and opt-in audit | No: provider-specific result/cursor regression pending | Yes: one-shot provider, persisted nonnative mode and cursor validation | No | No |
| `event.darv.offer_variant.index_draw` | Darv event / `context.event.DARV.player_0` | Yes: two legal offer shapes | Yes: bounded typed provider request | No: provider-specific result/cursor regression pending | Yes: one-shot provider, persisted nonnative mode and cursor validation | No | No |
| `event.darv.relic_offer.shuffle_swap_index` | Darv event / `context.event.DARV.player_0` | Yes: descending Fisher–Yates over the ordered eligible relic pool | Yes: typed provider at each swap | No: provider-specific result/cursor regression pending | Yes: one-shot provider, persisted nonnative mode and cursor validation | No | No |
| `event.pael.relic_offer.index_draw` | Pael event / `context.event.PAEL.player_0` | Yes: three ordered pools, including repeated entries as weights | Yes: typed provider; native entry preserves source draws | No: provider-specific result/cursor regression pending | Yes: one-shot generation and nonnative mode persisted | No | No |
| `event.tezcatara.relic_offer.index_draw` | Tezcatara event / `context.event.TEZCATARA.player_0` | Yes: three ordered pools with deck-dependent first-pool filtering | Yes: typed provider; native entry preserves source draws | No: provider-specific result/cursor regression pending | Yes: one-shot generation and nonnative mode persisted | No | No |
| `event.nonupeipe.relic_offer.shuffle_swap_index` | Nonupeipe event / `context.event.NONUPEIPE.player_0` | Yes: eligible source-ordered pool and saved Bracelet condition | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | Yes: one-shot generation and nonnative mode persisted | No | No |
| `event.tanx.relic_offer.shuffle_swap_index` | Tanx event / `context.event.TANX.player_0` | Yes: entry-time eligible relic pool and saved Tri-Boomerang condition | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | Yes: one-shot generation and nonnative mode persisted | No | No |
| `event.vakuu.relic_offer.shuffle_swap_index` | Vakuu event / `context.event.VAKUU.player_0` | Yes: three ordered relic pools; ordinals span all three shuffles | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | Yes: one-shot generation and nonnative mode persisted | No | No |
| `event.selection.weighted_ticket` | act/floor + eligible event pool / legacy `event` | Partial: event eligibility, history filters, and ordered weights; native event stream assignment remains unaudited | No: legacy typed bounded-integer provider only | No | No: fails closed pending an event-entry continuation | No | No |
| `event.generated_gold.index_draw` | event ID + primary/secondary gold stage / legacy `event` | Partial: implemented event-specific integer bins; source float laws are not represented for all events | No: legacy typed index provider only | No | No: fails closed pending an event-entry continuation | No | No |
| `event.random_upgrade.card_instance` | event choice + ordered eligible deck instances / legacy `event` | Yes: CanSelectEventDeckCard filters and deck order | No: legacy typed index provider only | No | No: fails closed pending an event-choice continuation | No | No |
| `event.reward.random_rare_card.model` | event choice + ordered Rare reward pool / legacy `reward` | Partial: emulator reward pool differs from native character pool | No: legacy typed index provider only | No | No: fails closed pending an event-choice continuation | No | No |
| `event.reward.random_potion.model` | event choice + ordered potion pool / legacy `event` | Partial: ordinary pool plus event-specific native route branches | No: legacy typed index provider only | No | No: fails closed pending an event-choice continuation | No | No |
| `event.potion.fill_empty_slots.model` | player potion slot + potion pool / legacy `event` | Yes: source-ordered empty-slot filling | No: legacy typed index provider only | No | No: fails closed pending an event-choice continuation | No | No |
| `relic.unowned_random_selection.index_draw` | event or Shovel + eligible unowned relic pool / legacy `event` | Partial: single-copy relic filter and pool order | No: legacy typed index provider only | No | No: fails closed pending an event/relic continuation | No | No |
| `event.reward.random_modifier_curse.index_draw` | event reward + ordered modifier-curse pool / legacy `reward` | Partial: implemented modifier-curse pool order; source event reward audit remains incomplete | No: legacy typed index provider only | No | No: fails closed pending an event-reward continuation | No | No |
| `event.neow.cursed_relic.index_draw` | Neow event + ordered cursed-relic names / legacy `event` | Partial: default curse pool and exclusion pairing; unlock filters remain unaudited | No: legacy typed index provider only | No | No: Neow option generation remains source-owned | No | No |
| `event.neow.lava_rock_pair.index_draw` | Neow event + conditional positive relic pair / legacy `event` | Yes: one bounded choice unless Large Capsule is selected | No: legacy typed index provider only | No | No: Neow option generation remains source-owned | No | No |
| `event.neow.oyster_pair.index_draw` | Neow event + conditional positive relic pair / legacy `event` | Yes: one bounded choice | No: legacy typed index provider only | No | No: Neow option generation remains source-owned | No | No |
| `event.neow.talisman_pair.index_draw` | Neow event + conditional positive relic pair / legacy `event` | Yes: one bounded choice | No: legacy typed index provider only | No | No: Neow option generation remains source-owned | No | No |
| `event.neow.positive_relic.shuffle_swap_index` | Neow event + filtered positive relic options / legacy `event` | Partial: source-shaped default pool and descending swaps; unlock filters remain unaudited | No: shared Fisher–Yates loop with legacy typed provider | No | No: Neow option generation remains source-owned | No | No |
| `shop.merchant.sale_slot.index_draw` | act/floor + five merchant card slots / legacy `shop` | Partial: selected before merchant inventory population | No: legacy typed index provider only | No | No: shop generation remains source-owned | No | No |
| `shop.merchant.card_price_modifier.index_draw` | merchant card model / legacy `shop` | Partial: integer 95–105 bins approximate source `NextFloat(0.95, 1.05)` | No: legacy typed index provider only | No | No: shop generation remains source-owned | No | No |
| `shop.card.price.index_draw` | legacy shop card model / legacy `shop` | Yes: rarity-specific integer price ranges | No: legacy typed index provider only | No | No: shop generation remains source-owned | No | No |
| `shop.card_rarity.ticket` | act + card type + offer ordinal / legacy `shop` | Partial: current rarity weights after pool exhaustion filtering | No: legacy typed bounded-integer provider only | No | No: shop generation remains source-owned | No | No |
| `shop.card_model.index_draw` | act + card type + offer ordinal + rarity / legacy `shop` | Partial: ordered available-card pool and without-replacement removal | No: legacy typed index provider only | No | No: shop generation remains source-owned | No | No |
| `shop.merchant.card_model.index_draw` | merchant card type + offer ordinal + rarity + ordered pool / legacy `shop` | Partial: ordered merchant candidate pool and without-replacement removal | No: legacy typed index provider only | No | No: shop generation remains source-owned | No | No |
| `shop.merchant.card_upgrade_roll.ticket` | merchant card offer + selected model / legacy `shop` | Yes: consumes the source-shaped 10,000-ticket roll even when upgrades are impossible | No: legacy typed bounded-integer provider only | No | No: shop generation remains source-owned | No | No |
| `shop.merchant.potion_price_modifier.index_draw` | merchant potion offer + model / legacy `shop` | Partial: integer 95–105 bins approximate source `NextFloat(0.95, 1.05)` | No: legacy typed index provider only | No | No: shop generation remains source-owned | No | No |
| `shop.merchant.relic_price_modifier.index_draw` | merchant relic offer + model / legacy `shop` | Partial: integer 85–115 bins approximate source `NextFloat(0.85, 1.15)` | No: legacy typed index provider only | No | No: shop generation remains source-owned | No | No |
| `shop.potion_offer.shuffle_swap_index` | act/floor + ordered potion pool / legacy `shop` | Yes: full descending shuffle before taking three offers | No: shared Fisher–Yates loop with legacy typed provider | No | No: shop generation remains source-owned | No | No |
| `shop.potion_offer.price.index_draw` | act/floor + potion offer / legacy `shop` | Yes: integer price range 40–60 | No: legacy typed index provider only | No | No: shop generation remains source-owned | No | No |
| `shop.relic_offer.shuffle_swap_index` | act/floor + ordered available relic pool / legacy `shop` | Partial: unowned relic filter, full descending shuffle, first three offers | No: shared Fisher–Yates loop with legacy typed provider | No | No: shop generation remains source-owned | No | No |
| `shop.relic_offer.price.index_draw` | act/floor + relic offer / legacy `shop` | Yes: integer price range 100–140 | No: legacy typed index provider only | No | No: shop generation remains source-owned | No | No |
| `reward.boss_relic_offer.shuffle_swap_index` | act/floor + eligible boss relic pool / legacy `reward` | Partial: unowned filter and full descending shuffle before three offers | No: shared Fisher–Yates loop with legacy typed provider | No | No: reward generation remains source-owned | No | No |
| `reward.random_potion.drop.ticket` | act/floor + room / legacy `reward` | Yes: `NextInt(2) < 1` drop ticket, only when potion capacity exists | No: legacy typed bounded-integer provider only | No | No: reward generation remains source-owned | No | No |
| `reward.random_potion.model` | act/floor + ordered potion pool / legacy `reward` | Yes: uniform pool draw after a positive drop ticket | No: legacy typed index provider only | No | No: reward generation remains source-owned | No | No |
| `reward.elite_relic.model` | act/floor + ordered relic pool / legacy `reward` | Partial: prototype relic pool and elite reward timing | No: legacy typed index provider only | No | No: reward generation remains source-owned | No | No |
| `reward.card_rarity.ticket` | act/floor + reward group + pick ordinal / legacy `reward` | Partial: current act weights after available-pool filtering | No: legacy typed bounded-integer provider only | No | No: reward generation remains source-owned | No | No |
| `reward.card_model.index_draw` | act/floor + reward group + rarity + pick ordinal / legacy `reward` | Partial: ordered models within selected rarity, without replacement | No: legacy typed index provider only | No | No: reward generation remains source-owned | No | No |
| `card_rarity.roll.ticket` | card reward source + current offset / legacy `reward` | Partial: integer ticket with source-specific rare threshold | No: legacy typed bounded-integer provider only | No | No: card generation remains source-owned | No | No |
| `card_rarity.upgrade_roll.ticket` | card model + act + ascension / legacy `reward` | Partial: source-shaped upgrade threshold and 10,000-ticket roll | No: legacy typed bounded-integer provider only | No | No: card generation remains source-owned | No | No |
| `card_rarity.encounter_model.index_draw` | act/room + pick + rarity + ordered candidate pool / legacy `reward` | Partial: available encounter-card models and without-replacement removal | No: legacy typed index provider only | No | No: reward generation remains source-owned | No | No |
| `potion.rarity_roll.ticket` | potion offer + legacy stream / legacy `reward`, `event`, or `shop` | Yes for the legacy integer 65/25/10 rarity bins; source-native float route is separate | No: legacy typed bounded-integer provider only | No | No: potion generation remains source-owned | No | No |
| `potion.model_within_rarity.index_draw` | potion offer + rarity + ordered eligible pool / legacy `reward`, `event`, or `shop` | Yes: source-ordered pool after excluded models are removed | No: legacy typed index provider only | No | No: potion generation remains source-owned | No | No |
| `potion.uniform_model.index_draw` | potion offer + full ordered potion pool / legacy `reward` or `event` | Partial: configured full-unlock Silent/shared pool | No: legacy typed index provider only | No | No: potion generation remains source-owned | No | No |
| `relic_grab_bag.queue_shuffle.swap_index` | player + relic rarity queue / legacy `reward` | Partial: ordered unowned relic queues and descending Fisher–Yates swaps | No: shared Fisher–Yates loop with legacy typed provider | No | No: relic generation remains source-owned | No | No |
| `relic_grab_bag.rarity.ticket` | reward or merchant relic draw / legacy `reward` | Partial: integer 50/33/17 bins; native source rarity uses a float draw | No: legacy typed bounded-integer provider only | No | No: relic generation remains source-owned | No | No |
| `reward.card_transform.replacement.index_draw` | transformed card instance + source model / legacy `reward` | Partial: source model excluded from ordered prototype reward pool | No: legacy typed index provider only | No | No: fails closed pending a persistent-deck continuation | No | No |
| `combat.draw_pile.shuffle.swap` | player / `run.shuffle` | Yes, pinned Fisher–Yates and hook stages | Partial: injectable primitive, not every ordinary Step shuffle | Partial: source algorithm/cursor checks; live operation capture pending | Partial: provider supports controlled outcomes; ordinary shuffle sites remain separate | Yes, primitive-level | No |
| `combat.hand_return.draw_pile.shuffle_swap_index` | player hand + draw pile / native `run.shuffle` hypothesis; legacy `combat` | Partial: source input order; native call-site assignment pending parity | Yes: typed native and legacy descending swaps | No: provider-specific result/cursor regression pending | No: fails closed pending a saved shuffle continuation | No | No |
| `combat.draw_pile_recycle.swap_index` | player / legacy `combat` stream | Yes, recycle swap bounds | No v111 provider at this site | No | Yes: legacy controlled recycle continuation | Yes | No |
| `combat.draw_pile.draw_card` | player / `semantic.draw_pile` | Yes, source `DrawCards` order and identity | Partial: exact native deck mode; no per-draw provider seam | No | Yes: deferred physical-card identity | Yes, actual draw segment | No |
| `combat.effect.random_enemy` | effect card / `run.combat_targets`; legacy `combat_targets` | Yes, live ordered formation support | Yes: native and legacy typed index providers | No | Yes: saved operation continuation | Yes | No |
| `combat.autoplay.random_enemy` | auto-play card / `run.combat_targets`; legacy `combat_targets` | Partial: source target pool; triggering contexts still need audit | Yes: native and legacy typed index providers | No | No | No | No |
| `combat.autoplay.random_card.select` | source pile + effect / native `run.combat_card_selection` hypothesis; legacy `combat` | Partial: ordered eligible card instances; native stream assignment unverified | Yes: typed native and legacy index providers | No: provider-specific result/cursor regression pending | No: fails closed pending an autoplay continuation | No | No |
| `combat.draw_pile.random_replay_card.select` | draw pile + replay effect / native `run.combat_card_selection` hypothesis; legacy `combat` | Partial: preferred ordered Attack/Skill/Power pool then fallback; native stream assignment unverified | Yes: typed native and legacy index providers | No: provider-specific result/cursor regression pending | No: fails closed pending an effect continuation | No | No |
| `combat.sly_autoplay.random_enemy` | Sly card instance / `run.combat_targets`; legacy `combat` | Yes, source ordered HittableEnemies pool | Yes: native and legacy typed index providers | No | Yes: saved auto-play continuation | Yes | No |
| `combat.generated_card.select` | effect / `run.combat_card_generation`; legacy `combat` | Yes: source-ordered supported model pool | Yes: native and legacy typed index providers | No: provider-specific result/cursor regression pending | Yes: generated-card continuation | Yes | No |
| `combat.card_transform.replacement.index_draw` | transformed card instance / native `run.combat_card_generation` hypothesis; legacy `combat` | Partial: source-shaped eligible model filter; native pool assignment unverified | Yes: typed native and legacy index providers | No: provider-specific result/cursor regression pending | No: fails closed pending a transform continuation | No | No |
| `combat.random_draw_attack.card_model` | draw insertion / `run.combat_card_generation`; legacy `combat` | Partial: source-ordered supported Attack pool; native unlock pool remains incomplete | Yes: native and legacy typed index providers | No: provider-specific result/cursor regression pending | Yes: model then insertion choices | Yes | No |
| `combat.generation.full_pool_shuffle.swap_index` | generated-card effect / native `run.combat_card_generation` or legacy `combat` | Partial: source-shaped descending swaps over modeled pools; native unlock eligibility incomplete | Partial: typed native and legacy swaps for supported effect pools | No: provider-specific result/cursor regression pending | Yes: semantic bounded swap continuation | Yes | No |
| `combat.draw_pile.random_insert_index` | generated draw-pile card / `run.shuffle`; legacy `combat` | Partial: bounded position in two supported operations | Yes: native and legacy typed providers at supported operations | No: provider-specific result/cursor regression pending | Yes: source-timed insertion continuation | Yes | No |
| `combat.draw_pile.random_rare_to_hand.shuffle_swap_index` | rare draw-pile instances + effect / native `run.combat_card_selection` hypothesis; legacy `combat` | Partial: source-ordered Rare instances; native stream assignment unverified | Yes: typed native and legacy descending swaps | No: provider-specific result/cursor regression pending | No: fails closed pending an effect continuation | No | No |
| `combat.relic.after_shuffle.generated_card.random_insert_index` | relic application order / `run.shuffle`; legacy `combat` | Yes: post-shuffle generated card position | Yes: native and legacy typed index providers | No: provider-specific result/cursor regression pending | No: fails closed pending a saved post-shuffle continuation | No | No |
| `combat.enemy.discard.random_insert_index` | creature CombatId + move / `run.shuffle`; legacy `combat` | Yes: random discard insertion from enemy effects | Yes: native and legacy typed bounded-index providers | No: provider-specific result/cursor regression pending | No: fails closed pending an enemy-action continuation | No | No |
| `combat.enemy.draw.random_insert_index` | creature CombatId + move / `run.shuffle`; legacy `combat` | Yes: random draw insertion from enemy effects | Yes: native and legacy typed bounded-index providers | No: provider-specific result/cursor regression pending | No: fails closed pending an enemy-action continuation | No | No |
| `combat.enemy.summon_pool.select` | enemy CombatId + move / legacy `combat` | Partial: last-spawn exclusion and ordered fallback pool; native summon-pool selection remains fail-closed | No: legacy typed index provider only | No | No: fails closed pending an enemy-action continuation | No | No |
| `combat.enemy.theft_card.select` | enemy CombatId + source card pool / native `run.combat_card_generation` hypothesis; legacy `combat` | Partial: rarity/Imbued priority and ordered card instances; native stream assignment unverified | Yes: typed native and legacy index providers | No: provider-specific result/cursor regression pending | No: fails closed pending an enemy-action continuation | No | No |
| `combat.effect.personal_hive.dazed.random_insert_index` | damaged enemy power + attack / native `run.shuffle` hypothesis; legacy `combat` | Partial: per-stack insertion order; native call-site assignment pending parity | Yes: typed native and legacy bounded-index providers | No: provider-specific result/cursor regression pending | No: fails closed pending a nested reaction continuation | No | No |
| `combat.random_hand_card.select` | player hand / native `run.combat_card_selection` hypothesis; legacy `combat` | Partial: ordered eligible instances; native stream assignment unverified | Yes: typed native and legacy index providers | No: provider-specific result/cursor regression pending | Yes: semantic selection event | Yes | No |
| `combat.random_hand_card.energy_cost` | hand card + effect / native `run.combat_energy_costs` hypothesis; legacy `combat` | Partial: bounded cost support; native stream assignment unverified | Yes: typed native and legacy bounded providers | No: provider-specific result/cursor regression pending | Yes: semantic cost event | Yes | No |
| `combat.card_draw.relic_energy_cost` | drawn card / native `run.combat_energy_costs` hypothesis; legacy `combat` | Yes, source draw-hook order | Yes: typed native and legacy bounded providers; native assignment unverified | No: provider-specific result/cursor regression pending | Yes, opt-in typed event | Yes | No |
| `combat.card_draw.slither_energy_cost` | drawn card / native `run.combat_energy_costs` hypothesis; legacy `combat` | Yes, source draw-hook order | Yes: typed native and legacy bounded providers; native assignment unverified | No: provider-specific result/cursor regression pending | Yes, opt-in typed event | Yes | No |
| `combat.card_selection.random_shortlist` | draw-pile selector / native `run.combat_card_selection` hypothesis; legacy `combat` | Partial: source filters and without-replacement prefix; native stream assignment unverified | Yes: native and legacy typed descending swaps | No: provider-specific result/cursor regression pending | Yes: semantic JSONL/Python event | No: not run or covered by a focused test | No |
| `combat.relic.jeweled_mask.random_power_draw_card` | player relic hook / `run.combat_card_selection`; legacy `combat` | Yes: prefers noninnate powers, then the ordered Power pool | Yes: typed native and legacy providers | No: provider-specific result/cursor regression pending | Yes: replayable semantic event resumes the saved BeforeHandDraw hook | No: this continuation has not been mechanics-tested | No |
| `combat.potion_generation.random_model` | effect + potion pool / native `run.combat_potion_generation` hypothesis; legacy `combat` | Partial: ordered supported potion pool; native pool and stream assignment unverified | Yes: typed native and legacy index providers | No: provider-specific result/cursor regression pending | No: fails closed pending a saved potion continuation | No | No |
| `run.potion.fill_empty_slots.model` | player potion slot + ordered potion pool / legacy `reward` | Partial: source slot iteration and supported potion pool | No: legacy typed index provider only | No | No: fails closed pending a run-effect continuation | No | No |
| `relic.acquisition.random_rare_card.model` | player + ordered Rare reward-card pool / legacy `reward` | Yes: rarity-filtered ordered pool | No: legacy typed index provider only | No | No: fails closed pending a relic-acquisition continuation | No | No |
| `relic.acquisition.random_potion.model` | player open potion slot + potion pool / legacy `event` | Yes: first open slot and ordered potion pool | No: legacy typed index provider only | No | No: fails closed pending a relic-acquisition continuation | No | No |
| `run.random_pool_distinct.shuffle_swap_index` | caller-defined ordered pool + take count / caller-owned legacy stream | Partial: shared typed Fisher–Yates compatibility path for legacy event/reward pools | No: shared Fisher–Yates loop with legacy typed provider | No | No: caller continuations remain source-owned | No | No |
| `potion_factory.rarity_roll.unit_float` | potion factory / `run.combat_potion_generation` | Yes: unit-float thresholds choose Common / Uncommon / Rare | Yes: typed unit-float provider around native `NextFloat` | No: provider-specific result/cursor regression pending | Yes: controlled unit-float request | No | No |
| `potion_factory.model_within_rarity.index_draw` | potion factory / `run.combat_potion_generation` | Yes: source-ordered remaining tier pool after rarity choice | Yes: typed model index provider | No: provider-specific result/cursor regression pending | Yes: model choice controlled within the chosen tier | No | No |
| `run.relic.beautiful_bracelet.deck_shuffle.swap_index` | Beautiful Bracelet acquisition / `run.niche` | Yes: ordered eligible deck cards; first four selected after full shuffle | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | No | No | No |
| `run.relic.distinguished_cape.modifier_curse.index_draw` | Distinguished Cape acquisition / `run.niche` | Yes: two ordered draws without replacement from source modifier-eligible curse pool | Yes: native-v111 and legacy typed providers | Yes: existing exact curse IDs and cursor regression | No | No | No |
| `run.relic.fur_coat.map_node_shuffle.swap_index` | Fur Coat acquisition + act / `context.player.*` | Yes: current-act Combat and Elite nodes; first eight marked after full shuffle | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | No | No | No |
| `map.legacy.floor_width.index_draw` | act + floor width / legacy `map` | Partial: prototype floor-rule width range; native source map is a separate generator | No: legacy typed index provider only | No | No: map generation remains source-owned | No | No |
| `map.legacy.edge_source.index_draw` | act + source/target floors + target node / legacy `map` | Partial: balanced eligible source nodes and ordered support | No: legacy typed index provider only | No | No: map generation remains source-owned | No | No |
| `map.legacy.orphan_target.index_draw` | act + source node + target floor / legacy `map` | Partial: least-incoming target filter and ordered support | No: legacy typed index provider only | No | No: map generation remains source-owned | No | No |
| `map.legacy.edge_extra_chance.ticket` | act + source node / legacy `map` | Partial: prototype 35% edge-extension ticket uses `NextInt(100)` | No: legacy typed bounded-integer provider only | No | No: map generation remains source-owned | No | No |
| `map.legacy.second_edge_source.index_draw` | act + layer pair / legacy `map` | Partial: eligible sources with fewer than two outgoing edges | No: legacy typed index provider only | No | No: map generation remains source-owned | No | No |
| `map.legacy.convergence.order_key` | act + target node / legacy `map` | Partial: per-target bounded random keys and stable ordering | No: legacy typed bounded-integer provider only | No | No: map generation remains source-owned | No | No |
| `map.legacy.convergence.source.index_draw` | act + target node + source support / legacy `map` | Partial: source nodes that can add a nonduplicate edge | No: legacy typed index provider only | No | No: map generation remains source-owned | No | No |
| `map.legacy.second_edge_target.index_draw` | act + source node + target support / legacy `map` | Partial: least-incoming available targets | No: legacy typed index provider only | No | No: map generation remains source-owned | No | No |
| `map.legacy.required_room.node.index_draw` | act + floor + required room / legacy `map` | Partial: legal nodes for each required room | No: legacy typed index provider only | No | No: map generation remains source-owned | No | No |
| `map.legacy.room_assignment.index_draw` | act + floor + node / legacy `map` | Partial: legal room pool after duplicate-special filtering | No: legacy typed index provider only | No | No: map generation remains source-owned | No | No |
| `map.later_act.path_start_column.index_draw` | act map / `context.act_map.*` | Partial: seven path starts and second-path collision retries | Yes: bounded typed provider requests | No: provider-specific result/cursor regression pending | No | No | No |
| `map.later_act.path_delta.shuffle_swap_index` | act map / `context.act_map.*` | Partial: ordered `[-1, 0, 1]` path deltas | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | No | No | No |
| `map.later_act.glory_rest_count.index_draw` | Glory map / `context.act_map.*` | Yes: source `NextInt(2)` rest quota | Yes: bounded typed provider request | No: provider-specific result/cursor regression pending | No | No | No |
| `map.later_act.room_assignment.shuffle_swap_index` | act map / `context.act_map.*` | Partial: ordered unassigned points, shuffled per room quota | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | No | No | No |
| `map.later_act.hive_rest_count.gaussian_int` | Hive map / `context.act_map.*` | Yes: truncated rounded Gaussian, inclusive 6–7 | Yes: typed Gaussian-int provider preserves native rejection sampling | No: provider-specific result/cursor regression pending | No | No | No |
| `map.later_act.unknown_count.gaussian_int` | Hive/Glory map / `context.act_map.*` | Yes: truncated rounded Gaussian 10–14, then minus one | Yes: typed Gaussian-int provider preserves native rejection sampling | No: provider-specific result/cursor regression pending | No | No | No |

This is the migrated typed-site inventory, not a complete v0.111 RNG
inventory. Other encounter resolvers, summon/death lifecycle effects,
monster-local draws, enemy-side insertion, remaining cost/reward sites,
other map/event/shop/treasure selection, and remaining native float/Gaussian/
rejection call sites still need stable identities and row-level audit. Typed unit-float,
ranged-float/double, Gaussian-integer, and Gaussian-double primitive adapters
exist, but that does not establish coverage for any additional gameplay site.
Their absence here is not evidence that they are deterministic or supported.
The two random hand-cost operations now use typed providers in both profiles:
native card selection and cost rolls are assigned to `run.combat_card_selection`
and `run.combat_energy_costs`, respectively, while legacy calls retain
`combat`. These native assignments remain hypotheses pending source-call-order
parity and are not Inspector-verified.
The random draw-pile shortlist also uses typed descending swaps; native mode
uses the `run.combat_card_selection` assignment hypothesis while legacy mode
retains `combat`. Controlled Nature continues to resolve its existing saved
shortlist continuation.
The distinct generated-colorless path uses the typed legacy shuffle but
remains fail-closed when native combat state is attached because its modeled
pool omits locked or unimplemented source card models.

The direct API source census is generated by
[`tools/rng_callsite_inventory.py`](../tools/rng_callsite_inventory.py) and
stored in [`RNG_DIRECT_CALLSITE_CENSUS.md`](RNG_DIRECT_CALLSITE_CENSUS.md).
The current source has 95 direct calls to the three scanned APIs: 67 legacy
`PrototypeRng` calls, 16 `NativeRngV111` primitive calls, and 12
`NativeFisherYatesV111` calls. The report provides file, line, expression and
review category. It is a code-site tracing aid; indirect provider dispatch,
delegates and the native game's own source call sites are outside its scope.
Rows must be linked to stable matrix IDs or classified as infrastructure
before the D0 inventory can be considered complete.

## First migrated site: `combat.initial_unique_hp`

The source-shaped mechanic computes an **ordered** HP candidate list, excluding
previously selected monster maxima. If no distinct HPs remain, it restores the
full range. It asks the provider for a *candidate index* at the actual creation
site. This preserves ordered eligibility and native `NextInt(n)` behavior;
specifically, `NextInt(1)` is **not skipped**.

Request identity includes `SiteId`, `StreamId`, `OwnerId` (stable CombatId),
`Ordinal` (creation order), `OrderedCandidates`, `OperationKind`, and `SourceVersion`.
The HP site uses `choose_index` with an ordered candidate array; the
committed-intent callback uses **compact** `bounded_integer` plus a positive
`BoundedCount`, because the sampled integer may represent a ticket within
weighted legal AI branches. Unlike an enumerated array, this supports even
large integer ranges without allocating the full support. Both use the
pinned primitive `NextInt`.
A transcript records selected index, provider provenance and, in native mode,
exact pre/post xoshiro counter and state words. Outcomes are **not** assigned
reference probabilities. The native attachment returns the unchanged
`CombatState` for existing callers, and optionally a separate audited
transcript via `AttachAndRollInitialHpWithAudit`.

Tests compare **whole exact state hashes** (including all named RNG streams)
against the pre-refactor deterministic native fixture across multiple seeds,
and assert ownership/order/counters. Standalone scripted replay additionally
rejects changed owner, eligibility, order, missing/excess choices, and invalid
controlled indices without consuming a native stream.

## D2 and D3 gates

**D2 progress:** initial `run.niche` unique HP, `run.monster_ai` committed
intent index rolls, and supported encounter-formation index draws now use
the common provider.
The intent engine's eligibility logic (source move state, weighted
branches, anti-repeat, opening sequence and per-creature commit order)
remains inside `PrototypeGameEngine.SelectEnemyMove`. The callback
to its existing `nextIndex(maxExclusive)` asks for an ordered index
`[0,...,maxExclusive-1]` after the eligibility checks. The legal primitive range is
represented by `BoundedCount=maxExclusive`, not an allocated integer array.
It records a per-enemy CombatId owner and global per-transition
occurrence sequence. `CommitIntentsWithAudit` is opt-in and returns
the selected indices and exact pre/post native stream words/counters.
Seeded fixture tests assert entire combat hashes and exact cursor
equality, including encounters with deterministic enemies that consume
no draws. Encounter-local formation index draws also pass through the
native provider under `combat.encounter.formation.index_draw`; the opt-in
`GenerateWithAudit` result exposes each request while the compatibility
`Generate` method keeps the existing formation result. Singleton formation
draws still consume `NextInt(1)`. The shared `combat.initial_unique_hp` site
also covers attached summoned-creature HP; Tough Egg's direct hatch roll is
separately handled by `NativeRandomIndexProviderV111` as
`combat.tough_egg.hatchling_hp`. It preserves one `NextInt(4)` on `run.niche`
and records the native `CombatId` as its owner. Distinguished Cape's two
modifier-curse picks also use the shared index provider in native mode; the
pool shrinks after each selection, preserving source bounds of ten then nine.
These are native provider integrations, not controlled summon/death
continuations.

**D2 remaining:** formation resolvers outside the integrated subset,
monster-local draws, summon/death continuation mechanics,
shuffle/targets/rewards, full map source-order parity, event sites beyond
Orobas, Darv, Pael, Tezcatara, Nonupeipe, Tanx and
Vakuu offer generation,
and other audited call sites; explicit weighted
outcome metadata at move-level instead of primitive index selections.
The separate semantic playable API now exposes typed weighted intent events,
deferred physical-card draws, random-enemy targets, generated-card model
pools, opt-in drawn-card costs and source card-selection choices. Those
controlled events do not complete the native provider seam or native cursor
parity rows in the migration matrix.

**D3 partial:** the initial HP and committed enemy intent sites now have
persisted, source-reachable pending Nature choices, with fork, restore,
precise eligibility validation and controlled (non-native) provenance.
Completing HP resumes into intents **without rerolling HP**; prior HP
transcripts are part of the intent snapshot. For incomplete intent
decisions, only the side-effect-free AI selector is re-entered with
verified tickets. No prior game actions or hooks are rerun.
This is still restricted to isolated combat-entry fixtures; it does
**not** attach controlled continuation to general `RunState` / player
actions in the D3 HP/intent provider itself and must not fabricate a
`NativeCombatRngV111` sidecar. A separate P0 semantic pilot attaches selected
source continuations to `RunState`, including end turn, card draw, random
targets and generated-card pools; broad player-card and trigger coverage
remains outstanding.

**D4 partial:** opt-in `nature_begin`, `nature_resolve`,
`nature_advance`, `nature_fork`, `nature_save`, `nature_load`, and
`nature_release_many` are available in the JSONL server and through
the thin Python binding, using **distinct non-playable handles**. The playable
semantic protocol also has a bounded enumerator for every legal outcome of
one small pending chance event. A separate playable semantic schema exposes
seeded entry, player actions, weighted
intent IDs, typed draw/target/generated-card resolution, and exact snapshot
replay through the same JSONL/Python bridge.
Existing `step` and native RNG paths retain semantics. Next D3/D4
work must wire controlled choices into general game action continuation
and trained agent-facing observations. Full native-oracle fidelity
remains separate from seeded emulator RNG equality. See
[the isolated Nature bridge](DESIRED_RNG_D3_INTENT_BRIDGE.md).

## Known limitations

- The `RandomIndex` provider handles finite ordered **index** decisions. A
  separate typed unit-float provider wraps native and controlled samples, and
  a Gaussian-integer provider wraps the native inclusive rejection sampler;
  a Gaussian-double provider wraps the native bounded rejection sampler and
  records its full cursor span, and ranged-float/double providers wrap native
  `NextFloat`/`NextDouble` range calculations. The unit-float provider accepts
  `1.0f`, which can result from rounding a native value below one.
  The semantic playable extension separately supports typed physical
  card, enemy-instance and generated-card-model identities. Ranged-float,
  ranged-double and Gaussian-double gameplay site identities, other rejection
  operations and dynamic weighted choices still need source call-site audits.
  The new primitive adapters add no gameplay coverage by themselves.
- The scripting provider validates an exact request sequence but is not yet
  a serializable resumable game controller.
- Provider instance state is not in canonical `RunState`, so callers must
  not treat replay/controlled providers as persisted Nature mode.
- The native Inspector is a **separate** read-only oracle; its current
  concrete Strike fixture validates only restricted visible mechanics.
- Native RNG cursor agreement on seeded emulator fixtures does not prove
  full native game agreement, conditional probability calibration, or
  mechanical equivalence at unmigrated call sites.

See [the complete desired RNG specification](DESIRED_RNG_IMPLEMENTATION.md).

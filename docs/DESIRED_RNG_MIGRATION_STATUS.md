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
| `combat.initial_unique_hp` | creature CombatId / `run.niche` | Yes: initial and attached summons | Yes | Yes: seeded result/hash/cursor | Yes: initial entry only | Yes | No |
| `combat.tough_egg.hatchling_hp` | Tough Egg CombatId / `run.niche` | Yes, direct hatch range and call timing | Yes: typed index provider | Yes: existing seeded hatch oracle | No | No | No |
| `combat.committed_intent.index_draw` | creature CombatId / `run.monster_ai` | Yes, supported moves | Yes, subset | Yes: seeded result/hash/cursor | Yes: isolated entry, source route and automatic turn continuation | Yes | No |
| `combat.encounter.formation.index_draw` | encounter entry + floor / `context.encounter.*` | Partial: supported source resolvers; singleton calls preserved | Yes: native typed provider | Yes: existing seeded result/cursor regressions | No | No | No |
| `event.orobas.relic_offer.index_draw` | Orobas event / `context.event.OROBAS.player_0` | Yes: three ordered eligible pools; singleton fallback still consumes | Yes: typed provider and opt-in audit | No: provider-specific result/cursor regression pending | Yes: one-shot provider, persisted nonnative mode and cursor validation | No | No |
| `event.darv.offer_variant.index_draw` | Darv event / `context.event.DARV.player_0` | Yes: two legal offer shapes | Yes: bounded typed provider request | No: provider-specific result/cursor regression pending | Yes: one-shot provider, persisted nonnative mode and cursor validation | No | No |
| `event.darv.relic_offer.shuffle_swap_index` | Darv event / `context.event.DARV.player_0` | Yes: descending Fisher–Yates over the ordered eligible relic pool | Yes: typed provider at each swap | No: provider-specific result/cursor regression pending | Yes: one-shot provider, persisted nonnative mode and cursor validation | No | No |
| `event.pael.relic_offer.index_draw` | Pael event / `context.event.PAEL.player_0` | Yes: three ordered pools, including repeated entries as weights | Yes: typed provider; native entry preserves source draws | No: provider-specific result/cursor regression pending | Yes: one-shot generation and nonnative mode persisted | No | No |
| `event.tezcatara.relic_offer.index_draw` | Tezcatara event / `context.event.TEZCATARA.player_0` | Yes: three ordered pools with deck-dependent first-pool filtering | Yes: typed provider; native entry preserves source draws | No: provider-specific result/cursor regression pending | Yes: one-shot generation and nonnative mode persisted | No | No |
| `event.nonupeipe.relic_offer.shuffle_swap_index` | Nonupeipe event / `context.event.NONUPEIPE.player_0` | Yes: eligible source-ordered pool and saved Bracelet condition | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | Yes: one-shot generation and nonnative mode persisted | No | No |
| `event.tanx.relic_offer.shuffle_swap_index` | Tanx event / `context.event.TANX.player_0` | Yes: entry-time eligible relic pool and saved Tri-Boomerang condition | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | Yes: one-shot generation and nonnative mode persisted | No | No |
| `event.vakuu.relic_offer.shuffle_swap_index` | Vakuu event / `context.event.VAKUU.player_0` | Yes: three ordered relic pools; ordinals span all three shuffles | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | Yes: one-shot generation and nonnative mode persisted | No | No |
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
| `combat.effect.personal_hive.dazed.random_insert_index` | damaged enemy power + attack / native `run.shuffle` hypothesis; legacy `combat` | Partial: per-stack insertion order; native call-site assignment pending parity | Yes: typed native and legacy bounded-index providers | No: provider-specific result/cursor regression pending | No: fails closed pending a nested reaction continuation | No | No |
| `combat.random_hand_card.select` | player hand / native `run.combat_card_selection` hypothesis; legacy `combat` | Partial: ordered eligible instances; native stream assignment unverified | Yes: typed native and legacy index providers | No: provider-specific result/cursor regression pending | Yes: semantic selection event | Yes | No |
| `combat.random_hand_card.energy_cost` | hand card + effect / native `run.combat_energy_costs` hypothesis; legacy `combat` | Partial: bounded cost support; native stream assignment unverified | Yes: typed native and legacy bounded providers | No: provider-specific result/cursor regression pending | Yes: semantic cost event | Yes | No |
| `combat.card_draw.relic_energy_cost` | drawn card / `run.combat_energy_costs` | Yes, source draw-hook order | Partial: private engine-owned native-codec stream; assignment unverified | No | Yes, opt-in typed event | Yes | No |
| `combat.card_draw.slither_energy_cost` | drawn card / `run.combat_energy_costs` | Yes, source draw-hook order | Partial: private engine-owned native-codec stream; assignment unverified | No | Yes, opt-in typed event | Yes | No |
| `combat.card_selection.random_shortlist` | draw-pile selector / native `run.combat_card_selection` hypothesis; legacy `combat` | Partial: source filters and without-replacement prefix; native stream assignment unverified | Yes: native and legacy typed descending swaps | No: provider-specific result/cursor regression pending | Yes: semantic JSONL/Python event | No: not run or covered by a focused test | No |
| `combat.relic.jeweled_mask.random_power_draw_card` | player relic hook / `run.combat_card_selection`; legacy `combat` | Yes: prefers noninnate powers, then the ordered Power pool | Yes: typed native and legacy providers | No: provider-specific result/cursor regression pending | Yes: replayable semantic event resumes the saved BeforeHandDraw hook | No: this continuation has not been mechanics-tested | No |
| `combat.potion_generation.random_model` | effect + potion pool / native `run.combat_potion_generation` hypothesis; legacy `combat` | Partial: ordered supported potion pool; native pool and stream assignment unverified | Yes: typed native and legacy index providers | No: provider-specific result/cursor regression pending | No: fails closed pending a saved potion continuation | No | No |
| `potion_factory.rarity_roll.unit_float` | potion factory / `run.combat_potion_generation` | Yes: unit-float thresholds choose Common / Uncommon / Rare | Yes: typed unit-float provider around native `NextFloat` | No: provider-specific result/cursor regression pending | Yes: controlled unit-float request | No | No |
| `potion_factory.model_within_rarity.index_draw` | potion factory / `run.combat_potion_generation` | Yes: source-ordered remaining tier pool after rarity choice | Yes: typed model index provider | No: provider-specific result/cursor regression pending | Yes: model choice controlled within the chosen tier | No | No |
| `run.relic.beautiful_bracelet.deck_shuffle.swap_index` | Beautiful Bracelet acquisition / `run.niche` | Yes: ordered eligible deck cards; first four selected after full shuffle | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | No | No | No |
| `run.relic.distinguished_cape.modifier_curse.index_draw` | Distinguished Cape acquisition / `run.niche` | Yes: two ordered draws without replacement from source modifier-eligible curse pool | Yes: native-v111 and legacy typed providers | Yes: existing exact curse IDs and cursor regression | No | No | No |
| `run.relic.fur_coat.map_node_shuffle.swap_index` | Fur Coat acquisition + act / `context.player.*` | Yes: current-act Combat and Elite nodes; first eight marked after full shuffle | Yes: typed provider at each descending swap | No: provider-specific result/cursor regression pending | No | No | No |
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
The current source has 176 direct calls to the three scanned APIs: 149 legacy
`PrototypeRng` calls, 16 `NativeRngV111` primitive calls, and 11
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

# Controlled run-action continuation

This opt-in D3 boundary saves the legacy `start_run` action at its Act 1 boss
encounter draw and unconfigured prototype `continue_act` at registered
`map.legacy.*` draws during later-act map generation. It also saves map choices
that enter an Event node at supported legacy event-entry draws, legacy
Shop-node offer generation, eligible legacy Treasure-node
relic selection, Native-regional Treasure and Shovel Dig through retained
legacy reward requests when `NativeRunRng` is absent, eligible legacy regional
Unknown-node entry, legal legacy Act 1 Overgrowth and Underdocks Combat/Elite
node entry, and legacy Shovel dig at its unowned-relic draw. Combat entry controls
registered `combat`-stream requests and the two legacy
potion-factory index sites used by pre-combat relic hooks through ordinary
room initialization; other requests and streams retain their existing
providers. Shop generation requires non-native regional systems and a legacy
run RNG. Ordinary legacy Treasure generation uses that same route. Native-
regional Treasure and Shovel fallbacks require `NativeRunRng` to be absent and
retain the legacy reward rarity-ticket and conditional relic-index requests.
The same saved action frame also supports legal `take_reward_relic` and
`take_reward_extra_relic` actions from ordinary Combat, Elite, or Boss rewards
when `NativeRunRng` is absent, if their acquisition hook reaches one of the
registered relic-acquisition index sites listed below. It saves the selected
relic action and replays all supported hook requests in order; configured
Native combat-reward relic picks remain with their existing specialized
ReferenceNature session.
The same frame can save a legal legacy `rest_heal` when an active relic
creates rest-site card rewards and `NativeRunRng` is absent. It controls the
registered reward-rarity and card-model requests, including the legacy
fallback requests used by the Native-shaped Dream Catcher reward generator;
configured Native reward generation uses the typed ReferenceNature
run-action continuation described below.
The frame also supports a legal later `choose_shop_relic_deck_card` or
`choose_relic_deck_card` action when a pending relic-acquisition choice makes a
random persistent-deck transform. It binds the action fingerprint to the
selected card and validates the source relic. The pending request binds the
selected card instance, original model and ordered replacement pool, then
replays through the same `reward.card_transform.replacement` Chance Law. Its
outcomes use `card:<model>` identities from that source law; the event ID also
binds the exact legal action boundary.
Fixed-result transforms and unrelated deck-choice actions retain their
existing owners.
For supported legacy acquisition requests, the run-action Chance Law also
reuses the existing `card:<model>` and `potion:<id>` laws for random Rare
card grants, random potion grants, and empty-slot potion fills. It checks the
saved request owner, ordinal and support against those laws and binds the
event ID to the action boundary. Native `player.rewards` requests keep their
Native index masses.
Legacy Treasure entry likewise reuses the existing `relic:<id>` law after
validating the saved Act, floor, owned-relic pool and ordered indices. The
player action still resolves by the selected index.
For legacy Rest Heal rewards, `reward.card_model.index_draw` reuses its
`card:<model>` law after reconstructing the source reward group, selected
rarity and prior card picks from the saved action prefix. The preceding
`reward.card_rarity.ticket` remains a raw ticket law for source-compatible
resolution.
`ControlledRunActionEngineV111.BeginLegacyActTransition` is the explicit
entry point for the unconfigured prototype transition route.
Legacy Treasure selection is not offered when Silver Crucible suppresses the
first treasure or its relic pool is exhausted. Treasure fallback entry still
records its rarity ticket when no eligible identity remains and then completes
without a model-index request. The boundary uses ordinary run-state
handles and does not change normal `step` behavior or configured Native
regional merchant/treasure routes.

For a configured Act 1 Underdocks start, `ReferenceNatureRunActionSessionV111`
also suspends on the exact `combat.encounter.underdocks.boss.index_draw`
request. It replays the existing configured initializer with the selected
boss index, then stores the sampled law and decision in the run-initialization
transcript with the committed Nature cursor. The request filter leaves the
initializer's other draws on their current providers; the hidden Overgrowth
boss draw inside the shared starter is not selected by Nature. The configured
experimental three-act path continues from that boss through Ancient
assignment and Hive/Glory encounter-bag preparation. The equal-index boss and
later-act laws remain `UNVERIFIED`.

For configured Act 1 Overgrowth, the same session suspends first on the
Overgrowth boss index and then composes the existing
`ReferenceNatureMapGenerationSessionV111`. Each resolve advances one boss or
typed map request. While map generation is pending, the run remains private;
the completed session installs the map and commits the complete ordered
run-initialization transcript, map audit, controlled boss trace, and Nature
cursor together. JSONL reports map requests with pending kind
`run_action_map_generation`. Other initializer draws retain their current
providers. Boss equal-index probability, Gaussian quota masses, Native map
fidelity, and Native cursor parity remain open.

On the experimental three-act route, Overgrowth continues from the completed
map into the existing Ancient-assignment and later-act bag generators.
Underdocks begins that preparation immediately after its boss choice. Each
resolve samples one assignment or bag choice; the run publishes only after all
prepared bags are complete. JSONL reports these requests with pending kind
`run_action_later_act_preparation`. The saved session replays the existing
generators from the ordered prefix, and the run snapshot carries a pending
initialization marker while the outer session owns the uncommitted Nature
cursor and partial bags.

For a configured Act 1 run using the experimental Native combat-entry profile,
the same boundary can also suspend on the regional weak, normal, or elite
encounter-pool index and on `combat.encounter.formation.index_draw` during a
Combat, Elite, or Boss map-node action. Pool selection uses the existing
ordered support and `combat` request stream; formation uses the encounter-local
context stream. When a combat-entry relic can generate a potion into an open
slot, this same run-action owner also captures the Native
`potion_factory.rarity_roll.unit_float` and
`potion_factory.model_within_rarity.index_draw` requests on
`run.combat_potion_generation`. It binds each rarity/model pair to the chosen
map node and potion slot, and conditions the model support on the preceding
rarity outcome. The run-action snapshot retains the typed provider prefix and
reconstructs it by replay on load; the committed run-action history keeps the
mixed typed decisions and its existing index projection. Other entry requests
keep their existing providers. A fixed pool assignment and formation can
complete at `begin` without taking a Nature sample when no other supported
draw is reached. These counterfactual choices leave their corresponding
provider cursors untouched.
Pool-selection weights and Native source-pool fidelity remain `UNVERIFIED`;
PotionFactory laws verify only the declared `NextFloat`/`NextInt` transforms
and conditioned pool, not source-pool completeness, stream attribution or
cursor parity.

## Saved state, support, and player projection

The pending frame stores the exact pre-action run state, action kind, request,
and ordered candidate labels. Event-entry and Act 1 combat-entry frames retain
the selected map-node ID. Resolving one index replays the action from that
checkpoint and records the controlled decision in
`RunState.ControlledRunActionHistory`. Run start commits the selected boss
encounter; map entry commits the selected Event, Shop, Treasure, Unknown, or
supported Act 1 Combat/Elite room through ordinary initialization; Shovel
commits the relic as the normal reward. The ordered candidate labels encode
the source-eligible support for the saved request. Player-facing
observations hide those private constraints and Nature actions until
mechanics commits a result.
For a random relic deck transform, the pending frame also retains the exact
legal deck-choice action. Replay verifies the relic's transform specification,
selected card, original model, replacement labels and request before the
transformed deck and cursor are published.
The pending continuation is part of ordinary run snapshots. Player-facing
observations hide its Nature actions; the explicit controller exposes only
`choose_controlled_run_action_outcome` actions while the frame is pending.
For supports larger than the advertised inline-action limit, the controller
omits expanded action objects and candidate labels but retains the compact
request bounds. The Chance Law still lists each legal request index, which
clients resolve through `controlled_run_action_resolve`.

## Probability provenance

Each pending response now includes a read-only `chanceLaw`, its
`boundaryFingerprint`, and an `eventId`. The law assigns equal weight to each
legal request index (`choice_index:n`). Candidate labels identify the
mechanical result for that index but do not change the index law. Every law
and controlled action transcript remains `UNVERIFIED` for source probability
and stream parity. The law fingerprints only the typed random request; the
pending boundary fingerprint binds action/request context without including
private RNG state. The top-level `fingerprint` is the exact-state
stale-request guard.

The JSONL server advertises `controlledRunActionSchemaId`. Its `hello`
response exposes `controlledRunActionSiteGroups`, with registry-owned arrays
for run-start/map generation, Event-node entry, legacy Shop-node generation,
legacy and Native-regional fallback Treasure-node selection, Shovel dig,
rest-heal reward generation, Native-regional fallback Shovel Dig, and
region-specific Overgrowth and
Underdocks combat entry. Shared formation, HP, intent, shuffle and potion sites
appear in both region groups. The controller supports:

| Operation | Inputs | Result |
| --- | --- | --- |
| `controlled_run_action_begin` | current `state_handle`, legal `action_id` | Pending request, labels and controlled outcome actions |
| `controlled_run_action_resolve` | pending `state_handle`, `selected_index` | Child run handle with the selected run-start, configured boss, event-entry, Shop, legacy or fallback Treasure, rest-heal reward, Act 1 combat-entry or Shovel result |
| `controlled_run_action_inspect` | pending `state_handle` | Restore the pending request and legal outcomes after snapshot load |

The JSONL server also exposes an atomic ReferenceNature envelope for these
supported run-action continuations:

| Operation | Inputs | Result |
| --- | --- | --- |
| `reference_nature_run_action_begin` | current `state_handle`, optional `nature_seed`, optional legal `action_id` (Shovel dig is the only default) | Session handle with pending law and sampler cursor |
| `reference_nature_run_action_status` | run-action session handle | Current pending or committed boundary; ordinary `stateHandle` after commit |
| `reference_nature_run_action_resolve_next` | handle and current session `fingerprint` | Child session after one sampled typed outcome is resolved through ordinary run mechanics |
| `reference_nature_run_action_fork` | run-action session handle | Independent child session |
| `reference_nature_run_action_save` | run-action session handle | Replay-validated snapshot payload |
| `reference_nature_run_action_load` | snapshot | New session handle after transcript replay |
| `reference_nature_run_action_release_many` | `reference_nature_run_action_handles` | Number of released sessions |

Supply `nature_seed` the first time a run uses ReferenceNature. Once a session
commits, its cursor is stored in the run snapshot and later sessions continue
it when `nature_seed` is omitted. Runs without a committed cursor require a
seed. New exact snapshots use `sts2-exact-snapshot-v4-simulation-envelope`;
the loader continues to accept flattened v1–v3 snapshots.

The session supports legacy `start_run` boss encounter and initial-map draws,
unconfigured prototype `continue_act` map-generation draws on the registered
legacy map sites, and the
configured Underdocks boss draw described above, legacy map Event-node entry
draws, the legacy regional Unknown-room 1000-way ticket,
seven `shop.*` sites in legacy Shop-node generation,
the `treasure.relic_model.index_draw` legacy Treasure-node site, the Native-
regional Treasure/Shovel fallbacks' `relic_grab_bag.rarity.ticket` and
conditional `treasure.relic_grab_bag.model.index_draw` sites, registered
combat-stream requests and the two legacy potion-factory index requests during
legal legacy Overgrowth or Underdocks Combat/Elite node entry, and Shovel's
unowned-relic draw. Combat entry covers each region's encounter-pool
selection, shared formation, initial unique-enemy HP, initial committed
intents, opening draw-pile shuffle,
and pre-combat relic potion generation. Filtered replay leaves unrelated
requests on their existing providers; an unregistered combat-stream request
remains unsupported and fails closed. The same ReferenceNature run-action session also accepts
`buy_card`, `buy_potion`, `replace_shop_potion`, `remove_card`, and `buy_relic`
Shop actions. Actions with no random request commit immediately with an empty
transcript and no Nature sample. The session can suspend at the registered
index-only acquisition sites advertised as `shopPurchaseAcquisitionEffects`:
Beautiful Bracelet and Distinguished Cape shuffles, Sand Castle upgrades,
random rare-card and potion acquisitions, potion-slot fills, and
persistent-card transform replacement. It replays the exact Shop action and
validates the pending request and labels against the saved source state.
Their generic request-index laws use exact Native `NextInt` grid masses and
report `SOURCE_VERIFIED` on canonical Native stream IDs such as `run.niche`
and `player.rewards`; legacy and unknown stream requests remain
`UNVERIFIED`. This claim covers only the bounded transform on the declared
support, not stream attribution, source-pool completeness, seed frequencies,
or cursor parity. Controlled choices do not advance game RNG. Unit-float
acquisition effects and all other unregistered purchase hooks remain
fail-closed. The session keeps supported Chance Law samples and run-state
resolution atomic across fork/save/restore; it does not enable native regional
merchant or treasure entry providers or other unlisted run/reward/map
operations.
The configured experimental Native Act 1 path adds the supported encounter
pool and formation index requests to this same continuation. When a
pre-combat potion relic has an open slot, its PotionFactory rarity and model
requests use the typed continuation above; initial HP, intents, opening shuffle
and other combat-entry requests remain on their existing providers.
Once committed, the session result also provides a regular `stateHandle` for
continuing play through the ordinary run API; no state handle is published
while a Nature choice remains pending.
The Python binding exposes `State.begin_reference_nature_run_action(nature_seed=None,
action_id=...)` and session `resolve_next()`, `fork()`, `save_snapshot()`,
`Session.load_reference_nature_run_action_snapshot()`, `release()` and the
committed session's `.state` property.

## Unsupported routes and remaining limits

Legacy run-start and unconfigured prototype act-transition continuations cover
the listed `map.legacy.*` request sites during initial and later-act legacy map
generation. Configured Overgrowth starts and configured experimental Hive/Glory
act transitions use the composed ReferenceNature map-generation session
described above. Ordinary Native map regeneration remains outside these continuations.
Event-entry continuation covers legacy
`event.selection.weighted_ticket` and listed legacy event initialization draws
when a map Event node is selected. The separate
`event.unknown_room.odds.ticket` request is supported only when an unconfigured
legacy run with no `NativeRunRng` enters an available Unknown node in a regional
map profile; its 1000 equal request indices remain `UNVERIFIED` because they
approximate the source unit-float mapping. Other configured Native run streams
and source cursor parity remain outside this boundary, apart from the
configured run-start and Act 1 encounter continuations described above.

The supported legacy event-entry initialization sites are:

- `event.generated_gold.index_draw`
- `event.stone_of_all_time.potion_slot.index_draw`
- `event.fake_merchant.relic_offer.shuffle_swap_index`
- `event.endless_conveyor.dish_weighted_ticket`
- `event.future_of_potions.card_type.index_draw`

The supported legacy Shop-node generation sites are:

- `shop.card_rarity.ticket`
- `shop.card_model.index_draw`
- `shop.card.price.index_draw`
- `shop.potion_offer.shuffle_swap_index`
- `shop.potion_offer.price.index_draw`
- `shop.relic_offer.shuffle_swap_index`
- `shop.relic_offer.price.index_draw`

The supported legacy Treasure-node site is `treasure.relic_model.index_draw`.
The Native-regional Treasure and Shovel fallbacks accept
`relic_grab_bag.rarity.ticket`, followed by
`treasure.relic_grab_bag.model.index_draw` only when the selected rarity has
eligible relics. Both use sorted Nature relic support and equal-index
`UNVERIFIED` legacy request weights.

The supported legacy Overgrowth combat-entry sites are:

- `combat.encounter.overgrowth_elite.index_draw`
- `combat.encounter.overgrowth_weak.index_draw`
- `combat.encounter.overgrowth_normal.index_draw`
- `combat.encounter.formation.index_draw`
- `combat.encounter.weighted_selection.ticket`
- `combat.initial_unique_hp`
- `combat.committed_intent.index_draw`
- `combat.draw_pile.shuffle.swap`
- `potion.rarity_roll.ticket` (pre-combat relic potion generation only)
- `potion.model_within_rarity.index_draw` (pre-combat relic potion generation only)

Underdocks entry shares the common formation, HP, intent, shuffle and potion
sites above, and adds these encounter-pool selections:

- `combat.encounter.underdocks.elite.index_draw`
- `combat.encounter.underdocks.weak.index_draw`
- `combat.encounter.underdocks.normal.index_draw`

This entry boundary requires the matching Act 1 region and encounter pool,
with Underdocks additionally requiring its native-shaped map profile. The
legacy entry boundary excludes configured Native runs and later-act
encounters. The separate experimental Native continuation described above
handles supported Act 1 encounter-pool and formation requests. Generic
request-index laws on canonical Native stream IDs report exact Native
`NextInt` grid masses for the declared bound; legacy `combat` and `event`
requests remain `UNVERIFIED`. Native stream attribution, source-pool
completeness, seed frequencies and cursor parity remain separate. The two
`potion.*` requests use the legacy `event` stream and are intercepted only at
this combat-entry boundary.

The registered index-only Shop-purchase acquisition sites are:

- `run.relic.beautiful_bracelet.deck_shuffle.swap_index`
- `relic.distinguished_cape.modifier_curse.index_draw`
- `relic.sand_castle.acquisition_upgrade.shuffle_swap_index`
- `relic.acquisition.random_rare_card.model`
- `relic.acquisition.random_potion.model`
- `event.potion.fill_empty_slots.model`
- `run.potion.fill_empty_slots.model`
- `reward.card_transform.replacement.index_draw`

The ordered site IDs are exposed in `hello` under
`controlledRunActionSiteGroups.shopPurchaseAcquisitionEffects`.

Although that capability group retains its established name, the same finite
registry set covers legacy combat-reward relic acquisition hooks. The
ReferenceNature run-action session can begin from a legal combat reward
`take_reward_relic` or `take_reward_extra_relic` action, sample each exact
pending index support, and replay the saved action before committing the
Nature cursor. Other reward actions, event rewards, and configured Native
combat-reward relic hooks stay with their existing boundaries.

Rest-heal reward-generation requests are advertised in the separate
`controlledRunActionSiteGroups.restHealRewardGeneration` set. The saved
`rest_heal` action replays those requests in order and commits the resulting
Reward with the Nature cursor. For configured Native Act 1 runs, the
ReferenceNature run-action session also captures `player.rewards`
`card_rarity.roll.unit_float`, `card_rarity.encounter_model.index_draw`, and
`card_rarity.upgrade_roll.unit_float` requests in one typed transcript. Its
rarity law derives the current offset from the saved preceding raw-rarity
outcomes; replay verifies the exact action and request prefix before the
Reward and Nature cursor are committed. The existing serialized
`NativeCombatEntryProviderDecisions` member remains the compatibility field
for this shared typed run-action transcript.

The Python binding exposes `State.begin_controlled_run_start()`,
`State.begin_controlled_run_action(action_id)`,
`State.begin_controlled_shovel_dig()`,
`State.resume_controlled_run_action()` and `ControlledRunActionChoice.resolve()`.
Use a legacy random profile for run-start, map generation, ordinary legacy
Event/Treasure entry, and Shovel flows. Native-regional Treasure fallback is
available when `NativeRunRng` is absent. Shop purchase actions can also start from a
configured native Shop state, but only the registered index-only acquisition
sites above are controlled; configured native Shop entry and relic-bag
generation remain outside this controller. The configured Native merchant
session separately handles Alchemical Coffer's four mixed PotionFactory
rarity and model picks, plus registered Native `player.rewards` model-index
draws during legal Shop purchases. Other Native unit-float purchase hooks
remain unsupported.

This boundary does not verify native player-stream ownership, source map or
relic-pool parity, cursor parity or reference probabilities. Native bag draws
remain outside the controlled continuation until those source details are
validated.

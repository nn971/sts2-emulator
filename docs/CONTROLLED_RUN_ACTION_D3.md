# Controlled run-action continuation

This opt-in D3 boundary saves the legacy `start_run` action at its Act 1 boss
encounter draw, map choices that enter an Event node at supported legacy event
entry draws, legacy Shop-node offer generation, eligible legacy Treasure-node
relic selection, legal legacy Overgrowth Combat/Elite node entry, and the
Shovel dig action at its unowned-relic draw. Overgrowth combat entry controls
registered `combat`-stream requests and the two legacy potion-factory index
sites used by pre-combat relic hooks through ordinary room initialization;
other requests and streams retain their existing providers. Shop and Treasure
generation require non-native regional systems and a legacy run RNG. Treasure
selection is not offered when Silver Crucible suppresses the first treasure or
the legacy relic pool is exhausted. The boundary uses ordinary run-state
handles and does not change normal `step` behavior or source-owned regional
merchant/treasure routes.

The pending frame stores the exact pre-action run state, action kind, request,
and ordered candidate labels. Event-entry and Overgrowth-combat frames retain
the selected map-node ID. Resolving one index replays the action from that
checkpoint and records the controlled decision in
`RunState.ControlledRunActionHistory`. Run start commits the selected boss
encounter; map entry commits the selected Event, Shop, Treasure, or Overgrowth
Combat/Elite room through ordinary initialization; Shovel commits the relic
as the normal reward.
The pending continuation is part of ordinary run snapshots. Player-facing
observations hide its Nature actions; the explicit controller exposes only
`choose_controlled_run_action_outcome` actions while the frame is pending.
For supports larger than the advertised inline-action limit, the controller
omits expanded action objects and candidate labels but retains the compact
request bounds. The Chance Law still lists each legal request index, which
clients resolve through `controlled_run_action_resolve`.

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
legacy Treasure-node selection, Shovel dig, and legacy Overgrowth combat
entry. The controller supports:

| Operation | Inputs | Result |
| --- | --- | --- |
| `controlled_run_action_begin` | current `state_handle`, legal `action_id` | Pending request, labels and controlled outcome actions |
| `controlled_run_action_resolve` | pending `state_handle`, `selected_index` | Child run handle with the selected run-start, event-entry, Shop, Treasure, Overgrowth combat-entry or Shovel result |
| `controlled_run_action_inspect` | pending `state_handle` | Restore the pending request and legal outcomes after snapshot load |

The JSONL server also exposes an atomic ReferenceNature envelope for these
supported run-action continuations:

| Operation | Inputs | Result |
| --- | --- | --- |
| `reference_nature_run_action_begin` | current `state_handle`, optional `nature_seed`, optional legal `action_id` (Shovel dig is the only default) | Session handle with pending law and sampler cursor |
| `reference_nature_run_action_status` | run-action session handle | Current pending or committed boundary; ordinary `stateHandle` after commit |
| `reference_nature_run_action_resolve_next` | handle and current session `fingerprint` | Child session after one sampled index is resolved through ordinary run mechanics |
| `reference_nature_run_action_fork` | run-action session handle | Independent child session |
| `reference_nature_run_action_save` | run-action session handle | Replay-validated snapshot payload |
| `reference_nature_run_action_load` | snapshot | New session handle after transcript replay |
| `reference_nature_run_action_release_many` | `reference_nature_run_action_handles` | Number of released sessions |

Supply `nature_seed` the first time a run uses ReferenceNature. Once a session
commits, its cursor is stored in the run snapshot and later sessions continue
it when `nature_seed` is omitted. Runs without a committed cursor require a
seed. Snapshots with a Nature-owned relic identity set use
`sts2-exact-snapshot-v3-nature-relic-bag-order`; otherwise snapshots with a
cursor use `sts2-exact-snapshot-v2-reference-nature-state`, and snapshots
without a cursor retain `sts2-exact-snapshot-v1`.

The session supports the `start_run` boss encounter draw, legacy map
Event-node entry draws, seven `shop.*` sites in legacy Shop-node generation,
the `treasure.relic_model.index_draw` legacy Treasure-node site, registered
combat-stream requests and the two legacy potion-factory index requests during
legal legacy Overgrowth Combat/Elite node entry, and Shovel's unowned-relic
draw. Combat entry covers Overgrowth encounter-pool selection, formation,
initial unique-enemy HP, initial committed intents, opening draw-pile shuffle,
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
Their generic request-index laws remain `UNVERIFIED`; these controlled choices
do not advance game RNG. Unit-float acquisition effects and all other
unregistered purchase hooks remain fail-closed. The session keeps supported
Chance Law samples and run-state resolution atomic across fork/save/restore;
it does not enable native regional merchant or treasure entry providers or
other run/reward/map operations.
Once committed, the session result also provides a regular `stateHandle` for
continuing play through the ordinary run API; no state handle is published
while a Nature choice remains pending.
The Python binding exposes `State.begin_reference_nature_run_action(nature_seed=None,
action_id=...)` and session `resolve_next()`, `fork()`, `save_snapshot()`,
`Session.load_reference_nature_run_action_snapshot()`, `release()` and the
committed session's `.state` property.

Run-start map continuation covers the listed `map.legacy.*` request sites
during initial legacy map generation. It does not cover later-act map
regeneration or native map generation. Event-entry continuation covers legacy
`event.selection.weighted_ticket` and listed legacy event initialization draws
when a map Event node is selected. Configured native event streams remain
outside this boundary.

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

This entry boundary requires Act 1 Overgrowth's legacy encounter pool and
excludes configured Native runs, Underdocks and later-act encounters. The
generic request-index laws remain `UNVERIFIED` for source probabilities and
stream parity. The two `potion.*` requests use the legacy `event` stream and
are intercepted only at this combat-entry boundary.

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

The Python binding exposes `State.begin_controlled_run_start()`,
`State.begin_controlled_run_action(action_id)`,
`State.begin_controlled_shovel_dig()`,
`State.resume_controlled_run_action()` and `ControlledRunActionChoice.resolve()`.
Use a legacy random profile for run-start, map generation, Event/Treasure
entry, and Shovel flows. Shop purchase actions can also start from a
configured native Shop state, but only the registered index-only acquisition
sites above are controlled; configured native Shop entry and relic-bag
generation remain outside this controller. The configured Native merchant
session separately handles Alchemical Coffer's four mixed PotionFactory
rarity and model picks during its legal relic purchase; other Native unit-float
purchase hooks remain unsupported.

This boundary does not verify native player-stream ownership, source map or
relic-pool parity, cursor parity or reference probabilities. Native bag draws
remain outside the controlled continuation until those source details are
validated.

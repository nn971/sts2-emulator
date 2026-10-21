# Controlled run-action continuation

This opt-in D3 boundary saves the legacy `start_run` action at its Act 1 boss
encounter draw, map choices that enter an Event node at supported legacy event
entry draws, legacy Shop-node offer generation, eligible legacy Treasure-node
relic selection, eligible legacy regional Unknown-node entry, legal legacy
Act 1 Overgrowth and Underdocks Combat/Elite node entry, and the Shovel dig
action at its unowned-relic draw. Combat entry
controls registered `combat`-stream requests and the two legacy
potion-factory index sites used by pre-combat relic hooks through ordinary
room initialization; other requests and streams retain their existing
providers. Shop and Treasure
generation require non-native regional systems and a legacy run RNG. Treasure
selection is not offered when Silver Crucible suppresses the first treasure or
the legacy relic pool is exhausted. The boundary uses ordinary run-state
handles and does not change normal `step` behavior or configured Native
regional merchant/treasure routes.

For a configured Act 1 Underdocks start, `ReferenceNatureRunActionSessionV111`
also suspends on the exact `combat.encounter.underdocks.boss.index_draw`
request. It replays the existing configured initializer with the selected
boss index, then stores the sampled law and decision in the run-initialization
transcript with the committed Nature cursor. The request filter leaves the
initializer's other draws on their current providers; the hidden Overgrowth
boss draw inside the shared starter is not selected by Nature. The configured
Underdocks path does not include experimental three-act prepared bags. The
equal-index boss law remains `UNVERIFIED`.

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

For a configured Act 1 run using the experimental Native combat-entry profile,
the same boundary can also suspend on the regional weak, normal, or elite
encounter-pool index and on `combat.encounter.formation.index_draw` during a
Combat, Elite, or Boss map-node action. Pool selection uses the existing
ordered support and `combat` request stream; formation uses the encounter-local
context stream. Other entry requests keep their existing providers. The
ReferenceNature run-action session samples each encountered index and saves
the Nature cursor with the committed combat. A fixed pool assignment and
formation can complete at `begin` without taking a Nature sample. These
counterfactual choices leave their provider cursors untouched. Pool-selection
weights and Native source-pool fidelity remain `UNVERIFIED`; formation makes
no Native cursor-parity or calibrated-formation-law claim beyond the declared
bounded-index transform.

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
legacy Treasure-node selection, Shovel dig, and region-specific Overgrowth
and Underdocks combat entry. Shared formation, HP, intent, shuffle and potion
sites appear in both region groups. The controller supports:

| Operation | Inputs | Result |
| --- | --- | --- |
| `controlled_run_action_begin` | current `state_handle`, legal `action_id` | Pending request, labels and controlled outcome actions |
| `controlled_run_action_resolve` | pending `state_handle`, `selected_index` | Child run handle with the selected legacy run-start, configured Underdocks boss, event-entry, Shop, Treasure, Act 1 combat-entry or Shovel result |
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

The session supports the legacy `start_run` boss encounter draw and the
configured Underdocks boss draw described above, legacy map Event-node entry
draws, the legacy regional Unknown-room 1000-way ticket,
seven `shop.*` sites in legacy Shop-node generation,
the `treasure.relic_model.index_draw` legacy Treasure-node site, registered
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
merchant or treasure entry providers or other run/reward/map operations.
The configured experimental Native Act 1 path adds the supported encounter
pool and formation index requests to this same continuation; initial HP,
intents, opening shuffle and relic potion generation remain on their existing
providers.
Once committed, the session result also provides a regular `stateHandle` for
continuing play through the ordinary run API; no state handle is published
while a Nature choice remains pending.
The Python binding exposes `State.begin_reference_nature_run_action(nature_seed=None,
action_id=...)` and session `resolve_next()`, `fork()`, `save_snapshot()`,
`Session.load_reference_nature_run_action_snapshot()`, `release()` and the
committed session's `.state` property.

## Unsupported routes and remaining limits

Run-start map continuation covers the listed `map.legacy.*` request sites
during initial legacy map generation. It does not cover later-act map
regeneration or native map generation. Event-entry continuation covers legacy
`event.selection.weighted_ticket` and listed legacy event initialization draws
when a map Event node is selected. The separate
`event.unknown_room.odds.ticket` request is supported only when an unconfigured
legacy run with no `NativeRunRng` enters an available Unknown node in a regional
map profile; its 1000 equal request indices remain `UNVERIFIED` because they
approximate the source unit-float mapping. Configured Native run streams and
source cursor parity remain outside this boundary, except for the configured
Underdocks run-start boss choice and experimental Act 1 encounter-pool and
formation continuations described above.

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
rarity and model picks, plus registered Native `player.rewards` model-index
draws during legal Shop purchases. Other Native unit-float purchase hooks
remain unsupported.

This boundary does not verify native player-stream ownership, source map or
relic-pool parity, cursor parity or reference probabilities. Native bag draws
remain outside the controlled continuation until those source details are
validated.

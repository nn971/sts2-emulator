# Controlled run-action continuation

This opt-in D3 boundary saves the legacy `start_run` action at its Act 1 boss
encounter draw, map choices that enter an Event node at supported legacy event
entry draws, legacy Shop-node offer generation, eligible legacy Treasure-node
relic selection, and the Shovel dig action at its unowned-relic draw. Shop and
Treasure generation require non-native regional systems and a legacy run RNG.
Treasure selection is not offered when Silver Crucible suppresses the first
treasure or the legacy relic pool is exhausted. The boundary uses ordinary
run-state handles and does not change normal `step` behavior or source-owned
regional merchant/treasure routes.

The pending frame stores the exact pre-action run state, action kind, request,
and ordered candidate labels. Event-entry frames also retain the selected
map-node ID. Resolving one index replays the action from that checkpoint and
records the controlled decision in
`RunState.ControlledRunActionHistory`. Run start commits the selected boss
encounter; map event entry commits the selected event and its initial state;
Shop entry commits its generated offers; Treasure entry commits the selected
relic as a normal reward; Shovel commits the relic as the normal reward.
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
legacy Treasure-node selection, and Shovel dig. The controller supports:

| Operation | Inputs | Result |
| --- | --- | --- |
| `controlled_run_action_begin` | current `state_handle`, legal `action_id` | Pending request, labels and controlled outcome actions |
| `controlled_run_action_resolve` | pending `state_handle`, `selected_index` | Child run handle with the selected run-start, event-entry, Shop, Treasure or Shovel result |
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
the `treasure.relic_model.index_draw` legacy Treasure-node site, and Shovel's
unowned-relic draw. The same ReferenceNature run-action session also accepts
legal `buy_card`, `buy_potion`, `replace_shop_potion`, and `remove_card` Shop
actions when they complete without a random request. These deterministic
actions commit immediately with no Nature sample; a stochastic hook during
one of them still fails closed. Relic purchases and other stochastic purchase
effects remain unsupported. The session keeps supported Chance Law samples
and run-state resolution atomic across fork/save/restore; it does not enable
native regional merchant or treasure providers or other run/reward/map
operations. Nature samples do not advance the game's RNG.
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

The Python binding exposes `State.begin_controlled_run_start()`,
`State.begin_controlled_run_action(action_id)`,
`State.begin_controlled_shovel_dig()`,
`State.resume_controlled_run_action()` and `ControlledRunActionChoice.resolve()`.
Use a legacy random profile for these flows; configured native map, event
streams and relic-bag routes remain outside this controller.

This boundary does not verify native player-stream ownership, source map or
relic-pool parity, cursor parity or reference probabilities. Native bag draws
remain outside the controlled continuation until those source details are
validated.

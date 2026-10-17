# Controlled run-action continuation

This opt-in D3 boundary saves the legacy `start_run` action at its Act 1 boss
encounter draw, map choices that enter an Event node at supported legacy event
entry draws, and the Shovel dig action at its unowned-relic draw. It uses
ordinary run-state handles and does not change normal `step` behavior or the
source-owned regional relic-bag route.

The pending frame stores the exact pre-action run state, action kind, request,
and ordered candidate labels. Event-entry frames also retain the selected
map-node ID. Resolving one index replays the action from that checkpoint and
records the controlled decision in
`RunState.ControlledRunActionHistory`. Run start commits the selected boss
encounter; map event entry commits the selected event and its initial state;
Shovel commits the relic as the normal reward.
The pending continuation is part of ordinary run snapshots. Player-facing
observations hide its Nature actions; the explicit controller exposes only
`choose_controlled_run_action_outcome` actions while the frame is pending.
Chance supports larger than the advertised inline-action limit keep their
compact request bounds and omit expanded labels/actions; clients can still
resolve a selected index through `controlled_run_action_resolve`.

Each pending response now includes a read-only `chanceLaw`, its
`boundaryFingerprint`, and an `eventId`. The law assigns equal weight to each
legal request index (`choice_index:n`). Candidate labels identify the
mechanical result for that index but do not change the index law. Every law
and controlled action transcript remains `UNVERIFIED` for source probability
and stream parity. The law fingerprints only the typed random request; the
pending boundary fingerprint binds action/request context without including
private RNG state. The top-level `fingerprint` is the exact-state
stale-request guard.

The JSONL server advertises `controlledRunActionSchemaId` and supports:

| Operation | Inputs | Result |
| --- | --- | --- |
| `controlled_run_action_begin` | current `state_handle`, legal `action_id` | Pending request, labels and controlled outcome actions |
| `controlled_run_action_resolve` | pending `state_handle`, `selected_index` | Child run handle with the selected run-start, event-entry or Shovel result |
| `controlled_run_action_inspect` | pending `state_handle` | Restore the pending request and legal outcomes after snapshot load |

The JSONL server also exposes an atomic ReferenceNature envelope for these
supported run-action continuations:

| Operation | Inputs | Result |
| --- | --- | --- |
| `reference_nature_run_action_begin` | current `state_handle`, `nature_seed`, optional legal `action_id` (Shovel dig is the only default) | Session handle with pending law and sampler cursor |
| `reference_nature_run_action_status` | run-action session handle | Current pending or committed boundary |
| `reference_nature_run_action_resolve_next` | handle and current session `fingerprint` | Child session after one sampled index is resolved through ordinary run mechanics |
| `reference_nature_run_action_fork` | run-action session handle | Independent child session |
| `reference_nature_run_action_save` | run-action session handle | Replay-validated snapshot payload |
| `reference_nature_run_action_load` | snapshot | New session handle after transcript replay |

The session supports only the `start_run` boss encounter draw, legacy map
Event-node entry draws, and Shovel's unowned-relic draw that have a controlled
continuation here. It keeps the Chance Law sample and run-state resolution
atomic across fork/save/restore; it does not turn other run, shop, reward or
map operations into supported ReferenceNature sites. The sample uses the
independent ReferenceNature stream and does not advance the game's RNG.

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

# Controlled run-action continuation

This opt-in D3 boundary saves the legacy `start_run` action at its Act 1 boss
encounter draw and the Shovel dig action at its unowned-relic draw. It uses
ordinary run-state handles and does not change normal `step` behavior or the
source-owned regional relic-bag route.

The pending frame stores the exact pre-action run state, action kind, request,
and ordered candidate labels. Resolving one index replays the action from that
checkpoint and records the controlled decision in
`RunState.ControlledRunActionHistory`. Run start commits the selected boss
encounter; Shovel commits the relic as the normal reward.
The pending continuation is part of ordinary run snapshots. Player-facing
observations hide its Nature actions; the explicit controller exposes only
`choose_controlled_run_action_outcome` actions while the frame is pending.
Chance supports larger than the advertised inline-action limit keep their
compact request bounds and omit expanded labels/actions; clients can still
resolve a selected index through `controlled_run_action_resolve`.

The JSONL server advertises `controlledRunActionSchemaId` and supports:

| Operation | Inputs | Result |
| --- | --- | --- |
| `controlled_run_action_begin` | current `state_handle`, legal `action_id` | Pending request, labels and controlled outcome actions |
| `controlled_run_action_resolve` | pending `state_handle`, `selected_index` | Child run handle with the selected run-start or Shovel result |
| `controlled_run_action_inspect` | pending `state_handle` | Restore the pending request and legal outcomes after snapshot load |

Run-start map continuation covers the listed `map.legacy.*` request sites
during initial legacy map generation. It does not cover later-act map
regeneration or native map generation.

The Python binding exposes `State.begin_controlled_run_start()`,
`State.begin_controlled_run_action(action_id)`,
`State.begin_controlled_shovel_dig()`,
`State.resume_controlled_run_action()` and `ControlledRunActionChoice.resolve()`.
Use the legacy prototype reset profile for these run-start and Shovel flows;
configured native map and relic-bag routes remain outside this controller.

This boundary does not verify native player-stream ownership, source map or
relic-pool parity, cursor parity or reference probabilities. Native bag draws
remain outside the controlled continuation until those source details are
validated.

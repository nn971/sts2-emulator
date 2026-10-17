# Controlled run-action continuation

This opt-in D3 boundary saves the legacy Shovel dig action when its
`relic.unowned_random_selection.index_draw` request needs a controlled outcome.
It uses ordinary run-state handles and does not change the normal `step`
behavior or the source-owned regional relic-bag route.

The pending frame stores the exact pre-reward run state, action ID, request,
and ordered eligible relic labels. Resolving one index replays the Shovel
action from that checkpoint, commits the relic as the normal reward, and
records the controlled decision in `RunState.ControlledRunActionHistory`.
The pending continuation is part of ordinary run snapshots. Player-facing
observations hide its Nature actions; the explicit controller exposes only
`choose_controlled_run_action_outcome` actions while the frame is pending.

The JSONL server advertises `controlledRunActionSchemaId` and supports:

| Operation | Inputs | Result |
| --- | --- | --- |
| `controlled_run_action_begin` | rest-site `state_handle` with Shovel | Pending request, labels and controlled outcome actions |
| `controlled_run_action_resolve` | pending `state_handle`, `selected_index` | Child run handle with committed Shovel reward |
| `controlled_run_action_inspect` | pending `state_handle` | Restore the pending request and legal outcomes after snapshot load |

The Python binding exposes `State.begin_controlled_shovel_dig()`,
`State.resume_controlled_run_action()` and `ControlledRunActionChoice.resolve()`.

This boundary does not verify native player-stream ownership, source relic-pool
parity, cursor parity or reference probabilities. Native bag draws remain
outside the controlled continuation until those source details are validated.

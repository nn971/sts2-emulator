# Whole-run ReferenceNature session

`ReferenceNatureScriptedRunSessionV111` composes the validated scripted
run-action executor with one ReferenceNature cursor. It starts at an ordinary
player action boundary, suspends on typed indexed requests, samples one
Chance Law outcome at a time, and commits each completed action together with
the updated Nature cursor. Completed and active actions replay from their
saved boundary before a session or snapshot is accepted.

The current scope is finite index requests. `QueryRandomIndex` uses exact
Native `NextInt` grid masses for canonical Native stream IDs; legacy and
unknown streams use the registered unverified uniform-index law. Unit-float,
ranged-float, ranged-double, and Gaussian boundaries remain pending and are
reported as unsupported. Specialized event, reward, merchant, Treasure, map
generation, and other existing ReferenceNature sessions keep their current
owners and should be used for those boundaries.

An initial session requires `nature_seed` unless the run already owns a
ReferenceNature cursor. Later actions continue the same cursor automatically.
While an action is suspended, `stateHandle` and `runFingerprint` still identify
the last committed run boundary. The active action, typed provider prefix,
Nature transcript, and cursor are kept together by the session snapshot.

JSONL operations are:

- `reference_nature_scripted_run_begin` with `state_handle` and optional
  `nature_seed`;
- `reference_nature_scripted_run_begin_action` with the session handle,
  current `fingerprint`, and currently legal `action_id`;
- `reference_nature_scripted_run_resolve_next` with the session handle and
  current `fingerprint` to sample and resolve one indexed boundary;
- `reference_nature_scripted_run_status`, `reference_nature_scripted_run_fork`,
  `reference_nature_scripted_run_save`,
  `reference_nature_scripted_run_load`, and
  `reference_nature_scripted_run_release_many`.

`hello` advertises the session schema, operation list, and the
`reference_nature_scripted_run` Nature-choice view kind. The standard-library
Python binding exposes `State.begin_reference_nature_scripted_run()` and
`ReferenceNatureScriptedRunSession`, including status, action selection,
sampled resolution, the Nature-choice projection, snapshot restore, fork, and
handle release.

This session supplies whole-run composition for indexed requests while
retaining explicit boundaries around unsupported operation types. It does not
turn every stochastic action into a ReferenceNature action or replace the
existing specialized session owners. Completed sessions can also be supplied
to `provider_replay_compare` with
`<side>_reference_nature_scripted_run_handle`. That capture replays each
action and provides the indexed typed-boundary trace, action checkpoints, and
physical draw identities; it does not yet project source-aware semantic
outcome identities.

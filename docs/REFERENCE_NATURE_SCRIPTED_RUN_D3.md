# Whole-run ReferenceNature session

`ReferenceNatureScriptedRunSessionV111` composes the validated scripted
run-action executor with one ReferenceNature cursor. It starts at an ordinary
player action boundary and commits each completed action together with the
current Nature cursor. Completed and active actions replay from their saved
boundary before a session or snapshot is accepted.

`resolve_next` samples finite index requests through `QueryRandomIndex`,
which uses exact Native `NextInt` grid masses for canonical Native stream IDs;
legacy and unknown streams use the registered unverified uniform-index law.
`resolve_outcome` accepts a caller-supplied typed transcript entry for any of
the six primitive operations: index, unit float, ranged float, ranged double,
Gaussian integer, and Gaussian double. The pending status includes the typed
request needed to construct that entry. Replay checks its operation and full
request identity before applying it. Supplied outcomes are labeled as
controlled decisions, do not advance the Nature cursor, and do not become
Chance Law selections. Session schema v2 records one optional Nature
selection beside each provider decision; restore still accepts v1 snapshots,
where every decision was sampled by Nature. Specialized event, reward,
merchant, Treasure, map generation, and other existing ReferenceNature
sessions keep their current owners.

An initial session requires `nature_seed` unless the run already owns a
ReferenceNature cursor. Later actions continue the same cursor automatically.
While an action is suspended, `stateHandle` and `runFingerprint` still identify
the last committed run boundary. The active action, typed provider prefix,
aligned optional Nature transcript, and cursor are kept together by the
session snapshot.

JSONL operations are:

- `reference_nature_scripted_run_begin` with `state_handle` and optional
  `nature_seed`;
- `reference_nature_scripted_run_begin_action` with the session handle,
  current `fingerprint`, and currently legal `action_id`;
- `reference_nature_scripted_run_resolve_next` with the session handle and
  current `fingerprint` to sample and resolve one indexed boundary;
- `reference_nature_scripted_run_resolve_outcome` with the session handle,
  current `fingerprint`, a `transcript_entry` in the scripted run-action
  boundary format, and optional `candidate_labels` for indexed support;
- `reference_nature_scripted_run_status`,
  `reference_nature_scripted_run_fork`,
  `reference_nature_scripted_run_save`,
  `reference_nature_scripted_run_load`, and
  `reference_nature_scripted_run_release_many`.

`hello` advertises the session schema, operation list, and the
`reference_nature_scripted_run` Nature-choice view kind. The standard-library
Python binding exposes `State.begin_reference_nature_scripted_run()` and
`ReferenceNatureScriptedRunSession`, including status, action selection,
sampled resolution, caller-supplied typed resolution, the Nature-choice
projection, snapshot restore, fork, and handle release.

This session composes all six primitive continuations across ordinary legal
actions, while only finite indexed requests are sampled by ReferenceNature.
It does not turn every stochastic action into a Nature sample or replace the
existing specialized session owners. Completed sessions can also be supplied
to `provider_replay_compare` with
`<side>_reference_nature_scripted_run_handle`. That capture replays each
action and provides the complete typed-boundary trace, action checkpoints, and
physical draw identities; it does not yet project source-aware semantic
outcome identities.

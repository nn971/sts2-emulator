# Whole-run scripted continuation contract

`ScriptedRunSessionV111` chains the existing
[`ScriptedRunActionSessionV111`](SCRIPTED_RUN_ACTION_D3.md) over a run. At a
player boundary, a caller starts the next currently legal ordinary action by
its stable action ID. If that action reaches a stochastic site, the session
returns the exact typed provider request and waits for one ordered transcript
outcome. When the action completes, its run and typed decisions become one
committed history entry and the session returns to the next player boundary.

While an action is suspended, `runStateHandle` and `runFingerprint` identify
the last committed run boundary. The in-progress action is reconstructed from
that boundary and its saved typed prefix; partial action mechanics are not
published as a separate run state.

Completed action records retain the legal action and all six typed stochastic
decisions. Validation and restore replay the records in order from the origin,
checking action legality, exact typed requests, candidate labels, and each
resulting run before accepting the current checkpoint. An active action must
replay from the latest committed checkpoint. A terminal run has no next legal
action. Unsupported controlled event/run-choice envelopes remain on their
existing specialized controllers.

JSONL operations are:

- `scripted_run_begin` with `state_handle`;
- `scripted_run_begin_action` with `scripted_run_handle`, current
  `fingerprint`, and a currently legal `action_id`;
- `scripted_run_resolve_next` with the session handle, current `fingerprint`,
  either the next `transcript_entry` and optional index `candidate_labels`, or
  `candidate_label` for a pending labeled index request;
- `scripted_run_status`, `scripted_run_fork`, `scripted_run_save`,
  `scripted_run_load`, and `scripted_run_release_many`.

`hello` advertises the session schema and operation list. Scripted outcomes
remain `UNVERIFIED`: this controller does not query a Chance Law, advance a
Native RNG cursor, or claim cross-provider equivalence. The session and its
action history are privileged replay state, not player or Nature observations.
The standard-library Python binding exposes the same lifecycle through
`State.begin_scripted_run()` and `ScriptedRunSession`, including
`resolve_by_candidate_label()`, snapshot restore, and handle release.

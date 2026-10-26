# Scripted run-action continuation contract

`ScriptedRunActionSessionV111` drives one current legal ordinary action
through `PrototypeGameEngine.Step` under the shared mixed typed continuation
executor. It replaces provider outcomes for that action and returns either a
completed run or the exact next typed request. A client can save or fork the
session, then resume by supplying one ordered random-boundary transcript entry
at a time.

Each supplied entry must use the next contiguous transcript sequence and its
typed request must match the pending request, including site, stream, owner,
ordinal, and operation bounds. Index entries may include candidate labels in
the `candidate_labels` field because the provider-boundary journal does not
store them. For a pending index request with saved labels, callers may provide
`candidate_label` instead of a transcript entry; the session resolves it to
the established index transcript and rejects missing, duplicate, or unknown
labels. All six provider primitives are accepted. Native cursor metadata
and the entry's original provider mode do not control replay; outcomes are
reissued in controlled mode. The replay does not advance Native streams and
makes no cursor-equivalence claim. The session's saved decisions are marked
`UNVERIFIED` because this controller supplies outcomes without a Chance Law.

JSONL operations are:

- `scripted_run_action_begin` with `state_handle` and the action's `action_id`;
- `scripted_run_action_resolve_next` with the session handle, current
  `fingerprint`, and either `transcript_entry` with optional
  `candidate_labels` or `candidate_label` for a labeled pending index;
- `scripted_run_action_status`, `scripted_run_action_fork`,
  `scripted_run_action_save`, `scripted_run_action_load`, and
  `scripted_run_action_release_many`.

The versioned session snapshot stores the origin run, selected action, typed
outcome prefix, pending mixed-primitive continuation, and completed run when
available. It does not alter canonical `RunState` provider ownership. The
controller covers one action boundary; the versioned
[whole-run scripted session](SCRIPTED_RUN_SESSION_D3.md) composes it across
ordinary legal player actions and replays completed typed histories from the
original run. Chance Law declarations and cross-provider
mechanical-equivalence review remain separate work.

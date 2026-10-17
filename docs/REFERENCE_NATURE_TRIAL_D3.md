# ReferenceNature Trial session

`ReferenceNatureTrialSessionV111` handles the Glory Trial `accept` draw on
either the opening page or its post-rejection page. The bounded integer call
selects one of the Merchant, Noble, or Nondescript cases. Its Chance Law
counts the exact `NextInt(3)` preimages on the uniform 53-bit `NextDouble`
grid, including the binary64 multiply boundary. The bounded-index mapping is
`SOURCE_VERIFIED` under that input model; stream attribution, seed
frequencies, and seeded Native cursor parity remain separate claims.

The session replays `accept` through the ordinary Trial event action with the
scripted index provider. It commits the resulting case page and
ReferenceNature cursor together while preserving the `TRIAL` Native event
cursor. The completed EventState records the `scripted-v111` provider marker;
the event invariant checks that the stream is still at its entry cursor and
the event has advanced to a case page or completed result page.

The session handles only this case draw. Rejecting the event and resolving
Merchant, Noble, or Nondescript choices continue through ordinary player
actions. Source pool fidelity for later relic/card rewards, seeded Native
cursor parity, and probability calibration remain open.

## JSONL operations

| Operation | Inputs | Result |
| --- | --- | --- |
| `chance_law_query` | pending Trial event `state_handle`, site ID, `ordinal: 0` | Three-case bounded integer law |
| `reference_nature_trial_begin` | Trial event `state_handle`, optional `nature_seed` | Session before acceptance draw |
| `reference_nature_trial_status` | session handle | Current law or committed case page |
| `reference_nature_trial_resolve_next` | handle and current `fingerprint` | Child with case page and sampler cursor committed |
| `reference_nature_trial_fork` | session handle | Independent child session |
| `reference_nature_trial_save` / `reference_nature_trial_load` | handle / snapshot | Replay-validated session snapshot and restored handle |
| `reference_nature_trial_release_many` | `reference_nature_trial_handles` | Number of released sessions |

The site is `event.glory_trial.damage_amount.index_draw`. Session and Chance
Law query coverage are advertised separately. The read-only query does not
inspect the saved Native event cursor; only session admission checks that the
source event action can be replayed from its untouched event-entry cursor.
The session does not change the player-facing event observation.

## Python binding

```python
session = state.begin_reference_nature_trial(nature_seed=123)
session = session.resolve_next()
case_page = session.state
```

Omit `nature_seed` to continue a cursor already committed to the run. The
session also supports `status()`, `fork()`, `save_snapshot()`,
`Session.load_reference_nature_trial_snapshot(snapshot)`, and bulk release.

The continuation uses the existing event resolver and does not depend on the
native reference mod or Fidelity Inspector.

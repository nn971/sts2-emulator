# ReferenceNature Trial session

`ReferenceNatureTrialSessionV111` handles the Glory Trial `accept` draw on
either the opening page or its post-rejection page. The bounded integer call
selects one of the Merchant, Noble, or Nondescript cases. Its Chance Law
counts exact `NextInt(3)` preimages on the uniform 53-bit `NextDouble` grid
for configured Native runs, including the binary64 multiply boundary. A
Native-regional run without `NativeRunRng` uses the saved legacy `event`
request ordinal and equal weights over the three cases; this fallback law is
`UNVERIFIED`.

The session replays `accept` through the ordinary Trial event action with the
route's controlled index provider. It commits the resulting case page and
ReferenceNature cursor together. Configured Native replay preserves the
`TRIAL` event cursor and records the `scripted-v111` provider marker. Fallback
replay leaves the selected legacy request unadvanced and does not create a
Native event cursor or provider marker. Stream attribution, seed frequencies,
and seeded Native cursor parity remain separate claims.

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
Law query coverage are advertised separately. The JSONL query's `ordinal: 0`
is the semantic case-draw ordinal; a fallback law reports the current saved
legacy `event` call count as its typed request ordinal. Configured Native
queries do not inspect the Native event cursor. The session does not change
the player-facing event observation.

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

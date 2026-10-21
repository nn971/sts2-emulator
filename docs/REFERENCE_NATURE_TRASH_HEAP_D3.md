# ReferenceNature Trash Heap session

`ReferenceNatureTrashHeapSessionV111` covers Trash Heap's fixed-pool `grab`
card reward and the nonlethal `dive` relic reward. It starts from the active
event before the choice, samples one index, then replays the selected action
through the ordinary event mechanics with the route's controlled index
provider. The resulting run and sampler cursor commit together.

The session also accepts Native-regional runs without `NativeRunRng`. That
fallback uses the saved legacy `event` stream ordinal and an equal-index law,
which remains `UNVERIFIED`; the Nature-selected index does not advance the
legacy stream. Other requests retain their existing providers.

The card and relic pools retain their source order and stable model IDs. `dive`
is supported only when the player's current HP is greater than 8; a lethal dive
ends the run before the source draws a relic and therefore has no Chance Law.
Configured Native replay requires the initial Trash Heap event stream and
leaves its Native cursor unchanged. Its completed EventState records
`scripted-v111`; validation checks the unchanged entry cursor, terminal choice,
and selected pool result. The Native law counts exact `NextInt` preimages on
the uniform 53-bit `NextDouble` grid for each declared source-ordered pool and
is `SOURCE_VERIFIED` under that input model. Native-regional fallback laws use
equal weights over the saved legacy index support and remain `UNVERIFIED`.
Event-pool completeness, stream attribution, seed frequencies, legacy stream
parity, and Native cursor parity remain separate claims. The read-only Chance
Law query does not inspect either saved cursor; session admission validates the
route and replay request.

## S0 projection, resolver, provenance, and unsupported boundary

The card and relic rows use the source-ordered fixed pools recorded in the
migration matrix. Pool order, player survival state, the selected event
branch, and request identity constrain each draw. Nature receives the current
pool law at the choice boundary; the player-facing observation retains the
ordinary event projection and shows only the committed reward.

The saved `grab` or nonlethal `dive` action is replayed through the existing
event resolver. The selected card or relic and the ReferenceNature cursor
commit together, without replaying a completed action twice. The configured
Native bounded-index transform is `SOURCE_VERIFIED` on the declared pools;
fallback equal-index weighting, source filtering, event-stream attribution,
seed frequencies, and cursor parity remain open.

Only the fixed card-pool `grab` and surviving relic-pool `dive` boundaries are
supported. Lethal dive is draw-free, and other event choices or event-triggered
relic effects remain outside this continuation.

## JSONL operations

| Operation | Inputs | Result |
| --- | --- | --- |
| `chance_law_query` | event `state_handle`, site ID, route's current request ordinal | Fixed-pool card or relic index law |
| `reference_nature_trash_heap_begin` | event `state_handle`, `choice_id` (`grab` or `dive`), required `nature_seed` | Session before the draw |
| `reference_nature_trash_heap_status` | session handle | Current law or committed result |
| `reference_nature_trash_heap_resolve_next` | handle and current `fingerprint` | Child with the event choice and sampler cursor committed |
| `reference_nature_trash_heap_fork` | session handle | Independent child session |
| `reference_nature_trash_heap_save` / `reference_nature_trash_heap_load` | handle / snapshot | Replay-validated snapshot and restored handle |
| `reference_nature_trash_heap_release_many` | `reference_nature_trash_heap_handles` | Number of released sessions |

The two stable sites are `event.trash_heap.card_reward.index_draw` and
`event.trash_heap.relic_reward.index_draw`. A query for the relic site fails
closed when `dive` is lethal. The session does not change the ordinary event
or player observation contract.

## Python binding

```python
session = state.begin_reference_nature_trash_heap(
    "grab", nature_seed=123)
session = session.resolve_next()
rewarded_run = session.state
```

Use `"dive"` for the relic branch. The session also supports `status()`,
`fork()`, `save_snapshot()`,
`Session.load_reference_nature_trash_heap_snapshot(snapshot)`, and bulk
release. A direct law query uses
`state.query_chance_law(site_id, ordinal=...)`. The configured Native route
uses ordinal 0; the fallback uses the current saved legacy `event` stream call
count. The session derives and validates that request from its origin.

## Limits

The session covers only the two fixed Trash Heap reward pools. Other event
choices and event-triggered relic effects remain outside this continuation. Native source filtering, installed-game RNG cursor
parity, and probability calibration remain open; no reference mod or Fidelity
Inspector work is included.

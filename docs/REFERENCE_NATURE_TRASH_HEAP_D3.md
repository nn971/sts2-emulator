# ReferenceNature Trash Heap session

`ReferenceNatureTrashHeapSessionV111` covers Trash Heap's fixed-pool `grab`
card reward and the nonlethal `dive` relic reward. It starts from the active
event before the choice, samples one index, then replays the selected action
through the ordinary event mechanics with the existing scripted index
provider. The resulting run and sampler cursor commit together.

The card and relic pools retain their source order and stable model IDs. `dive`
is supported only when the player's current HP is greater than 8; a lethal dive
ends the run before the source draws a relic and therefore has no Chance Law.
The session requires a configured Native event context and the initial
Trash Heap event stream. The scripted replay leaves that Native event cursor
unchanged. The completed EventState records `scripted-v111`; its invariant
checks the unchanged entry cursor, terminal choice, and selected pool result.
The laws count exact Native `NextInt` preimages on the uniform 53-bit
`NextDouble` grid for each declared source-ordered pool. The bounded-index
transform is `SOURCE_VERIFIED` under that input model; event-pool completeness,
stream attribution, seed frequencies, and Native cursor parity remain separate
claims. The read-only Chance Law query does not inspect the saved Native event
cursor; only session admission checks the untouched entry cursor required by
scripted replay.

## JSONL operations

| Operation | Inputs | Result |
| --- | --- | --- |
| `chance_law_query` | event `state_handle`, site ID, `ordinal: 0` | Fixed-pool card or relic index law |
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
`state.query_chance_law(site_id, ordinal=0)`.

## Limits

The session covers only the two fixed Trash Heap reward pools. Other event
other event choices and event-triggered relic effects remain
outside this continuation. Native source filtering, installed-game RNG cursor
parity, and probability calibration remain open; no reference mod or Fidelity
Inspector work is included.

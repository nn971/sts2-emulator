# Map-generation stochastic contract

This contract covers the five `map.overgrowth.*` and six `map.later_act.*`
rows in the migration matrix and their source-shaped generators in
[`PrototypeNativeOvergrowthMap`](../src/Sts2Emulator.Core/PrototypeNativeOvergrowthMap.cs)
and [`PrototypeNativeLaterActMap`](../src/Sts2Emulator.Core/PrototypeNativeLaterActMap.cs).
The stable site IDs remain the row keys; this document records each family's
current support, audience boundary, and implementation limits.

## Act 1 Overgrowth

The native generator owns `context.act_1_map`; the historical entry point uses
the supplied `map` stream. Native calls have per-site ordinals and source
owners for the path, retry, quota, or placement-pool occurrence. The map seed,
current partial path graph, earlier path starts, room quotas, and already-
assigned rooms constrain later requests. These are private generation state;
the emitted map is the committed mechanics result.

| Site | Occurrence and legal support |
| --- | --- |
| `map.overgrowth.path_start_column.index_draw` | One bounded column choice from `[0, 7)` for each of seven paths. The second path retries if it matches the first; later paths may share a start. The path ordinal and retry stage identify the occurrence. |
| `map.overgrowth.path_delta.shuffle_swap_index` | Fisher–Yates swaps over ordered deltas `[-1, 0, 1]` for each of 14 transitions along each path. The current path graph filters destinations that cross another route; the first legal shuffled delta is selected. |
| `map.overgrowth.rest_count.gaussian_component` | One native truncated rounded Gaussian request with mean 7, standard deviation 1, and accepted values 6–7. The legacy implementation uses bounded `int.MaxValue` components with Box–Muller and rejection; the site ID names that component path. |
| `map.overgrowth.unknown_count.gaussian_component` | One native truncated rounded Gaussian request with mean 12, standard deviation 1, and accepted values 10–14. The legacy implementation uses bounded `int.MaxValue` components with Box–Muller and rejection. |
| `map.overgrowth.room_assignment.shuffle_swap_index` | Fisher–Yates swaps over the currently unassigned points ordered by floor then column, once per room quota and retry attempt. The owner includes room type, attempt, and a hash of the ordered pool; prior assignments and ascension determine the support. |

The map fixes Combat on floor 1, Treasure on floor 9, Rest on floor 15, and
Boss on floor 16. Quotas are assigned in Rest, Shop, Elite, Unknown order;
Shop has quota 3, Elite has quota 5 or 8 according to ascension, and the two
Gaussian quotas use the supports above. Each quota receives at most three
shuffled placement attempts.

## Hive and Glory

The native generator owns one act-local map stream, `context.act_2_map` for
Hive and `context.act_3_map` for Glory. The legacy generator uses the supplied
`map` stream. Native calls are owned by `map:act:{act}`; legacy calls use
`map:prototype`. Both track a per-site ordinal. The map seed, partial path
graph, earlier path starts, room quotas, and already-assigned rooms constrain
later requests.

| Site | Occurrence and legal support |
| --- | --- |
| `map.later_act.path_start_column.index_draw` | One bounded column choice from `[0, 7)` for each of seven paths. Path two retries if its column matches a previous start; later paths may share a start. The current path-start set and retry count affect occurrence identity. |
| `map.later_act.path_delta.shuffle_swap_index` | Fisher–Yates swaps over the ordered deltas `[-1, 0, 1]` for each path transition. The current path graph filters destinations that cross another route; the first legal shuffled delta is selected. |
| `map.later_act.glory_rest_count.index_draw` | One bounded choice from `[0, 2)`, mapped to a Glory rest quota of 5 or 6. |
| `map.later_act.room_assignment.shuffle_swap_index` | Fisher–Yates swaps over the currently unassigned points ordered by floor then column, once per room quota and retry attempt. Prior assignments, act, ascension, quota, and attempt determine the support. |
| `map.later_act.hive_rest_count.gaussian_int` | Hive's truncated rounded Gaussian with mean 6, standard deviation 1, and accepted values 6–7. |
| `map.later_act.unknown_count.gaussian_int` | Truncated rounded Gaussian with mean 12, standard deviation 1, and accepted values 10–14; the generator subtracts one, producing a quota of 9–13. |

Hive and Glory use the same randomized quota and placement order. For both
map generators, room placement filters by current assignments and geometry:
Elite and Rest cannot be placed through floor 5; Rest cannot be placed at or
above `roomRows - 2` (the final three room rows). Rest, Elite, Treasure, and
Shop avoid matching parent or child rooms. Rest, Elite, Shop, and Unknown
avoid matching sibling rooms.

## Audience, resolver, and continuation

The player-facing result is the committed map: nodes, floors, room types,
entry nodes, and outgoing connections. The generation seed, stream cursor,
partial path graph, candidate ordering, and room-placement retries are not
part of that projection. There is currently no Nature-view observation or
controlled continuation for these sites. Read-only `chance_law_query` laws
cover the raw Hive/Glory `map.later_act.path_start_column.index_draw` before
any path-start retry, and the `map.later_act.glory_rest_count.index_draw` at
site-local ordinal zero. Path-start queries require the declared act and
site-local ordinal; both laws return exact Native bounded-index grid masses.
The queries do not read map state, seed, or cursor. The explicit
`GenerateNativeWithAudit` helpers can return the final native map stream
snapshot for source investigation; ordinary map generation does not publish
it, and no per-decision privileged-inspector transcript exists.

The resolver is the synchronous map-generation algorithm: it chooses starts
and paths, assigns fixed rooms and randomized quotas, then returns a complete
`MapState`. Neither family has a saved resolver continuation or hook-once
replay frame. The source-owned mechanics continuation is the completed map
itself. Exact parity for path traversal, pruning, room placement, and call
order is still open, as recorded by the generator source comments.

## Provenance and unsupported handling

The native stream construction is pinned to the v0.111.0 source revision
`706ef1c9fa2219220065849fd5265328607ece20`; see
[`NativeRngV111Contexts.ForActMap`](../src/Sts2Emulator.Core/NativeRngV111Contexts.cs).
The Glory rest-count `NextInt(2)` and Hive/Glory path-start `NextInt(7)`
transforms are `SOURCE_VERIFIED` under the uniform 53-bit input-grid model.
The path-start law describes each raw column draw before the Act 2/3 retry
condition and does not model its full accepted start. These laws do not
establish seed frequencies, stream derivation, or per-call cursor parity.
Other map operation laws remain **UNVERIFIED**. Seed derivation and typed
bounded, shuffle, and Gaussian providers do not establish distribution
calibration or per-call cursor parity.

No controlled ReferenceNature operation is supported for these sites, so a
Nature request must remain unavailable rather than inventing a law or
continuation. The Hive/Glory APIs reject acts outside 2 and 3 and ascension
outside 0–10. Overgrowth generation is fixed to Act 1 and does not locally
validate its ascension argument. Invalid Hive/Glory inputs and any path with
no legal noncrossing continuation throw without returning a map. The ordinary
source-owned generation path remains the resolver until a Nature session with
matching support fingerprints and atomic replay is implemented.

## S0 status

The migration matrix supplies each row's operation-specific support summary;
this contract supplies the player/Nature/inspector projections, resolver
boundary, probability provenance, and unsupported behavior for both map
families. S0 implementation acceptance remains open until the supported laws,
state fingerprints, source call order, and any future Nature continuation are
validated against the native source.

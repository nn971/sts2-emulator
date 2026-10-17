# Later-act map stochastic contract

This contract covers the six `map.later_act.*` rows in the migration matrix
and the source-shaped Hive and Glory generators in
[`PrototypeNativeLaterActMap`](../src/Sts2Emulator.Core/PrototypeNativeLaterActMap.cs).
The stable site IDs remain the row keys; this document records their current
support, audience boundary, and implementation limits.

## Owner and private constraints

The native generator owns one act-local map stream, `context.act_2_map` for
Hive and `context.act_3_map` for Glory. The legacy generator uses the supplied
`map` stream. Native calls are owned by `map:act:{act}`; legacy calls use
`map:prototype`. Both track a per-site ordinal.
The map seed, current partial path graph, earlier path starts, room quotas,
and already-assigned rooms constrain later requests. These are private
generation state; the emitted map is the committed mechanics result.

| Site | Occurrence and legal support |
| --- | --- |
| `map.later_act.path_start_column.index_draw` | One bounded column choice from `[0, 7)` for each of seven paths. Path two retries if its column matches a previous start; later paths may share a start. The current path-start set and retry count affect occurrence identity. |
| `map.later_act.path_delta.shuffle_swap_index` | Fisher–Yates swaps over the ordered deltas `[-1, 0, 1]` for each path transition. The current path graph filters destinations that cross another route; the first legal shuffled delta is selected. |
| `map.later_act.glory_rest_count.index_draw` | One bounded choice from `[0, 2)`, mapped to a Glory rest quota of 5 or 6. |
| `map.later_act.room_assignment.shuffle_swap_index` | Fisher–Yates swaps over the currently unassigned points ordered by floor then column, once per room quota and retry attempt. Prior assignments, act, ascension, quota, and attempt determine the support. |
| `map.later_act.hive_rest_count.gaussian_int` | Hive's truncated rounded Gaussian with mean 6, standard deviation 1, and accepted values 6–7. |
| `map.later_act.unknown_count.gaussian_int` | Truncated rounded Gaussian with mean 12, standard deviation 1, and accepted values 10–14; the generator subtracts one, producing a quota of 9–13. |

Room placement additionally filters by current room assignments and geometry.
Elite and Rest rooms cannot be placed through floor 5; Rest cannot be placed
in the final three room rows. Rest, Elite, Treasure, and Shop avoid matching
parent or child rooms. Rest, Elite, Shop, and Unknown avoid matching sibling
rooms. Quotas are assigned in Rest, Shop, Elite, Unknown order, with at most
three shuffled placement attempts per quota.

## Audience, resolver, and continuation

The player-facing result is the committed map: nodes, floors, room types,
entry nodes, and outgoing connections. The generation seed, stream cursor,
partial path graph, candidate ordering, and room-placement retries are not
part of that projection. There is currently no Nature-view observation,
chance-law query, or privileged-inspector transcript for these sites. The
explicit `GenerateNativeWithAudit` helper can return the final native map
stream snapshot for source investigation; ordinary map generation does not
publish it.

The resolver is the synchronous map-generation algorithm: it chooses starts
and paths, assigns fixed rooms and randomized quotas, then returns a complete
`MapState`. It has no saved resolver continuation or hook-once replay frame.
The source-owned mechanics continuation is the completed map itself. Exact
parity for path traversal, pruning, room placement, and their call order is
still open, as recorded by the generator's source comment.

## Provenance and unsupported handling

The native stream construction is pinned to the v0.111.0 source revision
`706ef1c9fa2219220065849fd5265328607ece20`; see
[`NativeRngV111Contexts.ForActMap`](../src/Sts2Emulator.Core/NativeRngV111Contexts.cs).
The operation laws remain **UNVERIFIED**. Seed derivation and typed bounded,
shuffle, and Gaussian providers do not establish distribution calibration or
per-call cursor parity.

No controlled ReferenceNature operation is supported for these sites, so a
Nature request must remain unavailable rather than inventing a law or
continuation. `Generate` and `GenerateNative` reject acts outside Hive (2) and
Glory (3); they reject ascension outside 0–10. Invalid inputs throw before a
map is returned. A path with no legal noncrossing continuation also throws
without publishing a partial map. The ordinary source-owned generation path
remains the resolver until a Nature session with matching support fingerprints
and atomic replay is implemented.

## S0 status

The migration matrix supplies each row's operation-specific support summary;
this contract supplies the shared owner, player/Nature/inspector projections,
resolver boundary, probability provenance, and unsupported behavior for this
family. S0 implementation acceptance remains open until the supported laws,
state fingerprints, source call order, and any future Nature continuation are
validated against the native source.

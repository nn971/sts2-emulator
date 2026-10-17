# Native event gold ReferenceNature continuation

**Snapshot:** 2026-10-18. **Status:** implemented configured Native generated-gold entry continuation; Native seed parity and event-pool fidelity remain open.

## Boundary

`ReferenceNatureNativeEventGoldSessionV111` starts from a legal configured
Native Event-node action in `RunPhase.MapChoice`. It replays that ordinary
action with its existing providers until it reaches the typed
`event.generated_gold.unit_float` request. The only accepted event branches
are Jungle Maze Adventure and Lost Wisp. Jungle Maze has primary and
secondary gold requests; Lost Wisp has one primary request.

Event selection and every request before or after the gold float retain their
existing provider. The session does not expose a Nature choice for event
selection and does not claim the entire configured event pool. Unsupported
branches or any different pending request are rejected before a session is
published.

## Law and replay

Each pending float uses the existing conditional Chance Law from
`ChanceLawEngineV111.QueryNativeEventGoldUnitFloat`. The law preserves the
source float mapping and its 53-bit `NextDouble` preimages after `NextFloat`
rounding, arithmetic, and integer truncation. Its declared transform is
`SOURCE_VERIFIED` for the pinned formulas. The law does not establish source
pool completeness, seed frequencies, stream attribution, or seeded cursor
parity.

`ResolveNext()` samples one law outcome, maps it to a typed float request, and
replays the ordinary map action from the saved origin. After all gold draws
complete, the session publishes the event-entry result and ReferenceNature
cursor together. Until then, the session's run remains at the uncommitted
origin. Existing relic bag queues are sorted and tagged as Nature unordered
sets in the replay origin; when the origin has no relic bag, the session
preserves that absence.

The serialized session records the source action, event branch, origin and
current run snapshots, initial and current Nature sampler states, and the
decision transcript. Restore recomputes the transcript by replay. Forks
retain independent snapshots. The JSONL and Python interfaces expose begin,
status, resolve, fork, save, load, release, and the committed playable state.

## Scope limits

This continuation only controls the generated-gold float requests in the two
named events. Legacy integer fallbacks, other event-entry choices, other
generated-gold sites, and subsequent event actions remain outside it. Native
event selection and any other draws remain on their existing providers.

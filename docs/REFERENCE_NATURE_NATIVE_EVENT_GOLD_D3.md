# Native event-entry ReferenceNature continuation

**Snapshot:** 2026-10-23. **Status:** implemented configured Native event-entry continuation for supported generated-gold, Stone of All Time, and Future of Potions draws, plus Native-regional legacy generated-gold, Stone, and Future index requests; Native seed parity and event-pool fidelity remain open.

## Boundary

`ReferenceNatureNativeEventGoldSessionV111` is the versioned session/API name
for supported Event-node entry draws. It starts from a legal configured
Native-regional Event-node action in `RunPhase.MapChoice` and replays that
ordinary action with its existing providers until it reaches a typed
`event.generated_gold.unit_float` or
`event.generated_gold.index_draw` request. The accepted branches are Dense Vegetation, Whispering Hollow, both
source-region Sunken Statue events, Luminous Choir, Sunken Treasury, Punch
Off, This or That, Jungle Maze Adventure, and Lost Wisp. All use one gold
request except Sunken Treasury and Jungle Maze, which use primary and
secondary requests. Configured Native runs retain their source-pinned Native
requests; Native-regional runs without `NativeRunRng` are admitted for
supported legacy `event.generated_gold.index_draw`,
`event.stone_of_all_time.potion_slot.index_draw`, and
`event.future_of_potions.card_type.index_draw` requests.

The same atomic session also consumes `event.stone_of_all_time.potion_slot.index_draw`
for Stone of All Time and each ordered
`event.future_of_potions.card_type.index_draw` request for The Future of
Potions. Stone has one draw over occupied potion slots. Future of Potions has
one draw per owned potion, in slot order, with each support determined by that
potion's card rarity. Configured Native requests retain their bounded-index
laws; Native-regional legacy requests retain their saved `event` ordinals and
use equal-support `UNVERIFIED` laws.

Event selection and every request before or after the declared entry draws
retain their existing provider. The session does not expose a Nature choice
for event selection and does not claim the entire configured event pool.
Unsupported branches or any different pending request are rejected before a
session is published.

## Law and replay

Each pending request uses its route-specific conditional Chance Law. Native
gold requests use
`ChanceLawEngineV111.QueryNativeEventGoldUnitFloat` or
`QueryNativeEventGoldIndex`. Float laws preserve 53-bit `NextDouble`
preimages after `NextFloat` rounding, source arithmetic, and integer
truncation. Index laws count exact `NextInt` preimages and aggregate indices
that map to the same gold value. Their declared transforms are
`SOURCE_VERIFIED` for the pinned formulas. Legacy generated-gold index laws
validate the saved `event` request ordinal and exact ordered event-stage
support, then assign equal weights to the saved integer candidates; these laws
remain `UNVERIFIED`. Native-regional legacy Stone and Future of Potions laws
likewise bind the saved request ordinal and ordered support, with equal-index
weights and `UNVERIFIED` provenance. Stone and Future of Potions reuse
`QueryNativeStoneOfAllTimePotionSlot` and
`QueryNativeFutureOfPotionsCardType`; their exact Native `NextInt` masses are
conditional on the declared event branch and ordered support. The laws do not establish source
pool completeness, seed frequencies, stream attribution, or seeded cursor
parity.

`ResolveNext()` samples one law outcome, maps it back to its typed float or
bounded-index request, and replays the ordinary map action from the saved
origin. After all declared entry draws complete, the session publishes the event-entry result and ReferenceNature
cursor together. Until then, the session's run remains at the uncommitted
origin. Existing relic bag queues are sorted and tagged as Nature unordered
sets in the replay origin; when the origin has no relic bag, the session
preserves that absence.

The Stone of All Time result composes with the existing push/lift event-choice
continuation. Its later Native ticket keeps source ordinal 1 after the
Nature-controlled ordinal-0 potion-slot draw. A controlled ticket remains on
the scripted entry boundary; an ordinary Native event draw clears that marker
after it advances the context stream. These paths preserve request chronology
but do not claim Native cursor parity for the counterfactual run.

The serialized session records the source action, event branch, origin and
current run snapshots, initial and current Nature sampler states, and the
decision transcript. Restore recomputes the transcript by replay. Forks
retain independent snapshots. The JSONL and Python interfaces expose begin,
status, resolve, fork, save, load, release, and the committed playable state.

Player-facing observations contain only the ordinary committed Event state;
the session's stream cursor and pending Chance Law remain private to the
ReferenceNature boundary. A pending law is available through the separate
Nature-choice view, while privileged RNG inspection remains a distinct
interface.

## Scope limits

The session controls the listed gold, Stone of All Time, and Future of Potions
entry requests on configured Native routes. Its Native-regional legacy route
controls the listed generated-gold, Stone potion-slot, and Future card-type
index requests; other event-entry choices and subsequent event actions remain
outside it. Native event selection and any other draws remain on their
existing providers. The public class, CLI command, and Python method retain the
earlier gold-oriented names for compatibility with the first session schema.

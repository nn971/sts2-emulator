# ReferenceNature Endless Conveyor dish law and session contract

This contract covers the first weighted dish roll made when a configured
Event-node action enters Endless Conveyor. The ordinary event-selection
provider remains in control; the query is available only when that selection
resolves to Endless Conveyor.

## Boundary and law

`ChanceLawEngineV111.QueryNativeEndlessConveyorDishEntry` accepts the stable
action ID of a currently legal Event-node action from the map-choice phase.
It probes the action on a fork, captures the typed unit-float request and the
pre-draw event/player state, then builds the law from that state. It does not
use the sampled dish outcome. The source run is unchanged. Only ordinal zero,
the initial dish roll, is supported; a map action that enters another event
has no Endless Conveyor law.

The support and integer weights follow the source-pinned candidate order:

| Dish | Weight | Eligibility |
| --- | ---: | --- |
| Caviar | 6 | Always |
| Spicy Snappy | 3 | Always |
| Jelly Liver | 3 | Always |
| Fried Eel | 3 | Always |
| Suspicious Condiment | 3 | At least one potion slot is empty |
| Clam Roll | 6 | Player is below max HP |

Golden Fysh is absent on the first roll. The forced fifth Seapunk Salad is
draw-free. The previous dish, dish count, HP, max HP, empty potion slots, map
action, ordered weighted support, and typed request bind the conditioning
fingerprint. The outcome IDs are `dish:<id>` and the weights reproduce the
emulator's source-shaped float-ticket categories.

The player-visible event projection contains only the committed dish and
legal actions. A Nature-only view may expose this law before the initial dish
is committed; the privileged pre-draw run and Native stream cursor stay out of
that projection.

Probability status remains `UNVERIFIED`: native float boundary behavior,
source call-order parity, and seeded result parity have not been calibrated.

## Atomic initial-roll continuation

`ReferenceNatureEndlessConveyorSessionV111` exposes this law through the
JSONL and Python session APIs. Begin requires the stable ID of a legal
Event-node action. The law is available only if ordinary event selection on
that action enters Endless Conveyor. Resolving the single Nature choice
replays the map action with only the dish unit-float request scripted; ordinary
event selection retains its existing provider. The selected initial dish and
ReferenceNature sampler cursor commit together, while the Endless Conveyor
Native event cursor remains at event entry. Fork, save/restore, replay
validation, release, capability coverage, and the Nature-only pending choice
view are supported. Jelly Liver and Suspicious Condiment retain the scripted
marker while their existing grab prompt is pending. Resolving that prompt and
rolling another dish returns to the ordinary Native provider and clears the
marker. Choosing Observe also clears it after the ordinary upgrade action.

## Limits

The session covers only the first weighted roll on Event-node entry. Later
dish rolls after Grab, including rolls deferred behind Jelly Liver or
Suspicious Condiment choices, remain outside the query and session boundary.
The pinned event source audit remains authoritative for dish effects and roll
timing.

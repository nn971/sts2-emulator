# ReferenceNature Endless Conveyor dish law and session contract

This contract covers one weighted dish roll during configured Event-node
entry or an active Endless Conveyor continuation. The ordinary
event-selection provider remains in control; entry coverage is available only
when that selection resolves to Endless Conveyor. Active-event coverage is
available only when a current legal action reaches exactly one weighted dish
ticket.

## Boundary and law

`ChanceLawEngineV111.QueryNativeEndlessConveyorDishEntry` accepts a stable
action ID. In map-choice, it must identify a legal Event-node action. In an
active Endless Conveyor event, it must identify a legal action whose ordinary
continuation reaches one weighted dish ticket. The query probes the action on
a fork, captures the typed unit-float request and the pre-draw event/player
state, then builds the law from that state. It does not use the sampled dish
outcome. The source run is unchanged. The initial roll is ordinal zero; later
roll ordinals are inferred from the captured Native event cursor, or checked
against `ordinal` when supplied. A map action that enters another event, an
action with no weighted roll, and the forced fifth Seapunk Salad have no law.

The support and integer weights follow the source-pinned candidate order:

| Dish | Weight | Eligibility |
| --- | ---: | --- |
| Caviar | 6 | Always |
| Spicy Snappy | 3 | Always |
| Jelly Liver | 3 | Always |
| Fried Eel | 3 | Always |
| Suspicious Condiment | 3 | At least one potion slot is empty |
| Clam Roll | 6 | Player is below max HP |

Golden Fysh is absent on the first roll and can enter later supports. The
forced fifth Seapunk Salad is draw-free. The previous dish, dish count, HP,
max HP, empty potion slots, action, pre-roll Native cursor, ordered weighted
support, and typed request bind the conditioning fingerprint. The outcome IDs
are `dish:<id>` and the weights reproduce the emulator's source-shaped
float-ticket categories.

The player-visible event projection contains only the committed dish and
legal actions. A Nature-only view may expose this law before the initial dish
is committed; the privileged pre-draw run and Native stream cursor stay out of
that projection.

Probability status remains `UNVERIFIED`: native float boundary behavior,
source call-order parity, and seeded result parity have not been calibrated.

## Atomic single-ticket continuation

`ReferenceNatureEndlessConveyorSessionV111` exposes this law through the
JSONL and Python session APIs. Begin requires a current legal action. On map
entry, the law is available only if ordinary event selection enters Endless
Conveyor; during an active event, the action must resolve exactly one weighted
ticket. Resolving the Nature choice replays the complete action with only the
dish unit-float request scripted, leaving event selection and any preceding
random effects on their existing providers. The dish and ReferenceNature
sampler cursor commit together. On entry, the Native event cursor stays at
event entry; during an active action, it stays at the pre-ticket cursor after
any ordinary random prefix. Fork, save/restore, replay validation, release,
capability coverage, and the Nature-only pending choice view are supported.
Jelly Liver and Suspicious Condiment retain the scripted marker while their
grab prompt is pending. Resolving that prompt and rolling another dish returns
to the ordinary Native provider and clears the marker. Choosing Observe also
clears it after the ordinary upgrade action.

## Limits

Each session resolves one weighted ticket through the action that reaches it.
This supports later rolls after Grab, including a deferred Jelly Liver or
Suspicious Condiment prompt, when its legal resolution action reaches exactly
one ticket. Other random requests within that action keep their ordinary
providers; the Native event cursor is preserved at the pre-ticket position.
The forced fifth Seapunk Salad is draw-free, and a session does not combine
multiple dish tickets. The pinned event source audit remains authoritative for
dish effects and roll timing.

Actions that do not produce a weighted ticket and the forced fifth dish are
unsupported as Nature choices; the ordinary event mechanics continue without
a Chance Law at those boundaries.

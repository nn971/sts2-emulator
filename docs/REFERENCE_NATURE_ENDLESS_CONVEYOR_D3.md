# ReferenceNature Endless Conveyor law and session contract

This contract covers one weighted dish roll during configured Event-node
entry or an active Endless Conveyor continuation, plus the first-dish Observe
Chef card upgrade. The ordinary event-selection provider remains in control;
entry coverage is available only when that selection resolves to Endless
Conveyor. Active dish coverage is available only when a current legal action
reaches exactly one weighted dish ticket. Observe coverage is limited to the
legal first-dish action on page zero.

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

## First-dish Observe Chef upgrade

`ChanceLawEngineV111.QueryNativeEndlessConveyorUpgrade` accepts only the
configured active event's legal `observe` action while the first dish is
active. It uses the same deck-order `CanSelectEventDeckCard(Upgrade)` filter
as the mechanics, preserves physical card-instance identities, and binds the
law to the persisted `ENDLESS_CONVEYOR` cursor and its request ordinal. The
Native action makes one `choose_index` request over indices in that ordered
support. An empty eligible pool skips the draw and has no law; a singleton
pool still has a law because the Native index boundary is called. The outcome
IDs pair each card instance with its source index. Equal weights describe the
emulator's modeled support only; the probability law remains `UNVERIFIED`.

The player-visible projection may expose this ordered support only through
the Nature choice view. The ordinary event projection continues to show the
committed upgrade effect without exposing deck order or the Native cursor.

Probability status remains `UNVERIFIED`: native float boundary behavior,
source call-order parity, and seeded result parity have not been calibrated.

## Atomic event-specific continuation

`ReferenceNatureEndlessConveyorSessionV111` exposes both boundaries through
the JSONL and Python session APIs. Begin requires a current legal action. On
map entry, the dish law is available only if ordinary event selection enters
Endless Conveyor; during an active event, a dish action must resolve exactly
one weighted ticket. The Observe action replays only its card-upgrade index
through the ordinary action and verifies the selected physical card changed
by one upgrade level. Dish replay scripts only the typed unit-float request,
leaving event selection and any preceding random effects on their existing
providers. Each result and ReferenceNature sampler cursor commit together;
the Native event cursor remains at its pre-draw position. Fork, save/restore,
replay validation, release, capability coverage, and the Nature-only pending
choice view are supported.
Jelly Liver and Suspicious Condiment retain the scripted marker while their
grab prompt is pending. Resolving that prompt and rolling another dish returns
to the ordinary Native provider and clears the marker. An ordinary Native
Observe clears it after the upgrade action; a scripted Nature Observe retains
the marker while preserving the Native cursor.

## Limits

Each session resolves one weighted ticket through the action that reaches it.
This supports later rolls after Grab, including a deferred Jelly Liver or
Suspicious Condiment prompt, when its legal resolution action reaches exactly
one ticket. Other random requests within that action keep their ordinary
providers; the Native event cursor is preserved at the pre-ticket position.
The forced fifth Seapunk Salad is draw-free, and a session does not combine
multiple dish tickets. The pinned event source audit remains authoritative for
dish effects and roll timing.

The Observe session is separate at the action boundary but uses the same
event-specific session schema and lifecycle. It covers only the first dish,
requires a nonempty upgradable-card support, and preserves the event cursor
while scripting the upgrade index. Empty support and later-page Observe
actions are outside this Nature boundary.

Actions that do not produce a weighted ticket and the forced fifth dish are
unsupported as Nature choices; the ordinary event mechanics continue without
a Chance Law at those boundaries.

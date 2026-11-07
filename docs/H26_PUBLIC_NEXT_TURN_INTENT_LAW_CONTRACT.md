# H26 — player-public next-turn enemy-intent law contract

**Status:** design/fidelity gate for H26 search-free tactical reinforcement.
Based on pinned emulator base `203d5114024b55c69dabc309f10d6f196eac8cca`.

## Source implementations already present

- `PrototypeGameEngine.EvaluateEnemyIntentChoice` and the corresponding
  `SelectEnemyMove` logic determine a semantic move support after canonical
  eligibility filters and a bounded random ticket.
- `SemanticIntentSupportV111` holds weighted candidate moves and ticket
  intervals. `ChanceLawEngineV111.QueryIntent` converts the interval weights
  into source-verified Native NextInt grid weights for that **suspended
  selection boundary**.
- `PrototypeAiEnemy` exposes the **currently committed** `move_id` and,
  when a uniform attack number is faithfully computable, per-hit
  `intent_damage`, `intent_hits`, and `intent_base_damage`.
  Player Block is separately observable; heterogeneous attacks may have
  no numeric damage field.
- Observing public state does not sample the next move or advance RNG.

## Exact next-turn intent vs. suspended choice

The probability distribution at an actual pending stochastic choice is
`P(I | canonical state immediately before the choice)`. For H26, the
desired player-facing quantity is instead

`P(I_{t+1} | public history through t, public post-action/turn state)`.

Those are the same only when every probability-relevant variable is
determined by the public history. The source selector references, for
example, `AiStateId`, `MoveIndex`, `LastMoveId`,
`ConsecutiveMoveUses`, `MoveUseCounts`, stun follow-ups, and formation
conditions. Not all are fields in the current `PrototypeAiEnemy` projection.

This creates an **information-boundary verification requirement**.
An exact declared law must be either:
1. derived from the public history alone, including a proved public
   reconstruction of all probability-relevant counters/state; or
2. marginalized over player-indistinguishable canonical possibilities
   using a justified public belief-state law.

Returning the chosen canonical state's law without that proof could
expose a hidden state, even if its probabilities are independently
source-verified. We should never substitute the current selected next
intent, a single RNG sample, or the unconditioned move list for a
player-public distribution.

## Proposed public API

`public_next_intent_law_v1(public_history, conditional_public_state)`

returns one of:

- `EXACT`: each enemy's eligible `move_id` and exact rational
  probability; a certified joint law when enemy choices are correlated.
- `CONDITIONAL`: source-verified law conditional on explicitly described
  **public** assumptions (for example, a still-alive unchanged formation
  after the end of the current turn).
- `UNAVAILABLE`: no certified player-public law; include a typed reason.

Include provenance and total-mass checks; distinguish unquantified
attack damage from a non-attacking move. Calculating next-turn damage
must apply the appropriate public post-action modifiers and the combat
engine's damage projection, rather than trusting present-turn numbers.

## Acceptance tests before supplying H26-C training features

1. Two canonical histories indistinguishable to the player produce
   identical law outputs; known observationally distinguishable
   histories may differ.
2. Calling the public-law query never advances RNG, mutates game state,
   or reveals a hidden committed move.
3. Exact law support/weights agree with Native source-controlled
   enumeration, including state-machine restrictions, consecutive-use
   limits, conditional branches, summons, stun, phase changes, and
   interaction between enemy intent draws.
4. Law probabilities sum to one. `UNAVAILABLE` is explicit, not a zero
   or uniform fallback.
5. Native inspector parity tests validate the shape and conditioning.

## Implemented first slice: versioned public source-rule catalog

`PublicEnemyIntentCatalogV111.Create()` exports the enemy ID, move IDs,
move-selection policy, opening sequence, consecutive-use limits, and loop
positions from `PrototypeContent.Enemies`. It also exports **exact**
Native `NextInt` integer grid weights using the existing
`ChanceLawEngineV111.NativeNextIntGridWeights`.

The pure CLI command is:

```shell
dotnet run --project src/Sts2Emulator.Cli/Sts2Emulator.Cli.csproj \
  -- public-intent-catalog-v111
```

It reads no run snapshot, consumes no RNG, and leaves the canonical
player observation schema unchanged. A scoped .NET test validates the
catalog against source enemy definitions and exact Native weights.

The H26 training branch consumes this catalog and calculates
`CONDITIONAL_EXACT` next-intent laws when the **visible current move**
uniquely determines the next move's eligible support. An explicit
`UNAVAILABLE` outcome handles ambiguity and unverified conditions.
The prediction is conditional on the displayed move executing normally.
State-machine history reconstruction and live native parity remain
unimplemented acceptance gates.

## H26 v1 gate

H26 v1 currently trains using exact **present visible** per-hit intent
numbers and a conservative incoming threat feature. No next-turn
probability law is supplied to the model while the above public
observability and conditionality acceptance gates are open. The
existing semantic chance-law engine is intended as the implementation
source for the subsequent H26-C extension.

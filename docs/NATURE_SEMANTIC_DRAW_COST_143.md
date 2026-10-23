# Nature #143 — semantic drawn-card energy-cost continuation

**Implementation:** `SemanticCombatDrawEngineV111`,
`PrototypeGameEngine.DrawCards`, and
`PrototypeGameEngine.ResumeSemanticCombatDrawCost`.
**Scope:** the opt-in, conditioned
`sts2-v111-semantic-real-combat-draw-v1` segment. The underlying deck
provider is `sts2-v111-semantic-deck-v1`.

## S0 stochastic site contract

The `combat.card_draw.relic_energy_cost` and
`combat.card_draw.slither_energy_cost` rows are owned by the selected physical
card instance and the matching draw-time source rule. Eligibility, printed
cost, current enchantment and active relic bounds remain private engine
constraints. Nature receives the bounded candidate cost only at the saved
pre-`CardDrawn` boundary. The fair player observation does not expose the
candidate cost set, pending request fingerprint or Nature transcript. After
resolution the card appears in the ordinary hand projection; the observation
schema does not expose a separate numeric temporary-cost preview.

The saved draw continuation resolves the cost by the same physical card
identity and dispatches `CardDrawn` once before resuming the remaining draw
count. Unsupported draw sources and stale or mismatched cost requests fail
closed at their existing continuation boundary. The read-only Chance Law
counts exact Native `NextInt` 53-bit grid masses for the declared bounded
support and reports `SOURCE_VERIFIED` for that transform. Native stream
assignment, seed frequencies, cursor parity, and any source-specific cost
eligibility beyond that support remain separate.

## Ordering contract

The actual card draw first resolves the semantic
`combat.draw_pile.draw_card` identity of the **physical card instance**,
moves that card into the hand and updates the physical/provider pile
membership. Before `CardDrawn` is dispatched, source mechanics inspect
the selected card's definition, enchantment and active relics:

1. If a source randomize-drawn-cost relic such as **Snecko Eye** applies
   to the selected card's fixed, nonnegative printed cost, Nature
   receives `combat.card_draw.relic_energy_cost`, bounded by the active
   relic's `RandomizeDrawnCardCostMaxExclusive`. The resulting
   temporary card cost is set on the same physical card instance.
2. If **Slither** is on that card, Nature next receives
   `combat.card_draw.slither_energy_cost`, bounded to integers 0–3.
   This second choice overwrites the previous relic cost by design.
3. After all applicable cost tickets have been resolved, the original
   `DispatchCombatEvent(CardDrawn)` executes **once** with the chosen
   card instance ID and original `FromHandDraw` flag.
4. The previously saved remaining draw count is resumed through the
   same `DrawCards` kernel. It can reach another per-card identity,
   another cost decision, or a source-timed discard recycle and new
   deferred shuffle epoch. A cost decision after the final requested
   card also executes its `CardDrawn` event before the segment is
   complete.

This continuation uses the actual existing
`MakeControlledDrawCostChoice` state (which records physical card
identity, source stage, relic bound, Slither eligibility, and remaining
draw count), while using **a separate semantic cost protocol**, with
its own `semantic.combat` stream label, ticket ordering and
`semantic-controlled-v1` provider provenance. No legacy controlled
shuffle-swap tickets are emitted. Under Nature no native RNG cursor
advances for these counterfactual cost selections.

Outside this Nature continuation, the ordinary `DrawCards` path now uses the
same two stable site IDs through typed providers: `run.combat_energy_costs`
when native combat streams are attached and legacy `combat` otherwise. Native
stream ownership and call-order parity remain unverified. Native semantic-deck
draws use this shared path once before `CardDrawn` dispatch.

When an opt-in Native-policy audit is active, the random-boundary journal also
annotates each of these exact Native cost tickets with the drawn card instance,
relic or Slither stage, and sampled cost. The existing semantic bounded-cost
decision is reused; source stream ownership and cursor parity remain open.

## Saved state and replay

A pending drawn-card cost is represented inside the forkable
`CombatState.PendingChoice.ControlledDrawCost`. The semantic draw
envelope now stores both:

- `SemanticDeckStateV111.Transcript`: chronological per-card identity
  commitments, tied to source shuffle epochs and selected instances.
- `SemanticCombatDrawV111.CostTranscript`: sequential bounded cost
  choices including site ID, eligible stage, card instance ID, bound,
  ordinal and explicit `UNVERIFIED` probability status.

`SemanticCombatDrawEngineV111.Restore` starts again from the exact
conditioned combat snapshot. It dynamically observes whether the
mechanics next requests a **card identity or drawn cost**, consumes one
matching saved decision from the appropriate transcript, runs the
original combat mechanics until the next suspension, and verifies
the entire resulting RunState and persisted continuation. Nested cost
tickets for the same card occur between two card-identity commitments.
Requests are compared structurally, including ordered candidates,
rather than by serialized-array object reference.

## Tests and scope

The focused tests cover Snecko-only, Slither-only, Snecko-then-Slither
overwriting the temporary cost, event counter timing, a one-card
terminal draw-cost suspension, multiple sequential card draws, an
effect-style `FromHandDraw=false` segment, stale/out-of-support and
forged-card rejection, native stream isolation, full/partial snapshot
replay and deterministic 15-worker sessions.

The semantic JSONL pilot now has a separate opt-in entry
(`fixture_nature_draw_costs`) that surfaces this saved continuation as a
typed `drawn_card_cost` event when paired with the explicit Snecko Eye test
loadout. It preserves the default P0 engine-owned cost fallback and does not
change the general conditioned draw-segment contract above. In the player
card-effect frame, recycle, Stratagem selection, after-shuffle relic insertion
and standard choices opened by `CardDrawn` hooks compose through serialized
event and outer-choice continuations. Knowledge Demon choices and standalone
or automatic draw-pipeline frames remain separate boundaries. The native
reference probability law remains explicitly `UNVERIFIED` until source-audited.

## Card-effect draw composition

The semantic player-action pilot now attaches the existing
`ControlledDrawCost` checkpoint to its saved card-effect draw continuation.
The same `combat.card_draw.relic_energy_cost` and
`combat.card_draw.slither_energy_cost` typed bounded-cost events are exposed
after the physical card identity is selected. Cost choices resolve before the
original `CardDrawn` dispatch, then the saved remaining draw count and queued
card effects resume. Cost decisions are recorded in the pilot's existing
`semantic_cost` action history and replay against the current card, relic
bound, Slither stage and event support. The card-effect path permits both
`FromHandDraw` values and nonzero event depths while the standalone conditioned
draw envelope retains its own source constraints.

The saved frame also retains card instances awaiting a `DrawnCardKeyword`
override. The override is applied after all requested cards and their draw
hooks finish, preserving source operation order across identity, cost,
recycle, Stratagem, after-shuffle insertion and nested `CardDrawn` choice
suspensions.
The frozen Silent all-Act `main` remains unchanged.

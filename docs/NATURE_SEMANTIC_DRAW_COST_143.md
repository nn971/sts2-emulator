# Nature #143 — semantic drawn-card energy-cost continuation

**Implementation:** `SemanticCombatDrawEngineV111`,
`PrototypeGameEngine.DrawCards`, and
`PrototypeGameEngine.ResumeSemanticCombatDrawCost`.
**Scope:** the opt-in, conditioned
`sts2-v111-semantic-real-combat-draw-v1` segment. The underlying deck
provider is `sts2-v111-semantic-deck-v1`.

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
change the general conditioned draw-segment contract above. A `CardDrawn`
event that itself creates a nested pending choice, Stratagem post-shuffle
selection, and a relic that inserts a newly generated card at a random
position after shuffle still need distinct continuations. The native
reference probability law remains explicitly `UNVERIFIED` until
source-audited.
The frozen Silent all-Act `main` remains unchanged.

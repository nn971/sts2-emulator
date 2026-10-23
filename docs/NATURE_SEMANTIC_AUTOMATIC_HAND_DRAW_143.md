# Nature #143 — semantic automatic DrawPlayerHand integration

**Branch scope:** opt-in staged Nature continuation in the RNG/Inspector
integration branch. **Schema:** `sts2-v111-semantic-real-combat-draw-v1`,
with `AutomaticHandDraw=true`.
**Entry:** `SemanticCombatDrawEngineV111.BeginAutomaticHandDraw`.

## Source-shaped automatic checkpoint

The caller supplies a real `RunState` already paused at the
`DrawPlayerHand` automatic pipeline index, with any earlier enemy
turn/intents and turn-start phases completed. The entry checks that
the cursor is an ordinary, unsuspended `DrawPlayerHand` checkpoint.

It then attaches a fresh unresolved semantic deck provider to the
physical draw-pile instances and executes the **actual**
`PrototypeGameEngine.ResolveAutomaticStep(DrawPlayerHand)`. This
executes `BeforeHandDraw`, the rebound/turn bonus and relic effects,
computes the source-defined hand-draw target (including Snecko's hand
draw increase), and enters the original `DrawCards` kernel.

The first unresolved card identity or drawn-cost decision persists:
- The complete `RunState` with every preceding side effect.
- `AutomaticPipelineContinuation.NextStepIndex = DrawPlayerHand`.
- `ControlledSemanticDrawRemaining` and
  `ControlledSemanticDrawRequested` (distinct from existing legacy
  controlled-Fisher–Yates shuffle/draw-cost cursors).
- Semantic deck instance, pending card identity and its recorded
  choices, plus a separate interleaved Snecko/Slither cost transcript.

Resolving a pending chance selection executes the **same** physical
`DrawCards` call, committing a card identity, temporarily changing
cost when needed, then running `CardDrawn` once. Every subsequent
decision continues from the saved state. `BeforeHandDraw`, earlier
turn phases and prior card/event hooks are not executed again. If a source
action choice suspends inside `BeforeHandDraw`, the legal actions are exposed
as a typed hook-choice event. Resolving it resumes the saved event dispatch
and the same automatic stage before the deterministic hand-draw prelude;
earlier subscribers are not replayed.

If Jeweled Mask suspends inside `BeforeHandDraw`, its separate random Power
card event exposes ordered eligible Power instances (noninnate Powers first).
Resolving it resumes the event and operation continuations before entering the
original draw stage; the selected card goes to hand or discard according to
the source hand-cap rule. Replay records both forms of hook choice alongside
draw and cost decisions.

If the draw recycles and an after-shuffle relic inserts a generated card, the
automatic cursor also saves the requested and remaining draw counts around the
typed insertion event. Resolving each insertion synchronizes the semantic deck
with the physical pile and resumes the same `DrawCards` call. This route uses
the existing post-hand-draw cleanup and advances the automatic cursor once the
draw finishes. If Stratagem is active after recycling, its source
`select_cards` choice is saved as part of the automatic cursor; resolving it
continues the same remaining draw. This also composes with after-shuffle
insertions and drawn-cost stages. Ordinary source action choices from
`CardDrawn` hooks are supported; nested controlled chance choices remain
unsupported.

When the requested draw ends, the source's `RemoveAfterHandDraw` and
`DecrementAfterHandDraw` power hooks execute once and the saved
pipeline index advances to **the next automatic step**. That later
step is intentionally left for a future whole-action Nature controller:
completion of this stage is not the same as reaching the next player
decision. The entry also preserves the semantic recycle epoch and
Perfect Fit constraints already introduced by PR #159.

## Replay and invariants

`Restore` reruns the automatic hand-draw stage from its exact
source checkpoint. It consumes the persisted card-identity and
cost-choice transcripts in live mechanical order, and checks the
complete resulting `RunState` and saved stage cursor. Branches
fork independently; stale fingerprints and tampered cursor/choice
states are rejected.

The focused tests cover real `BeforeHandDraw` and
`DrawPlayerHand` execution, hand-size bonuses, one-time card hooks,
Snecko-then-Slither cost ordering, source-timed discard recycle,
saved pending and finished stages, 15-worker deterministic forks,
and strict cursor validation.

## Remaining integration milestones

This is an **authentic automatic-stage continuation from an existing
checkpoint**, not yet a complete `end_turn` action. The next
integration should connect prior enemy actions, visible next-intent
Nature decisions, and later automatic steps into one
`step` → `nature_resolve` → `step` controller. Opening-hand and
player-card-effect draw contexts also require their own saved
continuations. Nested controlled chance choices and native RNG source-law
parity remain separate gates. The Jeweled Mask and ordinary
`BeforeHandDraw` action-choice continuations have not been mechanics-tested.

The old `controlled-v111-playable-fixture-v1` swap-ticket schema
is unchanged. Frozen Silent four-Act `main` is untouched.
Probability provenance remains `UNVERIFIED`.

## S0 stochastic boundary: Jeweled Mask Power selection

The `combat.relic.jeweled_mask.random_power_draw_card` operation is owned by
the relic's `BeforeHandDraw` hook. Its private support prefers eligible
non-Innate Power instances and falls back to the ordered Power pool. Nature
may inspect that support only in the opt-in pending event; the player's
fair player-visible projection exposes the resulting card and hand state,
not pool order or RNG state.

Resolving the card resumes the saved hook and then the same automatic hand
draw stage, applying the source hand-cap destination and card cost once.
Nested chance requests without a serialized continuation must fail closed.
The read-only Chance Law now reports exact Native `NextInt` 53-bit grid
masses and `SOURCE_VERIFIED` for the bounded-index transform on the declared
physical-card support. This Jeweled Mask continuation still lacks
mechanics-tested evidence; pool completeness, Native stream attribution,
seed frequencies, and cursor parity remain open. No privileged per-choice
transcript is exposed in the player projection. The opt-in Native-policy
audit separately retains the ordered physical-card support and selected
identity with the exact provider decision and cursor span; its new completeness
coverage still does not establish pool or stream parity.

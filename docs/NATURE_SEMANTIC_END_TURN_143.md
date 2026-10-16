# Nature #143 — restricted complete end-turn action

**Implementation:** `SemanticEndTurnV111.cs` and the opt-in automatic pipeline
handoff in `PrototypeGameEngine`.
**Schema:** `sts2-v111-semantic-end-turn-v1`.
**Source build:** pinned v0.111.0; single-player combat first.

## First complete real action boundary

This is a narrow **real `step` → Nature choices → next `step`**
capability. `BeginFromPlayerAction` receives a valid player-turn combat
`RunState` and original enemy encounter entry ID, confirms that
`end_turn` is a legal action, then executes that action through the
ordinary `PrototypeAiEnvironment.Step` under the controlled RNG guard.
The action executes its true source-shaped end-of-turn, enemy actions
and statuses until the original `AdvanceTurn` intent checkpoint.

1. `ControlledTurnIntentEngineV111` emits successive source-shaped,
   bounded enemy intent tickets with the stable creature ID and current
   AI state. Nature commits those source-eligible outcomes.
2. `ResumeControlledTurnAfterIntents(...,
   stopBeforeSemanticHandDraw: true)` commits the selected intents and
   executes the intervening **original automatic turn-start pipeline**,
   saving the exact `DrawPlayerHand` stage cursor before its hooks fire.
3. `SemanticCombatDrawEngineV111.BeginAutomaticHandDraw` invokes the
   authentic `BeforeHandDraw` / `DrawPlayerHand` stage. Source-triggered
   discard recycling produces a **new unresolved deck epoch**. Nature
   selects each card instance only when needed; Snecko/Slither cost tickets
   and `CardDrawn` hooks execute between physical draws. After-shuffle relic
   insertions use the typed insertion event, Stratagem uses its source
   `select_cards` action, and ordinary `CardDrawn` hook actions are exposed
   as player choices before the remaining draw resumes. Ordinary source
   action choices from `BeforeHandDraw` are also resolved at a player-choice
   boundary, then resume the saved dispatcher before the hand-draw prelude.
4. The ordinary one-time post-hand-draw power cleanup advances the
   automatic index. `ResumeSemanticAutomaticAfterHandDraw` executes
   the remaining **real pipeline**. The controller checks that combat
   has returned to the player's turn with a legal player action,
   no pending choice and no automatic cursor.

`SemanticEndTurnV111` preserves the original player-action source,
post-action AdvanceTurn source checkpoint, every enemy intent ticket,
the complete semantic hand-draw state, card, cost, insertion and Stratagem
selection transcripts, and the resulting combat state. The `Restore` path
**re-executes the genuine player action** and then each Nature ticket or
source selection in the actual observed mechanical order before comparing
the full state. `ResolveStratagemSelection` exposes the source card action
through the end-turn wrapper.

## Critical hidden-state restriction

When no provider-owned Nature deck exists at the AdvanceTurn checkpoint,
this implementation **requires the draw pile to be empty** and the discard
pile to be nonempty. The following hand draw then triggers a *genuine fresh
source-time recycle*. On later turns, an existing idle Nature deck is reused
with its exact constraints and may have a nonempty draw pile. An earlier
precommitted native or legacy-controlled shuffled order must never also
generate new per-card Nature decisions for the same deck; the code never
replaces that order with a newly attached semantic bag.

This gate is deliberately narrow; it provides an honest nontrivial
end-to-end real action boundary while the larger source-committed/
provider-owned state migration remains incomplete. At subsequent
turns, the semantic deck representation must persist through all
player actions and deck mutations, including knowledge of forced
positions, until the next shuffle. Reattaching a fresh semantic bag to
an existing unresolved pile is **not** an allowed shortcut.

## Validation and outstanding work

Focused tests cover legal end-turn actions, real enemy intent tickets,
intervening automatic stages, fresh recycle, per-card and card-cost
Nature choices, legal next-player actions, complete source replay
including unfinished enemy intent/hand-draw stages, tampered intent
decisions and stale fingerprints, the double-count safety gate, and
deterministic 15-worker sessions.

This slice establishes a **restricted first real action-to-action
end-turn**, not arbitrary-turn looping or the public semantic JSONL
contract. The next deliverables are persistent semantic deck state
across ordinary player actions, opening-hand and card-effect draws,
weighted candidate intent metadata, nested `CardDrawn` effects, replay
capability negotiation and terminal rollouts. Source probability law and
native seed/cursor parity are separately `UNVERIFIED`.

Legacy controlled shuffle-ticket transcripts remain versioned and
unchanged; frozen Silent four-Act `main` remains untouched.

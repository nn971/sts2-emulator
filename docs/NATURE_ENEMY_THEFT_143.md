# Nature #143 — Thieving Hopper card selection

## S0 stochastic site contract

`combat.enemy.theft_card.select` is conditioned on the source-eligible
persistent card instances in the draw and discard piles and the thief's
saved enemy-action boundary. The semantic enemy-action event pairs its
bounded-index request with ordered `card-instance:<id>` candidate labels; the
player observation does not expose those labels or the support fingerprint.
After resolution, the selected card's committed zone/deck change is visible
through the ordinary combat projection.

The existing replay frame resolves the selected physical identity once and
resumes the enemy action. A draw-pile selection that would split an unresolved
semantic deck pool fails closed before support is published; discard-pile
selection does not alter hidden draw order. The Chance Law uses exact Native
`NextInt` grid masses when the pending request names a recognized Native
stream, reporting `SOURCE_VERIFIED` for that bounded transform. Legacy or
unknown stream requests retain equal `UNVERIFIED` weights; Native stream
ownership and source-pool completeness remain open.

Thieving Hopper's `combat.enemy.theft_card.select` choice now uses the saved
enemy-action replay frame. The emulator replays the enemy action from its
pre-step `RunState`, injects earlier controlled enemy choices in order, then
uses Nature's index to select from the source-eligible persistent cards in the
draw and discard piles. The selected instance is removed from those combat
zones and from the persistent player deck.

For a Nature semantic deck, the choice is published only when every eligible
draw-pile instance can be removed without breaking the deck's current hidden
position constraints. Discard-pile choices do not mutate the semantic draw
deck. If any draw candidate would split an unresolved pool, the end-turn action
fails closed before exposing a partial support.

The JSONL bridge exposes the choice through the existing `enemy_action_chance`
event. Its `candidate_labels` now carry ordered `card-instance:<id>` outcomes,
and `semantic_resolve_enemy_action` accepts `candidate_label`; the prior
`selected_index` input remains available for compatibility. The Chance Law
continues to describe the saved index support and uses the typed Native stream
ID to choose between exact bounded-transform masses and the legacy equal-index
model. The stable request retains enemy ownership, ordered card-instance
support identity and source ordinal.
Native combat currently identifies `run.combat_card_generation` as a stream
hypothesis; its native assignment and cursor parity remain `UNVERIFIED`.

When the two-site Nature pilot carries the full run-owned Native boundary
provider, it resolves this saved enemy-action request through
`run.combat_card_generation` and replays the same theft continuation. The
Native primitive transcript stays private to the run owner; the end-turn frame
still records the selected card identity for mechanics replay. The provider
handoff does not establish native stream attribution, pool fidelity, or cursor
parity.

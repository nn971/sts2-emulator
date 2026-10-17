# Nature #143 — Thieving Hopper card selection

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
event and `semantic_resolve_enemy_action` request. Its Chance Law now exposes
the saved index support with an explicitly `UNVERIFIED` uniform index law.
The stable request retains enemy ownership, ordered card-instance support
identity and source ordinal.
Native combat currently identifies `run.combat_card_generation` as a stream
hypothesis; its native assignment, cursor parity and conditional law remain
`UNVERIFIED`.

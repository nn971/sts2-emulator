# Nature #143 — Controlled random draw-pile insertion

This slice adds two source-shaped chance sequences to the real `PrototypeGameEngine.ResolveOperations` combat-effect kernel, under the opt-in Nature guard:

1. `CreateGeneratedCardsAtRandomDrawPositions`: a **bounded position** ticket, `combat.draw_pile.random_insert_index`, for each fixed-model generated card.
2. `CreateRandomFreeAttacksInDrawPile`: a **source-ordered card-model** ticket, `combat.random_draw_attack.card_model`, followed by a bounded insertion-position ticket for each generated Attack.

The first site permits indices from 0 through the current draw-pile count inclusive, matching the normal `PrototypeRng.NextInt(rng, "combat", draw.Count + 1)` call. The second selects an eligible Attack from the existing ordered `NativeSilentCardPool` filter and then inserts that temporary zero-cost card at Nature's selected index. Card instances and `NextCardInstanceId` are created only at the second stage; partial card-model selection leaves the deck unchanged.

Native combat routes the model draw through `run.combat_card_generation` and
the random position through `run.shuffle`; `CardPileCmd` uses
`RunState.Rng.Shuffle` for `CardPilePosition.Random`. The retained
prototype profile preserves its `combat` stream through the legacy adapter.
The semantic v1 protocol continues to label its controlled event
`prototype.combat` for compatibility. Native cursor parity and the full
unlocked candidate pool remain open.

When either supported insertion operation is reached inside an automatic
pipeline stage, its saved cursor now stays attached through model selection,
each insertion position, and the resumed source operation. Enemy random draw
and discard insertion now have a separate automatic-pipeline frame: the
pre-action `RunState`, pipeline index, prior controlled outcomes, and pending
typed request are serialized. Resolving a position replays the source enemy
action resolver from that checkpoint, injects all prior positions in order,
and commits the result only when the stage completes. This is wired into both
the controlled playable and semantic end-turn continuations.

After-shuffle relic-generated draw-pile cards now use a separate typed
`run.shuffle` site in native combat and preserve legacy `combat` draws. That
hook records the inserted position in the native exact semantic deck. A
Nature card-effect draws and standalone conditioned draw segments reuse the
existing semantic insertion event and resume their saved draw frames;
automatic hand-draw and other recycle contexts still fail closed until their
pipeline continuations are serializable. Enemy-side random draw/discard
insertion uses typed `run.shuffle` sites and now resumes through the saved
enemy-action frame. Enemy summon-pool selection, enemy theft selection, and
nested death/summon random effects remain separate continuation gaps.

The private pending choice records the exact eligible draw-pile instance IDs, source operation, generated card ID or eligible Attack IDs, remaining repetition count in the saved queued-operation continuation, and source-card/event/card-play continuation identity. On completion, the original queue resumes without re-running earlier effects. Multiple insertions generate successive tickets and are recorded chronologically in the existing `LiveTranscript`, with independent fork outcomes and source-eligibility validation. Nature receives card models in `pending.candidateCardIds` for the model-selection stage. The ordinary fair player observation remains inaccessible during pending Nature decisions; source probability status is `UNVERIFIED`.

## Semantic JSONL/Python fixture bridge

The semantic P0 session can opt into the fixed-card position stage with
`fixture_generated_draw_insertion_card_id="proto.silent.strike"` (or another
source-valid card model), or into random Attack generation with
`fixture_random_free_attack_draw_insertion=true`. Both start at the existing
guarded insertion continuation. The fixed-card path exposes positions from
zero through the current draw-pile count. The random-Attack path first exposes
the source-ordered card-model event, then the bounded position event.
Resolving a position resumes the source operation and inserts the known card
identity into the semantic deck as a private known anchor, leaving all
unresolved positions in their existing shared pool. Python clients use
`SemanticCombat.resolve_draw_insertion(position)` after resolving the model
with `resolve_generated_card(card_model_id)`. Native law and fixture
acquisition provenance remain `UNVERIFIED`.

The automatic enemy-action frame is exposed as `enemy_action_chance` with its
typed bounded-index request and eligible indices. JSONL clients resolve it
with `semantic_resolve_enemy_action` and `selected_index`; subsequent enemy
insertions are presented one at a time, then the semantic turn-intent stage
continues.

## Validation scope

The focused tests exercise the real operation kernel through a narrow internal fixture entry with existing combat `RunState`. This is necessary because these two effect kinds are not yet reachable from a standard Silent card in the current isolated starter combat. The test fixture does not claim native-source entry provenance and is not yet replayable from a standard action-only controlled snapshot. Tests cover the two-stage Attack creation/placement, exact pile identities, repeated fixed insertions, invalid indices/stale fingerprints, malformed support, and deterministic 15-worker forks.

Enemy move draw/discard insertions now pass through typed providers and a
serialized source-replay frame. Focused mechanics regressions for this frame,
native cursor parity, random-cost mutations, full-card-pool shuffles, and
nested death/summon hooks remain open. Complete end-to-end controlled
action-to-terminal rollouts and probability calibration remain issue #143
work.

The frozen gameplay `main`, desired RNG and fidelity Inspector specifications, and ordinary gameplay RNG execution are unchanged.

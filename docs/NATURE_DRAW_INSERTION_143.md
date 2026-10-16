# Nature #143 — Controlled random draw-pile insertion

This slice adds two source-shaped chance sequences to the real `PrototypeGameEngine.ResolveOperations` combat-effect kernel, under the opt-in Nature guard:

1. `CreateGeneratedCardsAtRandomDrawPositions`: a **bounded position** ticket, `combat.draw_pile.random_insert_index`, for each fixed-model generated card.
2. `CreateRandomFreeAttacksInDrawPile`: a **source-ordered card-model** ticket, `combat.random_draw_attack.card_model`, followed by a bounded insertion-position ticket for each generated Attack.

The first site permits indices from 0 through the current draw-pile count inclusive, matching the normal `PrototypeRng.NextInt(rng, "combat", draw.Count + 1)` call. The second selects an eligible Attack from the existing ordered `NativeSilentCardPool` filter and then inserts that temporary zero-cost card at Nature's selected index. Card instances and `NextCardInstanceId` are created only at the second stage; partial card-model selection leaves the deck unchanged.

The private pending choice records the exact eligible draw-pile instance IDs, source operation, generated card ID or eligible Attack IDs, remaining repetition count in the saved queued-operation continuation, and source-card/event/card-play continuation identity. On completion, the original queue resumes without re-running earlier effects. Multiple insertions generate successive tickets and are recorded chronologically in the existing `LiveTranscript`, with independent fork outcomes and source-eligibility validation. Nature receives card models in `pending.candidateCardIds` for the model-selection stage. The ordinary fair player observation remains inaccessible during pending Nature decisions; source probability status is `UNVERIFIED`.

## Semantic JSONL/Python fixture bridge

The semantic P0 session can opt into the fixed-card position stage with
`fixture_generated_draw_insertion_card_id="proto.silent.strike"` (or another
source-valid card model). It starts at the existing guarded insertion
continuation and exposes positions from zero through the current draw-pile
count. Resolving a position resumes the source operation and inserts the
known card identity into the semantic deck as a private known anchor, leaving
all unresolved positions in their existing shared pool. Python clients use
`SemanticCombat.resolve_draw_insertion(position)`. This fixture exposes only
the fixed-card path; the random-Attack model-selection path remains available
in the older controlled fixture but is not bridged here. Native law and
fixture acquisition provenance remain `UNVERIFIED`.

## Validation scope

The focused tests exercise the real operation kernel through a narrow internal fixture entry with existing combat `RunState`. This is necessary because these two effect kinds are not yet reachable from a standard Silent card in the current isolated starter combat. The test fixture does not claim native-source entry provenance and is not yet replayable from a standard action-only controlled snapshot. Tests cover the two-stage Attack creation/placement, exact pile identities, repeated fixed insertions, invalid indices/stale fingerprints, malformed support, and deterministic 15-worker forks.

Other random insertions inside enemy move execution (draw/discard), random-cost mutations, full-card-pool shuffles, and nested death/summon hooks need their own resumable engine stages. Complete end-to-end controlled action-to-terminal rollouts and probability calibration remain issue #143 work.

The frozen gameplay `main`, desired RNG and fidelity Inspector specifications, and ordinary gameplay RNG execution are unchanged.

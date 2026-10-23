# Nature #143 — Controlled random draw-pile insertion

This slice adds two source-shaped chance sequences to the real `PrototypeGameEngine.ResolveOperations` combat-effect kernel, under the opt-in Nature guard:

1. `CreateGeneratedCardsAtRandomDrawPositions`: a **bounded position** ticket, `combat.draw_pile.random_insert_index`, for each fixed-model generated card.
2. `CreateRandomFreeAttacksInDrawPile`: a **source-ordered card-model** ticket, `combat.random_draw_attack.card_model`, followed by a bounded insertion-position ticket for each generated Attack.

The first site permits indices from 0 through the current draw-pile count inclusive, matching the normal `PrototypeRng.NextInt(rng, "combat", draw.Count + 1)` call. The second selects an eligible Attack from the existing ordered `NativeSilentCardPool` filter and then inserts that temporary zero-cost card at Nature's selected index. Ordinary Native sampling, Nature suspension, and replay validation now use the same `RandomDrawAttackModelPool` resolver. Card instances and `NextCardInstanceId` are created only at the second stage; partial card-model selection leaves the deck unchanged.

Native combat routes the model draw through `run.combat_card_generation` and
the random position through `run.shuffle`; `CardPileCmd` uses
`RunState.Rng.Shuffle` for `CardPilePosition.Random`. The retained
prototype profile preserves its `combat` stream through the legacy adapter.
The semantic v1 event retains `prototype.combat` for legacy routes and uses
`run.shuffle` when a Native combat stream owner is present. On that Native
request route, the insertion-position Chance Law uses exact `NextInt` grid
masses. Native cursor parity and the full unlocked candidate pool remain
open.

Opt-in Native-policy audits also annotate the supported draw-pile position
requests with the ordered physical pile, inserted card model, selected gap,
and exact Native ticket. This replay evidence does not resolve Native stream
ownership or cursor parity.

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
hook records the inserted position in the native exact semantic deck.
Card-effect draws, standalone conditioned draws, and semantic automatic
hand-draw segments reuse the insertion event and resume their saved frames;
other recycle contexts remain fail-closed where their pipeline continuation
is not serialized. When the saved event carries a Native `run.shuffle` request,
the run-owned `SemanticOtherChanceStateV111` delegates each supported
generated-card, after-shuffle relic, or Personal Hive position through the
exact SiteId/operation mapping. It preserves the event's source owner and
bounded pile count, then resumes the saved frame without a Nature history
choice. Prototype-labeled events retain their existing provider path. The
Native transcript stays private to the engine; Native-game cursor parity
remains open. The semantic pilot and draw/end-turn adapters use one
insertion-event factory and select `run.shuffle` from the same Native
stream-owner check. Enemy-side random draw/discard insertion uses typed
`run.shuffle` sites and now resumes through the saved enemy-action frame; the
run-owned Native provider consumes those registered requests before replay,
without adding a Nature history choice. Native-policy audits retain the
existing ordered player pile, inserted model, selected gap, and exact ticket
at both enemy insertion sites. The same frame delegates registered Native
`run.niche` summon-HP and isolated Tough Egg hatch-HP requests through the
run-owned provider, then resumes the source action. Legacy Fabricator
summon-pool choices remain Nature-owned, Native summon-pool selection remains
fail-closed pending source RNG audit, and nested death/summon random effects
remain separate continuation gaps. Personal Hive Dazed cards
created by powered hits use the saved card-operation continuation; the hit is
committed once, each insertion is resolved in order, the rest of that hit's
hooks resume, and then the remaining effect queue continues.

The private pending choice records the exact eligible draw-pile instance IDs, source operation, generated card ID or eligible Attack IDs, remaining repetition count in the saved queued-operation continuation, and source-card/event/card-play continuation identity. On completion, the original queue resumes without re-running earlier effects. Multiple insertions generate successive tickets and are recorded chronologically in the existing `LiveTranscript`, with independent fork outcomes and source-eligibility validation. Nature receives card models in `pending.candidateCardIds` for the model-selection stage. The ordinary fair player observation remains inaccessible during pending Nature decisions. Native insertion-position transforms are `SOURCE_VERIFIED` on canonical `run.shuffle`; legacy insertion laws remain `UNVERIFIED`. Standard Native events require a source-shaped card/effect owner, while standard legacy events retain the `prototype.combat` owner; relic and Personal Hive events retain their source-specific owner prefixes.

The Personal Hive site `combat.effect.personal_hive.dazed.random_insert_index`
has a direct read-only Native index law through `chance_law_query`. Its caller
declares the source-specific owner, current draw-pile length plus one, and
per-stack ordinal. The same query supports `combat.draw_pile.random_insert_index`,
`combat.relic.after_shuffle.generated_card.random_insert_index`,
`combat.enemy.draw.random_insert_index`, and
`combat.enemy.discard.random_insert_index`, each with caller-declared owner,
current pile-position count, and request ordinal. These queries are conditional
on canonical `run.shuffle`; they do not calibrate stream assignment or inspect
the preceding operation prefix.

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
with `resolve_generated_card(card_model_id)`. The model-selection Chance Law
reports exact Native `NextInt` grid masses and `SOURCE_VERIFIED` on the
declared supported Attack pool; full Native pool eligibility, stream
attribution, seed frequencies, and cursor parity remain open. The Native law
for insertion position is `SOURCE_VERIFIED` when its typed request uses
canonical `run.shuffle`; legacy positions and fixture acquisition provenance
remain `UNVERIFIED`.

The automatic enemy-action frame is exposed as `enemy_action_chance` with its
typed request and eligible indices. JSONL clients resolve it with
`semantic_resolve_enemy_action` and `selected_index`; subsequent enemy
insertions or Hopper theft choices are presented one at a time, then the
semantic turn-intent stage continues. It also handles legacy Fabricator
summon-pool choices and any resulting summoned-enemy HP choices. Native
summon-pool selection still fails closed pending source audit; stream
assignment and pool eligibility remain open. Canonical Native `run.shuffle`
insertion requests receive exact bounded-index `SOURCE_VERIFIED` masses;
legacy insertion requests remain `UNVERIFIED`.

## Validation scope

The focused tests exercise the real operation kernel through a narrow internal fixture entry with existing combat `RunState`. This is necessary because these two effect kinds are not yet reachable from a standard Silent card in the current isolated starter combat. The test fixture does not claim native-source entry provenance and is not yet replayable from a standard action-only controlled snapshot. Tests cover the two-stage Attack creation/placement, exact pile identities, repeated fixed insertions, invalid indices/stale fingerprints, malformed support, and deterministic 15-worker forks.

Enemy move draw/discard insertions and Thieving Hopper's persistent-card
selection now pass through typed providers and the serialized source-replay
frame. Focused mechanics regressions for this frame, native cursor parity,
random-cost mutations, full-card-pool shuffles, and nested death/summon hooks
remain open. Complete end-to-end controlled action-to-terminal rollouts and
probability calibration remain issue #143 work.

The frozen gameplay `main`, desired RNG and fidelity Inspector specifications, and ordinary gameplay RNG execution are unchanged.

## S0 stochastic boundary: generated and enemy draw-pile insertions

The linked rows cover random Attack model selection, generated-card insertion
positions, after-shuffle relic insertions, enemy draw/discard insertions, and
Personal Hive's Dazed insertions. Their private supports are the source-time
ordered model pool or physical draw-pile positions, scoped to the active card,
relic, enemy `CombatId`, or operation repetition. Nature receives candidate
model IDs only at model-selection events; position events expose legal
indices. The ordinary player view does not reveal those supports or cursors.

Each saved frame replays its original effect, enemy action, or automatic
pipeline from the pre-operation checkpoint, applies each choice once, and
resumes the remaining hooks and queue in source order. Unsupported nested
death/summon and recycle contexts remain fail-closed where stated above. When
active, the run-owned Native provider retains each registered Native request
and cursor span in its private mixed transcript; the transcript is not part of
the Nature or ordinary player projection. Their Nature insertion law is
conditional: Native `run.shuffle` positions have source-verified bounded-index
masses, while legacy positions remain `UNVERIFIED`.

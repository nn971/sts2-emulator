# Nature #143: Live Random-Target Continuations

Implementation: [PR #148](https://github.com/nn971/sts2-emulator/pull/148). Parent: dedicated RNG/Inspector integration branch; immutable Silent `main` unaffected.

## New live chance sites

- `combat.effect.random_enemy`: The ordinary `ResolveOperations` card/effect kernel pauses just before its random enemy roll and saves the pending operation, the remaining operation queue, card-play series, source card, event completion, and nested outer choice continuation. The controller chooses a legal enemy by its ordered instance ID. Resumption inserts the target into the saved operation and continues the same kernel; earlier operations and combat side effects remain applied.
- `combat.sly_autoplay.random_enemy`: When a discarded Sly card is targeted at an enemy, the existing ordered discard continuation pauses before selecting a target. Nature selects from living, nonescaped enemy instances; the emulator records the actual `AutoPlayTargetRoll` and resumes the saved Sly auto-play and remaining discard operations.
- `combat.autoplay.random_enemy`: A targeted `AutoPlayCombatCard` operation saves the operation and queued suffix before choosing a living enemy. In a player action, it resumes that operation directly. When a nested choice occurs during an automatic pipeline, the controlled playable fixture and semantic end-turn continuation retain the pipeline cursor and resume it after the target is selected. The semantic pilot also admits Whispering Earring's exact first-turn opening autoplay when its saved cursor is `Complete` and its relic driver matches. None of these routes reruns earlier card effects. The semantic end-turn continuation records target and enemy-action decisions in chronological boundary order so restoration can replay the complete automatic segment.

All supported sites use the versioned `choose_index` request (ordered candidate instance IDs), `run.combat_targets` ownership metadata, an `UNVERIFIED` compatibility status on the stored event, and the existing `controlled_resolve` fingerprint guard. The read-only Chance Law now reports exact Native `NextInt` 53-bit grid masses and `SOURCE_VERIFIED` for the bounded-index transform on that declared support. Source-pool completeness, provider parity, stream attribution, seed frequencies and cursor parity remain separate. The controlled pending choice is entirely chance-side, outside the player's legal action menu. The fair player observation is available again only at a proper player boundary.

Native combat routes all three runtime selections through the shared index
provider using the actual ordered enemy-instance IDs and `run.combat_targets`.
The retained prototype profile preserves its prior streams (`combat_targets`
for effect operations and `combat` for Sly auto-play) through the explicit
legacy adapter. Both the controlled playable fixture and the semantic end-turn
API persist automatic-pipeline targets with their saved cursor. These provider
migrations do not establish native source cursor parity. The opt-in Native
policy audit also reuses the semantic target event and decision to retain each
ordered support and selected enemy identity beside its exact provider ticket
and cursor span; newly marked audits validate complete coverage for the three
registered target sites. This result audit does not establish source
eligibility, stream attribution, or seeded cursor parity.

The persistent semantic combat pilot also admits Volley from its physical
source-card identity. Each paid X hit reuses the existing random-target event;
the guard checks the manual `CardPlayed` frame and the exact remaining sequence
of Volley damage operations. Native target-pool and cursor parity remain open.

Bouncing Flask now enters the same pilot from its physical source-card
identity. Its repeated Poison applications reuse the saved random-target
operation and ordered living-enemy support; each target is committed before
the next repetition resumes. No new Nature decision or chance-event schema is
introduced. Native stream assignment, target-pool fidelity, and cursor parity
remain open.

Knife Trap also enters from its physical source-card identity and queues its
source-ordered Exhaust-pile Shivs through the existing mechanics engine. If a
Shiv kills the selected enemy and a later Shiv needs a target, the saved
`combat.autoplay.random_enemy` continuation chooses from the current living
formation and resumes the remaining autoplay queue. Native target-pool,
stream, and cursor parity remain open.

Tingsha's `CardDiscarded` target now enters from a manual Storm of Steel,
Shadow Step, Survivor, Hidden Daggers, Concentrate, Dagger Throw, Acrobatics,
Prepared, or Calculated Gamble play when the exact relic subscriber,
discarded-card event, source card-play frame, source-card destination, and
saved continuation suffix match. Each discarded card reuses the existing
random-target event and resumes the saved discard-event queue in order. Other
Tingsha hook origins remain unsupported on player-card actions. End-turn
hand-discard targets already use the saved automatic-pipeline target transcript
at its supported cursor. Native target-pool, stream, and cursor parity remain
open.
Each discard from Gambler's Brew and opening Gambling Chip also admits
Tingsha's target through the same event when its exact relic dispatch,
remaining live discard queue, and selected-count draw continuation match.
Pending Sly autoplay remains outside these routes.

## Validation and limitations

The focused C# regression tests cover a real Ricochet card action, a Sly-enabled targeted card discarded during end-turn, stale/illegal decisions, branch separation, private support validation and 15-worker deterministic target choices. The Bouncing Flask pilot admission has static source review only; this increment adds no test or build claim. The existing target tests do not cover the `AutoPlayCombatCard` continuation or semantic end-turn boundary save/restore. This fixture test setup intentionally injects an available card/keyword into the playable combat state; it does not establish native deck/acquisition probabilities. The joint live transcript is replay-validated for source-reachable states.

Unmigrated random targeting inside other callbacks, random costs, card generation, summon/death and nested effects can still fail closed. The bounded-index transform is source-verified on the declared target support; source-pool completeness, Native stream parity and cursor parity remain open. Keep issue #143 open for full site coverage and terminal rollouts. Desired RNG and native Inspector design documents are unchanged.

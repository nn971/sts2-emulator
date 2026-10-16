# Nature #143: Live Random-Target Continuations

Implementation: [PR #148](https://github.com/nn971/sts2-emulator/pull/148). Parent: dedicated RNG/Inspector integration branch; immutable Silent `main` unaffected.

## New live chance sites

- `combat.effect.random_enemy`: The ordinary `ResolveOperations` card/effect kernel pauses just before its random enemy roll and saves the pending operation, the remaining operation queue, card-play series, source card, event completion, and nested outer choice continuation. The controller chooses a legal enemy by its ordered instance ID. Resumption inserts the target into the saved operation and continues the same kernel; earlier operations and combat side effects remain applied.
- `combat.sly_autoplay.random_enemy`: When a discarded Sly card is targeted at an enemy, the existing ordered discard continuation pauses before selecting a target. Nature selects from living, nonescaped enemy instances; the emulator records the actual `AutoPlayTargetRoll` and resumes the saved Sly auto-play and remaining discard operations.
- `combat.autoplay.random_enemy`: A targeted `AutoPlayCombatCard` operation inside a player action saves the operation and its queued suffix before choosing a living enemy. Nature selects an enemy instance, then the engine replays that same auto-play operation with the chosen target. This route does not rerun earlier card effects. Late automatic-pipeline auto-play still fails closed pending an end-turn target continuation.

All supported sites use the versioned `choose_index` request (ordered candidate instance IDs), `run.combat_targets` ownership metadata, `UNVERIFIED` probability status and the existing `controlled_resolve` fingerprint guard. The controlled pending choice is entirely chance-side, outside the player's legal action menu. The fair player observation is available again only at a proper player boundary.

Native combat routes all three runtime selections through the shared index
provider using the actual ordered enemy-instance IDs and `run.combat_targets`.
The retained prototype profile preserves its prior streams (`combat_targets`
for effect operations and `combat` for Sly auto-play) through the explicit
legacy adapter. Player-action auto-play now has a saved controlled
continuation; late automatic-pipeline contexts still need an end-turn
continuation. These provider migrations do not establish native source cursor
parity.

## Validation and limitations

The focused C# regression tests cover a real Ricochet card action, a Sly-enabled targeted card discarded during end-turn, stale/illegal decisions, branch separation, private support validation and 15-worker deterministic target choices. They do not yet cover the new `AutoPlayCombatCard` continuation. This fixture test setup intentionally injects an available card/keyword into the playable combat state; it does not establish native deck/acquisition probabilities. The joint live transcript is replay-validated for source-reachable states.

Unmigrated random targeting inside other callbacks, random cost/card generation, summon/death and nested effects can still fail closed. Native stream parity and original conditional chance laws remain unverified. Keep issue #143 open for full site coverage and terminal rollouts. Desired RNG and native Inspector design documents are unchanged.

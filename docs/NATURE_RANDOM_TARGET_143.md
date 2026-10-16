# Nature #143: Live Random-Target Continuations

Implementation: [PR #148](https://github.com/nn971/sts2-emulator/pull/148). Parent: dedicated RNG/Inspector integration branch; immutable Silent `main` unaffected.

## New live chance sites

- `combat.effect.random_enemy`: The ordinary `ResolveOperations` card/effect kernel pauses just before its random enemy roll and saves the pending operation, the remaining operation queue, card-play series, source card, event completion, and nested outer choice continuation. The controller chooses a legal enemy by its ordered instance ID. Resumption inserts the target into the saved operation and continues the same kernel; earlier operations and combat side effects remain applied.
- `combat.sly_autoplay.random_enemy`: When a discarded Sly card is targeted at an enemy, the existing ordered discard continuation pauses before selecting a target. Nature selects from living, nonescaped enemy instances; the emulator records the actual `AutoPlayTargetRoll` and resumes the saved Sly auto-play and remaining discard operations.

Both sites use the versioned `choose_index` request (ordered candidate instance IDs), `run.combat_targets` ownership metadata, `UNVERIFIED` probability status and the existing `controlled_resolve` fingerprint guard. The controlled pending choice is entirely chance-side, outside the player's legal action menu. The fair player observation is available again only at a proper player boundary.

Native combat now routes these two runtime selections through the shared index
provider using the actual ordered enemy-instance IDs and `run.combat_targets`.
The retained prototype profile preserves its prior streams (`combat_targets`
for effect operations and `combat` for Sly auto-play) through the explicit
legacy adapter. A separate `combat.autoplay.random_enemy` site covers other
automatic card plays; its trigger contexts and controlled continuation remain
unassessed. These provider migrations do not establish native source cursor
parity.

## Validation and limitations

The focused C# regression tests cover a real Ricochet card action, a Sly-enabled targeted card discarded during end-turn, stale/illegal decisions, branch separation, private support validation and 15-worker deterministic target choices. This fixture test setup intentionally injects an available card/keyword into the playable combat state; it does not establish native deck/acquisition probabilities. The joint live transcript is replay-validated for source-reachable states.

Unmigrated random targeting inside other callbacks, random cost/card generation, summon/death and nested effects can still fail closed. Native stream parity and original conditional chance laws remain unverified. Keep issue #143 open for full site coverage and terminal rollouts. Desired RNG and native Inspector design documents are unchanged.

# Autoplay of enemy-targeted cards: target selection

## Observed game rule (user-provided)

When an enemy-targeted **Sly** card is automatically played because it was
discarded, its target is **chosen randomly** by the game, not selected by the
player. The same applies to **other automatic card-play mechanics** when an
automatically played card requires an enemy target. This is distinct from
playing the card manually, where the player explicitly chooses a target.

This note concerns target selection for an *automatically played card*, not
effects intrinsically worded "random enemy" (which already randomize their
own effect targets). Eligible-target filters, exact probabilities, multiple
replays, and no-target edge cases should be checked against the native game
before claiming full fidelity.

## Current implementation gap

As of the branch's initial pin:

- `PrototypeCombat.cs:AutoPlaySlyCard` deliberately throws
  `NotSupportedException` for `PrototypeCardTarget.Enemy`, including
  **Bubble Bubble**, because it assumed native automatic targeting was unknown.
- `PrototypeCombat.cs:ResolveOperations`, at
  `PrototypeCombatEffectKind.AutoPlayCombatCard`, already chooses an
  RNG-driven live enemy **when the operation has no valid explicit target**.
  When it inherits a still-valid target, it currently reuses that target.
  This inherited-target path also needs auditing rather than assuming it
  matches native random-target semantics.
- The generic `RandomEnemy` combat effect uses an RNG draw separately; do
  not conflate these cases.

## Desired behavior / acceptance tests

1. A Sly auto-play of an enemy-targeted card does not require an explicit
   player choice and does not fail solely because the target is omitted.
2. With multiple legal living enemies, targeting is RNG-driven, and the
   chosen enemy is used consistently for the particular auto-play as native
   semantics require. No fixed "first enemy" fallback.
3. With one legal living enemy, the auto-play targets that enemy; dead or
   invalid enemies are never chosen.
4. Preserve emulator replay determinism under the same seed and action path;
   the policy only sees the resolved public effects, never the RNG state.
5. Test both Sly-discard auto-play and other automatic-play routes, including
   target-dependent statuses and damage, and inspect native behavior for
   repeated plays, no targets, and targeting restrictions.
6. Preserve unsupported markers for edge cases not yet investigated.

This is a **behavior specification and backlog item, not a claim that the
Sly implementation has already been corrected**. Do not paper over it by
auto-selecting a player-visible target action or by treating exceptions as
combat defeats.

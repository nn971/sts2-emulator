# Public enemy intent damage and hit counts (isolated AI interface branch)

## Scope and branch isolation

This changes only the public AI observation projection, a shared damage
calculation helper, and tests. It is based on the exact
`sts2-ai`-pinned emulator commit
`3e897c80c4c694a8c4ae75ecdbfb98e6fc15eb56` and is deliberately
**not merged into `main` or any ongoing feature branch**. It does not
modify combat RNG, future move selection, card mechanics, or Act 1 content.

The public `PrototypeAiEnemy` now has two nullable optional fields:

- `intent_damage`: publicly projectable **damage per hit**, after
  modifiers relevant to the currently displayed combat state.
- `intent_hits`: publicly projectable **number of hits**, before
  Block absorbs damage.

When an attack is not public, is not a predictable current move, has mixed
hit values, or is otherwise not representable as a homogeneous
per-hit damage/hit-count pair, both fields are omitted from serialized
`prototype-ai-v0` observations. Consumers must treat missing fields as
unknown, **not zero damage**. A nonattacking move can have a visible
`move_id` with no damage fields.

## Which current moves are knowable?

The underlying prototype selects many random enemy moves when enemies
ACT, *after the player's actions*. There is therefore no public,
precommitted intent for those cases yet, even though the older
observation implementation emitted `Moves[MoveIndex % Moves.Length]`,
which could be **incorrect** for random and state-machine enemies.

The new projection is conservative:

1. Deterministic sequential loops: use the same loop-start logic as combat.
2. Random-after-opener enemies: show only their forced opening moves,
   honoring the actual `OpeningMoveIndex` / `OpeningMoveIndices`.
3. State-machine enemies: resolve deterministic Move and Conditional
   states with the engine's existing formation condition helper.
   Stop at Random states.
4. Hidden intents (e.g. Runic Dome), stunned/skipping or dead enemies:
   emit no move, damage or hit count.
5. Unresolved random current move: emit `move_id: null`, with no numeric
   threat data, rather than claim to know a private RNG outcome.

**Known limitation:** on genuine native STS2 runs the player sees a chosen
intent even for random-policy enemies. Full parity requires changing the
prototype combat engine to select and store that intent when the player's
turn begins (plus validating the corresponding RNG consumption and state
machine timing). This branch deliberately **does not** do that; it keeps
combat behavior and pinned training runs reproducible. Until that
separate fidelity project, it would be incorrect to claim that all
announced intents have been completely implemented.

## Damage derivation

The actual combat damage calculation and intent projection now share
`PrototypeGameEngine.EnemyHitDamage`. It uses the same act/ascension
damage deltas, per-enemy power bonuses (Strength), enemy statuses
(Weak), player powers (Vulnerable), and incoming damage caps. It does
not deduct player Block or predict how a player might alter their
block before the end of turn.

Only `DamagePlayer` effects marked `IsAttack` are currently projected.
Mixed unequal attacks or moves which self-buff Strength before/among
attacks are conservatively not projected, rather than reporting
misleading per-hit numbers. Ordinary multi-hit attacks such as
`proto.enemy.assassin` Flurry are represented as damage and repetitions.
Move selection and RNG are not invoked during observation.

This is a **prototype engine-consistent projection**, not a verified
equivalence to the native game UI. Native-player HUD parity remains
a separate evidence/test obligation (see emulator issue #45).

## Integration

The `sts2-ai` relational tactical v5 encoder reads optional
`intent_damage` and `intent_hits` from each public enemy frame.
Its four reserved threat-value slots stay zero when no visible intent
number is present.

No change is required to the AI model format or the legal action API.
Old `PrototypeAiEnemy` constructors still compile because the two new
fields are optional at the end. The observation hash changes where
actual new numbers or corrected visible move IDs are returned. This is
why running it against existing trained checkpoint/baseline studies
requires an explicit emulator revision update and fresh paired evaluation.

## Verification

`tests/Sts2Emulator.Core.Tests/PrototypePublicEnemyIntentTests.cs`
checks deterministic single- and multi-hit attacks, enemy Strength,
player Vulnerable, ascension damage deltas, enemy Weak, nonattacking
state-machine moves, random unresolved intents, Runic Dome and
observation/RNG stability. The tests compare projection with actual
combat damage in deterministic cases.

This branch is **ready for review**, not an automatic change to the
production or on-going development line.

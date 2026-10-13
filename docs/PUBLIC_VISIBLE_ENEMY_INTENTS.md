# Committed enemy intents and player-visible attack numbers

## Isolation and implementation status

This branch is based on `sts2-ai`'s pinned emulator revision
`3e897c80c4c694a8c4ae75ecdbfb98e6fc15eb56`. Neither the
emulator's older `main` branch nor its ongoing feature branches are
changed. The isolated draft review target
`review-base/pinned-ai-emulator-20261008` points to that commit and
has no substantive changes itself.

## Move-selection lifecycle

**The enemy's move is selected before the player acts.** In particular:

1. At combat entry, once the combat-start event has resolved, the engine
   calls `CommitEnemyIntents` and stores each enemy's
   `PlannedMoveIndex` and `PlannedNextAiStateId` in canonical combat
   state. This includes random-policy and state-machine monsters.
2. During the player's turn, their next move does **not** change as
   cards are played. The public observation reads the committed move
   index only; it never selects a new move or advances RNG.
3. At the enemy-action stage, the engine executes the stored move,
   updates LastMoveId, consecutive uses, AI-state continuation and move
   use counters, and clears the consumed commitment.
4. Once the enemy turn's end-status effects, deaths, and summons
   complete, the next move is committed at the AdvanceTurn step,
   *before* the next player turn begins.

Random draws for choosing moves are moved from enemy execution to
the pre-player-turn commitment boundaries. This deliberately changes
the order of the combat RNG stream compared with earlier emulator
revisions. It is **not** an assumption about a native RNG seed, and
native-game fidelity should be rechecked against a reference trace.

Skipped/dead enemies do not receive a visible move. Fresh summons that
skip a turn do not claim an attack they cannot execute. A compatibility
fallback in enemy execution is retained solely for legacy or manually
constructed states that lack a commitment; correctly initialized seeded
runs select every move before the player makes a decision.

## Base damage, modified damage, and hit count

The AI projection returns an enemy's committed `move_id` and, when a
uniform damaging attack can be displayed faithfully, the following
**optional** JSON values:

- `intent_base_damage`: base **per-hit** damage from the move definition,
  including current act/ascension scaling, excluding Strength/Weak/
  Vulnerable/damage caps.
- `intent_damage`: currently modified **per-hit** damage, accounting
  for enemy powers/statuses and player powers, using the actual combat
  engine's damage calculation.
- `intent_hits`: number of hits for the announced attack.

The move ID and repetition count are tied to the committed attack.
`intent_damage` is recomputed on **each public observation**; the
player's Weak/Vulnerable/other modifiers can change after a card is
played. This projected attack damage is **before player Block**:
existing player Block is separately visible, and additional Block can
be acquired mid-turn. `intent_base_damage` is stable under buffs and
debuffs but will reflect act/ascension differences between combats.

A heterogeneous or context-sensitive sequence of hits cannot be
faithfully represented by one damage scalar, so its numeric values
are omitted. Nonattack damage and self-buffs that alter the attack
before a later hit are likewise withheld. A known non-attacking move
still has a `move_id` but omits attack numerics.

When a relic such as Runic Dome conceals intents, `move_id` and all
three attack fields are null/omitted. The private committed move
remains in canonical engine state because combat execution must know
which move was selected, but is **never** projected to the player.

## Contracts and compatibility

The public JSON is additive (`prototype-ai-v0`). The three numeric
fields are nullable and excluded from JSON entirely when unavailable.
Previously constructed `PrototypeAiEnemy` objects remain compatible
through optional record constructor parameters.

Because seeded enemy move selection now occurs earlier, old trace
hashes and seed-to-outcome expectations may change. Updating the
`sts2-ai` emulator submodule to this branch should be a separate
deliberate integration step. The AI v5 relational feature encoder
already consumes `intent_damage` and `intent_hits`; it can be extended
later to consume `intent_base_damage` explicitly.

## Verification

`PrototypePublicEnemyIntentTests` and the existing emulator suite
exercise commit timing, guaranteed execution of a committed random move,
RNG and observation stability, multi-hit damage, ascension, dynamic
Strength/Weak/Vulnerable changes, nonattacks and Runic Dome. Tests
compare reported damage against actual combat execution.

This branch is for isolated implementation and review only, not for
automatic merge into unrelated emulator feature work.

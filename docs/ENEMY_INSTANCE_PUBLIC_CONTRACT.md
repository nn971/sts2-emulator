# Public enemy-instance identity contract (v3)

The combat engine creates each enemy in an encounter with a distinct positive
`EnemyCombatState.InstanceId` (including two enemies of the same species).
A monotone `CombatState.NextEnemyInstanceId` allocator is initialized with
the encounter and advanced by regular and death-triggered summons; no combat
instance ID is deliberately reused.

The public JSONL observation has one `combat.enemies[]` record per instance.
Each record contains:

- `instance_id` — stable combat-local key (never a learned numeric feature)
- `enemy_id` — **species**, used to learn the distribution of future intents
- `formation_position` — public visible battlefield ordering
- `hp`, `block`, `move_id` and optionally homogeneous attack
  `intent_damage`, `intent_hits`, `intent_base_damage`
- `last_move_id` — most recently observed executed move, if any. This
  is a record of player-observable history, NOT the hidden AI automaton state
- `statuses` and `powers[].stacks`, owned by this exact enemy

All targeted legal actions use `TargetEnemyId` referring to the same
`instance_id`. Player-visible identity, species, formation and history are
stable input to the AI; the internal `AiStateId`, future move RNG,
hidden conditional transitions and draw-pile order are never projected.

The new fields are additive. The emulator remains free to continue later
acts after Act 1 completion; the early-terminal training goal is in sts2-ai.

Regression coverage: `PrototypeAiEnvironmentTests` ensures two same-species
enemies remain separate with separate statuses and prior moves, legal targeted
actions point at their IDs, and new combats reserve an unused summon ID.

```fish
dotnet test tests/Sts2Emulator.Core.Tests \
    --filter FullyQualifiedName~PrototypeAiEnvironmentTests
```

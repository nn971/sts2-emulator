# Public combat observation: unordered draw pile and relic counters

The opt-in `agent/public-observations-v2` branch extends the existing JSONL
player-visible observation without changing the canonical engine's RNG, draw
ordering, or replay state.

## New fields

- `combat.draw_pile` contains the actual remaining card instances, including
  card IDs, upgrade levels, temporary flags, card-local state, and afflictions.
  These are **sorted by stable card instance ID**, never by the engine's hidden
  draw-pile order. Duplicates remain distinct card instances.
- `combat.draw_pile_count` remains for compatibility and equals the length
  of the exposed multiset.
- `combat.relic_counters` carries a relic identity and its ordered per-trigger
  progress counts from `CombatRelicState.TriggerCounts`. Indices correspond to
  the existing relic trigger definition order. It is separate from each
  `relics[].state` persistent state.
- `combat.player_powers[].stacks`, `combat.enemies[].powers[].stacks`,
  and `combat.enemies[].statuses` already expose public strength/counts.

This projection must not be used to infer a draw-pile permutation or hidden
RNG stream. If we encounter a relic trigger whose progress is genuinely
hidden from the player, its counter needs an explicit visibility gate before
that relic is included in the training pool.

## Regression checks

```fish
dotnet test tests/Sts2Emulator.Core.Tests \
    --filter FullyQualifiedName~PrototypeAiEnvironmentTests
```

The tests check that permuting the exact draw array does not change the
public observation, while changing a relic counter *does* change its hash.

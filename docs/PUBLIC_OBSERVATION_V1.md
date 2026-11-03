# Public AI observation v1 — first H18 repair slice

Issue: [#237](https://github.com/nn971/sts2-emulator/issues/237).
Base: `research/h18-generated-compact-selection-20261103`.

## Compatibility

`PrototypeAiEnvironment.SchemaId` is `prototype-ai-v1` for the enriched
public projection. The `RunState`/native snapshot format is unchanged.
Previously published observation fields retain their names and shapes;
new optional card/combat members append to existing records.

**Training gate:** H16/H18 checkpoint encoders remain on their previous
codec versions until the AI-side v1 parser and typed relational encoder are
tested. Do not silently treat v0 and v1 observations as identical.

## Exported fields and visibility

| Field | Visibility | Source | Meaning |
| --- | --- | --- | --- |
| `deck[].enchantment` | Public inventory | `CardInstance.Enchantment` | Current persistent card enchantment, including amount/disabled |
| `combat.hand/discard_pile/exhaust_pile/play_pile/selection_candidates[].enchantment` | Visible cards/candidate dialogs | `CombatCardInstance.Enchantment` | Enchantment of that concrete visible card |
| `combat.*[].affliction` | Visible cards/candidates | `CombatCardInstance.Affliction` | Card-local affliction, already exported |
| `combat.*[].combat_energy_cost_delta` | Visible card-local cost modifier | `CombatCardInstance.CombatEnergyCostDelta` | Delta, **not** final effective cost |
| `combat.*[].temporary_energy_cost` | Visible card-local cost override | `CombatCardInstance.TemporaryEnergyCost` | Current override and expiry |
| `combat.*[].keyword_overrides` | Visible card keyword state | `CombatCardInstance.KeywordOverrides` | Applied keyword overrides and expiry |
| `combat.*[].enchantment_triggered_this_combat` | Publicly reconstructible activation | `CombatCardInstance.EnchantmentTriggeredThisCombat` | Known use state for revealed card |
| `combat.history` | Publicly reconstructible played-card history | `CombatState.CounterState` | Cards/attacks/skills played this turn; attacks last turn; cards this combat; manually played this turn |
| `potion_slot_capacity` | Public inventory | `PlayerState.PotionSlots.Length` | Maximum number of inventory positions |
| `potion_slots_occupied` | Public inventory | Non-null potion slots | Filled positions |
| `potion_slots_empty` | Public inventory | Null potion slots | Empty positions |
| `potions[].slot` | Public inventory | Potion slot index | Retains noncontiguous inventory positions (existing field) |

`combat.*[]` above means the visible hand, discard, exhaust, play piles
and, only when the compact research selection is active, pending selection
candidates/selected cards. The draw pile still exposes **count only**;
unseen card instances and their modifiers must not be inspected.

For existing public records, `relics[].state`,
`relics[].is_wax`, `relics[].is_melted`, `potions[].state`, and
`deck[].state` remain owned by the relevant resource record. The AI
codec must read `state`, not `persistent_state`.

## Still outstanding from the roadmap

- Calculate and expose the **actual current effective cost** using the same
  action-eligibility mechanics as the engine, with dynamic contexts covered.
  The delta and override added in this slice are not a substitute.
- Add full cross-phase field-path coverage fixtures and machine-readable
  omitted/visible classifications (including nested reward/shop/event offers).
- Match the v1 schema in the AI parser, validate missing versus zero, and
  owner-link persistent attributes.
- Add complete relational map/action representation, visibility contracts
  for every action payload, vocabulary/collision audits, and checkpoint
  compatibility gates.
- Later, separately migrate Poison/Weak/Vulnerable into canonical powers
  with differential mechanics and replay parity tests.

The new tests in `PrototypeAiEnvironmentTests` exercise public inventory
capacity, modifier distinctions and combat-history projection. Native
parity and integrated AI consumption still need independent validation.

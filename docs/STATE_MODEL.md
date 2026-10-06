# Canonical state model

## Representations and purposes

The emulator distinguishes:

### Exact engine state `S`

Everything required to continue the run exactly, including hidden RNG state and hidden history. This is the canonical semantic state of the emulator.

### Player-visible projection `O(S)`

A versioned projection containing information exposed to a normal player under a declared information policy. This is provided for downstream consumers; it does not define engine semantics.

### Consumer representations

Tensor, token, graph, database, or search representations belong outside the emulator and are derived from `S` or a projection. The canonical simulator must never be designed around a particular learned/search representation.

## Proposed top-level run state

```text
RunState
├── Version / provenance
├── Seed / run identity
├── Phase + decision index
├── Player
│   ├── HP / max HP
│   ├── gold
│   ├── character/run modifiers
│   ├── deck (persistent card instances)
│   ├── relics + mutable counters
│   └── potion slots + potions
├── RNG bundle
├── Map / route state
├── Pools / history / eligibility state
├── Current room
│   ├── CombatState
│   ├── RewardState
│   ├── ShopState
│   ├── EventState
│   ├── RestState
│   └── other phase-specific state
└── Extension state for audited mechanics not yet promoted to typed fields
```

The scaffold intentionally contains only a minimal typed subset. New fields should be promoted when their semantics are understood.

## Canonical identity

Exact/transposition identity should use a canonical semantic fingerprint, not raw object serialization. The current scaffold provides deterministic JSON canonicalization as a bootstrap tool.

Long-term, consider separate hashes:

- `ExactStateHash`: all hidden continuation-relevant state;
- `ObservationHash`: player-visible state only;
- `EquivalenceHash`: an optional proven coarser equivalence used only where safe.

Never collapse states because they “look equivalent” until the equivalence is justified.

## Card instances

Cards should be treated as persistent instances when the game can attach state to individual cards. A canonical card record should be able to represent:

- stable instance identity within a run;
- base card type;
- upgrade level;
- enchantments/modifiers;
- mutable persistent fields;
- combat-local copy identity where needed.

## Hidden histories

A major source of long-horizon divergence is state that is invisible on the current screen but affects future generation. Examples may include encounter/event histories, pools, probabilities/counters, and eligibility flags.

These belong in `S` whenever they affect `T(S,a)`.

## Extension state

Early reverse-engineering often discovers fields before their long-term model is clear. The scaffold permits canonical extension fields so we can capture truth without immediately forcing a final type hierarchy.

Extension fields must still be:

- named;
- versioned;
- canonicalized;
- included/excluded from public projections deliberately;
- promoted to typed fields once stable.

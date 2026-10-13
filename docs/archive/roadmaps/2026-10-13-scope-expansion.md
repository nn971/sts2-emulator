# Scope expansion after the Silent / Overgrowth foundation (October 2026)

## Decision

**Proceed with game-content breadth as the main development track.**
Fidelity auditing, exact native RNG and throughput tuning remain separate
regression tracks rather than prerequisites for every new card or encounter.
The first whole-run kernel now has typed cards, powers, event hooks,
suspendable choices, card instance identity, encounter policies, map routing,
rewards, shops, relics, potions, forkable state and terminal transitions.
This is sufficient to expand without redesigning the entire engine.

**Scope:** single-player Silent first. Do not import multiplayer-only
mechanics, and do not change the `sts2-ai` repository. Preserve legacy
prototype resets and versioned consumer contracts while optional native-shaped
run modes evolve.

## Content expansion order

| Phase | Next deliverable | Why / compatibility gate |
| --- | --- | --- |
| 1. Shared colorless cards | Extend the 65-card pinned catalog; implement eligible single-player cards, beginning with Finesse, Flash of Steel, Dramatic Entrance, Ultimate Strike, Ultimate Defend, Master of Strategy. Native merchant has Uncommon + Rare dedicated colorless slots. | Small incremental use of the *existing* effect kernel; gives new strategic deck archetypes without adding a region |
| 2. Act 1 Underdocks | Add a real opt-in `Underdocks` region factory and encounter pools; build its 20 encounters and region events incrementally, without substituting Overgrowth-specific content. | Shares Act 1 map shape and 3 weak fights with Overgrowth; exercises region-specific enemy/formation mechanics |
| 3. Act 2 Hive | Replace legacy Act 2 room fixtures with a native-shaped 14-room Hive act, 20 encounters, 2 weak fights, 3 bosses, events and act-entry Ancient choices. | Extends world routing across a real act boundary and introduces new mid-run scaling mechanics |
| 4. Act 3 Glory | Replace legacy Act 3 fixtures with a native-shaped 13-room Glory act, 18 encounters, 2 weak fights, 3 bosses, events and act-entry Ancients. | Enables complete three-act Silent native-shaped runs |
| 5. Remainder of shared content | Unimplemented colorless cards, relic/potion/event gaps, status/affliction mechanisms, cross-act content and other characters only after Silent is broadly supported. | Keeps the mechanics kernel generalized and the corpus manageable |

**Native pinned reference**: `Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20`,
`src/Core/Models/Acts/{Overgrowth,Underdocks,Hive,Glory}.cs`,
`src/Core/Models/CardPools/ColorlessCardPool.cs`, and
`src/Core/Entities/Merchant/MerchantInventory.cs`.

The current `PrototypeNativeOvergrowthRunFactory` is an Act 1 opening and
map implementation; normal prototype Act 2/3 transitions are already possible
but still carry largely **prototype room/encounter content**, not native Hive/
Glory completeness. Region and act choice must be stored as forkable world
state, not used as global configuration. Boss/encounter/event pools should be
version-pinned and reject unsupported slots, rather than silently filling
with enemies from another act.

## Colorless merchant vertical slice

The pinned merchant rolls five character-card offers followed by **two separate
colorless slots** (`Uncommon`, then `Rare`), each selected from its own pool
without shared-card repeats. Its colorless card base price is the standard
rarity price with a 15% premium; the on-sale card is selected only from the five
character-card slots.

The first batch implements **six** cards using existing combat effect
primitives. The native 65-card pool membership is catalogued in exact source
order, but the remaining cards are NOT considered implemented and are NOT
silently admitted to generation. The limited Rare pool currently has only
Master of Strategy; diversity will increase as more Rare mechanics are added.
Only the opt-in native-shaped Overgrowth merchant gains the two new offers.
The ordinary Silent combat-reward pool stays untouched; legacy six-floor
prototype merchants keep their earlier five-card shape. There is no attempt
to claim full native shop RNG consumption or meta-progression unlock gates.

## Expansion acceptance gate (lightweight)

For each new content family:

1. **Representability**: mechanics use typed effects/hooks/continuations; no
   unbounded or silent placeholder action.
2. **Reachability**: content appears only in the appropriate run, shop, event,
   encounter, or reward pool.
3. **Determinism**: a fixed seed/action sequence replays exactly under the
   emulator's explicitly versioned stochastic rules; forks are independent.
4. **Playable legality**: generated actions remain legal, including nested
   selections and depleted pools; unsupported models are excluded from
   generation, not converted into a no-op.
5. **Whole-run viability**: automated random/legal-action sweeps progress to
   a terminal state without crashes, including act transitions.
6. **Semantic evidence**: source-backed mechanics and focused tests; native
   oracle differentials recorded as a separate fidelity level and repaired
   when strategically significant.

More detailed L1–L4 fidelity checks remain tracked in
`docs/reference-builds/v0.111.0-native-rng-fidelity-matrix.md`.

The broadening strategy is **horizontal integration followed by vertical
correction**: first make a playable new mechanic available in a complete run,
then improve the exact probabilities and seeded semantics as needed.

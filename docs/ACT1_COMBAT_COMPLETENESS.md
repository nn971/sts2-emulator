# Act 1 combat completeness

This document inventories both native Act 1 regions, but the active completion target is now
**Overgrowth**. Underdocks remains tracked below so that its work can resume later without losing
the dependency map.

## Reference boundary

- Native oracle: STS2 v0.111.0 / 41cef1ea.
- Structured inventory source: `ptrlrd/spire-codex@b59c6f96043051afee8683237d04f943309ced35`.
- Pinned blobs: `encounters.json` d948d8b6bcbb6b664fe0bd65526310dd4960f1b0; `monsters.json` 5fe2f086b44266fc41e56169f26d13c577ca966d.
- Inventory scope: both first-act regions, **Overgrowth** and **Underdocks**.
- Active implementation scope: **Overgrowth only**; Underdocks is deferred.
- Event-only combats are excluded from this milestone unless they reuse a required Act 1 monster mechanic.

The structured corpus contains **42 Act 1 combat encounters** and **51 distinct monster models**: 22 Overgrowth encounters and 20 Underdocks encounters.

An encounter is not complete merely because all of its monster classes exist. Completion requires independently:

1. source-backed enemy values and move effects;
2. exact move-selection/state-machine behavior;
3. required powers/status/summon/death mechanics;
4. Ascension breakpoints;
5. exact encounter formation/slot semantics;
6. native encounter-pool eligibility/selection;
7. at least one oracle differential for mechanics that cannot be established from static source alone.

## Current foundation

- generic fixed-prefix → constrained-random move policy (used by Spectral Knight and reusable in Act 1);
- source-shaped move/random/conditional AI state graphs with weighted branches, typed repeat rules, and formation predicates;
- per-move consecutive-use limits plus total move-use accounting;
- enemy self-powers and flat Strength attack scaling;
- timed player Vulnerable / Weak / Frail with attack/card-Block multipliers;
- typed Ascension value deltas;
- native enemy HP ranges (added for Act 1);
- source-backed Overgrowth models currently promoted in this ledger include **Twig Slime (S)**,
  **Assassin Raider**, **Snapping Jaxfruit**, **Mawler**, **Leaf Slime (S)**, **Leaf Slime (M)**,
  **Twig Slime (M)**, **Nibbit**, **Fuzzy Wurm Crawler**, and **Shrinker Beetle**;
- typed Act-1 region state plus a forkable Overgrowth weak-encounter queue.

## Encounter coverage

Legend: `model` = all listed enemy models implemented; `formation` = exact multiplicity/slots/composition; `pool` = native encounter-selection semantics; `oracle` = live differential evidence.

| Region | Room | Encounter | Native monsters | Model | Formation | Pool | Oracle |
|---|---|---|---|---|---|---|---|
| Overgrowth | Boss | `CEREMONIAL_BEAST_BOSS` | `CEREMONIAL_BEAST` | no | pending | pending | no |
| Overgrowth | Boss | `THE_KIN_BOSS` | `KIN_FOLLOWER`, `KIN_PRIEST` | no | pending | pending | no |
| Overgrowth | Boss | `VANTOM_BOSS` | `VANTOM` | no | pending | pending | no |
| Overgrowth | Elite | `BYGONE_EFFIGY_ELITE` | `BYGONE_EFFIGY` | no | pending | pending | no |
| Overgrowth | Elite | `BYRDONIS_ELITE` | `BYRDONIS` | no | pending | pending | no |
| Overgrowth | Elite | `PHROG_PARASITE_ELITE` | `PHROG_PARASITE`, `WRIGGLER` | no | pending | pending | no |
| Overgrowth | Monster | `CUBEX_CONSTRUCT_NORMAL` | `CUBEX_CONSTRUCT` | yes | yes | pending | no |
| Overgrowth | Monster | `FLYCONID_NORMAL` | `FLYCONID`, `LEAF_SLIME_M`, `TWIG_SLIME_M` | yes | yes | pending | no |
| Overgrowth | Monster | `FOGMOG_NORMAL` | `EYE_WITH_TEETH`, `FOGMOG` | no | pending | pending | no |
| Overgrowth | Monster weak | `FUZZY_WURM_CRAWLER_WEAK` | `FUZZY_WURM_CRAWLER` | yes | yes | yes | no |
| Overgrowth | Monster | `INKLETS_NORMAL` | `INKLET` | yes | yes | pending | no |
| Overgrowth | Monster | `MAWLER_NORMAL` | `MAWLER` | yes | yes | pending | no |
| Overgrowth | Monster | `NIBBITS_NORMAL` | `NIBBIT` | yes | yes | pending | no |
| Overgrowth | Monster weak | `NIBBITS_WEAK` | `NIBBIT` | yes | yes | yes | no |
| Overgrowth | Monster | `OVERGROWTH_CRAWLERS` | `FUZZY_WURM_CRAWLER`, `SHRINKER_BEETLE` | yes | yes | pending | no |
| Overgrowth | Monster | `RUBY_RAIDERS_NORMAL` | `ASSASSIN_RUBY_RAIDER`, `AXE_RUBY_RAIDER`, `BRUTE_RUBY_RAIDER`, `CROSSBOW_RUBY_RAIDER`, `TRACKER_RUBY_RAIDER` | yes | yes | pending | no |
| Overgrowth | Monster weak | `SHRINKER_BEETLE_WEAK` | `SHRINKER_BEETLE` | yes | yes | yes | no |
| Overgrowth | Monster | `SLIMES_NORMAL` | `LEAF_SLIME_M`, `LEAF_SLIME_S`, `TWIG_SLIME_M`, `TWIG_SLIME_S` | yes | yes | pending | no |
| Overgrowth | Monster weak | `SLIMES_WEAK` | `LEAF_SLIME_M`, `LEAF_SLIME_S`, `TWIG_SLIME_M`, `TWIG_SLIME_S` | yes | yes | yes | no |
| Overgrowth | Monster | `SLITHERING_STRANGLER_NORMAL` | `LEAF_SLIME_M`, `LEAF_SLIME_S`, `SLITHERING_STRANGLER`, `SNAPPING_JAXFRUIT`, `TWIG_SLIME_M`, `TWIG_SLIME_S` | no | pending | pending | no |
| Overgrowth | Monster | `SNAPPING_JAXFRUIT_NORMAL` | `FLYCONID`, `SNAPPING_JAXFRUIT` | yes | yes | pending | no |
| Overgrowth | Monster | `VINE_SHAMBLER_NORMAL` | `VINE_SHAMBLER` | no | pending | pending | no |
| Underdocks | Boss | `LAGAVULIN_MATRIARCH_BOSS` | `LAGAVULIN_MATRIARCH` | no | pending | pending | no |
| Underdocks | Boss | `SOUL_FYSH_BOSS` | `SOUL_FYSH` | no | pending | pending | no |
| Underdocks | Boss | `WATERFALL_GIANT_BOSS` | `WATERFALL_GIANT` | no | pending | pending | no |
| Underdocks | Elite | `PHANTASMAL_GARDENERS_ELITE` | `PHANTASMAL_GARDENER` | no | pending | pending | no |
| Underdocks | Elite | `SKULKING_COLONY_ELITE` | `SKULKING_COLONY` | no | pending | pending | no |
| Underdocks | Elite | `TERROR_EEL_ELITE` | `TERROR_EEL` | no | pending | pending | no |
| Underdocks | Monster | `CORPSE_SLUGS_NORMAL` | `CORPSE_SLUG` | no | pending | pending | no |
| Underdocks | Monster weak | `CORPSE_SLUGS_WEAK` | `CORPSE_SLUG` | no | pending | pending | no |
| Underdocks | Monster | `CULTISTS_NORMAL` | `CALCIFIED_CULTIST`, `DAMP_CULTIST` | no | pending | pending | no |
| Underdocks | Monster | `FOSSIL_STALKER_NORMAL` | `FOSSIL_STALKER` | no | pending | pending | no |
| Underdocks | Monster | `GREMLIN_MERC_NORMAL` | `FAT_GREMLIN`, `GREMLIN_MERC`, `SNEAKY_GREMLIN` | no | pending | pending | no |
| Underdocks | Monster | `HAUNTED_SHIP_NORMAL` | `HAUNTED_SHIP` | no | pending | pending | no |
| Underdocks | Monster | `LIVING_FOG_NORMAL` | `GAS_BOMB`, `LIVING_FOG` | no | pending | pending | no |
| Underdocks | Monster | `PUNCH_CONSTRUCT_NORMAL` | `PUNCH_CONSTRUCT` | no | pending | pending | no |
| Underdocks | Monster | `SEAPUNK_NORMAL` | `CALCIFIED_CULTIST`, `SEAPUNK` | no | pending | pending | no |
| Underdocks | Monster weak | `SEAPUNK_WEAK` | `SEAPUNK` | partial/yes* | pending | pending | no |
| Underdocks | Monster | `SEWER_CLAM_NORMAL` | `SEWER_CLAM` | no | pending | pending | no |
| Underdocks | Monster weak | `SLUDGE_SPINNER_WEAK` | `SLUDGE_SPINNER` | no | pending | pending | no |
| Underdocks | Monster weak | `TOADPOLES_WEAK` | `TOADPOLE` | no | pending | pending | no |
| Underdocks | Monster | `TWO_TAILED_RATS_NORMAL` | `TWO_TAILED_RAT` | no | pending | pending | no |

\* Underdocks rows are retained as deferred inventory. The four Overgrowth weak encounters now
participate in a typed native-shape queue: the first three ordinary combats select three distinct
encounters uniformly from the four-entry weak pool. Exact native RNG codec/call-order parity is not
claimed. `SLIMES_NORMAL`, `NIBBITS_NORMAL`, and `OVERGROWTH_CRAWLERS` have source-backed
formations but still await the normal-pool translation.

## Monster-model coverage

| Monster | Type | HP A0 | HP asc. | Pattern | Status |
|---|---|---:|---:|---|---|
| `ASSASSIN_RUBY_RAIDER` — Assassin Raider | Normal | 18–23 | 19–24 | cycle | implemented + focused tests |
| `AXE_RUBY_RAIDER` — Axe Raider | Normal | 20–22 | 21–23 | cycle | implemented + focused tests |
| `BRUTE_RUBY_RAIDER` — Brute Raider | Normal | 30–33 | 31–34 | cycle | implemented + focused tests |
| `BYGONE_EFFIGY` — Bygone Effigy | Elite | 127 | 132 | cycle | not started |
| `BYRDONIS` — Byrdonis | Elite | 81–84 | 90–90 | cycle | not started |
| `CALCIFIED_CULTIST` — Calcified Cultist | Normal | 38–41 | 39–42 | cycle | not started |
| `CEREMONIAL_BEAST` — Ceremonial Beast | Boss | 252 | 262 | cycle | not started |
| `CORPSE_SLUG` — Corpse Slug | Normal | 25–27 | 27–29 | cycle | not started |
| `CROSSBOW_RUBY_RAIDER` — Crossbow Raider | Normal | 18–21 | 19–22 | cycle | implemented + focused tests |
| `CUBEX_CONSTRUCT` — Cubex Construct | Normal | 65 | 70 | cycle | implemented + focused tests |
| `DAMP_CULTIST` — Damp Cultist | Normal | 51–53 | 52–54 | cycle | not started |
| `EYE_WITH_TEETH` — Eye with Teeth | Normal | 6 | — | cycle | not started |
| `FAT_GREMLIN` — Fat Gremlin | Normal | 13–17 | 14–18 | cycle | not started |
| `FLYCONID` — Flyconid | Normal | 47–49 | 51–53 | random | implemented + focused tests |
| `FOGMOG` — Fogmog | Normal | 74 | 78 | random | not started |
| `FOSSIL_STALKER` — Fossil Stalker | Normal | 51–53 | 54–56 | random | not started |
| `FUZZY_WURM_CRAWLER` — Fuzzy Wurm Crawler | Normal | 55–57 | 58–59 | cycle | implemented + focused tests |
| `GAS_BOMB` — Gas Bomb | Normal | 7 | 8 | cycle | not started |
| `GREMLIN_MERC` — Gremlin Merc | Normal | 47–49 | 51–53 | cycle | not started |
| `HAUNTED_SHIP` — Haunted Ship | Normal | 63 | 67 | cycle | not started |
| `INKLET` — Inklet | Normal | 11–17 | 12–18 | random | implemented + focused tests |
| `KIN_FOLLOWER` — Kin Follower | Boss | 58–59 | 62–63 | cycle | not started |
| `KIN_PRIEST` — Kin Priest | Boss | 190 | 199 | cycle | not started |
| `LAGAVULIN_MATRIARCH` — Lagavulin Matriarch | Boss | 222 | 233 | conditional | not started |
| `LEAF_SLIME_M` — Leaf Slime (M) | Normal | 32–35 | 33–36 | cycle | implemented + focused tests |
| `LEAF_SLIME_S` — Leaf Slime (S) | Normal | 11–15 | 12–16 | random | implemented + focused tests |
| `LIVING_FOG` — Living Fog | Normal | 80 | 82 | cycle | not started |
| `MAWLER` — Mawler | Normal | 72 | 76 | random | implemented + focused tests |
| `NIBBIT` — Nibbit | Normal | 42–46 | 44–48 | conditional | implemented + focused tests |
| `PHANTASMAL_GARDENER` — Phantasmal Gardener | Elite | 26–31 | 27–32 | conditional | not started |
| `PHROG_PARASITE` — Phrog Parasite | Elite | 61–64 | 66–68 | random | not started |
| `PUNCH_CONSTRUCT` — Punch Construct | Normal | 55 | 60 | cycle | not started |
| `SEAPUNK` — Seapunk | Normal | 44–46 | 47–49 | cycle | implemented + focused tests |
| `SEWER_CLAM` — Sewer Clam | Normal | 56 | 58 | cycle | not started |
| `SHRINKER_BEETLE` — Shrinker Beetle | Normal | 38–40 | 40–42 | cycle | implemented + focused tests |
| `SKULKING_COLONY` — Skulking Colony | Elite | 75 | 80 | cycle | not started |
| `SLITHERING_STRANGLER` — Slithering Strangler | Normal | 53–55 | 54–56 | random | not started |
| `SLUDGE_SPINNER` — Sludge Spinner | Normal | 37–39 | 41–42 | random | not started |
| `SNAPPING_JAXFRUIT` — Snapping Jaxfruit | Normal | 31–33 | 34–36 | cycle | implemented + focused tests |
| `SNEAKY_GREMLIN` — Sneaky Gremlin | Normal | 10–14 | 11–15 | cycle | not started |
| `SOUL_FYSH` — Soul Fysh | Boss | 211 | 221 | cycle | not started |
| `TERROR_EEL` — Terror Eel | Elite | 140 | 150 | cycle | not started |
| `TOADPOLE` — Toadpole | Normal | 21–25 | 22–26 | conditional | implemented + focused tests |
| `TRACKER_RUBY_RAIDER` — Tracker Raider | Normal | 21–25 | 22–26 | cycle | implemented + focused tests |
| `TWIG_SLIME_M` — Twig Slime (M) | Normal | 26–28 | 27–29 | random | implemented + focused tests |
| `TWIG_SLIME_S` — Twig Slime (S) | Normal | 7–11 | 8–12 | cycle | implemented + focused tests |
| `TWO_TAILED_RAT` — Two-Tailed Rat | Normal | 17–21 | 18–22 | random | not started |
| `VANTOM` — Vantom | Boss | 173 | 183 | cycle | not started |
| `VINE_SHAMBLER` — Vine Shambler | Normal | 61 | 64 | cycle | not started |
| `WATERFALL_GIANT` — Waterfall Giant | Boss | 240 | 250 | cycle | not started |
| `WRIGGLER` — Wriggler | Elite | 17–21 | 18–22 | conditional | not started |

## Implementation order

Work by reusable mechanic dependency rather than encounter list order:

1. **Simple deterministic enemies** — fixed cycles/always-one-move enemies using damage, Block, Strength and already-supported debuffs. These validate HP ranges and basic Ascension scaling cheaply.
2. **Constrained random enemies** — use the typed source-shaped AI graph for native weighted branches and repeat rules; confirm any ambiguous native API semantics before production use, and use oracle traces selectively without claiming RNG codec parity.
3. **Formation/slot-dependent enemies** — Nibbits, Toadpoles, slime groups, Ruby Raiders and other encounters whose opening state depends on slot/composition.
4. **Summon/lifecycle enemies** — rats, parasites/wrigglers, fog/gas-bomb style interactions, and any enemy whose semantics need spawn/despawn/death hooks.
5. **Elites**, then **bosses** — only after their shared primitives exist; boss-specific phase transitions should not be generalized prematurely.
6. **Native Act 1 encounter pools** — replace prototype placeholder selection only after individual encounter formations are reliable.
7. **Act 1 differential sweep** — use Loadout/native harnesses to sample representative deterministic, random, conditional, elite and boss fights at A0/A10; then run deterministic emulator sweeps over the completed Act 1 environment.

## Promotion criterion

The milestone is complete when every row above has source-backed model + formation + Ascension semantics, the native weak/normal/elite/boss pools are translated, and the oracle suite contains enough representative captures to detect move-order, power-timing, summon/death and random-policy regressions. Full card/relic/event completeness is explicitly not required.

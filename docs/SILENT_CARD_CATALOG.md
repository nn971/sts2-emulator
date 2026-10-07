# Silent card catalog — pinned v0.111.0

This file tracks **inventory completeness** separately from **mechanics completeness**.

The source of truth for membership is the pinned v0.111.0
`SilentCardPool.GenerateAllCards()` list. It contains 91 cards. The emulator keeps that exact
inventory in `PrototypeContent.NativeSilentCardPool`.

- **implemented** means the prototype currently permits the card to enter the single-player
  simulation when otherwise eligible. This label describes the engine support flag; individual
  mechanics still remain subject to oracle/differential fidelity work.
- **catalog-only** means source-backed identity/cost/type/rarity/keyword metadata is present while
  `MechanicsImplemented` is false. These cards cannot be played or sampled into single-player
  rewards yet.
- **multiplayer-only** mirrors the native card constraint and keeps the card out of single-player
  rewards.

| # | Card | Engine support | Native constraint |
|---:|---|---|---|
| 1 | Abrasive | implemented |  |
| 2 | Accelerant | implemented |  |
| 3 | Accuracy | implemented |  |
| 4 | Acrobatics | implemented |  |
| 5 | Adrenaline | implemented |  |
| 6 | Afterimage | implemented |  |
| 7 | Anticipate | implemented |  |
| 8 | Assassinate | implemented |  |
| 9 | Backflip | implemented |  |
| 10 | Backstab | implemented |  |
| 11 | Blade of Ink | catalog-only |  |
| 12 | Blade Dance | implemented |  |
| 13 | Blade Symphony | catalog-only | multiplayer-only |
| 14 | Blur | implemented |  |
| 15 | Bouncing Flask | implemented |  |
| 16 | Bubble Bubble | implemented |  |
| 17 | Bullet Time | implemented |  |
| 18 | Burst | implemented |  |
| 19 | Calculated Gamble | implemented |  |
| 20 | Cloak and Dagger | implemented |  |
| 21 | Corrosive Wave | implemented |  |
| 22 | Concoct | catalog-only | multiplayer-only |
| 23 | Dagger Spray | implemented |  |
| 24 | Dagger Throw | implemented |  |
| 25 | Dash | implemented |  |
| 26 | Deadly Poison | implemented |  |
| 27 | Defend | implemented |  |
| 28 | Deflect | implemented |  |
| 29 | Dodge and Roll | implemented |  |
| 30 | Echoing Slash | catalog-only |  |
| 31 | Envenom | implemented |  |
| 32 | Escape Plan | implemented |  |
| 33 | Expertise | implemented |  |
| 34 | Expose | implemented |  |
| 35 | Fade | catalog-only | multiplayer-only |
| 36 | Fan of Knives | catalog-only |  |
| 37 | Finisher | implemented |  |
| 38 | Flanking | catalog-only | multiplayer-only |
| 39 | Flechettes | implemented |  |
| 40 | Flick-Flack | implemented |  |
| 41 | Sidestep | implemented |  |
| 42 | Footwork | implemented |  |
| 43 | Grand Finale | implemented |  |
| 44 | Hand Trick | implemented |  |
| 45 | Haze | implemented |  |
| 46 | Hidden Daggers | implemented |  |
| 47 | Infinite Blades | implemented |  |
| 48 | Knife Trap | catalog-only |  |
| 49 | Leading Strike | implemented |  |
| 50 | Leg Sweep | implemented |  |
| 51 | Malaise | implemented |  |
| 52 | Master Planner | implemented |  |
| 53 | Memento Mori | implemented |  |
| 54 | Mirage | implemented |  |
| 55 | Murder | implemented |  |
| 56 | Neutralize | implemented |  |
| 57 | Nightmare | implemented |  |
| 58 | Noxious Fumes | implemented |  |
| 59 | Outbreak | implemented |  |
| 60 | Phantom Blades | implemented |  |
| 61 | Piercing Wail | implemented |  |
| 62 | Pinpoint | implemented |  |
| 63 | Poisoned Stab | implemented |  |
| 64 | Pounce | implemented |  |
| 65 | Precise Cut | implemented |  |
| 66 | Predator | implemented |  |
| 67 | Prepared | implemented |  |
| 68 | Reflex | implemented |  |
| 69 | Ricochet | implemented |  |
| 70 | Serpent Form | implemented |  |
| 71 | Shadow Step | implemented |  |
| 72 | Shadowmeld | implemented |  |
| 73 | Skewer | implemented |  |
| 74 | Slice | implemented |  |
| 75 | Snakebite | implemented |  |
| 76 | Sneaky | catalog-only | multiplayer-only |
| 77 | Speedster | implemented |  |
| 78 | Storm of Steel | implemented |  |
| 79 | Strangle | implemented |  |
| 80 | Strike | implemented |  |
| 81 | Sucker Punch | implemented |  |
| 82 | Suppress | implemented |  |
| 83 | Survivor | implemented |  |
| 84 | Tactician | implemented |  |
| 85 | The Hunt | catalog-only |  |
| 86 | Tools of the Trade | implemented |  |
| 87 | Tracking | implemented |  |
| 88 | Untouchable | implemented |  |
| 89 | Up My Sleeve | implemented |  |
| 90 | Well-Laid Plans | catalog-only |  |
| 91 | Wraith Form | implemented |  |

## Catalog-only implementation queue

The 11 source-backed catalog-only cards are:

- Blade of Ink
- Blade Symphony (multiplayer-only)
- Concoct (multiplayer-only)
- Echoing Slash
- Fade (multiplayer-only)
- Fan of Knives
- Flanking (multiplayer-only)
- Knife Trap
- Sneaky (multiplayer-only)
- The Hunt
- Well-Laid Plans

A useful implementation order is to start with cards expressible through existing generic
primitives (for example Strangle, Outbreak, Calculated Gamble), then add mechanics that create
new reusable engine concepts (Fatal rewards, Intangible, teammate targeting, enchantment creation,
and discard/exhaust replay).

## Compatibility definitions outside the pinned pool

The prototype still carries Quick Slash, Concentrate, Catalyst, Crippling Cloud, and Die Die Die
for existing focused regressions. They are deliberately absent from
`NativeSilentCardPool` and therefore cannot appear in native single-player card rewards.

Shiv is a generated token and is likewise outside the 91-card native pool.

## Reference pin

- Native build: v0.111.0
- Project oracle pin: 41cef1ea
- Source snapshot used for the inventory: `Flaaax/STS2-source` commit
  `706ef1c9fa2219220065849fd5265328607ece20` ("Add 0.111.0 source snapshot")

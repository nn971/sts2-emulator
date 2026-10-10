# v111 implementation status

Target: [FULL_IMPLEMENTATION_ROADMAP.md](FULL_IMPLEMENTATION_ROADMAP.md).
Pin: v0.111.0 / 41cef1ea and the fingerprint in
[the reference manifest](../data/reference_builds/v0.111.0-41cef1ea.json).
This is an intermediate implementation. Final release gates remain open.

## Delivered

- Imported the existing `prototype/full-run-silent` implementation: all 20
  ordinary Underdocks encounters, ten regional event models, all 53 eligible
  colorless cards, and committed public enemy intents. Event models do not
  establish exhaustive branch or native parity coverage.
- Compiled an identifier-only inventory from both pinned reference caches:
  1,555 eligible entries and 58 multiplayer-only exclusions. All 90 encounters
  include ten special/event encounters outside ordinary act pools. Cards have
  base/upgrade variant IDs; events have known page/option/Ancient IDs. Dynamic
  event branch completeness is explicitly false and requires source auditing.
- Added reproducible coverage by native ID with separate registration,
  implementation, integration and validation dimensions. Metadata registration
  never establishes implementation. Claims derive from typed executable
  registries; all native validation remains unverified. Reports retain gaps.
- Added fork-owned character, A0–A10, profile, route, ruleset, fingerprint and
  fidelity configuration. All five starting definitions and persistent resource
  types are present. Execution initializes Silent only with a fully unlocked
  profile. Native A0 decks omit the legacy Restlessness training addition.
- Added a standalone 15-stream native RNG codec: xxHash64, historical seed
  compatibility, SplitMix64, xoshiro256**, native bounded sampling, float
  rounding, Gaussian sampling and shuffle. Independent vectors and fork/restore
  tests pass. **The engine still uses six prototype streams**; context ownership,
  call order and outcome pool order remain to be integrated.
- Added exact snapshots, ordered deterministic batches, a Python binding and
  canonical replay. Replay checks legal actions, state and RNG, and reports
  the first divergent JSON path. Synthetic tests do not establish native parity.
- Implemented all ten Trash Heap cards, including Clash, Distraction, Dual Wield,
  Entrench, Hello World, Rebound, Rip and Tear and Stack, plus Hand Drill with
  base/upgrade, identity, choice, generation timing, targeting and Block
  tests. Underdocks shares native-shaped Act 1 shops, rewards, treasures and
  unknown-room resolution. Recovered stolen gold has its own reward group.
  Neow continuations preserve pre-map history. Trash Heap preserves duplicate
  relic rolls as separate instances; duplicate Dream Catchers offer independently
  selectable rest rewards using the shared rarity lifecycle, and fresh Maw Banks
  retain independent purchase state.
- Corrected v111 Endless Conveyor continuation ordering: the next dish is
  rolled after Jelly Liver deck transformation or Suspicious Condiment potion
  reward resolution. The visible dish ID/number are now exported to consumers;
  a pending roll remains fork- and snapshot-safe. Punch Off also consumes its
  pinned event-entry dynamic-gold roll (91–98), even when that value is unused
  by its options. The event RNG still uses the prototype codec, so native
  stream parity remains unverified.
- Exposed only already-visible v111 event economics and fixed item rewards in
  optional AI event-choice projections: independent chest rolls, Statue/Conveyor
  outcomes, cost/HP trades, escalation in Abyssal Baths and source-known
  curses, potions and relics. Internal Punch Off RNG rolls stay private.
  Suspended card/potion prompts hide the parent page's option metadata.
- Updated Sword of Stone and Fishing Rod victory callbacks to advance **each**
  owned relic instance separately. Two rods can upgrade two eligible cards
  after their third ordinary combat, while each Sword has its own five-elite
  transformation counter. Fishing Rod still uses prototype event RNG instead
  of the native Niche stream.
- Extended structural validation to the Underdocks multi-page event state
  machine, source-range persisted Treasury/Statue/Punch Off rolls, deferred
  Conveyor selections and native potion-offer slots. This narrows invalid
  states admitted by synthetic snapshots and fixes valid special-event states
  rejected by legacy single-page assumptions.
- Source-audited lethal event actions: pinned solo Overgrowth/Underdocks
  choices are legal even when their HP loss is fatal (native options warn but
  do not lock). Damage-first paths now stop immediately on defeat without
  yielding deferred rewards; Sunken Statue's source-ordered gold-before-damage
  path still credits its gold. Legacy prototype-only event safety gating stays
  unchanged. Regression tests cover Maze, Statue and delayed Whispering Hollow.
- Extended source-backed public event option projections across Overgrowth:
  Dense Vegetation's rolled gold and rest heal, Byrdonis Nest fixed Egg/HP,
  Morphic Grove's entire current gold sacrifice, Sapphire Seed heal,
  Unrest Site's current healing and fixed curse/max-HP sacrifice, Wellspring's
  curse, Luminous Choir's card, and Wood Carvings' fixed transformations.
  All projections derive from visible state and consume no random draws.
- Fixed three source-pinned direct event relic pickups: both variants of
  Sunken Statue grant a fresh Sword of Stone even when already owned, and
  Drowning Beacon grants a second Fresnel Lens. Each keeps independent
  persistent state and stacks its ordinary effects. Random rewards, shops
  and relic grab bags retain their single-copy eligibility restrictions.
- Acquisition-only run callbacks now execute for the **newly acquired relic
  instance**, not all owned copies of the same ID. Verified with duplicate
  Old Coin and Mango; continuous RestSiteHealed triggers still execute for
  every owned Stone Humidifier. This follows native RelicModel.AfterObtained.
- Event UpgradeRandomCard and UpgradeAllCards now use the typed
  upgradability predicate (maximum upgrade level and card state) instead of
  assuming any upgrade-level-zero card is eligible. Added Doors of Light
  and Dark regression with a non-upgradable Injury in the deck. Native
  StableShuffle RNG call-order for Doors remains unverified.
- Added CI pin/inventory/coverage baseline checks and real-server Python
  snapshot/batch/compatibility checks. Added configured decision-boundary
  benchmarks for forks, allocations, stepping, batches, hashing, snapshots and
  retained branch memory; whole-run benchmarks explicitly use the prototype.

## Coverage interpretation

Run `dotnet src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll v111-coverage`.
`--missing` lists eligible implementation/integration/validation gaps;
`--check` exits 1 while release coverage is incomplete. That is expected at
this milestone, rather than a regression failure.

The reviewed report is [v111-baseline.json](../data/coverage/v111-baseline.json).
`implemented` for a card means its typed mechanics flag is true; `integrated`
for ordinary cards/encounters means actual supported pool membership. Event
and generated acquisition paths require separate evidence and may remain
conservatively missing. Other categories remain partial until completeness
evidence is expressed; zero counts do not mean no potion/relic/event behavior.

Scope excludes 37 cards, one relic, one rest option and 19 powers whose audited
content-owner reference graph reaches only excluded content. Re-audit that
inference if reflection/dynamic acquisition is added. No proprietary text,
assets, assemblies or source are bundled. Top-level identifier mapping is
complete; exhaustive variant and event branch enumeration is not.

Reproduce with local pinned caches:

```sh
python tools/build_v111_inventory.py /path/to/pinned/eng /path/to/pinned/source --check
python tools/check_v111_coverage.py
```

After reviewing intentional changes, regenerate the inventory and run
`python tools/check_v111_coverage.py --update`; commit the reviewed baseline
diff. Native parity claims require a real reference fixture ledger.

## Remaining gates

1. Exhaustive event branches, Ancient options, acquisition paths, profiles and
   evidence-ledger coverage for every variant.
2. Remaining Act 1 mechanics and native validation; exhaustive event outcomes
   and reward modifiers remain unaudited.
3. Native Hive/Glory encounters, maps, events, Ancients, transfers and terminal
   rules. Configured transitions reject unrelated prototype acts.
4. Ironclad, Defect, Regent and Necrobinder execution/content; all remaining
   shared/generated cards, modifications, potions and relics.
5. Native RNG ownership/context generators, capture normalization and complete
   cross-character/act/difficulty replay. The codec alone does not provide parity.
6. At least 1,000 pinned complete/terminal real-game runs without unexplained
   divergence, plus rare-content fixtures and performance budgets.

The workspace has neither the pinned game installation nor a real-game trace
corpus. Source-backed implementation and synthetic validation can proceed,
but those native release gates cannot be certified from the available files.

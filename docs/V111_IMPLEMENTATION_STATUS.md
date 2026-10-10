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
  Neow continuations preserve pre-map history.
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
2. Remaining Act 1 mechanics and native validation. Duplicate Trash Heap relic
   acquisition still fails explicitly; exhaustive event outcomes remain unaudited.
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

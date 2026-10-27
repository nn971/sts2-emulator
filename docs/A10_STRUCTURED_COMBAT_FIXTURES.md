# Research A10 Silent combat fixtures — consolidated #181

**Status (2026-10-27):** Implementation branch for the first engine-owned,
single-enemy structured research contract. This is opt-in; the older H2/H3
preset fixtures remain available. This contract is for simulated research
encounters. It is **not** an assertion of native Slay the Spire 2 encounter or
deck-reward frequencies.

## Contract

`ResearchCombatFixtureV111` is passed as `fixture_spec` to Python
`Session.begin_seeded_semantic_combat(...)`; the CLI deserializes the
snake_case JSON field into `SemanticSeededEntryV111.FixtureSpec`.

The full research spec is checked **before** resolving entry HP, enemy intent,
shuffle, or drawing. A valid spec is materialized through the existing run
player state into physical cards, persistent card instance IDs, relics and
potions before the combat registry and opening hooks are constructed.
Snapshot/replay retains the fixture in its private opening provenance.
The externally observed state remains the existing fair `observe()` payload.
Game actions, Chance Law and Nature interfaces are unchanged.

A seed is separate from the spec: identical spec, different seed means
different physical source outcomes from the declared Chance Law; identical
spec and seed replay identically. `SpecHash` hashes the complete structured
spec and is intentionally separate from the `SemanticPilotCombatV111`
opening fingerprint. Neither the spec nor hidden physical draw order is
included in the player's public observation.

## Research distribution v1

- `silent-a10-structured-combat-v1`; generator inputs: integer index and
  curriculum seed (the Python H8 mirror defaults to `20261027`).
- Act 1, Silent, Ascension 10; one source-supported enemy:
  Shrinker Beetle, Nibbits, Mawler, Cubex Construct, Vine Shambler, Ceremonial
  Beast, or Vantom.
- Source-pinned prototype entry nodes: floor 2/map row 1 for weak/normal;
  floor 6/map row 5 for bosses (prototype, **not** native floor-16 geometry);
  map column 3.
- Physical deck: 12 starting Silent cards plus eight entries from Acrobatics,
  Backflip, Slice, Poisoned Stab, Deadly Poison and Catalyst. Additional cards
  may have an upgrade level of 0 or 1; each upgrade is checked against its
  registered card definition. A canonical sorted multiset, **not** card order
  or shuffled outcomes, determines structural identity.
- Relics: Silent Ring, plus two different supported relics selected from
  Lantern, Vajra, Ornamental Fan and Orichalcum. Relic counters are owned by
  the engine.
- Potion slots: two; optional Block, Fire or Poison Potion in slot zero.
- Starting HP: 45/55/65/70, MaxHP 70. HP is a *starting condition* and is
  validated before chance sampling; no mid-combat HP injection.
- The generator rejects duplicate structured specs when building
  `GenerateDistinct(count)`; the first milestone is 200 independent
  structural configurations and an expanded 1,000-spec census.

Whitelists are explicitly conservative. Multi-enemy formations, relic state
with complex mutable native counters, unsanctioned potion models, arbitrary
source decks, and non-Silent characters are unsupported in v1. Expanding
coverage requires mechanic tests and a fair public enemy-position bridge.

## Acceptance and evidence

`ResearchCombatFixtureV111Tests` checks a 1,000-spec deterministic census,
rejection of unsupported cards/upgrades/relics/potions/health/floor,
independence of spec from source RNG seeds, and two real semantic openings
with persistent deck IDs, upgrades, relics, health, and saved snapshot replay.

Build/test locally after checking out the isolated research branch:

```sh
dotnet test tests/Sts2Emulator.Core.Tests --filter FullyQualifiedName~ResearchCombatFixtureV111Tests
dotnet build src/Sts2Emulator.Cli -c Release
```

The follow-up acceptance must run 100, then 1,000 complete fixture
*execution* smokes across 15 independent workers, including terminal
combat/replay and reference-law draw/intent checks. A 1,000-spec validation
census is **not** a 1,000-combat execution result. Keep that distinction in
CI reports and PR descriptions.

Native fidelity, the installed-game Inspector, exact RNG parity and
`UNVERIFIED` native calibration are not agent-training gates.
The active Nature comparator branch and inspector remain unchanged.

**Cross-repo consumer:** sts2-ai H8 structured curriculum, using the H7
shared Transformer reference-only PPO. The same spec index can later be
reused to collect combat outcome targets without changing the spec or
introducing hidden information to the player.

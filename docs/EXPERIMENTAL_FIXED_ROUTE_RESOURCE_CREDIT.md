# Fixed single-route Overgrowth for delayed-resource-credit pilots

**Status:** opt-in experimental map, separate from native RNG/map fidelity.
**Implementation:** `ExperimentalFixedRouteOvergrowthMapV111`.

## Purpose

Run Silent A10 training cohorts with exactly the same visible Act 1 route across seeds.
Vary player decisions and Nature outcomes rather than generated path topology.
This is for the two-combat/future-HP experiment and later joint combat/macro
continuation learning. It is *not* a new native STS2 Act 1 map claim.

## Route

This hand-authored research route takes the single-path topology of Golden
Compass as a reference. Its room sequence is explicitly specified for
the resource-credit pilot (version 2):

| Floor | Room |
|---:|---|
| 1 | Combat |
| 2 | Combat |
| 3 | Unknown |
| 4 | Shop |
| 5 | Combat |
| 6 | Rest |
| 7 | Elite |
| 8 | Treasure |
| 9 | Unknown |
| 10 | Combat |
| 11 | Rest |
| 12 | Combat |
| 13 | Elite |
| 14 | Unknown |
| 15 | Rest |
| 16 | Boss |

Exactly one column (3), one entry, and one edge from each room to the next.
The boss floor (16) matches the **existing emulator native-structure
Overgrowth profile**, which has 15 map-room rows plus boss. This profile
does not assert parity with the installed game's actual native map length.
The Golden Path original has 17 total map nodes, but this training
sequence is separately authored rather than mechanically shortened from it.

Unknown rooms retain the existing Overgrowth resolution mechanics and
stochastic odds, so their resolved room kinds may differ between Nature
realizations. This deliberately fixes *map topology and declared room
types*, not every subsequent stochastic room result. The actual Golden
Compass has special Unknown-room semantics, which are not transferred to
this training map.

## How to opt in

JSONL:

```json
{"op":"reset_native_overgrowth","request_id":"fixed-1","seed":"credit-pilot-1","ascension":10,"map_profile_id":"experimental-act1-fixed-route-credit-v2"}
```

Python binding:

```python
state = emulator.reset_native_overgrowth(
    "credit-pilot-1", ascension=10,
    map_profile_id="experimental-act1-fixed-route-credit-v2")
```

Omit `map_profile_id` to retain the original native-structure Overgrowth map.
Unsupported profile IDs fail before storing a new state.

The reset first performs the original native-structure initialization,
including Neow, player resources and RNG construction, and overwrites only
`World.Map` before the first map entry. The map's `GenerationProfileId`
records its experimental provenance. No Golden Compass relic is granted.

## Experiment discipline

- Fix this map across cohorts, varying independent player/Nature seeds.
- Continue mechanics and stochastic outcomes normally. Keep public
  observations, legal actions, and full post-combat states.
- Compare short-horizon HP, delayed HP after future combats, and resource
  diversity using independent held-out Nature realizations.
- Do not infer map-routing quality from this profile: there is only one route.
- Evaluate generalization on ordinary generated maps separately.
- Do not label these synthetic maps native-fidelity references or use them in
  inspector parity tests.

## Validation

Focused tests: geometry and room sequence, exactly one edge per floor,
default-route preservation, unchanged non-map startup/RNG state,
deterministic map across seeds, first combat entry, and invalid override
rejection.

Run locally:

```fish
dotnet test tests/Sts2Emulator.Core.Tests -c Release --filter FullyQualifiedName~ExperimentalFixedRouteOvergrowthMapV111Tests
```

The branch has not been compiled or executed by the remote GitHub editing
session. Confirm these focused tests before using it in training.

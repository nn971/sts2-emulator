> **Historical alternative design from draft PR #125.** Its earlier HP boundary API differs from the canonical #124→#126 implementation. Integrated history preserves this design note, but the unified branch uses the #126 serializable HP+intent continuation. Features unique to the alternative must be ported as explicit tests/guards, not by replacing the newer API.

# D3 controlled Nature continuation — initial monster HP

**Status:** implemented isolated **D3 vertical slice** (2026-10-13), stacked on
[the D1/D2 provider branch](DESIRED_RNG_MIGRATION_STATUS.md). Not a playable
Nature-controlled run; no public JSONL/Python protocol yet.

## Completed

`ControlledInitialHpV111.Begin(bareCombat, formation)` suspends **before**
the source-authentic `run.niche` HP selection for the first monster. The
returned `ControlledInitialHpContinuationV111` is the actual continuation
state and contains:

- `Combat` — fully cloned battle state with all HP results **already
  applied** for earlier monsters, and no falsely native-seeded RNG sidecar
- `Formation` — cloned source encounter/monster RNG ownership, unchanged
- `NextEnemyIndex` — next source-order initial HP operation
- `Pending` — typed source/owner/candidate request and SHA256-derived
  boundary ID bound to current exact partial state/formation/transcript
- `Transcript` — validated selected indices with controlled provenance
- `SchemaId`, `ProviderMode` — explicit compatibility and counterfactual
  mode tags

Call `Resolve(state, pending.BoundaryId, selectedIndex)`. The resolver
first validates the entire prior transcript and the pending request, checks
the boundary ID and candidate index, then **forks** the current partial
combat, applies this monster's source-shaped HP mutation and constructs
the *next* choice using the updated list of assigned HP maxima. There is
no rewind, no replay of an earlier monster, no guessed native RNG cursor and
no RNG draw when Nature selects the outcome. Reusing an old boundary ID or
an invalid index is rejected before any change.

`Save(state)` serializes the complete continuation using .NET JSON;
`Restore(json)` verifies schema, owner/stream identity, previous choices,
observed HP, candidate pool, and boundary identity before returning an
independent fork. `state.Fork()` clones mutable arrays and game state, so
branches may choose different legal HP results independently.

The two modes share the **same source-authentic mechanical eligibility and
effect helpers** in `NativeCombatRngEntryV111`:

- inclusive monster HP range in pinned creation order
- exclude previous HP maxima when eligible
- restore full range when exclusion empties it
- assign chosen HP to current and maximum HP

Native mode continues consuming one `NextInt` per monster, including
singleton pools. D3 controlled mode emits exactly one boundary per monster
and does not advance any native cursor.

## Example from an already prepared native combat-entry fixture

```csharp
var state = ControlledInitialHpV111.Begin(bareCombat, nativeFormation);
while (!state.Complete)
{
    var pending = state.Pending!;
    int natureIndex = ChooseSupportedIndex(pending.Request); // external policy
    state = ControlledInitialHpV111.Resolve(
        state, pending.BoundaryId, natureIndex);
}
CombatState completedHpSlice = state.Combat; // still NOT a playable native combat
```

`ChooseSupportedIndex` is illustrative and intentionally NOT a method
implemented by the emulator. The sole external input is an integer
`0 <= index < pending.Request.SupportCount`.

## Verification

`ControlledInitialHpV111Tests` exercises dozens of seeded encounter
fixtures and supplies the **same selected indices** from native audit to
the controlled resumer. The final combat states (excluding provider-specific
native cursor fields) must have identical canonical hashes. Other tests
check:

- save/restore at each pending boundary, preserving the canonical hash
- no mutation of the original initial combat or its formation
- independent forks with different valid Nature outcomes and different
  subsequent eligibility
- rejection of stale/duplicate boundary IDs, wrong owners/candidates, and
  out-of-range indices
- explicit refusal to treat an already native-attached combat as controlled

```fish
dotnet test Sts2Emulator.sln -c Release --filter FullyQualifiedName~ControlledInitialHpV111Tests
```

These are **emulator-internal native/controlled equivalence tests**; no
native-game Inspector trace is consumed in this slice.

## Still required for full D3/D4

- A persisted resumable **enemy-intent state machine**, which can encounter
  multiple random calls separated by conditional states and weighted branches
- Suspension/continuation across normal combat turns, hook cascades, delayed
  card effects, and nested player-owned choices
- Integrating provider mode/pending chance into the versioned
  `RunState` / engine `Step` contract, preserving canonical snapshots
- A capability-negotiated JSONL/Python API for Nature decisions and
  player-visible information-policy constraints
- Full source-authentic native parity, seeded cursor verification against
  the real game, and separately calibrated conditional outcome laws

**Gate:** Never convert this controlled result to `NativeCombatRngV111`
or allow unimplemented gameplay steps to silently fall back to native or
prototype RNG. Successful HP-source equivalence alone is **not** playable
Nature or full D3 completion.

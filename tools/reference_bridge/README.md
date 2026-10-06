# Native reference bridge workspace

This project is intentionally **not** part of `Sts2Emulator.sln`.

It is compiled only against the exact assemblies shipped by the pinned STS2 reference build. CI
does not contain those proprietary assemblies.

## Pinned target

The current bridge target is:

- STS2 `v0.111.0`
- commit `41cef1ea`
- target framework `net9.0`
- build fingerprint
  `3bb5598a35f7763c9de22078643ac2777190994b2be85ebd4ebf5c9d154aa45e`

See `../../data/reference_builds/v0.111.0-41cef1ea.json`.

The recorder verifies the exact hashes of `sts2.dll`, `0Harmony.dll`,
`GodotSharp.dll`, `release_info.json`, and `sts2.runtimeconfig.json` at startup. If any file
differs, it refuses to record.

## Current recorder

The first recorder is passive:

- enters through the game's `[ModInitializer]` loader surface;
- does **not** Harmony-patch combat methods;
- subscribes reflectively to `CombatManager` lifecycle events;
- reads `CombatManager.DebugOnlyGetState()`;
- snapshots run/player RNG through their native `ToSerializable()` methods;
- writes JSONL under `<instrumented-game>/reference_traces/`;
- optionally subscribes to `CombatStateTracker.CombatStateChanged` when
  `STS2_REFERENCE_VERBOSE=1`.

Default recorded boundaries currently include combat setup/start, turn start/end, player end-turn,
switch-to-enemy, combat victory, and combat end.

`sts2-reference-probe-v0` has now been live-validated on two Silent A0 combats. The current bridge
emits **`sts2-reference-probe-v1`**, adding passive `CombatHistory.Changed` records, player-creature
discovery, field-level native RNG serialization, and one-time runtime type catalogs. It is still a
probe rather than parity-complete `reference-trace-v0.2` until those additions are live-validated.

## Preserve the clean oracle

Do not install this mod into the clean oracle copy.

Build **against** the oracle, but stage the mod separately:

```fish
fish tools/reference_bridge/stage.fish "/path/to/clean/oracle/game"
```

By default this creates:

```text
artifacts/reference_bridge_mod/Sts2ReferenceBridge/
  Sts2ReferenceBridge.dll
  Sts2ReferenceBridge.json
```

The staging script does not modify the oracle.

Copy that folder into an expendable/instrumented game copy:

```fish
cp -r artifacts/reference_bridge_mod/Sts2ReferenceBridge \
    "/path/to/instrumented/game/mods/"
```

Then launch the instrumented copy with mods enabled and play one ordinary Silent combat. The v1 probe should appear
under:

```text
<instrumented-game>/reference_traces/
```

## Verbose state-change capture

The default lifecycle stream is intentionally small. For a diagnostic run, set:

```fish
set -x STS2_REFERENCE_VERBOSE 1
```

before launching the game from the same environment. This additionally records
`CombatStateTracker.CombatStateChanged` and can produce a much larger trace.

## Re-audit after updates

For any different installed build:

```fish
fish scripts/reference_audit.fish "/path/to/game" > reference-audit.txt 2>&1
```

Do not update the pinned constants merely because the displayed version is similar. Treat a new
fingerprint as a separate oracle version and inspect its semantic surfaces first.

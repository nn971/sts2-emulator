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

`sts2-reference-probe-v0` was live-validated on two Silent A0 combats, and the later action-boundary
capture validated the v1 history/player/RNG additions. The current bridge emits
**`sts2-reference-probe-v4`**.

Relative to those earlier probes, v4 remains passive and additive. It also:

- catalogs enemy Creature/Model runtime layouts;
- opportunistically projects current/next move, intent, move index, last move and move history;
- enriches compact card projection with Affliction, Enchantment/modifier and additional Upgrade
  fields when the pinned runtime exposes them.

These fields are aimed at Knight Gang differential validation. Missing reflective properties are
simply absent from the JSON rather than inferred. The stream remains a probe rather than a
parity-complete `reference-trace-v0.2`.

### Action-frame and duplicate-card observation

The bridge now also attaches optional `recorder_card_id` values to card models
in both card piles and CombatHistory records. Each ID follows **reference
equality of the native card object within a single combat**, letting a later
analyzer distinguish two otherwise identical Strikes or other duplicate cards.
The IDs reset at CombatSetUp and are released at combat end. This is a read-only
observer identity, not a persistent card instance ID from the game.

After playing with the updated instrumented mod, extract action frames:

```fish
python tools/native_overgrowth_action_frames.py \
  '/path/to/overgrowth-silent-act1-capture.jsonl' \
  --combats 5 6 9 --output /tmp/native-action-frames.json
```

Older v4 traces without the new IDs remain supported and are labeled
`card_class_only`. Callback-time player observations are *not* synchronized
pre/post-decision checkpoints; the extractor's field names and diagnostics make
that distinction explicit. See
[`docs/reference-builds/v0.111.0-native-combat-action-frames.md`](../../docs/reference-builds/v0.111.0-native-combat-action-frames.md).

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

Then launch the instrumented copy with mods enabled and play the target combat. The v4 probe should appear
under:

```text
<instrumented-game>/reference_traces/
```

## Natural Silent / Overgrowth corpus mode

For broad Act 1 oracle checking, prefer ordinary unmodified gameplay over synthetic encounters.
Stage the dedicated corpus variant:

```fish
fish tools/reference_bridge/stage_overgrowth_corpus.fish "/path/to/clean/oracle/game"
```

This produces the same passive bridge mod plus an `overgrowth-silent-act1.mode` marker, so the
capture mode survives a normal Steam launch. Copy the staged `Sts2ReferenceBridge` folder into the
instrumented game's `mods/` directory as usual.

After copying or replacing the mod, **fully exit and relaunch STS2**. The recorder is initialized
only when the mod loader starts. Before beginning the first run, verify that a fresh
`overgrowth-silent-act1-*.jsonl` file already exists under `reference_traces/`; the file is
created at recorder startup, before the first combat. If no fresh file exists, do not start the
oracle run yet.

Then play normal **Silent** runs through **Act 1 Overgrowth** and stop after the Act 1 boss (or
after a defeat). Do not enable Loadout or other gameplay mods for this corpus.

The environment variable `STS2_REFERENCE_CORPUS_MODE=overgrowth-silent-act1` remains available as
an override for development launches, but it is not required for the staged corpus mod.

Corpus mode keeps the passive CombatManager/CombatHistory subscriptions and full combat snapshots,
but suppresses one-time runtime type catalogs. This makes multi-run logs substantially smaller while
preserving the combat state, native history entries, and RNG snapshots needed for differential
analysis. It also adds a stable session id and a session-local combat index to every boundary.

The recorder does not force the character, region, path, Ascension, card choices, or combat actions.
Those remain ordinary player choices. The mode label is provenance metadata rather than a gameplay
filter. The analyzer should reject or segregate captures whose observed character/act/region fall
outside the requested corpus.

A single game launch may contain several runs. The output file is named like:

```text
reference_traces/overgrowth-silent-act1-YYYYMMDD-HHMMSS-3bb5598a35f7.jsonl
```

For the first corpus, A0 is preferred. Play naturally rather than stalling to expose mechanics; the
goal is representative vanilla evidence. Several runs are enough for the first audit. Any remaining
high-risk mechanic can be targeted later only if the natural corpus leaves an important ambiguity.

Keep `STS2_REFERENCE_VERBOSE` unset for this pass. Verbose state-tracker capture is reserved for
diagnosing a divergence found by the compact corpus.

## Knight Gang capture

For the current fidelity milestone, capture a **Knight Gang** combat from the pinned v0.111.0
instrumented copy. A0 is useful; A9 is even better because it simultaneously checks the typed
Ascension breakpoints.

Prefer verbose capture for this diagnostic run:

```fish
set -x STS2_REFERENCE_VERBOSE 1
```

After the fight, run:

```fish
dotnet run --project src/Sts2Emulator.Cli -- \
    reference-knight-gang-audit "/path/to/probe.jsonl"

dotnet run --project src/Sts2Emulator.Cli -- \
    reference-knight-gang-audit "/path/to/probe.jsonl" --json \
    > knight-gang-audit.json
```

The audit does not fill missing native fields from emulator expectations. It reports observed enemy
HP/Block/powers/move/intent evidence, card Upgrade/Affliction evidence, and monster-AI RNG
fingerprints when present, plus static HP mismatches against the emulator at the captured Ascension.

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

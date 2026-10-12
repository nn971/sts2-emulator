# STS2 native fidelity inspector — passive combat slice

This directory now has a **source-audited, opt-in passive v0.111 combat adapter**,
a read-only F8 timeline panel, and development packaging. On 2026-10-12 the
user confirmed native compilation, a packaged DLL/PCK, and successful game
startup with the installed inspector. **Passive capture has not yet produced
a trace**; whether the initializer, SHA pin, frame hook, and stable-decision
gate run correctly remains an explicit acceptance test. No native trace,
passive no-perturbation, or emulator parity success is claimed.

The .NET 9 / Python contracts are tested without game assets. STS2-specific
code is compiled only with `Sts2DataDir` set. In a native build the
Sts2Emulator.Core and Sts2Emulator.Trace protocol sources are **embedded in
Sts2ReferenceMod.dll** rather than loaded as separate assemblies. The game
mod loader's managed dependency resolution is not guaranteed to find
additional DLLs in the mod directory.

## Minimal manifest-only PCK (no Godot editor)

For a first passive native smoke test, the inspector has no Godot scenes or
assets to export. A manifest-only Godot 4.5 PCK v3 can be produced with
`tools/make_inspector_pck.py`. It follows the upstream Godot 4.5.1 pack
reader/writer layout, with one unencrypted root file
`res://mod_manifest.json`. **Game-local PCK load validation is still required.**

To build into a staging directory while keeping Codex's checkout and the
game unmodified:

```fish
cd ~/projects/sts2-inspector-test
set staging ~/projects/sts2-inspector-stage
fish scripts/build-native-inspector.fish "$staging"
ls -lh "$staging/mods/Sts2ReferenceMod/Sts2ReferenceMod.pck"
```

This uses the existing staged game-data symlink and embeds Core/Trace into
one `Sts2ReferenceMod.dll`. Alternatively, if the DLL is already built,
create **only** the missing PCK:

```fish
python tools/make_inspector_pck.py \
    --manifest native/Sts2ReferenceMod/mod_manifest.json \
    --out ~/projects/sts2-inspector-stage/mods/Sts2ReferenceMod/Sts2ReferenceMod.pck
```

Passing a Godot/MegaDot binary as a second build-script argument still
uses editor export. Do not install into the live game directory until the
staged PCK and matching DLL are both present and any incompatible mods are
disabled.

## After a previous Godot .NET export: duplicate assembly attributes

Older versions of the build script ran Godot against the inspector's C# source
directory, leaving generated files under
`native/Sts2ReferenceMod/.godot/mono/temp/obj`.
Subsequent `dotnet build` mistakenly included those files and reported
`CS0579: Duplicate Assembly...Attribute` and
`Duplicate TargetFrameworkAttribute`. The project now explicitly excludes
`.godot/**` from SDK compile globs. Delete **only that generated cache once**
in the isolated worktree before rebuilding:

```fish
cd ~/projects/sts2-inspector-test
rm -rf native/Sts2ReferenceMod/.godot
fish scripts/build-native-inspector.fish \
  ~/projects/sts2-inspector-stage "$STS2_GODOT"
```

The corrected export path uses a temporary resource-only project, so it
should not recreate this cache in the source directory.

## Clean Godot export (no .NET solution required)

The prior two-argument Godot exporter ran from `native/Sts2ReferenceMod`,
which contains `.cs` files but no Godot-specific `.sln`. The Godot .NET
export plugin produced repeated `This project contains C# files but no
solution file was found` errors and a warning-filled PCK. This was an
export configuration failure even though the native C# compiler had succeeded.

The build script now copies only `mod_manifest.json` and the export preset
into a temporary resource-only Godot project and exports **that** project.
C# sources are already compiled into `Sts2ReferenceMod.dll` by `dotnet
build`; they are not PCK assets. Either the one-argument Python packer
or two-argument clean Godot exporter produces a manifest-containing PCK.
The edited Godot path still needs an installed-game smoke test.

```fish
cd ~/projects/sts2-inspector-test
git fetch origin feature/native-fidelity-inspector-foundation
git switch --detach origin/feature/native-fidelity-inspector-foundation
fish scripts/build-native-inspector.fish \
  ~/projects/sts2-inspector-stage "$STS2_GODOT"
```

Do not install any warning-filled older PCK from the previous exporter.

## Build and pin a native installation (fish / WSL)

Run these commands from the emulator repository root. Provide a real MegaDot
4.5.1 Mono editor compatible with the game build. The build script selects
Windows or Linux game data by inspecting the installed files, even under WSL.

```fish
set game "/mnt/c/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2"
set godot "/path/to/MegaDot_v4.5.1-stable_mono_linux.x86_64"
fish scripts/build-native-inspector.fish "$game" "$godot"
cat native/Sts2ReferenceMod/build-pin.json
```

This runs `tools/native_game_preflight.py`, hashes the local sts2.dll,
0Harmony.dll, GodotSharp.dll, release info and runtime configuration as
available, then compiles, exports a PCK, and installs under `mods/Sts2ReferenceMod`.
The generated `build-pin.json` is local and Git-ignored. No game DLLs are
committed or redistributed.

The game loader must be verified locally. A successful export or C# compile
alone does not establish that the correct Harmony/native API hooks fire.

## Opt-in for passive capture

The mod is inert by default. Set both environment variables in the game
process:

- `STS2_FIDELITY_CAPTURE=1`
- `STS2_FIDELITY_EXPECT_SHA256=<sha256 of exact installed sts2.dll>`

If either variable is missing or the hash differs, the mod refuses capture.

For a Windows Steam game, set *user environment variables* via PowerShell,
using the hash from `build-pin.json`, then **fully restart Steam** so the
Windows game process inherits the settings:

```powershell
$env:STS2_FIDELITY_CAPTURE = '1'
$pin = Get-Content 'C:\path\to\build-pin.json' | ConvertFrom-Json
$sha = $pin.managed_assemblies.'sts2.dll'.sha256
[Environment]::SetEnvironmentVariable('STS2_FIDELITY_CAPTURE', '1', 'User')
[Environment]::SetEnvironmentVariable('STS2_FIDELITY_EXPECT_SHA256', $sha, 'User')
```

After the test, disable capture and restart Steam:

```powershell
[Environment]::SetEnvironmentVariable('STS2_FIDELITY_CAPTURE', $null, 'User')
[Environment]::SetEnvironmentVariable('STS2_FIDELITY_EXPECT_SHA256', $null, 'User')
```

Play a *single-player Silent* ordinary combat and choose a simple Strike or
Defend. The adapter observes action-executor events and combat-history entries
and records them in Godot's user data directory under
`native_fidelity_traces/passive-*.jsonl`. F8 toggles a read-only timeline
showing captured states, legal actions, coverage and ordered history effects.
The overlay defaults to hidden. A diagnostic export writes an adjacent
`*.inspection.json` asynchronously.

Current capture scope: player/creature HP, block and powers; turn/energy;
hand/draw/discard/exhaust piles; normal card-play and end-turn actions;
selected scalar combat-history events. The mod refuses to claim parity for
missing RNG, continuation history, relic/potion mutable state, unverified
human-origin attribution, and nested card selections. The bridge does not
inject player actions or modify RNG. The Step/Pause/Resume/Stop coordinator is
implemented independently and awaits audited native action dispatch.

## First local trace files confirmed (2026-10-12)

The user supplied native Windows `godot.log` proving the game finds the
PCK, loads the DLL, calls `Plugin.Initialize`, enables capture, and passes
the SHA-256 pin. It also logs multiple
`Recording (passive): D:\SteamLibrary\sts2-inspector-traces\passive-*.jsonl`
and `Passive combat trace completed` messages. This establishes that
combat sessions **were opened and closed**, but not that they contain valid
decision/action/transition lifecycles. First inspect actual files via
`/mnt/d/SteamLibrary/sts2-inspector-traces` in WSL and audit the JSONL.

An accompanying log line noted:
`mod_manifest.json looks like a mod manifest but is missing the 'id' field`.
This is a **loose legacy manifest** mistakenly copied by the old installer.
The PCK still needs an internal `res://mod_manifest.json`; the native
game's modern loose manifest is `Sts2ReferenceMod.json` with `id`.
The packaging script now removes `mods/Sts2ReferenceMod/mod_manifest.json`
from the installed folder and includes it only in the PCK, while retaining
`Sts2ReferenceMod.json` and the matching DLL/PCK. This warning does not
invalidate the already observed bridge startup.

```fish
set traces /mnt/d/SteamLibrary/sts2-inspector-traces
ls -lah "$traces"
set trace (find "$traces" -maxdepth 1 -type f -name 'passive-*.jsonl' | sort | tail -n 1)
python tools/native_trace_audit.py "$trace" --out ~/projects/sts2-inspector-stage/capture-audit.json
```

Short or incomplete files are useful diagnostic evidence, especially if a
combat was interrupted or the recording prematurely closed. Retain them
rather than claiming native parity.

## Diagnostics when game launches but produces no native trace

The main-menu smoke test validates that a PCK/DLL can load; it does not
prove the capture hook was enabled. Search the Windows game log:

```powershell
$log = Join-Path $env:APPDATA 'SlayTheSpire2\logs\godot.log'
Select-String -Path $log -Pattern 'Sts2ReferenceMod|CAPTURE_STATUS|INCOMPLETE_CAPTURE|Exception' |
  Select-Object -Last 60
```

New game-local log breadcrumbs:

- `CAPTURE_STATUS=MOD_INITIALIZED`: the game's mod loader invoked our
  initializer. The log also records whether the capture environment variable
  was present.
- `CAPTURE_STATUS=PIN_REJECTED`: exact SHA-256 check failed; compare the
  expected value from Steam's environment against the game runtime assembly
  SHA-256 printed in the log. Do not bypass the check.
- `CAPTURE_STATUS=HOOK_ENABLED`: Godot frame hook registered, with the
  actual trace output directory printed.
- `CAPTURE_STATUS=WAITING_FOR_COMBAT`: hook active but no native combat
  detected yet.
- `CAPTURE_STATUS=COMBAT_STATE_UNAVAILABLE` or
  `UNSUPPORTED_PLAYER_COUNT`: native combat is active but the state
  cannot be captured under this adapter's scope.
- `CAPTURE_STATUS=RECORDING_STARTED`: the JSONL writer opened its file;
  use the logged path, not a guessed location.
- `CAPTURE_STATUS=WAITING_FOR_DECISION:<reason>`: the adapter is waiting for
  a player-choice boundary; the reason identifies the failing gate
  (action executor, hand UI, overlay, phase, etc.).
- `CAPTURE_STATUS=DECISION_CAPTURED:<id>`: snapshot captured.
- `CAPTURE_STATUS=CAPTURE_EXCEPTION`: native adapter threw; fail closed
  for that encounter instead of endlessly reopening files.

Messages are emitted on status transitions, not every frame. A file opened
without decisions is not a fidelity success; inspect the partial JSONL
and capture audit. If no `MOD_INITIALIZED` message appears, first inspect
whether the game actually enabled/loaded our PCK, Steam Workshop mod
conflicts, or a duplicate mod package. Environment variables set for
Windows **user** scope require a complete Steam restart; setting values
in WSL does not change the Windows Steam process.

For the first realistic differential check, first capture native
Strike/Defend decision-boundary states and transitions. Feed the native
expectation fixture to an independent emulator continuation. A structurally
valid JSONL, or replaying recorded post-state as expected output, is
**not** emulator parity.

## Restricted full-visible-snapshot step (2026-10-13)

The user supplied `first-strike-expectation.json` and its basic-rule
check. The check reports all **seven** rule-level fields agreeing
(`RULE_PROJECTION_CONSISTENT`); native `STRIKE_SILENT` card instance 0
cost 1 energy, moved from hand to discard, and reduced
`SLUDGE_SPINNER` HP 41 to 35 with no observed other state changes.

Added a stricter *independent* and *restricted* C# visible-state step under
`Sts2Emulator.Core.NativeBasicVisibleStepper`. It deep-copies the native
**pre-state**, applies only basic Strike/Defend effects (energy, creature
HP/block, card zone movement), preserves every other captured field and
compares the resulting recorded-state JSON to the separately observed
post-state. It uses the existing fail-closed basic-rule guard and has
synthetic tests for matching Strike, Defend, rejected powers and a deliberately
tampered native result. **It does not consult the post-state to compute
the step.**

To run it on the user's already exported genuine fixture, without launching
or rebuilding the game/mod:

```fish
cd ~/projects/sts2-inspector-test
git fetch origin feature/native-fidelity-inspector-foundation
git switch --detach origin/feature/native-fidelity-inspector-foundation
set reports ~/projects/sts2-inspector-stage/reports
dotnet run -c Release --project src/Sts2Emulator.Cli -- \
  native-visible-step "$reports/first-strike-expectation.json" \
  --out "$reports/first-strike-visible-step.json"
```

The result is `VISIBLE_SLICE_CONSISTENT`, `VISIBLE_SLICE_MISMATCH` (with a
specific first JSON path), or `UNSUPPORTED`. The report includes the
independently predicted visible post-state. Even on consistency, its
`emulator_parity` remains **`NOT_TESTED`**: absence of powers is
observable, but card enchantments, relic/potion effects, hidden continuation,
pending effects and RNG streams were not captured. This does not implement
a general `IDeterministicEngine.Step` and cannot establish complete
Slay the Spire 2 combat fidelity or hook order.

## Independent basic-card rule projection (2026-10-13)

After the user uploaded `first-strike-delta.json` and `triage.json`, the first
observed Strike against `SLUDGE_SPINNER` is:

- target `creature:1`: 41 HP -> 35 HP, a six-damage delta
- energy: 3 -> 2
- card instance 0: hand -> discard, with hand 7 -> 6 and discard 0 -> 1
- native effect kinds: energy_spent, card_play_started, damage,
  native_history_entry (diagnostic), card_play_finished

These agree with the separately verified `StrikeSilent` card definition
(1 energy, base damage 6, upgraded damage 9). **This is only native checkpoint
consistency;** uploaded files do not contain a complete native pre-state or
post-state and cannot support verified emulator continuation.

Added a conservative .NET 9 pure rule projection in
`Sts2Emulator.Core.NativeBasicCardStepper`, and comparator
`NativeBasicRuleComparer`, which independently predict an *unmodified*
Strike/Defend action from a structurally imported pre-state. They refuse
power-bearing snapshots, unsupported costs/targets/upgrade levels, target
block or lethal hits outside the first slice. Even on success, the report
`emulator_parity` is **always `NOT_TESTED`**, with unobserved relic,
enchantment, pending-effect, hook-order and RNG conditions explicit.
This is a separate rule-level diagnostic; it does not implement a
full `IDeterministicEngine.Step` from true continuation state.

Use a real **native-expectation fixture** produced from the clean Strike trace
(not the compressed delta/triage files):

```fish
cd ~/projects/sts2-inspector-test
set trace "/mnt/d/SteamLibrary/sts2-inspector-traces/passive-20261012-124318892-31fed163df2f4f03b417a53da0bb9428.jsonl"
set fixture ~/projects/sts2-inspector-stage/reports/first-strike-expectation.json
python tools/native_expectation_fixture.py "$trace" --decision 1 --out "$fixture"
dotnet run -c Release --project src/Sts2Emulator.Cli -- \
    native-basic-check "$fixture" \
    --out ~/projects/sts2-inspector-stage/reports/first-strike-basic-rule-check.json
```

Exit codes: `0` = rule projection consistent, `1` = rule projection differs,
`2` = unsupported/insufficient input. None imply verified native/emulator parity.
The next phase requires implementing genuine deterministic continuation in
the emulator and an independently audited set of comparison preconditions.

## First authentic Strike decision selected (2026-10-12)

The first recorded native action suitable for a parity fixture is decision 1
of `passive-20261012-124318892-31fed163df2f4f03b417a53da0bb9428.jsonl`:
`STRIKE_SILENT` card instance 0, target `creature:1`.
The full trace passed the native lifecycle audit with 2 decisions,
2 accepted actions and 8 diagnostic events. Decision 2 plays `SURVIVOR`,
which is outside the first Strike/Defend continuation slice.
Another clean recording at 12:44:33 (2 actions, 12 events) starts with
`end_turn` and then `SURVIVOR`, so no extra current fixture.

Many incomplete sessions contain accepted actions before an unresolved final
transition; these are potential future prefix evidence, **not** whole-trace
validations. One header/terminal-only recording was previously labeled
`STRUCTURALLY_VALID` by the triage tool despite the authoritative audit
reporting `No native decision boundary recorded`. Triage now delegates
the complete-capture verdict to `native_trace_audit.audit`.

The action summary previously displayed `origin=None` even for human actions
because it inspected an event field. Native action records call it `source`;
triage now displays `source`. That field is declarative, however: the
native human-action hook origin still needs independent auditing.

Run triage again to obtain corrected verdicts and native checkpoint deltas:

```fish
python tools/native_trace_triage.py /mnt/d/SteamLibrary/sts2-inspector-traces \
  --out ~/projects/sts2-inspector-stage/reports/triage.json \
  --fixture-dir ~/projects/sts2-inspector-stage/reports/native-fixtures
python tools/native_observed_delta.py \
  ~/projects/sts2-inspector-stage/reports/native-fixtures/passive-20261012-124318892-31fed163df2f4f03b417a53da0bb9428-decision-1.json \
  --out ~/projects/sts2-inspector-stage/reports/first-strike-delta.json
```

The delta tool reports measured changes between native checkpoints only.
It does not apply Strike logic, prove causality, or run an independent emulator
step. Native parity remains `NOT_TESTED`.

## Inspect a captured trace

```fish
python tools/native_trace_audit.py /path/to/passive.jsonl --out capture-audit.json
python tools/native_scenario_import.py /path/to/passive.jsonl --decision 1 --out import.json
python tools/native_expectation_fixture.py /path/to/passive.jsonl --decision 1 --out strike-expectation.json
python tools/inspector_html.py /path/to/passive.jsonl --out inspector.html
```

The capture audit checks decision/checkpoint structure and reports
`native_parity: NOT_TESTED`. Open `inspector.html` locally to view the
native-only decision timeline. Once an emulator trace with genuine equivalent
continuation state exists, compare explicitly covered deterministic steps:

```fish
python tools/native_parity.py native.jsonl emulator.jsonl --decision 1 --out parity.json
python tools/inspector_html.py native.jsonl --emulator emulator.jsonl --out comparison.html
```

Until the emulator native-combat importer and game-local parity fixtures are
validated, `PARTIAL`/`UNSUPPORTED` classifications are expected.

## Future controller connection

`LocalBridgeServer` is implemented but intentionally disabled in passive
mode. When native action dispatch is audited, it can expose authenticated
loopback endpoints (`/v1/status`, `/v1/events`, `/v1/actions`,
`/v1/pause`, `/v1/step`, `/v1/resume`, `/v1/stop`). The network thread
only submits versioned commands. Native execution must happen on the Godot
main thread and only at a stable input-ready boundary.

Reference implementation and confidence gates are recorded in
`docs/NATIVE_FIDELITY_INSPECTOR_IMPLEMENTATION_STATUS.md`.

## Native expectation and structural emulator import

The Python fixture extractor packages a recorded native action, pre/post
state and event sequence as an oracle expectation. It reports parity as
`NOT_TESTED` and retains unsupported continuation fields.

The .NET `NativeCombatSnapshotImporter` under `Sts2Emulator.Core` parses
the canonical snapshot without game assemblies. It validates identities,
scalar ranges and card zones, returning a typed **PARTIAL** state for inspection.
Actual deterministic replay remains gated on hidden continuation state and
a verified emulator action engine.

## Windows mod loader: PCK + one DLL

The game's loader detects a mod by scanning `mods` recursively for
`*.pck`; it then loads the matching `<pck_name>.dll` if present.
A loose DLL-only folder is **not** a supported installation procedure for
this pinned API. The exported PCK must include a root-level
`mod_manifest.json` with `pck_name` matching the PCK basename.

The native build now ships just `Sts2ReferenceMod.dll` and
`Sts2ReferenceMod.pck` (plus descriptive manifests). Do not copy
`Sts2Emulator.Trace.dll` or `Sts2Emulator.Core.dll`: their code is
compiled into the native DLL. This change addresses the launch-time
`FileNotFoundException: Sts2Emulator.Trace` observed in the first
game-local smoke test. Runtime confirmation is still pending.

When testing in WSL, the Windows Steam library may be mounted at
`/mnt/d/SteamLibrary`. Always use that path if it matches your
installation, not `/d/SteamLibrary`.

If another incompatible mod is active, disable it separately. The first
local crash log also contained an unrelated
`UndoAndRedoForkCode.UndoInputBindings` missing-field exception;
the inspector fix cannot repair that other mod.

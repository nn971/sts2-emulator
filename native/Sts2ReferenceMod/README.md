# STS2 native fidelity inspector — passive combat slice

This directory now has a **source-audited, opt-in passive v0.111 combat adapter**,
a read-only F8 timeline panel, and development packaging. The native hook
implementation **has not been compiled or run against the user's pinned game
install**; gameplay compatibility and passive/no-perturbation remain acceptance
gates. No real native trace or emulator parity success is claimed.

The .NET 9 / Python contracts are tested without game assets. STS2-specific
code is compiled only with `Sts2DataDir` set.

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

## Inspect a captured trace

```fish
python tools/native_trace_audit.py /path/to/passive.jsonl --out capture-audit.json
python tools/native_scenario_import.py /path/to/passive.jsonl --decision 1 --out import.json
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

# Native reference bridge workspace

This project is intentionally **not** part of `Sts2Emulator.sln`.

It must compile against the exact assemblies shipped by the STS2 build being observed. CI does not
have those proprietary game assemblies and should not download or vendor them.

## Preflight first

On the machine with STS2 installed:

```fish
dotnet run --project ../../src/Sts2Emulator.Cli -- \
    reference-preflight "/path/to/Slay the Spire 2"
```

Keep the emitted build fingerprint with every trace corpus.

Then inspect candidate semantic APIs from that exact `sts2.dll`:

```fish
dotnet run --project ../../src/Sts2Emulator.Cli -- \
    reference-inspect "/path/to/Slay the Spire 2" "ModInitializer"

dotnet run --project ../../src/Sts2Emulator.Cli -- \
    reference-inspect "/path/to/Slay the Spire 2" "Combat"

dotnet run --project ../../src/Sts2Emulator.Cli -- \
    reference-inspect "/path/to/Slay the Spire 2" "Rng"
```

The search patterns are discovery aids, not protocol assumptions.

## Build

The project accepts either `GameDir` or an exact `Sts2DataDir`:

```fish
dotnet build Sts2ReferenceBridge.csproj \
    -p:GameDir="/path/to/Slay the Spire 2"
```

or:

```fish
dotnet build Sts2ReferenceBridge.csproj \
    -p:Sts2DataDir="/path/to/Slay the Spire 2/data_sts2_linuxbsd_x86_64"
```

## Why there is no hook code yet

Do not add a guessed `[ModInitializer]`, Harmony target, RNG field name, or decision-boundary
method based on another STS2 version. The first hook commit should cite the local build fingerprint
and the inspected type/method names it was written against.

The bridge should emit `reference-trace-v0.2` records and depend on
`Sts2Emulator.Trace` for the versioned trace/build DTOs. It must not depend on prototype gameplay
semantics from `Sts2Emulator.Core` beyond protocol utilities transitively used by Trace.

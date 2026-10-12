#!/usr/bin/env fish
# Development build/install: explicitly enabled passive capture only.
# Usage: scripts/build-native-inspector.fish GAME_ROOT GODOT_4_5_1_MONO_BINARY
if test (count $argv) -ne 2
    echo "Usage: scripts/build-native-inspector.fish GAME_ROOT GODOT_4_5_1_MONO_BINARY" >&2
    exit 2
end
set -l root (realpath $argv[1])
set -l godot (realpath $argv[2])
set -l data ""
# WSL host is Linux, but its mounted Steam game may be the Windows build.
for candidate in "$root/data_sts2_windows_x86_64" "$root/data_sts2_linuxbsd_x86_64"
    if test -f "$candidate/sts2.dll"
        set data "$candidate"
        break
    end
end
if test -z "$data"
    echo "No supported STS2 data directory found under $root" >&2
    exit 1
end
set -l project native/Sts2ReferenceMod
set -l dest "$root/mods/Sts2ReferenceMod"
for file in "$data/sts2.dll" "$data/0Harmony.dll" "$data/GodotSharp.dll" "$godot"
    if not test -f "$file"
        echo "Missing pinned build dependency: $file" >&2
        exit 1
    end
end
python tools/native_game_preflight.py "$data" --out "$project/build-pin.json"
or exit 1
dotnet build "$project/Sts2ReferenceMod.csproj" -c Release -p:Sts2DataDir="$data"
or exit 1
mkdir -p "$dest"
# Core and Trace protocol sources are embedded in the native mod DLL to
# avoid depending on the game's managed assembly resolver for sibling DLLs.
# Clean up only our own obsolete companion DLLs from an earlier installation.
if not test -f "$project/bin/Release/net9.0/Sts2ReferenceMod.dll"
    echo "Missing native mod DLL" >&2
    exit 1
end
cp "$project/bin/Release/net9.0/Sts2ReferenceMod.dll" "$dest/"
rm -f "$dest/Sts2Emulator.Trace.dll" "$dest/Sts2Emulator.Core.dll"
cp "$project/Sts2ReferenceMod.json" "$dest/"
cp "$project/mod_manifest.json" "$dest/"
pushd "$project"
"$godot" --headless --path . --import
or exit 1
"$godot" --headless --path . --export-pack BasicExport "$dest/Sts2ReferenceMod.pck"
set -l result $status
popd
if test $result -ne 0
    echo "PCK export failed; remove incomplete mod folder: $dest" >&2
    exit 1
end
echo "Installed development mod at $dest"
echo "To opt in to passive recording, set STS2_FIDELITY_CAPTURE=1"
echo "Also set STS2_FIDELITY_EXPECT_SHA256 to the local sts2.dll SHA-256 in build-pin.json"
echo "Without this setting the mod remains inactive."

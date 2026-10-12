#!/usr/bin/env fish
# Development build/install: explicitly enabled passive capture only.
# Usage: scripts/build-native-inspector.fish GAME_ROOT [GODOT_4_5_1_MONO_BINARY]
# When no Godot binary is passed, generate a minimal manifest-only PCK v3.
if test (count $argv) -lt 1 -o (count $argv) -gt 2
    echo "Usage: scripts/build-native-inspector.fish GAME_ROOT [GODOT_4_5_1_MONO_BINARY]" >&2
    exit 2
end
set -l root (realpath $argv[1])
set -l godot ""
if test (count $argv) -eq 2
    set godot (realpath $argv[2])
end
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
set -l required "$data/sts2.dll" "$data/0Harmony.dll" "$data/GodotSharp.dll"
if test -n "$godot"
    set -a required "$godot"
end
for file in $required
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
if test -n "$godot"
    # Do not export from the C# project itself: the .NET editor export plugin
    # attempts to rebuild every .cs and demands a Godot .sln. Our binary code
    # has already been compiled by dotnet; this PCK needs only mod_manifest.json.
    set -l pack_project (mktemp -d "$root/.inspector-pack-XXXXXXXX")
    or exit 1
    cp "$project/mod_manifest.json" "$pack_project/mod_manifest.json"
    cp "$project/export_presets.cfg" "$pack_project/export_presets.cfg"
    printf '%s\\n' 'config_version=5' '' '[application]' 'config/name="Sts2ReferenceModPackOnly"' 'config/features=PackedStringArray("4.5")' > "$pack_project/project.godot"
    "$godot" --headless --path "$pack_project" --import
    set -l result $status
    if test $result -eq 0
        "$godot" --headless --path "$pack_project" \\
            --export-pack BasicExport "$dest/Sts2ReferenceMod.pck"
        set result $status
    end
    # A generated staging project contains nothing from the user's game.
    rm -rf "$pack_project"
    if test $result -ne 0
        echo "Godot pack-only export failed; do not install the incomplete package: $dest" >&2
        exit 1
    end
else
    python tools/make_inspector_pck.py \
        --manifest "$project/mod_manifest.json" \
        --out "$dest/Sts2ReferenceMod.pck"
    or exit 1
end
echo "Packaged development mod at $dest (staging directory; only installed when root is game directory)"
echo "To opt in to passive recording, set STS2_FIDELITY_CAPTURE=1"
echo "Also set STS2_FIDELITY_EXPECT_SHA256 to the local sts2.dll SHA-256 in build-pin.json"
echo "Without this setting the mod remains inactive."

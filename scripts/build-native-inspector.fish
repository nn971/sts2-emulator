#!/usr/bin/env fish
# Development build/install: explicitly enabled passive capture only.
# Usage: scripts/build-native-inspector.fish GAME_ROOT GODOT_4_5_1_MONO_BINARY
if test (count $argv) -ne 2
    echo "Usage: scripts/build-native-inspector.fish GAME_ROOT GODOT_4_5_1_MONO_BINARY" >&2
    exit 2
end
set -l root (realpath $argv[1])
set -l godot (realpath $argv[2])
set -l platform windows
if test (uname) = Linux
    set platform linuxbsd
end
set -l data "$root/data_sts2_"$platform"_x86_64"
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
cp "$project/bin/Release/net9.0/Sts2ReferenceMod.dll" "$dest/"
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
echo "To opt in to passive recording, start the game with STS2_FIDELITY_CAPTURE=1"
echo "Without this setting the mod remains inactive."

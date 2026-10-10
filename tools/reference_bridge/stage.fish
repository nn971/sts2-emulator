#!/usr/bin/env fish

function usage
    echo "Usage: tools/reference_bridge/stage.fish <oracle-game-dir> [staging-dir]" >&2
end

if test (count $argv) -lt 1 -o (count $argv) -gt 2
    usage
    exit 2
end

set script_dir (realpath (dirname (status filename)))
set repo_root (realpath "$script_dir/../..")
set game_dir (realpath $argv[1])

if test (count $argv) -eq 2
    set staging_root (realpath -m $argv[2])
else
    set staging_root "$repo_root/artifacts/reference_bridge_mod"
end

set project "$script_dir/Sts2ReferenceBridge.csproj"
set build_dir "$script_dir/bin/Release/net9.0"
set mod_dir "$staging_root/Sts2ReferenceBridge"

echo "Building against pinned oracle assemblies:"
echo "  $game_dir"

dotnet build $project -c Release -p:GameDir="$game_dir"
or exit $status

rm -rf "$mod_dir"
mkdir -p "$mod_dir"

cp "$build_dir/Sts2ReferenceBridge.dll" "$mod_dir/"
cp "$script_dir/Sts2ReferenceBridge.json" "$mod_dir/"

echo
echo "Staged passive recorder:"
echo "  $mod_dir"
echo
echo "The oracle game directory was NOT modified."
echo "Copy this folder into <instrumented-game>/mods/ before launching that disposable copy."

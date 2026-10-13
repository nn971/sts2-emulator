#!/usr/bin/env fish

if test (count $argv) -ne 1
    echo "usage: decompile.fish <sts2-game-directory>" >&2
    exit 2
end

set -l game_dir (realpath "$argv[1]")
set -l data_dir "$game_dir/data_sts2_linuxbsd_x86_64"
set -l assembly "$data_dir/sts2.dll"
set -l output_dir "data/extracted/v0.111.0/decompiled"

if not test -f "$assembly"
    echo "sts2.dll not found at: $assembly" >&2
    exit 1
end

if not command -q ilspycmd
    echo "ilspycmd is not installed." >&2
    echo "Install it with: dotnet tool install --global ilspycmd" >&2
    exit 1
end

rm -rf "$output_dir"
mkdir -p "$output_dir"

echo "Decompiling pinned STS2 assembly:"
echo "  $assembly"
echo "into:"
echo "  $output_dir"

ilspycmd     --nested-directories     --project     --outputdir "$output_dir"     "$assembly"
or exit 1

printf '%s\n'     "Local decompilation of the user's pinned STS2 v0.111.0 oracle."     "Do not commit or redistribute this directory."     > "$output_dir/LOCAL_ONLY.txt"

echo "Done."

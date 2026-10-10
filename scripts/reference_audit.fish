#!/usr/bin/env fish

function usage
    echo "Usage: scripts/reference_audit.fish <game-dir> [data-dir]" >&2
end

if test (count $argv) -lt 1 -o (count $argv) -gt 2
    usage
    exit 2
end

set game_dir $argv[1]
set data_args
if test (count $argv) -eq 2
    set data_args $argv[2]
end

set cli src/Sts2Emulator.Cli

echo "== Installed build fingerprint =="
dotnet run --project $cli -- reference-preflight $game_dir $data_args
or exit $status

set patterns \
    ModInitializer \
    Harmony \
    Rng \
    Random \
    Combat \
    Reward \
    Map \
    Shop \
    Rest \
    Event

for pattern in $patterns
    echo
    echo "== Metadata search: $pattern =="
    dotnet run --project $cli -- reference-inspect $game_dir $pattern $data_args
    or exit $status
end

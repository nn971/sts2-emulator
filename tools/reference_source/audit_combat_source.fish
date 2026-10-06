#!/usr/bin/env fish

if test (count $argv) -ne 1
    echo "usage: audit_combat_source.fish <sts2-game-directory>" >&2
    exit 2
end

set -l game_dir (realpath "$argv[1]")
set -l source_dir "data/extracted/v0.111.0/decompiled"
set -l report "data/extracted/v0.111.0/source-audit.txt"
set -l cli_dll "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"

if not test -d "$source_dir"
    fish tools/reference_source/decompile.fish "$game_dir"
    or exit 1
end

dotnet build Sts2Emulator.sln -c Release
or exit 1

mkdir -p (dirname "$report")

printf '%s\n'     "STS2 v0.111.0 pinned source audit"     "Game directory: $game_dir"     "Source directory: $source_dir"     > "$report"

set -l queries     Sly     CardKeyword     Discard     CardDiscarded     CardPlay     Retain     Innate     Ethereal     RandomEnemy     CombatTargets     CombatCardSelection     PlayerActionsDisabled     SelectionScreen     ChooseCards     SerializableRng     MonsterAi

for query in $queries
    printf '\n===== %s =====\n' "$query" >> "$report"
    dotnet "$cli_dll" \
        reference-source-search \
        "$source_dir" \
        "$query" \
        60 \
        8 \
        >> "$report"
    or exit 1
end

echo "Wrote source audit to $report"

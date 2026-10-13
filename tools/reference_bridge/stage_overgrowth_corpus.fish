#!/usr/bin/env fish

function usage
    echo "Usage: tools/reference_bridge/stage_overgrowth_corpus.fish <oracle-game-dir> [staging-dir]" >&2
end

if test (count $argv) -lt 1 -o (count $argv) -gt 2
    usage
    exit 2
end

set script_dir (realpath (dirname (status filename)))
set repo_root (realpath "$script_dir/../..")

if test (count $argv) -eq 2
    set staging_root (realpath -m $argv[2])
else
    set staging_root "$repo_root/artifacts/reference_bridge_mod"
end

fish "$script_dir/stage.fish" $argv[1] "$staging_root"
or exit $status

set mod_dir "$staging_root/Sts2ReferenceBridge"
touch "$mod_dir/overgrowth-silent-act1.mode"

echo
echo "Enabled natural Silent / Overgrowth Act 1 corpus mode:"
echo "  $mod_dir/overgrowth-silent-act1.mode"
echo
echo "Copy the staged Sts2ReferenceBridge folder into the instrumented game's mods directory."
echo "Then fully exit and relaunch STS2. Before starting a run, verify that a new"
echo "overgrowth-silent-act1-*.jsonl file already exists under reference_traces/."

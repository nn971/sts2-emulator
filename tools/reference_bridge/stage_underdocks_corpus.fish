#!/usr/bin/env fish

function usage
    echo "Usage: tools/reference_bridge/stage_underdocks_corpus.fish <clean-oracle-game-dir> [staging-dir]" >&2
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

# Rebuild from source against exactly the pinned, unmodified game binaries.
# The clean oracle installation is only READ by this script.
fish "$script_dir/stage.fish" $argv[1] "$staging_root"
or exit $status

set mod_dir "$staging_root/Sts2ReferenceBridge"
touch "$mod_dir/underdocks-silent-act1.mode"

echo
echo "Prepared one-run Silent / Underdocks Act 1 capture:"
echo "  $mod_dir/underdocks-silent-act1.mode"
echo
echo "Copy the Sts2ReferenceBridge directory into the mods directory"
echo "of an EXPENDABLE instrumented game copy, never the clean oracle."
echo "Fully exit/relaunch the game, and verify that a fresh"
echo "underdocks-silent-act1-*.jsonl exists under reference_traces/"
echo "BEFORE spending time on the actual playthrough."
echo "Run: python tools/native_one_run_audit.py --preflight <jsonl>"

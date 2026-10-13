# Tools

## `trace_diff.py`

Compares two JSONL traces and reports the first structural difference.

```fish
python tools/trace_diff.py reference.jsonl emulator.jsonl
```

This is intentionally a simple bootstrap utility. Once canonical state becomes large, add specialized summaries that highlight HP/deck/RNG/legal-action differences without replacing the exact structural diff.


## Native Fidelity Inspector protocol tooling

All commands below run independently of STS2 game assets. The first commands
validate capture evidence; only the parity comparison consumes an emulator trace.

```fish
python tools/native_game_preflight.py /path/to/data_sts2_windows_x86_64 --out native-build-pin.json
python tools/native_trace_audit.py native.jsonl --out capture-audit.json
python tools/native_scenario_import.py native.jsonl --decision 1 --out importer-coverage.json
python tools/inspector_html.py native.jsonl --out native-viewer.html

# After a verified emulator continuation exporter exists:
python tools/native_parity.py native.jsonl emulator.jsonl --decision 1 --out native-parity.json
python tools/inspector_html.py native.jsonl --emulator emulator.jsonl --out parity-viewer.html
```

The passive auditor checks JSONL lifecycle and event cursor integrity but always
reports native-parity status as NOT_TESTED. The importer marks continuation as
PARTIAL or UNSUPPORTED until relevant hidden state has been faithfully restored.

The parity comparator explicitly classifies MATCH, MISMATCH, CHANCE_UNALIGNED,
UNSUPPORTED, INVALID_SETUP, and INCOMPLETE_CAPTURE, with the shortest known
native failure prefix and declared field coverage. Synthetic tests exercise
this protocol without proving any actual game mechanics.

See [native mod instructions](../native/Sts2ReferenceMod/README.md) for opt-in
capture, build pinning, and the read-only in-game inspector.

## Native Strike/Defend expectation export

```fish
python tools/native_expectation_fixture.py native.jsonl --decision 1 --out native-strike.json
```

This exports one recorded native pre-state/action/post-state and diagnostic
events, labelled `RECORDED_NATIVE_EXPECTATION`. It always retains
`emulator_parity=NOT_TESTED`. The .NET structural importer can inspect such
a pre-state but continues to report `PARTIAL` until engine continuation is
available and audited.

## Minimal native PCK packaging without Godot

The optional `tools/make_inspector_pck.py` writes a single-file unencrypted
Godot 4.5 PCK v3 containing the root-level `mod_manifest.json` required by
StS2's PCK-first mod loader:

```fish
python tools/make_inspector_pck.py --manifest native/Sts2ReferenceMod/mod_manifest.json \
  --out /mnt/d/SteamLibrary/sts2-inspector-traces/Sts2ReferenceMod.pck
```

The PCK is not an arbitrary ZIP or bare JSON file; the on-disk format is
checked against Godot's PCK header/directory layout in synthetic tests.
It still needs a native game-local loader smoke test.

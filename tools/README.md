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

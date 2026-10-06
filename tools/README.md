# Tools

## `trace_diff.py`

Compares two JSONL traces and reports the first structural difference.

```fish
python tools/trace_diff.py reference.jsonl emulator.jsonl
```

This is intentionally a simple bootstrap utility. Once canonical state becomes large, add specialized summaries that highlight HP/deck/RNG/legal-action differences without replacing the exact structural diff.

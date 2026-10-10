# Python binding

The standard-library `sts2_emulator` package provides a persistent JSONL consumer
binding. Add `bindings/python` to `PYTHONPATH`; build the C# solution in Release.

```python
from sts2_emulator import Session

with Session(["dotnet", "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll",
              "prototype-ai-jsonl"]) as emulator:
    root = emulator.reset("example", first_act="Underdocks")
    action = root.actions()[0]["actionId"]
    child = root.step(action)
    fair_observation = child.observe()
    restored = emulator.load_snapshot(child.save_snapshot())
    assert restored.exact_hash() == child.exact_hash()
    branches = emulator.step_batch([(root, action), (root, action)], parallelism=2)
    assert branches[0].exact_hash() == branches[1].exact_hash()
    child.release()
```

Construction checks wire/snapshot/batch compatibility. Handles belong to one
session; release invalidates them. Calls are serialized with a lock.
`EmulatorError` exposes server failures; the context manager closes the server.
`mode="prototype"` selects legacy reset. Default `mode="v111"` supports Silent
Act 1 mechanics with explicit prototype RNG fidelity; unsupported characters
fail on start. Exact snapshots contain hidden state; `observe()` is fair.

All mechanics remain in C#. Search, learning and strategy belong in the parent
AI repository. See [the versioned contract](../../docs/V111_INTERFACE.md).

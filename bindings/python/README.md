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

The opt-in controlled run-action controller can be sampled atomically with
`State.begin_reference_nature_run_action(seed, action_id=...)`. Its typed
session supports `resolve_next()`, `fork()`, `save_snapshot()`,
`Session.load_reference_nature_run_action_snapshot()`, `release()` and a
playable `.state` after its Nature choice commits. It is
limited to the run-start, legacy Event-node entry and Shovel continuations
listed in the [run-action contract](../../docs/CONTROLLED_RUN_ACTION_D3.md).

Supported event choices also have an atomic ReferenceNature session through
`State.begin_reference_nature_event_choice(choice_id, nature_seed)`. It keeps
reward actions and event Nature choices in one replay order; the committed
session exposes `.state` for ordinary play. See the
[event-choice contract](../../docs/CONTROLLED_EVENT_CHOICE_D3.md).

`Session.stochastic_session_coverage` reports the site IDs accepted by the
semantic, run-action, and event-choice ReferenceNature session schemas.
`Session.supports_reference_nature_site(site_id, session="any")` checks that
versioned capability report before a client starts a continuation.

All mechanics remain in C#. Search, learning and strategy belong in the parent
AI repository. See [the versioned contract](../../docs/V111_INTERFACE.md).

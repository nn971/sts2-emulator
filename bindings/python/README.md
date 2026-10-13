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


## Experimental Nature choice-boundary API (isolated combat entry)

This version adds an opt-in D3 Nature boundary in the same JSONL process.
The `NatureBoundary` handle type is **not** a playable `State` and
must not be passed to `State.step`, `State.observe` or regular rollout
methods. The source-audited scope is the first HP and committed enemy
intent choices of isolated Hive/Glory encounter fixtures only.

```python
from sts2_emulator import Session

with Session(["dotnet", "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll",
              "prototype-ai-jsonl"]) as session:
    hp = session.begin_nature(
        "trial", kind="initial_hp", act=2,
        encounter_id="proto.native.hive.encounter.exoskeletons_normal",
        native_entry="ExoskeletonsNormal", total_floor=20)

    # Fork the immutable pending choice, then resolve one legal index.
    branch = hp.fork()
    while not branch.complete:
        branch = branch.resolve(0)

    # Continue exactly this controlled HP outcome into source-shaped intents.
    intents = branch.advance()
    restored = session.load_nature_snapshot(
        intents.save_snapshot(), kind="intents")
    while not restored.complete:
        restored = restored.resolve(0)

    # Explicitly release retained handles during training/search.
    session.release_nature_many([hp, branch, intents, restored])
```

A pending HP request contains an `orderedCandidates` list and
`operationKind="choose_index"`; a pending intent request exposes
`boundedCount` and `operationKind="bounded_integer"`.
Every `resolve` returns a new handle, without mutating its parent.
Snapshots record all resolved HP and intent tickets, source eligibility,
owner identity and pending continuation; `release_nature_many` frees
only the listed session handles. Native RNG mode remains independent.

The reference probability is **not calibrated** and Nature outcomes
do **not** advance native RNG streams. This API does not yet
support mid-card chance decisions or complete combat/whole-run
transitions. See [Nature D3 bridge contract](../../docs/DESIRED_RNG_D3_INTENT_BRIDGE.md).


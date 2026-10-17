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
`State.begin_reference_nature_run_action(nature_seed=None, action_id=...)`. Supply a seed
for the first ReferenceNature session on a run; after a session commits, its
sampler cursor is saved with the run and later sessions can omit the seed. Its
typed session supports `resolve_next()`, `fork()`, `save_snapshot()`,
`Session.load_reference_nature_run_action_snapshot()`, `release()` and a
playable `.state` after its Nature choice commits. It is
limited to the run-start, legacy Event-node entry and Shovel continuations
listed in the [run-action contract](../../docs/CONTROLLED_RUN_ACTION_D3.md).

Supported event choices also have an atomic ReferenceNature session through
`State.begin_reference_nature_event_choice(choice_id, nature_seed=None)`. It keeps
reward actions and event Nature choices in one replay order; the committed
session exposes `.state` for ordinary play. See the
[event-choice contract](../../docs/CONTROLLED_EVENT_CHOICE_D3.md).

The event-local shuffle session is available through
`State.begin_reference_nature_native_event_shuffle(nature_seed=None, action_id=None)`. It
covers the Light shuffle in Doors of Light and Dark, Fragrant Mushroom's
eligible-card shuffle, and Tinker Time's type and rider offer shuffles. Pass
`action_id="fragrant_mushroom"` for the eligible-card shuffle;
`action_id="choose_card_type"` for the initial Tinker stage; and the offered
type (`"attack"`, `"skill"`, or `"power"`) for its rider stage. It resolves
one conditional swap per `resolve_next()` and
publishes `.state` after the full shuffle replays. It supports `fork()`, `save_snapshot()`,
`Session.load_reference_nature_native_event_shuffle_snapshot()`, and
`release()`. See the
[native event shuffle contract](../../docs/REFERENCE_NATURE_NATIVE_EVENT_SHUFFLE_D3.md).

Reflections' `touch_mirror` action has an atomic continuation through
`State.begin_reference_nature_glory_reflections(nature_seed=None)`. It samples
the downgrade and upgrade card identities in source order, rebuilding the
eligible deck after each pick, and publishes `.state` after the ordinary event
action completes. The session supports `status()`, `resolve_next()`, `fork()`,
`save_snapshot()`, `release()`, and
`Session.load_reference_nature_glory_reflections_snapshot()`. Its two card-pick
site IDs are advertised under the `glory_reflections` session kind. Laws remain
`UNVERIFIED`; see the [Native event-context contract](../../docs/NATIVE_EVENT_CONTEXT_STOCHASTIC_CONTRACT.md).

`Session.stochastic_session_coverage` reports the site IDs admitted by the
advertised ReferenceNature session schemas.
`Session.supports_reference_nature_site(site_id, session="any")` checks that
versioned capability report before a client starts a continuation. A pending
semantic peek or scry can be resolved by physical card identity with
`SemanticCombat.resolve_deck_reveal()`; its source gameplay callsites are not
integrated yet.

`Session.observe_nature_choice(session_kind, session_handle)` checks the
advertised Nature-choice view schema and session kinds before calling the
shared `nature_choice_observe` endpoint. It returns the versioned Nature-only
payload and verifies that the response matches the requested session. Use
`State.observe()` separately for the fair player view.

All mechanics remain in C#. Search, learning and strategy belong in the parent
AI repository. See [the versioned contract](../../docs/V111_INTERFACE.md).

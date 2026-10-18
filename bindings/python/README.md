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

The initial Endless Conveyor dish law can be queried before resolving a map
Event-node action:

```python
law = state.query_chance_law(
    "event.endless_conveyor.dish_weighted_ticket",
    action_id=event_node_action_id,
)
```

The query is admitted only when the ordinary event-selection provider resolves
that action to Endless Conveyor. It covers the first dish roll; later rolls
and a ReferenceNature unit-float continuation are not included yet.

The Fishing Rod combat-end index law takes a caller-declared support size and
the source owner ID:

```python
law = state.query_chance_law(
    "relic.persistent_combat_end.random_upgrade.index_draw",
    owner_id="relic:proto.native.neow.fishingrod:slot:0",
    support_count=eligible_card_count,
)
```

This reports the exact Native `NextInt` index masses for that support size. It
does not validate the Fishing Rod trigger or eligible-card pool. For an
atomic victory replay, use the saved victory-hook session described below.

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
event player actions and Nature choices in one replay order, including Stone
of All Time push's deck-card action before its ticket; the committed session
exposes `.state` for ordinary play. See the
[event-choice contract](../../docs/CONTROLLED_EVENT_CHOICE_D3.md).

The event shuffle and combat-victory session is available through
`State.begin_reference_nature_native_event_shuffle(nature_seed=None, action_id=None, site_id=None)`. It
covers the Light shuffle in Doors of Light and Dark, Fragrant Mushroom's
eligible-card shuffle, Tinker Time's type and rider offer shuffles, and
Battleworn Dummy setting 2's upgrade shuffle, plus Fake Merchant's relic
stock shuffle on Event-node entry. Pass
`action_id="fragrant_mushroom"` for the eligible-card shuffle;
`action_id="choose_card_type"` for the initial Tinker stage; and the offered
type (`"attack"`, `"skill"`, or `"power"`) for its rider stage. It resolves
one conditional swap per `resolve_next()` and
publishes `.state` after the full shuffle replays. It supports `fork()`, `save_snapshot()`,
`Session.load_reference_nature_native_event_shuffle_snapshot()`, and
`release()`. See the
[native event shuffle contract](../../docs/REFERENCE_NATURE_NATIVE_EVENT_SHUFFLE_D3.md).

The same saved replay session accepts a configured ordinary combat victory
that triggers one or more Fishing Rod upgrades. Pass the legal victory
`action_id` and `site_id="relic.persistent_combat_end.random_upgrade.index_draw"`;
each Nature index is conditioned on the deck remaining after earlier Rod
callbacks, and `.state` becomes available after all triggered upgrades replay.

It also accepts a legal combat victory that triggers Mad Science Improvement.
Pass `site_id="combat.glory_tinker.improvement_card.index_draw"` and the
legal victory `action_id`. Each pick uses an `UNVERIFIED` legacy index law and
conditions on cards left after earlier picks; `.state` becomes available after
the ordinary victory upgrade hook replays.

The Improvement law can be queried directly with the same legal victory
action ID and a complete `prior_indices` prefix. Later pick support is rebuilt
after those earlier selected cards are removed.

For supported configured Native event-entry draws, call
`State.begin_reference_nature_native_event_gold(action_id, nature_seed=None)`
with the stable legal Event-node action ID. The session covers generated gold,
Stone of All Time's occupied-potion-slot choice, and The Future of Potions'
ordered card-type offers. Resolve one law per `resolve_next()`; Future of
Potions uses one draw per owned potion in slot order. Sunken Treasury and
Jungle Maze use primary then secondary draws; the other gold branches use one.
The session exposes the playable
`.state` after entry commits, plus
`status()`, `fork()`, `save_snapshot()`,
`Session.load_reference_nature_native_event_gold_snapshot()`, and `release()`.
Event selection and other requests keep their current providers. See the
[Native event-entry contract](../../docs/REFERENCE_NATURE_NATIVE_EVENT_GOLD_D3.md).

For an Endless Conveyor weighted dish roll, call
`State.begin_reference_nature_endless_conveyor_dish(action_id,
nature_seed=None)` with the stable legal action ID. On map entry, the session is
admitted only when ordinary event selection enters Endless Conveyor. During an
active event, it is admitted only when the action reaches exactly one weighted
ticket; preceding random effects keep their ordinary providers. Each session
resolves one ticket and commits it with the Nature cursor. Forced fifth dishes
and actions that do not roll a dish remain outside this session. Fork,
save/restore, and release are available. See the
[Endless Conveyor contract](../../docs/REFERENCE_NATURE_ENDLESS_CONVEYOR_D3.md).
For Battleworn, pass `site_id="event.battleworn.upgrade.shuffle_swap_index"`
and the stable `action_id` of a legal combat action that wins setting 2 in one
step. For Fake Merchant, pass
`site_id="event.fake_merchant.relic_offer.shuffle_swap_index"` and the stable
`action_id` of a legal Event-node action; the session is admitted when ordinary
event selection resolves that action to Fake Merchant.

Reflections' `touch_mirror` action has an atomic continuation through
`State.begin_reference_nature_glory_reflections(nature_seed=None)`. It samples
the downgrade and upgrade card identities in source order, rebuilding the
eligible deck after each pick, and publishes `.state` after the ordinary event
action completes. The session supports `status()`, `resolve_next()`, `fork()`,
`save_snapshot()`, `release()`, and
`Session.load_reference_nature_glory_reflections_snapshot()`. Its two card-pick
site IDs are advertised under the `glory_reflections` session kind. Laws remain
`UNVERIFIED`; see the [Native event-context contract](../../docs/NATIVE_EVENT_CONTEXT_STOCHASTIC_CONTRACT.md).

Infested Automaton's `study` and `touch_core` offers each have a one-draw
continuation through `State.begin_reference_nature_infested_automaton(
action_id, nature_seed=None)`. Resolve the pending card with `resolve_next()`;
the committed session exposes `.state` and `.selected_card_id`, and supports
`status()`, `fork()`, `save_snapshot()`, `release()`, and
`Session.load_reference_nature_infested_automaton_snapshot()`. The law uses the
implemented Silent reward pool, so pool fidelity and Native parity remain open.
The site is advertised under the `infested_automaton` session kind, and direct
Chance Law queries require `action_id="study"` or `action_id="touch_core"`.
The law remains `UNVERIFIED`.

Punch Off's relic and potion rewards can be sampled atomically with
`State.begin_reference_nature_punch_off_rewards(action_id, nature_seed=None)`.
Pass a stable legal combat action ID that wins the event fight immediately.
The session samples relic then potion, scripts only those two reward requests
during replay, and publishes `.state` after both choices. It supports
`status()`, `resolve_next()`, `fork()`, `save_snapshot()`, `release()`, and
`Session.load_reference_nature_punch_off_reward_snapshot()`. The reward stream
owner remains a `player.rewards` hypothesis, so the laws and Native parity are
unverified. Both sites are advertised under `punch_off_reward`; direct queries
require the action ID, ordinal, and full prior index prefix.

Punch Off's two construct HP reductions can be sampled before combat with
`State.begin_reference_nature_punch_off_fight(action_id, nature_seed=None)`.
Begin on the configured event's Fight page with its stable legal fight action
ID. The session samples both CombatId-owned 2–9 offsets, replays the ordinary
event action after both picks, and publishes `.state` in combat. It supports
`status()`, `resolve_next()`, `fork()`, `save_snapshot()`, `release()`, and
`Session.load_reference_nature_punch_off_fight_snapshot()`. The two local
Native monster cursors stay at entry during Nature replay; the bounded-index
law and Native parity remain unverified. The site is advertised under
`punch_off_fight`; direct queries require the action ID, ordinal, and complete
preceding offset-index prefix.

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

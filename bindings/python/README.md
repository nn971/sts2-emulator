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

For configured Underdocks starts, pass `nature_seed` to `Session.reset(...)`
to seed the run-owned ReferenceNature cursor before the `start_run` action.
That startup samples the Underdocks boss-pool law and advances the cursor once;
the law remains `UNVERIFIED` and the default reset path is unchanged.

The `hello` handshake negotiates the current exact run-snapshot schema and its
supported load schemas. Run snapshot save/load methods check each envelope
against that negotiation; current v4 servers write the composed
mechanics/provider envelope and continue to load legacy v1–v3 snapshots.

Pass `unlocked_colorless_epochs=["Colorless1Epoch", ...]` to `Session.reset(...)`
to override the fully unlocked profile's Colorless epoch set. An empty list
keeps other unlocks fully open while excluding epoch-gated Colorless cards.

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

For a run state with an idle Nature-owned combat deck, the exact joint law for
an ordered top-first prefix can be queried directly:

```python
distribution = state.query_deck_prefix_distribution(
    3, maximum_orders=4096)
```

The result groups physical card-instance prefixes by exact completion count
and carries its state fingerprint, policy, and `UNVERIFIED` provenance. This
Nature-only analysis is separate from the fair observation and is bounded by
the full-order cap. To audit the prefix counts, pass `audit_sequential=True`:

```python
distribution = state.query_deck_prefix_distribution(
    3, maximum_orders=4096, audit_sequential=True)
```

The audit limit is negotiated from `hello` (currently 64 draw positions). It
composes the `QueryDraw` support at each unresolved draw. A successful call
means the sequential support matched the full-order enumeration exactly. The
audit uses the same full-order cap and does not verify the original game's draw
law.

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
playable `.state` after its Nature choice commits. It covers supported run
start, Event-node entry, Shop generation and purchases, Treasure-node,
Shovel, and combat-entry continuations listed in the
[run-action contract](../../docs/CONTROLLED_RUN_ACTION_D3.md).

Supported event choices also have an atomic ReferenceNature session through
`State.begin_reference_nature_event_choice(choice_id, nature_seed=None)`. It keeps
event player actions and Nature choices in one replay order, including Stone
of All Time push's deck-card action before its ticket; the committed session
exposes `.state` for ordinary play. See the
[event-choice contract](../../docs/CONTROLLED_EVENT_CHOICE_D3.md).

Pael's Legion skin selection is available through those existing acquisition
boundaries: use the event-choice session for a supported event reward, or the
run-action session with the stable legal `buy_relic` action ID in a Shop. It
samples one of the four skins with an `UNVERIFIED` index law and saves the
result on the acquired relic. The direct query takes its relic-instance owner
ID:

```python
law = state.query_chance_law(
    "relic.paels_legion.skin.index_draw",
    owner_id="relic:paels_legion:instance:0",
    ordinal=0,
)
```

The emulator derives the skin RNG from the reward-stream snapshot and relic
slot. That seed derivation remains unverified.

Combat gold has a read-only Native law over the inclusive reward amounts:

```python
law = state.query_chance_law(
    "reward.combat_gold.index_draw",
    ordinal=declared_rewards_stream_ordinal,
    room="Combat",
    ascension=3,
    gold_proportion=0.8,
)
```

Declare `gold_proportion` for normal Combat; Elite and Boss use their
Ascension-adjusted fixed bounds. A zero Combat proportion means no draw occurs.
The law's exact Native bounded-index masses are `SOURCE_VERIFIED`; the query
does not validate earlier reward-generation calls or stream parity.

Configured combat potion presence has a read-only Native threshold law:

```python
law = state.query_chance_law(
    "reward.potion_odds.unit_float",
    ordinal=declared_rewards_stream_ordinal,
    current_thousandths=400,
    elite=False,
    forced=False,
)
```

The query conditions on the declared persistent pity value, Elite bonus, and
non-forced branch. It counts exact source-rounded `NextFloat()` grid inputs
below the source threshold. White Beast Statue forced rewards bypass this draw;
stream-prefix validation and cursor parity remain open.

Card-rarity thresholds can be queried for an explicit source and saved odds
state. The outcome is the raw rolled rarity before available-pool fallback:

```python
law = state.query_chance_law(
    "card_rarity.roll.unit_float",
    ordinal=declared_rewards_stream_ordinal,
    card_rarity_source="RegularEncounter",
    current_offset_basis_points=-500,
    ascension=7,
    owner_id="act:1:room:Combat:pick:0:offset:-500",
)
```

The law counts exact source-rounded Native `NextFloat()` masses. The query
does not validate the reward prefix, available card pool, stream assignment,
or cursor parity.

The combat-card upgrade check also has an exact inclusive-threshold law:

```python
upgrade_law = state.query_chance_law(
    "card_rarity.upgrade_roll.unit_float",
    ordinal=declared_rewards_stream_ordinal,
    card_id=native_silent_reward_card_id,
    act=1,
    ascension=0,
)
```

Outcomes describe the raw comparison flag before card mutation. At a zero
threshold the single `NextFloat()` grid input that rounds to `0.0` passes the
source's inclusive comparison. Card mutation and reward-prefix parity remain
outside this query.

PotionFactory rarity is also queryable from a declared Native request:

```python
potion_rarity_law = state.query_chance_law(
    "potion_factory.rarity_roll.unit_float",
    ordinal=declared_potion_factory_ordinal,
    owner_id=declared_potion_factory_owner,
    stream_id="player.rewards",
)
```

The law returns exact rounded-grid masses for Rare at `NextFloat() <= 0.1`,
Uncommon through `NextFloat() <= 0.35`, and Common otherwise. The source call
prefix and Native cursor parity remain open.

Relic rarity has a separate raw-threshold query on `player.rewards`:

```python
relic_rarity_law = state.query_chance_law(
    "relic_grab_bag.rarity.unit_float",
    ordinal=declared_rewards_stream_ordinal,
    owner_id=declared_relic_rarity_owner,
    stream_id="player.rewards",
)
```

It counts the strict source thresholds at 0.50 and 0.83 without exposing
remaining relic queues. The Treasure identity-mixture query is separate.

Encounter card-model selection also has a direct bounded-index law. Declare
the remaining support size, owner, and `player.rewards` ordinal:

```python
card_model_law = state.query_chance_law(
    "card_rarity.encounter_model.index_draw",
    owner_id="act:1:room:Combat:pick:0:rarity:Common",
    ordinal=declared_rewards_stream_ordinal,
    support_count=remaining_card_model_count,
)
```

The law gives exact Native index masses over that declared support. It does
not verify the source-derived card pool or reward prefix, stream assignment,
or cursor parity.

Native unique-monster HP queries bind the current ordered HP support to the
`run.niche` index law and require the creature CombatId owner:

```python
hp_law = state.query_chance_law(
    "combat.initial_unique_hp",
    owner_id="creature:7",
    ordinal=0,
    ordered_hp_support=[34, 35, 36],
)
```

The outcomes are support indices; map each index through the supplied HP
order. The query verifies the Native `NextInt` grid masses for that support.
It does not reconstruct prior-monster exclusions or verify the source pool.

Native relic-bag construction also exposes each Fisher–Yates swap law. Pass
the current exclusive bound, rarity-queue owner, and swap ordinal:

```python
queue_swap_law = state.query_chance_law(
    "relic_grab_bag.queue_shuffle.swap_index",
    owner_id="player:0:relic_grab_bag:Common",
    ordinal=0,
    support_count=current_queue_shuffle_bound,
)
```

This counts exact Native `NextInt(bound)` masses for the declared swap on
`run.treasure_room_relics`; it does not validate relic eligibility, the prior
shuffle prefix, full queue identity, or cursor parity.

The three-item boss relic offer exposes its Native shuffle swaps as well:

```python
boss_relic_swap_law = state.query_chance_law(
    "reward.boss_relic_offer.shuffle_swap_index",
    owner_id="act:1:floor:15:boss_relics",
    ordinal=0,
    support_count=current_boss_relic_shuffle_bound,
)
```

Each request reports exact Native bounded-index masses for the declared
`player.rewards` bound. The query does not validate the unowned relic pool,
earlier reward calls, or cursor parity.

Lava Rock's extra relic shuffle uses the same query shape with its own owner:

```python
lava_rock_swap_law = state.query_chance_law(
    "reward.lava_rock.relic_offer.shuffle_swap_index",
    owner_id="run:act:1:boss:lava_rock_extra_relics",
    ordinal=0,
    support_count=current_lava_rock_shuffle_bound,
)
```

Its exact Native index law does not check Lava Rock eligibility, the earlier
reward prefix, or cursor parity.

An individual Native draw-pile Fisher–Yates swap can be queried with the same
bounded-index shape:

```python
draw_shuffle_law = state.query_chance_law(
    "combat.draw_pile.shuffle.swap",
    owner_id="player:0",
    ordinal=0,
    support_count=current_draw_pile_shuffle_bound,
)
```

The law covers that `run.shuffle` bound only; it does not return the shuffled
pile or validate the full call prefix or cursor.

The supported end-turn recycle shuffle exposes its own per-swap law with the
same bounded-index parameters:

```python
recycle_swap_law = state.query_chance_law(
    "combat.draw_pile_recycle.swap_index",
    owner_id="player:0",
    ordinal=declared_recycle_swap_ordinal,
    support_count=current_recycle_swap_bound,
)
```

This counts exact Native `NextInt` masses for the declared `run.shuffle`
bound. It does not check the discard-pile identity/order, earlier draw calls,
legacy request law, or cursor parity.

Personal Hive's Dazed insertion position also has a bounded-index query. Pass
the source-specific insertion owner, current draw-pile length plus one, and
the stack's ordinal:

```python
personal_hive_insertion_law = state.query_chance_law(
    "combat.effect.personal_hive.dazed.random_insert_index",
    owner_id=declared_personal_hive_insertion_owner,
    ordinal=personal_hive_stack_ordinal,
    support_count=current_draw_pile_count + 1,
)
```

The law is exact when the request uses canonical Native `run.shuffle`. It does
not validate source stream assignment, pile identity/order, the powered-hit
prefix, legacy insertion law, or cursor parity.

The same query shape is available for standard generated-card insertion,
after-shuffle relic insertion, and enemy draw/discard insertion at their
respective stable site IDs.

The fixed Act 1 boss pools have read-only law queries. Each returns
equal-weight outcomes identified by encounter ID; legacy seed-to-index
distributions and Native cursor parity remain `UNVERIFIED`:

```python
law = state.query_chance_law(
    "run.encounter.overgrowth_boss.index_draw",
    ordinal=0,
    support_count=3,
)
law = state.query_chance_law(
    "combat.encounter.underdocks.boss.index_draw",
    ordinal=0,
    support_count=3,
)
```

The Overgrowth law complements its existing legacy run-start continuation.
The Underdocks query does not make configured run creation resumable.

Glory Ascension 10+ second-boss assignment has a conditional query. Declare
the first boss selected for this run and the call's ordinal on the legacy
`combat` stream. That ordinal includes the earlier queue draws from later-act
run initialization; the read-only query does not validate the complete prefix.
The law returns the two other bosses in source pool order:

```python
law = state.query_chance_law(
    "combat.encounter.later_act.second_boss.index_draw",
    ordinal=second_boss_combat_stream_ordinal,
    first_boss="proto.native.glory.encounter.queen_boss",
    ascension=10,
    support_count=2,
)
```

The second-boss draw still occurs during configured run initialization and
remains outside the ReferenceNature run-action session. Its declared
equal-index model is `UNVERIFIED` for the legacy seed-to-index distribution.

The initial Hive/Glory boss-pool index can also be queried by act. Supply its
declared ordinal on the legacy `combat` stream after queue preparation:

```python
law = state.query_chance_law(
    "combat.encounter.later_act.boss.index_draw",
    ordinal=initial_boss_combat_stream_ordinal,
    act=2,
    support_count=3,
)
```

This law describes the raw pool index before the first-undiscovered-boss
preference may replace the selected boss. The equal-index model is
`UNVERIFIED`, and later-act encounter preparation remains source-owned.

Hive and Glory map path-start calls have a read-only Native law. Supply the
act and the site's actual call ordinal; the query describes the raw column
draw before the source retry constraint on the second path:

```python
law = state.query_chance_law(
    "map.later_act.path_start_column.index_draw",
    ordinal=path_start_site_ordinal,
    act=2,
    support_count=7,
)
```

Each query returns exact Native `NextInt(7)` weights for columns 0 through 6.
It does not validate earlier path starts or the complete map-generation prefix.
For Act 1 Overgrowth, declare the path/retry owner ID instead:

```python
law = state.query_chance_law(
    "map.overgrowth.path_start_column.index_draw",
    owner_id="act:1:path:0:start",
    ordinal=0,
    support_count=7,
)
```

Native map delta-shuffle queries report each raw Fisher-Yates swap index.
Three-entry delta pools use bound 3 then bound 2 at successive ordinals:

```python
first_swap = state.query_chance_law(
    "map.later_act.path_delta.shuffle_swap_index",
    ordinal=0,
    act=2,
    support_count=3,
)
second_swap = state.query_chance_law(
    "map.later_act.path_delta.shuffle_swap_index",
    ordinal=1,
    act=2,
    support_count=2,
)
```

For Act 1 Overgrowth, declare the path/floor owner, for example
`owner_id="act:1:path:0:floor:1:deltas"`. These laws describe the swap
indices; they do not validate or return the final shuffled path deltas.

Room-assignment shuffle queries require the current `support_count` for the
swap. Act 1 also requires its source owner ID, which fingerprints the room,
attempt, and ordered pool; later-act ownership is selected by `act`:

```python
law = state.query_chance_law(
    "map.later_act.room_assignment.shuffle_swap_index",
    ordinal=room_assignment_swap_ordinal,
    act=2,
    support_count=current_swap_bound,
)
```

The query returns exact Native `NextInt(current_swap_bound)` grid masses for
the raw index. It does not read or validate the current room pool or map state.

Glory's later-act map rest-count draw has a separate read-only Native law:

```python
law = state.query_chance_law(
    "map.later_act.glory_rest_count.index_draw",
    ordinal=0,
    support_count=2,
)
```

The exact Native `NextInt(2)` law returns rest quotas 5 and 6 and is
`SOURCE_VERIFIED` under the uniform 53-bit input-grid model. The query does
not read the map state, seed, or cursor, and does not make map generation
resumable.

Later-act Ancient assignment has read-only conditional laws for the local
Ancient choice and shared Darv partition. Supply each call's declared ordinal
on the legacy `event` stream and declare every earlier condition. These
ordinals depend on the complete run-start prefix and are not inferred or
validated by the query:

```python
hive_darv = state.query_chance_law(
    "event.later_act.darv_assignment.hive.index_draw",
    ordinal=hive_darv_event_stream_ordinal,
    support_count=2,
)
glory_darv = state.query_chance_law(
    "event.later_act.darv_assignment.glory.index_draw",
    ordinal=glory_darv_event_stream_ordinal,
    hive_receives_darv=False,
    support_count=2,
)
ancient = state.query_chance_law(
    "event.later_act.ancient.index_draw",
    ordinal=hive_ancient_event_stream_ordinal,
    act=2,
    include_darv=False,
    support_count=3,
)
```

These laws expose the declared ordered pools and bounded assignments, but
remain `UNVERIFIED`. They do not provide an atomic run-initialization
continuation or validate the caller-supplied assignment prefix.

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

The same session also accepts a legal combat victory that returns cards from
Pael's Tooth. Pass `site_id="relic.paels_tooth.card_return.index_draw"` and
the legal victory `action_id`. Each nonempty, non-melted Tooth queue is
sampled in relic-slot order with an `UNVERIFIED` legacy `reward` index law.
Unrelated reward draws before or between returns make the action ineligible;
later ordinary reward generation keeps its legacy provider. The law can be
queried directly with the same action ID and a complete `prior_indices`
prefix.

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

For a supported configured Native event choice with random relic effects or
Toy Box acquisition, or Round Tea Party's `continue_fight` or Trial's
`merchant_guilty` relic reward, call
`State.begin_reference_nature_native_event_relic(choice_id, nature_seed=None)`
with its legal choice ID. The session resolves each random relic rarity
request and eligible identity request, plus the five Wax relic draws triggered
by each Toy Box, then publishes the event result and Nature cursor together. A relic with no eligible
bag identity consumes its rarity request and then follows the source's no-relic
result. Choices with another fixed relic effect or an unrelated stochastic
site remain unsupported. Use
`resolve_next()`, `status()`, `fork()`, `save_snapshot()`,
`Session.load_reference_nature_native_event_relic_snapshot()`, and
`release()` to manage the continuation. The rarity thresholds and bounded
index draws use configured Native laws; relic identity pools remain
`UNVERIFIED`. See the [Native event relic contract](../../docs/REFERENCE_NATURE_NATIVE_EVENT_RELIC_D3.md).

For a Large Capsule offered by Native Neow's Bones, call
`State.begin_reference_nature_native_neow_relic_pickup(action_id, nature_seed=None)`
with the current legal relic-pickup action ID. The session resolves the two
random relic rarity requests and their eligible identity requests triggered by
Large Capsule, then commits the pickup and Nature cursor together. Other Neow
relic-pickup effects remain outside this boundary.

For Battleworn Dummy's setting-three reward, call
`State.begin_reference_nature_native_battleworn_relic_reward(action_id, nature_seed=None)`
with the current legal combat action that wins the fight. Replay must reach the
setting-three relic draw; unrelated stochastic requests make the action
ineligible. The action, reward, event result, and Nature cursor commit
together.

For Pael's Wing, call
`State.begin_reference_nature_native_pael_wing_relic_reward(action_id, nature_seed=None)`
with the current legal `sacrifice_reward_card` or
`sacrifice_reward_card_group` action that triggers its every-other-sacrifice
relic draw. The session commits the sacrifice, relic acquisition, and Nature
cursor together.

For a configured Native Rest Shovel dig, call
`State.begin_reference_nature_native_shovel(nature_seed=None)`. The session
resolves one rarity and one relic identity request, then commits the ordinary
Reward and Nature cursor together. Use `resolve_next()`, `status()`, `fork()`,
`save_snapshot()`, `Session.load_reference_nature_native_shovel_snapshot()`,
and `release()` to manage it. The declared Native threshold and bounded-index
transforms are exact; source-pool fidelity and Native cursor parity remain
open. See the [Native Shovel contract](../../docs/REFERENCE_NATURE_NATIVE_SHOVEL_D3.md).

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

Configured Native combat rewards can be sampled with
`State.begin_reference_nature_combat_reward(action_id, nature_seed=None)`.
Begin from a current legal combat action that immediately wins and generates a
complete Reward. Resolve its pending reward Chance Laws with `resolve_next()`;
the session's `.state` becomes available after the full reward and Nature
cursor commit. It supports `status()`, `fork()`, `save_snapshot()`, and
`release()`, with restore through
`Session.load_reference_nature_combat_reward_snapshot()`. The session site
coverage is negotiated as `combat_reward`; its scope and Native source limits
are described in the
[`ReferenceNature combat reward contract`](../../docs/REFERENCE_NATURE_COMBAT_REWARD_D3.md).

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
payload and verifies that the response matches the requested session. Session
startup rejects inconsistent stochastic manifests or session/query coverage;
the returned view is checked against its Chance Law identity, support and
conditioning fingerprints, probability metadata, and outcome weights. Use
`State.observe()` separately for the fair player view.

The negotiated `Session.observe_fair(view_kind, handle)` returns one common
fair observation frame for `run_state`, `controlled_combat`, `semantic_combat`,
and `semantic_reference` handles. It verifies the envelope schema, policy,
handle, observation schema and hash, and unique legal action IDs. The existing
`State.observe()`, `ControlledCombat.observe()`, `SemanticCombat.observe()`, and
`SemanticReferenceCombat.observe()` methods use this frame when the server
advertises it, and fall back to their existing operations with older compatible
servers. Clients should accept additional advertised view kinds while rejecting
unknown envelope schemas.

Replay semantic combat with the ReferenceNature sampler using
`Session.begin_semantic_reference_combat(combat, nature_seed=None)`. The returned
`SemanticReferenceCombat` exposes the shared fair observation and action menu,
the separate Nature-only `observe_nature_choice()`, legal player `step()`,
automatic `resolve_next()` for pending Nature events, `status()`, `fork()`,
`save_snapshot()`, and `release()`. Restore a saved session with
`Session.load_semantic_reference_snapshot()`. Session startup checks that the
negotiated ReferenceNature manifest includes every operation required by this
wrapper.

`State.query_chance_law()` checks the advertised query site and validates the
returned Chance Law schema, negotiated probability status, unique positive
outcomes, total weight, requested site and ordinal, support fingerprint, and
law identifier before returning it.

All mechanics remain in C#. Search, learning and strategy belong in the parent
AI repository. See [the versioned contract](../../docs/V111_INTERFACE.md).

Whole-run scripted replay is available through
`State.begin_scripted_run()`. At each player boundary, call `begin_action()`
with a current legal action ID. When a typed random request is pending, pass
its next ordered journal entry to `resolve_next(transcript_entry,
candidate_labels=None)`. The session retains the last committed run state
while an action is suspended and supports `status()`, `fork()`,
`save_snapshot()`, and `release()`. Restore with
`Session.load_scripted_run_snapshot(snapshot)`. See the
[`ScriptedRunSessionV111` contract](../../docs/SCRIPTED_RUN_SESSION_D3.md)
for wire fields and replay limits.

# D3 Nature combat-entry continuation: HP → committed intents

**Scope:** pinned v0.111.0, single player, isolated Hive/Glory
source-shaped combat-entry fixtures. This is a stacked change on
[desired RNG D1/D2](DESIRED_RNG_MIGRATION_STATUS.md) and
[initial HP pending choices](DESIRED_RNG_D3_PENDING_HP.md).

## Mechanics

`PrototypeGameEngine.EvaluateEnemyIntentChoice` is the SAME pure move
eligibility/state-machine selector invoked by native `CommitEnemyIntents`.
It evaluates opening sequences, slot aliases, nonrepeat moves, weighted
branch tickets, and conditional AI transitions **without game-state
mutation or side effects**.

`ControlledIntentsV111.Begin` receives a native HP-prepared combat,
copies its creature formation and independent streams, and drops the
native-owned RNG sidecar in the counterfactual continuation. It runs
deterministic intent selection until the first actual `run.monster_ai`
bounded-integer request. The pending request contains precise
`SiteId`, `StreamId`, CombatId owner, ordinal, and bounded ticket
range. Some AI states consume zero random draws.

`Resolve(state, fingerprint, index)` checks the stale fingerprint,
recorded choices, stream/owner/order and recomputed source eligibility.
Previously committed enemies are kept in `Combat`; for a partially
resolved *current* enemy, only the **pure selector** is re-evaluated
with its validated ticket prefix. The resulting plan is committed
only when that enemy's selector reaches a concrete move. Hooks and
whole combat actions are never re-executed. The next pending site
is derived from the updated partial formation.

`Save`, `Restore`, and `Fork` preserve the typed combat, baseline
pre-intent state, source formation, reference streams, next enemy
index, pending operation, and complete applied ticket history.
Restore regenerates the entire supported choice prefix from the
baseline and rejects unreachable state, wrong owner/range, or
tampered committed enemy plan. Native source-owned RNG cursors
are never advanced in controlled mode.

`BeginAfterControlledHp(hp)` accepts a **completed and verified**
`ControlledInitialHpStateV111`, preserves all HP outcomes as
`PriorHpTranscript`, and starts intent commitment from those
actual counterfactual HPs, with no repeated HP roll.
On restore, the HP prefix is revalidated against source eligible
HP pools. This provides a two-stage serializable example of
the same mechanics under externally supplied chance outcomes.

## Opt-in JSONL operations

The existing `prototype-ai-jsonl` server is unchanged for standard
`reset_v111`, `step`, `observe`, `save_snapshot`, and legacy
training. `hello` advertises:

```json
{
  "natureBoundarySchemaId": "sts2-v111-nature-isolated-boundary-v1",
  "natureBoundaryScope": "isolated-combat-entry-only",
  "natureBoundaryKinds": ["initial_hp", "intents"]
}
```

Requests use JSONL (one object per line). Each Nature operation
returns a distinct `natureHandle`, never a playable
`stateHandle`. All `nature_resolve` calls create a **child** handle
and leave the parent available for branching.

```json
{"op":"nature_begin","kind":"initial_hp","seed":"trial","act":2,"encounter_id":"proto.native.hive.encounter.exoskeletons_normal","native_entry":"ExoskeletonsNormal","total_floor":20,"map_column":3,"map_row":5}
```

This creates a **pre-roll** native formation fixture (not a post-hoc
modification of already rolled monster HP). To start at committed
intents from an ordinary native-seeded HP state, use the same operation
with `"kind":"intents"`. The latter path still does NOT advance
`run.monster_ai` in controlled mode.

```json
{"op":"nature_resolve","nature_handle":"n1","fingerprint":"<exact fingerprint returned by server>","selected_index":0}
```

The response supplies the new `natureHandle`, `fingerprint`,
`pending` (or `null` when complete), and `complete`.
`pending` uses `operationKind=choose_index` with ordered
`orderedCandidates` for HP, or `operationKind=bounded_integer`
with `boundedCount` for AI weighted tickets. Only indices
within support are legal. The stored event retains its compatibility
`UNVERIFIED` status. Its Chance Law now counts exact Native `NextInt` 53-bit
grid masses through the validated weighted ticket intervals and reports
`SOURCE_VERIFIED` for that transform on the declared support. This does not
establish player-information conditional probabilities, source-pool
completeness, or Native stream/cursor parity.

Further operations:

- `nature_fork`: `{"op":"nature_fork","nature_handle":"n1"}`.
- `nature_save`: `{"op":"nature_save","nature_handle":"n1"}`;
  returns `kind` and opaque exact `snapshot` string.
- `nature_load`: `{"op":"nature_load","kind":"initial_hp","snapshot":"..."}`.
  `kind=intents` accepts completed or pending intent snapshots.
- `nature_advance`: `{"op":"nature_advance","nature_handle":"n1"}`
  on a **completed HP** handle yields an intent-continuation child.
  Does not reroll HP, and carries all previous HP selections forward.

Protocol errors (stale fingerprint, unsupported type/act, illegal
index, malformed snapshot, unresolved HP stage) return `ok=false`
without publishing a child or changing a parent.

## Validation

- C# unit tests: source-shaped native choices replay via controlled
  HP and then intent boundaries with identical chosen enemy HPs,
  planned moves and next AI states, plus deep forks and save/restore.
- Native seeded mode: original exact `run.niche` /
  `run.monster_ai` cursor and combat hashes remain covered
  by existing v111 tests.
- Integration: `python tools/nature_jsonl_smoke.py` runs the
  actual .NET JSONL server, exercises negative paths, fork, save,
  load, resolution and HP-to-intent composition.
- Existing game-facing/native Inspector remains independent and
  is not modified.

## Limitations / next milestones

This is **not** the full `IDeterministicEngine.Step` continuation.
It is not yet integrated into ordinary complete runs or exposed through
a `sts2-ai` Python binding. Mid-card RNG, nested hooks, summoning,
shuffling, generated cards, map/events, etc., are unsupported controlled
sites. Actual enemy execution and the player legal-action boundary after
the intro must be wired in before Nature can train against whole
episodes.

The isolated-fixture nature protocol is an architectural acceptance
test, **not** a claim of full STS2 native parity, correct conditional
randomness, or complete observable training state.

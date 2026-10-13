# Source shuffle → actual engine Step replay (v0.111)

**Branch:** `integration/fidelity-rng-on-silent-all-act-v111`, isolated
[PR #137](https://github.com/nn971/sts2-emulator/pull/137).

## Implemented

`NativeShuffleStepReplayV111` wraps the **existing**
`PrototypeGameEngine.Step` in an opt-in diagnostic context. The ordinary
player-training path uses its existing `PrototypeRng.Shuffle` implementation
when the scope is absent. The engine's discard-to-draw recycling sites in
`DrawCards` and `AutoPlayTopDrawCards` call the shared seam.

Inside a sourced replay:

1. Check the engine's actually eligible card identities against the
   independent native-to-emulator mapping. A mismatch is an explicit
   `SOURCE_SHUFFLE_ELIGIBLE_CARD_IDENTITIES_MISMATCH`, including both orders.
2. Use the independently supplied native **input order**, run the exact
   `NativeFisherYatesV111.Execute` swaps through a
   `ScriptedRandomIndexProviderV111`, and require every source choice
   to be consumed.
3. Compare the computed **pre-hook** order against the source-observed native
   pre-hook order.
4. Run the emulator's **existing** `ApplyPerfectFitShuffleOrder` and compare
   its unmodified result with the source-observed native **post-hook** order.
5. Continue the actual engine step and report the final successor's phase,
   decision index, HP and card piles, with `NativeGameParity=NOT_TESTED`
   and `ExactNativeRngParity=NOT_TESTED`.

The source-native origin is checked by
`NativeShuffleSourceImportV111.Import`. Each source-card `card:N` token
must have an explicitly provided, one-to-one mapping to the emulator's
`CombatCardInstance.InstanceId`; duplicated card **models** are allowed,
duplicated card **instances** are rejected. Native card model IDs are checked
against the pinned `V111Coverage` inventory and actual emulator cards,
and all eligible cards must exist in the emulator pre-state. The importer
independently recomputes every native xoshiro256** RNG outcome and verifies
the entire pre/post cursor chain. Source `initial` shuffles are **not**
misclassified as in-combat discard recycling.

A synthetic end-turn test exercises the **real Step**, including an enemy
creating an extra card before the recycle. This demonstrated why the shuffle
source pool must be observed at the actual recycle boundary, not guessed
from the state at turn start. The separate engine-only eligibility probe
is explicitly labeled **synthetic diagnostic evidence**.

## Fixture contract

The command is:

```fish
dotnet run --project src/Sts2Emulator.Cli -- \\
    native-shuffle-step-fidelity /path/to/one-source-step-fixture.json \\
    --out /path/to/one-source-step-report.json
```

Provide a JSON file with this structure (the objects in parentheses
must be replaced by complete real records, not strings):

```json
{
  "schema": "native-shuffle-step-fixture-v1",
  "emulator_before_state": {"GameBuild": "...", "Phase": "Combat"},
  "emulator_action": {"Kind": "end_turn", "Payload": {}},
  "native_source_shuffles": [
    {
      "type": "native_shuffle_operation",
      "data": {
        "source_version": "v0.111.0",
        "kind": "recycle",
        "operation_id": 1,
        "card_identity_scope": "shuffle_operation_local",
        "hook_order_stage": "modify_shuffle_order_completed",
        "ordered_input": ["card:0", "card:1"],
        "card_models": ["STRIKE_SILENT", "DEFEND_SILENT"],
        "choices": [
          {
            "bounded_count": 2,
            "selected_index": 1,
            "cursor_before": {"Counter": 1, "State0": 1, "State1": 2, "State2": 3, "State3": 4},
            "cursor_after": {"Counter": 2, "State0": 5, "State1": 6, "State2": 7, "State3": 8}
          }
        ],
        "pre_hook_order": ["card:0", "card:1"],
        "post_hook_order": ["card:0", "card:1"]
      }
    }
  ],
  "native_to_emulator_card_tokens": [
    {"card:0": 12, "card:1": 16}
  ]
}
```

**The JSON above is schematic and deliberately not an executable passing
fixture**: `emulator_before_state` must contain a fully serialized
`RunState`, complete `World.Combat`, real per-card identities, real source
ordered candidate lists and the exact valid 256-bit xoshiro cursor words
from the source record. The example's numeric cursor placeholders must be
replaced with original native observations. Its purpose is to document the
fields and strict provenance requirements. For every shuffle operation,
provide its corresponding independent one-to-one token map.

Return codes: 0 = all **observed shuffle stages** agree under scripted
replay; 1 = independently mapped source shuffle stage mismatch; 2 =
unsupported/incomplete source, missing identity maps or engine execution
blockers. The result's native whole-game parity remains `NOT_TESTED`
in every classification.

## Important remaining work

The current saved Shrinker Beetle capture predates the source shuffle
observer and therefore contains 18 audited `run.shuffle` primitive
outcomes but **no complete** `native_shuffle_operation` with original
ordered cards and hook output. It cannot be converted to this fixture
without additional actual-native source evidence.

A fully independent `emulator_before_state` importer remains incomplete.
It needs native ordered creature identities, all card combat instance IDs,
native powers/relics/potions, turn/history continuations and trustworthy
actual RNG cursors. New cards generated *during the same Step* may appear
in the shuffle source pool despite not existing in its beginning state;
the current strict importer stops on those cases. The longer-term
engine importer must reconstruct those generated cards at the correct
mechanic boundaries, **without** injecting native post-state into the engine.

Native source-only initial shuffles and HP rolls have their own offline
verification contracts and are not fabricated into a later in-combat
`Step`. The all-Act feature branch has continued advancing; the isolated
fidelity branch preserves its pinned baseline to keep active game
development untouched until a reviewed source reconciliation.

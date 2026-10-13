# AI training integration — versioned emulator handoff

Scope: single-player Silent, native-structure Act 1 Overgrowth under the existing
`prototype-native-overgrowth-reset-v1` JSONL contract. The emulator does **not**
train agents and the bridge does **not** expose future RNG, map events or hidden intents.

## Canonical training integration

The active development branch is `prototype/full-run-silent`. The original
`sts2-ai/main` gitlink, at preparation time, was
`3e897c80c4c694a8c4ae75ecdbfb98e6fc15eb56`; that legacy revision
continues to work and remains the old experiment's reproducible baseline.
To use this updated mechanics version, update the *AI repository's pinned
gitlink on a new branch*, not the untracked contents of its `emulator/` checkout.

This development line integrates these mechanical changes:

- all 53 solo colorless cards, and 12 explicitly unsupported multiplayer-only cards;
- enemy move selection **before** the player makes a combat decision,
  including subsequent turn-boundary commitments;
- `move_id` and optional `intent_base_damage`, `intent_damage`,
  `intent_hits` in public enemy observations, without future RNG or
  Runic Dome leaks;
- current damage reflecting Strength/Weak/Vulnerable, stored scripted
  damage and escalating prior move-use modifiers;
- scripted last-stand overrides replacing previously announced intent.

The bridge keeps `prototype-ai-jsonl-v0`, `prototype-ai-v0`,
`prototype-fair-v0` and `prototype-native-overgrowth-reset-v1` unchanged,
but announces **`publicEnemyIntentId`** =
`prototype-committed-public-enemy-intents-v1` in `hello`. Consumers
that require announced damage should verify that capability before training.

### Local cross-repository smoke (fish, from sts2-ai root)

```fish
git pull --ff-only
git submodule update --init --recursive
git -C emulator rev-parse HEAD
python tools/smoke_native_overgrowth.py --build --runs 8 \
  --max-decisions 4096 --report results/native-intents-smoke.json
```

The submodule hash **must** match the AI gitlink committed for that experiment.
`sts2-ai/src/sts2_ai/emulator/jsonl_backend.py` enforces this check and
rejects an unrelated checkout. Run the parent repository tests and a
small phase-split or REINFORCE pilot before initiating a large workload.

### Experiment reproducibility and checkpoint safety

Treat this emulator as a **new training environment version** even though
the wire DTO/schema IDs are backward compatible. Move-selection order and
RNG consumption changed. In particular:

1. Do **not** resume optimizer/model checkpoints or mix experience data
   collected against the previous submodule pin.
2. Keep old and new model artifacts in different checkpoint/report files
   and use the same held-out evaluation seeds only for a controlled
   paired comparison of policy performance.
3. Preserve `emulator_revision`, AI git SHA, model/feature schema,
   policy and seed list in every report.
4. There is **no** native RNG parity guarantee. The current six synthetic
   streams and Act 2/3 prototype geometry are still deliberate
   approximations; native Overgrowth starts with an Act 1 16-floor map.

### Validation standard

Emulator CI runs C# unit tests, 500-seed prototype diversity, 64-seed
Overgrowth audit, native-shaped Overgrowth smoke, and real JSONL bridge
contract checks. A green emulator build **does not** establish that a
new `sts2-ai` model/featurizer expects or uses the optional intent
fields: test that in an AI-side pinned-submodule pilot.

For the isolated original implementation of intent timing see
`docs/PUBLIC_VISIBLE_ENEMY_INTENTS.md`. Both the previous isolated
branch and its outdated base were superseded by the integrated
training-ready development line.

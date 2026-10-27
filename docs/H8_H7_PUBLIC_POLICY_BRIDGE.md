# H8 ↔ H7 public observation/action and reference-law bridge

Status: research PR #211 stacked on consolidated H8 #210.
The Python H7 Transformer integration uses AI PR #120.
This is a **staged integration**, not a completed PPO backend.

## Source-public H7 policy observations

`H8CombatRuntimeV1.ObservePlayerFrame()` uses the existing
`PrototypeAiEnvironment.Observe` on the live H8 mechanics `RunState`.
The version-checked `h8_observe` JSONL endpoint returns exactly the
legacy source-fair `payloadJson`, `observationHash` and
`ActionWire` legal-action metadata. Player observations never contain
`SemanticDeck` or deferred Nature support. The public frame is cached
**once per committed H8 boundary**, shared by status menu queries and
policy inference, and invalidated on `Commit`. Forks do not share
projected frames.

The H8 native end-turn path and direct physical automatic draw remain
active; other source-legal card and potion actions invoke the checked
v111 deterministic mechanics fallback. Nested
`player_card_selection` and `draw_hook_action_choice` expose the
*same* semantic pending action support as the H7 source-fair
`fair_player_choice_actions` filter; unknown other nested choices
are explicitly flagged unsupported.

`h8_begin_seeded` additionally accepts `fixture_deck_preset_id`
with the source's existing six Silent deck presets. Each research A10
scenario/deck cell is fixed before its stochastic opening state is
created; no pseudo gameplay content is injected by Python.

## Nature interchangeability and exact H7 Chance Law

`h8_status` exposes compact, source-derived weighted semantic
enemy-intent support and ordered eligible physical card instance IDs.
This is enough to sample the correct **distribution** in a small fast
pilot, with all stochastic state private to the Nature sampler.

The optional `h8_reference_law` JSONL endpoint, by contrast, queries
the **unchanged v111 declared Chance Law** for the currently pending
intent/draw event. It requires an exact H8 boundary token and uses a
defensive legacy snapshot *only on explicit request*. H7's existing
Python `DeclaredLaw.from_pending` + `SeededLawSampler` can then
sample from the identical source event IDs, conditioning fingerprints,
outcome IDs and weights, rather than approximating the reference
distribution by a sequential PRNG.

This is an interim **correctness bridge**, with extra projection,
canonical hashing and JSON overhead. It is **not** the intended fast
H8 typed stochastic API. Preserve the semantic site/order and
interchangeable provider contract while replacing the v111 projection
with a compact typed Chance Law request and directly owned provider
state. Keep explicit replay audit/checkpoints outside the hot path.

## Verified gates

- Exact C# parity of the public H7 observation and full legal action
  list on real source-valid Silent A10 combat.
- Live Python JSONL boundary and stale/foreign token checks.
- Separate real H7 14.66M-parameter shared-inference AI pilot on the
  16 curated source cells: [AI PR #120](https://github.com/nn971/sts2-ai/pull/120).
- The existing H8 paired state/journal/replay tests remain required.
- The eventual complete PPO benchmark must match the *same* H7 event
  reference law, source/actor seeds, public action menus, terminal
  outcomes and behavior log-probabilities before any `sts2-ai` pin
  update.

## Local commands (fish)

```fish
cd ~/projects/sts2-emulator
git fetch origin
git switch research/h8-h7-public-policy-bridge-20261028
dotnet build Sts2Emulator.sln -c Release -m:1
dotnet test Sts2Emulator.sln -c Release --no-build -m:1 \
  --filter 'FullyQualifiedName~H8CombatRuntimeV1Tests'
```

Then use the actual 15-worker/128-cell shared CUDA command in
`sts2-ai/docs/H8_H7_TRANSFORMER_PILOT.md`. No worktree/submodule
mutation is required.

# Local combat-stream conditional replay (v1)

**Status:** new experimental, standalone hypothesis-generation primitive for
the prototype emulator. The operation is **not** a native STS2 full-history
posterior, and must not be registered as a certified fair RL root sampler.

## Why this is different from draw-order injection

The prototype RNG uses named stateful SplitMix64 streams. In particular, the
same `combat` stream is used for encounter selection, enemy HP rolls, opening
draw-pile Fisher–Yates shuffle, enemy decision randomness, generated-card
selection and later discard reshuffles. Reordering the remaining draw pile in
an already realized combat while leaving the stream untouched changes the
joint law, even if the marginal card probabilities look correct.

`PrototypeConditionedCombatEntry.Sample` instead starts at a **hypothetical
map-choice** state, replaces its combat RNG stream with a deterministically
derived *independent search-side* stream for each candidate, and executes the
normal chosen room-entry action from start to finish. It accepts a candidate
only if the resulting **entire public observation hash and ordered legal
action IDs** equal those provided by the caller. The accepted successor
retains the exact combat RNG stream state after all RNG events consumed during
room entry. Subsequent combat actions run through normal engine rules and
continue from that correlated stream cursor.

Given an explicit prior over those synthetic pre-entry streams and fixed
source state, the accepted candidates implement rejection conditioning under
that **local stream prior**. The finite candidate cap causes an explicit error
instead of an unrelated substitute or simulated defeat.

## JSONL request

```json
{
  "op": "condition_combat_entry",
  "state_handle": "synthetic-map-state",
  "action_id": "choose_map_node:...",
  "expected_observation_hash": "public-frame-observation-hash",
  "expected_legal_action_ids": ["play_card:...", "end_turn:..."],
  "search_seed": "0000000000000000000000000000002a",
  "max_candidates": 1024
}
```

The required `state_handle` must originate from the bridge's separately
marked `reset_hypothetical` lineage. An ordinary `reset` handle is refused.
The `action_id` must be a legal map-choice action entering combat, the expected
hash and legal menu must be nonempty, and the search seed must be a 32-character
hex value. The `max_candidates` budget is bounded to 1–16384.

Successful response fields: `child`, `schemaId` equal to
`prototype-local-combat-stream-condition-v1`, `policyId`,
`observationHash`, and `candidates`. No private state, shuffle permutation,
or raw stream cursor is returned. All unsuccessful candidates are temporary
in-process values, with no published handles.

## Exactness boundary

This method **does preserve** dependencies between *the candidate's opening
shuffle*, its other room-entry `combat`-stream RNG events, and all later
uses of that stream. It preserves the fixed hypothetical map state and does
not mutate its source.

It **does not preserve or infer** correlation with:
- events that consumed `combat` RNG **before the map-choice boundary**,
  such as preselected Act 1 boss encounter identity or previous combats;
- other hidden streams, notably `map`, `event`, `reward`,
  `combat_targets` and any historical evidence that conditions those;
- an actual live run's hidden seed; the caller supplies independent research
  seeds and *only* public observations/actions.

The synthetic seed derivation `local-combat-condition-v1:<seed>:<trial>`
passes through SHA-256 to a 64-bit SplitMix64 stream state. The resulting
distribution is an explicitly defined search-side *experimental model*,
not a demonstrated exact native seed prior nor a mathematically independent
ideal source for all finite candidate pairs.

It is consequently incorrect to claim the resulting trajectory is sampled
from the exact game's posterior, or to use these outcomes directly as
full-game fair stochastic PUCT training/variance labels.

## Tests

`PrototypeConditionedCombatEntryTests` creates a deterministic accepted
candidate and verifies the resulting complete state, RNG cursor and next-turn
successor exactly match normal engine replay. Other tests verify invalid
inputs, exhaustion and immutable source. A JSONL smoke test checks that live
handles are refused and no child handle leaks on rejection.

## Next work

Integrate the versioned operation as a research-only optional Python
backend capability, with a clear statistical-regime label. To achieve
actual full-run fairness, the solver must reconstruct the posterior of
**all correlated hidden state** conditional on the full public transcript,
or introduce a validated explicit random-event model with appropriate
independence assumptions and replay semantics. This local kernel is one
controlled reference point for that work.

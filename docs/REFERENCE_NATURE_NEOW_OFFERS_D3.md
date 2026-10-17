# ReferenceNature Neow-offer session

`ReferenceNatureNeowOfferSessionV111` provides an atomic, replayable
continuation for the emulator's existing Act 1 Neow initial-option generator.
It samples the cursed relic, the conditional Lava Rock pair, the Oyster and
Talisman pairs, and each descending Fisher–Yates swap of the positive pool.
The Lava Rock pair is skipped for Large Capsule. Pair suppression and the
current single-player pool filters use the existing generator model.

The session starts from a pristine `PrototypeGameFactory.Create` run-start
state, before `start_run` or Native Neow offers exist. Each pending Chance Law
conditions on the complete selected-index prefix and, during the shuffle, the
current positive-pool order. After the last swap, the saved index transcript
is replayed through `NativeNeowEventV111.GenerateInitialOptions` using the
existing `ScriptedRandomIndexProviderV111`. The ordinary run and updated
ReferenceNature sampler cursor are published together. The scripted provider
does not advance Neow's Native event cursor; the completed EventState retains
the existing provider-mode marker and compatibility shape.

## JSONL operations

| Operation | Inputs | Result |
| --- | --- | --- |
| `chance_law_query` for a Neow site | pristine `state_handle`, `site_id`, global `ordinal`, and all prior `prior_indices` | Current conditional offer or shuffle-index law |
| `reference_nature_neow_offer_begin` | pristine `state_handle`, required `nature_seed` | Session before the first Neow decision |
| `reference_nature_neow_offer_status` | session handle | Current ordinal and Nature-choice view, or committed offers |
| `reference_nature_neow_offer_resolve_next` | handle and current `fingerprint` | Child after one sampled decision; commits after the final shuffle swap |
| `reference_nature_neow_offer_fork` | session handle | Independent child session |
| `reference_nature_neow_offer_save` / `reference_nature_neow_offer_load` | handle / snapshot | Replay-validated session snapshot and restored handle |
| `reference_nature_neow_offer_release_many` | `reference_nature_neow_offer_handles` | Number of released sessions |

`ordinal` is the total number of prior Neow decisions, not the per-site
request ordinal. `prior_indices` contains one selected request index for each
earlier decision in transcript order. The session status exposes the next
site and Chance Law directly. Query and session sites appear in the separate
Chance Law query and ReferenceNature session coverage lists.

## Python binding

Start from a pristine run-start `State`:

```python
session = state.begin_reference_nature_neow_offer(nature_seed=123)
while not session.complete:
    session = session.resolve_next()
neow_run = session.state
```

The session also supports `status()`, `fork()`, `save_snapshot()`,
`Session.load_reference_nature_neow_offer_snapshot(snapshot)`, and bulk
release. The ordinary State API can query a Neow law with
`query_chance_law(site_id, ordinal=..., prior_indices=...)` from the same
pristine run-start state.

## Limits

The session uses the current emulator's source-shaped default Neow pools and
decision graph. Native unlock filtering, the complete installed-game call
order, seeded Native cursor parity, and all source probabilities remain
unaudited; every law is labelled `UNVERIFIED`. The session does not add Neow
relic effects or change the player-facing observation. It does not depend on
the native reference mod or Fidelity Inspector.

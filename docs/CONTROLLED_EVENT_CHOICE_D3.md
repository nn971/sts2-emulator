# Controlled event-choice continuation

This opt-in D3 boundary suspends selected ordinary event choices at supported
legacy random-index sites. The ordinary engine step and legacy six-stream
behavior remain unchanged.

## Saved state and replay

On `Begin`, the engine saves the run immediately before the event option and
replays the option under a controlled chance guard. At a supported random
site, it stores the typed request and candidate labels in `EventState` without
committing the option's local effects. Each selected index replays the full
option from the checkpoint with the recorded prefix. If the option reaches a
second supported random site, it suspends again with that next request. Only
after every random request has an outcome does the engine commit the option
once.

The serialized request includes site, legacy stream, owner, ordinal, operation
kind and ordered integer support. Candidate labels identify event card
instances, card models, potion models or eligible relics. Completed controlled
decisions remain in `RunState.ControlledEventChoiceHistory`; pending and
completed transcripts identify `controlled-v111` outcomes and retain
`UNVERIFIED` probability status. Controlled outcomes do not advance the legacy
RNG stream.

The supported event-choice sites are:

- `event.random_upgrade.card_instance`
- `event.reward.random_rare_card.model`
- `event.reward.random_potion.model` on the legacy uniform-pool route
- `event.potion.fill_empty_slots.model`
- `potion.uncommon_model.index_draw` for the Potion Courier event choice
- `relic.unowned_random_selection.index_draw` for eligible event options

Any other random site reached by the event option continues to fail closed.
Native regional potion routes, Shovel's relic acquisition, custom event reward
generation, source-pool differences, native stream ownership and reference
probabilities remain outside this boundary.

## JSONL and Python

`hello` advertises `controlledEventChoiceSchemaId` and the site list. Use an
ordinary run `state_handle`:

| Operation | Inputs | Result |
| --- | --- | --- |
| `controlled_event_begin` | `state_handle`, `choice_id` | Child run handle, pending request and labels, transcript, legal actions |
| `controlled_event_resolve` | pending `state_handle`, `selected_index` | Child run handle with the next request or committed option |
| `controlled_event_inspect` | pending `state_handle` | Restore the Nature-facing request and legal outcome actions after loading a snapshot |

The exact continuation is part of the ordinary run snapshot. A pending
continuation exposes only `choose_controlled_event_outcome` actions through
the explicit controller response. Player-facing observations hide those
Nature actions, and the ordinary step endpoint cannot submit them. Other
actions, including out-of-combat potions, are rejected until it resumes. The
Python binding exposes `State.begin_controlled_event_choice()`,
`State.resume_controlled_event_choice()` after snapshot restore, and
`ControlledEventChoice.resolve()`.

## Evidence and limits

The Release CLI build compiles the core engine and JSONL wire surface. The
Python wrapper is documented but has not been independently exercised. No
native Inspector operation capture, seeded native cursor parity,
reference-law calibration or controlled mechanics regression is claimed by
this slice.

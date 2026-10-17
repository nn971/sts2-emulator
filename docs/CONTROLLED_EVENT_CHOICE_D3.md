# Controlled event-choice continuation

This opt-in D3 boundary suspends selected ordinary event choices at supported
legacy random-index sites. The ordinary engine step and legacy six-stream
behavior remain unchanged.

## Saved state and continuation

The engine stores the current event-effect frame at a supported random site,
including partially updated player and world state, effect index, deferred
effects, typed request and candidate labels. Earlier effects remain in the
serialized frame. A selected index resumes at the unresolved effect and
continues in source order. If the option reaches another supported random
site, it suspends again with that next request. The option commits after all
effects finish; earlier effects are not replayed.

The serialized request includes site, legacy stream, owner, ordinal, operation
kind and ordered integer support. Candidate labels identify event card
instances, card models, potion models or eligible relics where those identities
are the request support; swap positions and rarity tickets retain numeric
labels. Completed controlled decisions remain in
`RunState.ControlledEventChoiceHistory`; pending and
completed transcripts identify `controlled-v111` outcomes and retain
`UNVERIFIED` probability status. Controlled outcomes do not advance the legacy
RNG stream.

The supported event-choice sites are:

- `event.random_upgrade.card_instance`
- `event.reward.random_rare_card.model`
- `event.reward.random_potion.model` on the legacy uniform-pool route
- `event.reward.offer_potion.model.index_draw`
- `event.reward.card_bundle.shuffle_swap_index`
- `reward.card_rarity.ticket` and `reward.card_model.index_draw` while
  constructing selected event card offers
- `run.random_pool_distinct.shuffle_swap_index` while constructing selected
  event relic and card offers
- `event.reward.random_modifier_curse.index_draw` after a supported event's
  post-reward modifier-curse offer is completed
- `relic_grab_bag.queue_shuffle.swap_index` and
  `relic_grab_bag.rarity.ticket` while acquiring Toy Box through a supported
  controlled event choice
- `event.potion.fill_empty_slots.model`
- `potion.uncommon_model.index_draw` for the Potion Courier event choice
- `relic.unowned_random_selection.index_draw` for eligible event options

Any other random site reached by the event option continues to fail closed.
Native regional potion routes, post-reward random generation outside the
modifier-curse site, other custom event reward effects,
source-pool differences, native stream ownership and reference probabilities
remain outside this boundary. The post-reward modifier-curse draw has a
separate saved reward frame. It preserves completed player reward choices and
extends the original event-choice transcript when resumed. For supported
multi-draw event effects, the frame re-enters only the current effect from its
saved start and injects that effect's saved decision prefix; all completed
earlier effects remain in the partial run.

Legacy run-start and Shovel action draws use the separate
[`CONTROLLED_RUN_ACTION_D3.md`](CONTROLLED_RUN_ACTION_D3.md) boundary.

## JSONL and Python

`hello` advertises `controlledEventChoiceSchemaId` and the site list. Use an
ordinary run `state_handle`:

| Operation | Inputs | Result |
| --- | --- | --- |
| `controlled_event_begin` | `state_handle`, `choice_id` | Child run handle, pending request and labels, transcript, legal actions |
| `controlled_event_advance` | completed controlled event-choice `state_handle`, legal `action_id` | Apply one reward action under the controlled guard; suspend if it reaches the post-reward modifier-curse draw |
| `controlled_event_resolve` | pending `state_handle`, `selected_index` | Child run handle with the next request or committed option |
| `controlled_event_inspect` | pending `state_handle` | Restore the Nature-facing request and legal outcome actions after loading a snapshot |
| `controlled_event_enumerate` | pending `state_handle`, current `fingerprint`, optional `maximum_outcomes` | Independent child run handle for each outcome of this one request |

The exact event-effect or event-reward continuation frame is part of the
ordinary run snapshot. A pending continuation exposes only
`choose_controlled_event_outcome` actions through
the explicit controller response. Player-facing observations hide those
Nature actions, and the ordinary step endpoint cannot submit them. Other
actions, including out-of-combat potions, are rejected until it resumes. The
Python binding exposes `State.begin_controlled_event_choice()`,
`State.resume_controlled_event_choice()` after snapshot restore,
`State.advance_controlled_event_reward()` and `ControlledEventChoice.resolve()`.
The optional one-boundary enumerator is
advertised as `sts2-v111-controlled-event-enumeration-v1`; its cap is 256
outcomes. Each branch resolves only the current request, leaving any next
request pending in that child.

## Evidence and limits

The Release CLI build compiles the core engine and JSONL wire surface. The
Python wrapper is documented but has not been independently exercised. No
native Inspector operation capture, seeded native cursor parity,
reference-law calibration or controlled mechanics regression is claimed by
this slice.

# Controlled event-choice continuation

This opt-in D3 boundary suspends selected ordinary event choices at supported
legacy random-index sites. It also supports the Native Stone of All Time
`push_ticket` and `lift_ticket` as one-request boundaries around their existing
custom event actions. Ordinary event steps and legacy six-stream behavior
remain unchanged.

## Saved state and continuation

The engine stores the current event-effect frame at a supported random site,
including partially updated player and world state, effect index, deferred
effects, typed request and candidate labels. Earlier effects remain in the
serialized frame. A selected index resumes at the unresolved effect and
continues in source order. If the option reaches another supported random
site, it suspends again with that next request. The option commits after all
effects finish; earlier effects are not replayed.

For `OfferThreeCardsAndPotion` and `OfferFiveClassCards`, the effect-frame
query routes `reward.card_rarity.ticket` and `reward.card_model.index_draw`
through the existing legacy reward-card laws. It derives the Act and reward
group from the saved request boundary, and rebuilds each later pick from the
selected card labels earlier in the same effect. The rarity-ticket indices
remain numeric; model choices retain their ordered card-ID labels. Source
pool fidelity, equal-index calibration, and Native cursor parity remain open.

Stone of All Time uses the custom-action exception. Lift saves the untouched
pre-choice run, so potion removal and Max HP wait until Nature resolves its
ticket; resume replays the lift action once and commits the terminal page.
Push applies its HP loss and presents the existing deck-card choice first. The
session records that player action, suspends at the ticket, and later resumes
only the ticket tail before committing the page. If the HP loss ends the run,
the source never reaches the ticket. Native ticket requests use event-stream
ordinal 1 because the entry potion-slot draw owns ordinal 0. Legacy fallback
requests retain their `event` stream call ordinal. Controlled outcomes do not
advance either provider cursor.

The saved event-reward frame also suspends a legal Driftwood
`reroll_reward_card` action on an event-generated card reward. It saves the
pre-action reward boundary and action, then replays the reroll with its exact
selected prefix until the reward options commit. Only
`reward.card_rarity.ticket` and `reward.card_model.index_draw` are admitted for
this action; another stochastic site reached by a reward action remains
fail-closed. The reroll decisions extend the originating event-choice trace.

The serialized request includes site, stream, owner, ordinal, operation kind
and ordered integer support. Generic D3 event requests use the legacy stream;
Stone push and lift use their Native event stream on configured Native runs
and the legacy `event` stream on retained prototype and Native-regional
fallback routes. Candidate
labels identify event card instances, card models, potion models, eligible relics,
and Sand Castle's physical deck-card instances where those identities describe
the request support; other swap positions and rarity tickets retain numeric
labels.
Completed controlled decisions remain in
`RunState.ControlledEventChoiceHistory`; pending and
completed transcripts identify `controlled-v111` outcomes and retain
`UNVERIFIED` outcome provenance. Controlled outcomes do not advance the legacy
or Native RNG stream.

Pending event-effect and post-reward modifier-curse responses also include a
read-only `chanceLaw` and `boundaryFingerprint`. For generic requests on
recognized Native `run.*` and `player.*` streams or context streams created by
`NativeRngV111Contexts`, the law assigns exact Native `NextInt` 53-bit grid
masses to each legal request index (`choice_index:n`); legacy and unknown
streams retain equal index weights.
Candidate labels describe the mechanical result associated with an index but
do not change those index masses. Stone push and lift each collapse all 100
bounded indices to a single `no_effect` outcome under a `SOURCE_VERIFIED` law.
Source-pool completeness, stream parity and original-game probabilities
remain separate.
The pending boundary fingerprint binds the event/choice/request context and
excludes private RNG state. The response's top-level `fingerprint` remains the
exact-state stale-request guard.

The direct `chance_law_query` for `reward.card_rarity.ticket` can return the
rarity-category law after callers declare the reward group and earlier card
models. The pending event-effect frame still records its typed ticket-index
support; the direct category law does not replace that saved continuation.
The generic legacy `event.reward.random_potion.model` site also has a direct
pool query keyed by `event_id` and the selected choice `action_id`; it does not
cover Wellspring or Whispering Hollow's separate Native-shaped routes. Their
typed `potion.uniform_model`, `potion.rarity_roll.ticket`, and
`potion.model_within_rarity` requests are handled by the saved event-choice
continuation below. Uniform, rarity-conditioned, and Uncommon-potion model
requests carry the event and choice in their owner IDs, and model candidates
expose potion IDs as labels; the 100 rarity-ticket indices remain numeric.
Direct typed queries can describe the uniform, rarity-ticket, rarity-model,
and Uncommon-model requests. Equal-index weights, Native source-pool behavior,
and source parity remain unverified.
The legacy `event.reward.random_rare_card.model` site has the same direct
query inputs and returns the current prototype Rare-card reward pool.
The custom event reward potion option also has a direct pool query; callers
declare its selected event-choice owner prefix (`Event:<event_id>:choice:<choice_id>`)
and confirm potion acquisition is allowed.

The supported event-choice sites are:

- `event.random_upgrade.card_instance`
- `event.reward.random_rare_card.model`
- `event.reward.random_potion.model` on the legacy uniform-pool route
- `potion.uniform_model.index_draw` for the Wellspring `bottle` choice and
  `potion.rarity_roll.ticket` followed by
  `potion.model_within_rarity.index_draw` for the two Whispering Hollow
  `gold` choice potions. These Native-shaped event routes use the existing
  controlled event-choice continuation; their equal-index laws and source
  pool behavior remain `UNVERIFIED`.
- `event.reward.offer_potion.model.index_draw`, with the selected event choice
  bound into its owner and potion IDs retained as candidate labels
- `event.reward.card_bundle.shuffle_swap_index`
- `reward.card_rarity.ticket` and `reward.card_model.index_draw` while
  constructing selected event card offers and while a legal Driftwood reroll
  regenerates an event reward card group
- `run.random_pool_distinct.shuffle_swap_index` while constructing selected
  event relic and card offers
- `event.reward.random_modifier_curse.index_draw` after a supported event's
  post-reward modifier-curse offer is completed
- `event.stone_of_all_time.push_ticket` after its player deck-card choice and
  `event.stone_of_all_time.lift_ticket` at the lift action; on the configured
  Native, retained legacy, and Native-regional fallback routes, all 100
  ticket indices map to one `no_effect` outcome and the pending Chance Law is
  `SOURCE_VERIFIED`; direct
  pre-choice queries support all three routes, using Native ordinal 1 or a
  legacy `event` request ordinal; replay matches that typed
  request against its saved boundary and chronological prefix
- `relic_grab_bag.queue_shuffle.swap_index` and
  `relic_grab_bag.rarity.ticket` while acquiring Toy Box through a supported
  controlled event choice
- `treasure.relic_grab_bag.model.index_draw` for Native Darv's Calling Bell
  acquisition; it samples the three forced Common, Uncommon, and Rare relic
  identities through the canonicalized Nature bag, preserving each request in
  the event-choice prefix
- `event.potion.fill_empty_slots.model`
- `potion.uncommon_model.index_draw` for the Potion Courier event choice,
  bound to its event and choice owner and labeled with potion IDs
- `relic.acquisition.random_rare_card.model`,
  `relic.acquisition.random_potion.model` and
  `run.potion.fill_empty_slots.model` for random effects triggered by an
  event-gained relic
- `run.relic.beautiful_bracelet.deck_shuffle.swap_index` for an
  event-gained Beautiful Bracelet's conditional shuffle over eligible
  physical deck cards; each swap carries the current ordered instance-ID labels
- `run.relic.distinguished_cape.modifier_curse.index_draw` for an
  event-gained Distinguished Cape's two ordered, without-replacement curse
  picks; each request carries the remaining curse IDs
- `run.relic.fur_coat.map_node_shuffle.swap_index` for an event-gained Fur
  Coat's conditional shuffle over current-act Combat and Elite node IDs; each
  swap carries the current ordered node-ID labels
- `relic.sand_castle.acquisition_upgrade.shuffle_swap_index` for the
  event-gained Sand Castle's conditional shuffle over eligible physical deck
  cards; each swap carries the current ordered instance-ID labels
- `reward.card_transform.replacement.index_draw` for a direct persistent-card
  transform effect or an event-gained relic that transforms persistent cards
  on acquisition; direct `TransformFirstCardOfId` requests reuse the existing
  typed reward-pool transform law after validating the saved physical card
  and effect
- `run.random_pool_distinct.shuffle_swap_index` for deferred Rare-card,
  Colorless-card, and Neow's Bones relic offers; the query rebuilds the
  effect-specific source pool and replays its saved descending-swap prefix
- `event.reward.card_bundle.shuffle_swap_index` for `OfferCardBundles`; the
  query validates the active Common or Uncommon source pool and its saved
  descending-swap prefix
- `relic.unowned_random_selection.index_draw` for eligible event options;
  `GainRandomRelic` and `OfferRandomRelic` effects rebuild the unowned pool
  from the saved player state, validate its request owner and stream, and
  expose the ordered relic IDs through the existing typed law

Any other random site reached by the event option or reward action continues
to fail closed. Other Native regional potion routes, post-reward random
generation outside the modifier-curse and Driftwood card-reroll sites, other
custom event reward effects,
source-pool differences, native stream ownership and reference probabilities
remain outside this boundary. The Calling Bell identity uses an exact Native
bounded-index transform, while hidden-queue identity, source-pool completeness,
and Native cursor parity remain open. The post-reward modifier-curse draw has a
separate saved reward frame. It preserves completed player reward choices and
extends the original event-choice transcript when resumed. For supported
multi-draw event effects, the frame re-enters only the current effect from its
saved start and injects that effect's saved decision prefix; all completed
earlier effects remain in the partial run.

Stone of All Time push and lift sessions accept the configured Native event,
retained legacy event, and Native-regional legacy fallback routes. Their
pending Chance Law collapses the discarded ticket to `no_effect`. Direct
pre-choice ticket-law queries accept all three routes; legacy queries take
their source-call ordinal explicitly and leave cursor validation to
chronological request replay. Seeded stream parity remains open.

Legacy run-start and Shovel action draws use the separate
[`CONTROLLED_RUN_ACTION_D3.md`](CONTROLLED_RUN_ACTION_D3.md) boundary.

## JSONL and Python

`hello` advertises `controlledEventChoiceSchemaId` and the site list. Use an
ordinary run `state_handle`:

| Operation | Inputs | Result |
| --- | --- | --- |
| `controlled_event_begin` | `state_handle`, `choice_id` | Child run handle, pending request and labels, transcript, legal actions |
| `controlled_event_advance` | controlled event-choice `state_handle`, legal player `action_id` | Apply one supported player action under the controlled guard, including Stone's deck-card choice or a supported reward action |
| `controlled_event_resolve` | pending `state_handle`, exactly one of `selected_index` or `candidate_label` | Child run handle with the next request or committed option |
| `controlled_event_inspect` | pending `state_handle` | Restore the Nature-facing request and legal outcome actions after loading a snapshot |
| `controlled_event_enumerate` | pending `state_handle`, current `fingerprint`, optional `maximum_outcomes` | Independent child run handle for each outcome of this one request |

`candidate_label` selects a unique saved label from the active ordered support;
the existing `selected_index` form remains supported. Both forms retain the
controlled index transcript.

The exact event-effect or event-reward continuation frame is part of the
ordinary run snapshot. A pending continuation exposes only
`choose_controlled_event_outcome` actions through
the explicit controller response. Player-facing observations hide those
Nature actions, and the ordinary step endpoint cannot submit them. Other
actions, including out-of-combat potions, are rejected until it resumes. The
Python binding exposes `State.begin_controlled_event_choice()`,
`State.resume_controlled_event_choice()` after snapshot restore,
`State.advance_controlled_event_action()` (with
`advance_controlled_event_reward()` retained as a compatibility alias) and
`ControlledEventChoice.resolve()` or
`ControlledEventChoice.resolve_candidate_label()` when the pending support
publishes candidate labels.
The optional one-boundary enumerator is
advertised as `sts2-v111-controlled-event-enumeration-v1`; its cap is 256
outcomes. Each branch resolves only the current request, leaving any next
request pending in that child.

The JSONL response adds the Chance Law payload without changing the existing
controlled-event schema or resolution operations. The legacy
`controlled_event_*` routes remain externally resolved and do not own a
ReferenceNature sampler cursor. The new atomic event-choice session is
documented below.

An additional JSONL session wraps the same saved event resolver with a seeded
ReferenceNature cursor. Its transcript orders Nature samples and legal reward
actions together, so a post-reward modifier-curse request continues from the
same sampler state:

| Operation | Inputs | Result |
| --- | --- | --- |
| `reference_nature_event_choice_begin` | current `state_handle`, `choice_id`, optional `nature_seed` | Session at a Nature or player-action boundary |
| `reference_nature_event_choice_status` | event-choice session handle | Current boundary, pending Chance Law and legal player actions |
| `reference_nature_event_choice_resolve_next` | handle and current session `fingerprint` | Child after one sampled index is resolved by the saved event continuation |
| `reference_nature_event_choice_advance` | handle, fingerprint and legal player `action_id` | Child after one player action; Chance Law resolution remains in the session |
| `reference_nature_event_choice_fork` | event-choice session handle | Independent child session |
| `reference_nature_event_choice_save` / `reference_nature_event_choice_load` | handle / snapshot | Replay-validated session snapshot and restored handle |
| `reference_nature_event_choice_release_many` | `reference_nature_event_choice_handles` | Number of released sessions |

Supply `nature_seed` the first time a run uses ReferenceNature. Once a session
commits, its cursor is stored in the run snapshot and later sessions continue
it when `nature_seed` is omitted. Runs without a committed cursor require a
seed. New exact snapshots use `sts2-exact-snapshot-v4-simulation-envelope`;
the loader continues to accept flattened v1–v3 snapshots.

The session covers only the allowlisted controlled event-choice and
post-reward modifier-curse continuations. When the event action and any
controlled reward chance finish, the result provides a normal `stateHandle`
for continuing play. Unsupported event chance still fails closed; other
event-page choices, shops and general run actions remain outside this session,
apart from Stone push's documented deck-card boundary. The six supported
Orobas, Pael, Tezcatara, Nonupeipe, Tanx
and Vakuu opening offers use the separate [ReferenceNature Ancient-offer
session](REFERENCE_NATURE_ANCIENT_OFFER_D3.md).
The Python binding exposes
`State.begin_reference_nature_event_choice(choice_id, nature_seed=None)`, session
`resolve_next()`, `advance(action_id)`, `fork()`, `save_snapshot()`,
`Session.load_reference_nature_event_choice_snapshot()` and `release()`.

## Evidence and limits

The Release CLI build compiles the core engine and JSONL wire surface. The
Python wrapper is documented but has not been independently exercised. No
native Inspector operation capture, seeded native cursor parity,
reference-law calibration or controlled mechanics regression is claimed by
this slice.

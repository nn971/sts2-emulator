# ReferenceNature Ancient-offer session

`ReferenceNatureAncientOfferSessionV111` is the versioned, atomic continuation
for the Orobas, Pael and Tezcatara Act 2 offer sets; the Nonupeipe, Tanx and
Vakuu Act 3 offer shuffles; and Darv's Act 2/3 mixed offer generator. The first
six roots retain their existing shape: three sequential choice laws for Hive,
and source Fisher–Yates bounded-index laws for Glory. Nonupeipe and Tanx use
the entry-time eligible pool length, while Vakuu's ordinals span all three
shuffles.

Darv starts at `event.darv.offer_variant.index_draw`. Its session first samples
the binary Dusty Tome/two-relic versus three-relic variant, then samples every
descending Fisher–Yates swap at `event.darv.relic_offer.shuffle_swap_index`.
Each shuffle law binds the initial ordered relic pool, selected variant and
prior swap prefix. On completion, the full transcript is replayed through
`NativeDarvEventV111.BeginOpeningWithAudit`; the generated run and updated
ReferenceNature cursor are published together. Partial decisions remain
private to the session.

Each law uses the existing ordered candidate pool and opening-boundary
validation. Pael duplicate entries remain separate probability mass through
integer outcome weights. The Ancient generators perform the actual offer
generation through provider mode `scripted-v111`. This counterfactual provider
does not advance the Native event RNG cursor, and the completed event retains
its provider-mode marker. The variant and swap laws currently have uniform
integer weights. Darv's binary variant law is `SOURCE_VERIFIED` under the
uniform 53-bit input model. Its Fisher–Yates swaps, and the Nonupeipe, Tanx,
and Vakuu swaps, are source-verified individually only when their bounded
count is a power of two; other bounds retain the equal-weight law as
`UNVERIFIED`. Orobas, Pael and Tezcatara direct offer laws remain
`UNVERIFIED`.

## JSONL operations

| Operation | Inputs | Result |
| --- | --- | --- |
| `chance_law_query` for an Ancient-offer site | current `state_handle`, `site_id`, `ordinal`; shuffle sites also need `prior_indices` for every earlier swap; Darv shuffle queries may include `prior_variant_index` | Direct offer law, Darv variant law, or one conditioned Fisher–Yates swap law |
| `reference_nature_ancient_offer_begin` | current `state_handle`, supported session-root `site_id`, optional `nature_seed` | Session before its first offer decision |
| `reference_nature_ancient_offer_status` | session handle | Current ordinal and pending Chance Law, or committed offers |
| `reference_nature_ancient_offer_resolve_next` | handle and current session `fingerprint` | Child after one sampled decision; commits after all draws in the selected generator |
| `reference_nature_ancient_offer_fork` | session handle | Independent child session |
| `reference_nature_ancient_offer_save` / `reference_nature_ancient_offer_load` | handle / snapshot | Replay-validated session snapshot and restored handle |
| `reference_nature_ancient_offer_release_many` | `reference_nature_ancient_offer_handles` | Number of released sessions |

Use `site_id` from the Chance Law query coverage registry. The six existing
session roots remain available. Darv's combined session begins at the offer
variant site; its relic-shuffle site is queryable directly but is not a session
root. Direct variant queries accept only ordinal 0. For any standalone shuffle
query, ordinal 0 uses an empty `prior_indices` array and later queries supply
every previous `swap_index` in ordinal order. A standalone Darv shuffle query
may also supply `prior_variant_index` (`0` for Dusty Tome/two relics or `1`
for three relics); the session binds this value automatically. `nature_seed` is required
for the first ReferenceNature session on a run; after a session commits its
cursor into the run state, later sessions omit the seed. An incomplete session
has no ordinary `stateHandle`, and its cursor is not published to the source
run. Its snapshot retains the origin run, initial and current sampler states,
and decision transcript. Restore replays every law, choice/swap and completed
event generation before accepting the snapshot.

The result includes the session fingerprint, next ordinal, pending law and
decision transcript. After completion it also includes the ordinary
`stateHandle`, offered choice IDs and `eventRandomProvider`. The result is a
ReferenceNature view; it does not change normal Native execution or the
player-facing observation schema.

## Python binding

`State.begin_reference_nature_ancient_offer(site_id, nature_seed=None)`
returns a session for any supported session root, including Darv's offer
variant root. `State.query_chance_law(site_id, ordinal=..., prior_indices=...)`
supports the registered direct-query sites, including Darv's variant and
relic-shuffle sites; pass `prior_variant_index` when querying a Darv swap after
the variant decision. A session supports `status()`, `resolve_next()`, `fork()`,
`save_snapshot()`, `release()`, and its ordinary `state` property after all
source draws complete. Restore with
`Session.load_reference_nature_ancient_offer_snapshot(snapshot)` and release
multiple sessions with
`Session.release_reference_nature_ancient_offer_many(sessions)`. Query coverage
and atomic session coverage remain separate capability registries.

## Limits

The session does not certify source probabilities, Darv's complete source pool
and call order, or native cursor parity. EventState keeps its established
compatibility shape; a saved session reconstructs support from its origin run
instead of adding a separate event-level support fingerprint. Ancient
assignment and other Ancient offer sites remain outside this session. No native
reference mod or Fidelity Inspector work is included.

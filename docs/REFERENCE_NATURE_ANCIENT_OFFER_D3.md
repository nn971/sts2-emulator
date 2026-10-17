# ReferenceNature Ancient-offer session

`ReferenceNatureAncientOfferSessionV111` is the versioned, atomic continuation
for the Orobas, Pael and Tezcatara Act 2 offer sets and the Nonupeipe, Tanx and
Vakuu Act 3 offer shuffles. It samples the three sequential choice laws for the
Hive events. For Glory it samples each source Fisher-Yates bounded index:
Nonupeipe and Tanx use the entry-time eligible pool length, while Vakuu's
ordinals span all three shuffles. Partial outcomes remain in the session until
the complete transcript is replayed through the existing event generator; the
generated run and updated ReferenceNature cursor are published together.

Each law uses the existing ordered candidate pool and opening-boundary
validation. Pael duplicate entries remain separate probability mass through
integer outcome weights. For Glory offers, every bounded swap law conditions
on the initial ordered relic pool, all prior swap indices, and the resulting
current pools. On completion, event mechanics perform the actual offer
generation from the selected identities or swap indices through provider mode
`scripted-v111`. This counterfactual provider does not advance the native event
RNG cursor, and the completed event retains its provider-mode marker.
Probability provenance stays `UNVERIFIED`.

## JSONL operations

| Operation | Inputs | Result |
| --- | --- | --- |
| `chance_law_query` for an Ancient offer site | current `state_handle`, `site_id`, `ordinal`; shuffle sites also need `prior_indices` for every earlier swap | Direct offer law, or one conditioned Fisher-Yates swap law |
| `reference_nature_ancient_offer_begin` | current `state_handle`, `site_id`, optional `nature_seed` | Session before the first offer decision |
| `reference_nature_ancient_offer_status` | session handle | Current ordinal and pending Chance Law, or committed offers |
| `reference_nature_ancient_offer_resolve_next` | handle and current session `fingerprint` | Child after one sampled offer decision; commits after all three direct offers or all shuffle swaps |
| `reference_nature_ancient_offer_fork` | session handle | Independent child session |
| `reference_nature_ancient_offer_save` / `reference_nature_ancient_offer_load` | handle / snapshot | Replay-validated session snapshot and restored handle |
| `reference_nature_ancient_offer_release_many` | `reference_nature_ancient_offer_handles` | Number of released sessions |

Use `site_id` from the chance-law query coverage registry. For a direct offer
site, the ordinal is 0 through 2. For a shuffle site, ordinal 0 uses an empty
`prior_indices` array; each later query supplies the previous `swap_index`
values in ordinal order. `nature_seed` is required for the first
ReferenceNature session on a run; after a session commits its cursor into the
run state, later sessions omit the seed. An incomplete session has no ordinary
`stateHandle`, and its cursor is not published to the source run. Its snapshot
retains the origin run, initial and current sampler states, and decision
transcript. Restore replays every law, choice/swap and completed event
generation before accepting the snapshot.

The result includes the session fingerprint, next ordinal, pending law and
decision transcript. After completion it also includes the ordinary
`stateHandle`, offered choice IDs and `eventRandomProvider`. The result is a
ReferenceNature view; it does not change normal Native execution or the
player-facing observation schema.

## Python binding

`State.begin_reference_nature_ancient_offer(site_id, nature_seed=None)`
returns a `ReferenceNatureAncientOfferSession` for any of the six supported
sites. `State.query_chance_law(site_id, ordinal=..., prior_indices=...)`
supports the same direct and conditioned shuffle queries. The session supports
`status()`, `resolve_next()`, `fork()`, `save_snapshot()`, `release()`, and its
ordinary `state` property after the third choice. Restore with
`Session.load_reference_nature_ancient_offer_snapshot(snapshot)` and release
multiple sessions with
`Session.release_reference_nature_ancient_offer_many(sessions)`. Query coverage
and atomic session coverage remain separate capability registries.

## Limits

The session does not certify the source probabilities or native cursor parity.
EventState keeps its established compatibility shape; a saved session
reconstructs the support from its origin run instead of adding a separate
event-level support fingerprint. Ancient assignment and other Ancient offer
sites remain outside this session. No native reference mod or Fidelity
Inspector work is included.

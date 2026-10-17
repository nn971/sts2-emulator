# ReferenceNature Ancient-offer session

`ReferenceNatureAncientOfferSessionV111` is the versioned, atomic continuation
for the Orobas, Pael and Tezcatara Act 2 opening offer sets. It samples the
three sequential offer laws from `ChanceLawEngineV111.QueryAncientOffer`, keeps
partial choices in the session, then passes the complete choice transcript to
the existing event-specific offer generator. The generated run and updated
ReferenceNature cursor become visible together after the third choice.

Each law uses the existing ordered candidate pool and opening-boundary
validation. Pael duplicate entries remain separate probability mass through
integer outcome weights. On completion, the selected stable choice identity is
mapped to a legal source-pool index and replayed with provider mode
`scripted-v111`; event mechanics perform the actual offer generation. This
counterfactual provider does not advance the native event RNG cursor, and the
completed event retains its provider-mode marker. Probability provenance stays
`UNVERIFIED`.

## JSONL operations

| Operation | Inputs | Result |
| --- | --- | --- |
| `reference_nature_ancient_offer_begin` | current `state_handle`, `site_id`, optional `nature_seed` | Session before the first offer choice |
| `reference_nature_ancient_offer_status` | session handle | Current ordinal and pending Chance Law, or committed offers |
| `reference_nature_ancient_offer_resolve_next` | handle and current session `fingerprint` | Child after one sampled offer choice; commits the run after choice three |
| `reference_nature_ancient_offer_fork` | session handle | Independent child session |
| `reference_nature_ancient_offer_save` / `reference_nature_ancient_offer_load` | handle / snapshot | Replay-validated session snapshot and restored handle |
| `reference_nature_ancient_offer_release_many` | `reference_nature_ancient_offer_handles` | Number of released sessions |

Use `site_id` from the chance-law query coverage registry. `nature_seed` is
required for the first ReferenceNature session on a run; after a session
commits its cursor into the run state, later sessions omit the seed. An
incomplete session has no ordinary `stateHandle`, and its cursor is not
published to the source run. Its snapshot retains the origin run, initial and
current sampler states, and decision transcript. Restore replays every law,
choice and completed event generation before accepting the snapshot.

The result includes the session fingerprint, next ordinal, pending law and
decision transcript. After completion it also includes the ordinary
`stateHandle`, offered choice IDs and `eventRandomProvider`. The result is a
ReferenceNature view; it does not change normal Native execution or the
player-facing observation schema.

## Python binding

`State.begin_reference_nature_ancient_offer(site_id, nature_seed=None)`
returns a `ReferenceNatureAncientOfferSession`. The session supports
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

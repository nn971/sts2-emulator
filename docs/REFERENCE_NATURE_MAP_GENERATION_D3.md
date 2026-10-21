# ReferenceNature map-generation session

`ReferenceNatureMapGenerationSessionV111` is an opt-in, resumable continuation
for the existing source-shaped Overgrowth, Hive, and Glory map generators.
Ordinary Native map generation keeps its current source-owned routing.
Configured Overgrowth `start_run` composes this session after the boss-pool
choice inside `ReferenceNatureRunActionSessionV111`, continuing the run-owned
Nature cursor through Act 1 map generation. Each run-action resolve samples
one boss or map request; map requests are exposed as pending kind
`run_action_map_generation`. The run remains unpublished until map generation
completes. Its run snapshot retains a compact typed audit (ordered requests
and committed map fingerprint) so replay does not duplicate the map stored in
mechanics.

The session reruns the selected generator from its seed under the saved ordered
typed prefix. It captures the next index or Gaussian-integer request, queries
the matching Chance Law, samples one outcome, and replays. It returns the map
only after the generator finishes and the transcript, map, and Nature sampler
cursor validate together. Fork, save, load, and stale-fingerprint checks use
the same boundary.

The session can begin from either a new `nature_seed` or an existing
`ReferenceNatureStateV111` cursor. JSONL operations are
`reference_nature_map_generation_begin`, `status`,
`resolve_next`, `fork`, `save`, `load`, and `release_many`. Begin requires
`seed` and `nature_seed`; `act` defaults to 1 and supports 1–3, while
`ascension` defaults to 0 and supports 0–10. Resolve and fork use
`reference_nature_map_generation_handle`; resolve also requires the current
`fingerprint`. The `map` field is present only after completion. The pending
Nature choice is available through `nature_choice_observe` with
`session_kind: "map_generation"`.

Path starts, delta swaps, room-assignment swaps, and Glory's rest quota use
their bounded-index laws. Their source `NextInt` transforms are
`SOURCE_VERIFIED` on each declared support. The Gaussian quota outcomes use
the existing continuous rounded/truncated-normal approximation and remain
`UNVERIFIED` against Native finite-grid probabilities. Replaying the emulator
generator verifies its own ordered request prefix and output; it does not
verify source map geometry, source call order, seed frequencies, or Native
cursor parity. The legacy Box–Muller map path is outside this session.

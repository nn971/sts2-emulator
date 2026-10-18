# ReferenceNature map-generation session

`ReferenceNatureMapGenerationSessionV111` is an opt-in, resumable continuation
for the existing source-shaped Overgrowth, Hive, and Glory map generators.
Ordinary Native map generation keeps its current source-owned routing.

The session reruns the selected generator from its seed under the saved ordered
typed prefix. It captures the next index or Gaussian-integer request, queries
the matching Chance Law, samples one outcome, and replays. It returns the map
only after the generator finishes and the transcript, map, and Nature sampler
cursor validate together. Fork, save, load, and stale-fingerprint checks use
the same boundary.

JSONL operations are `reference_nature_map_generation_begin`, `status`,
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

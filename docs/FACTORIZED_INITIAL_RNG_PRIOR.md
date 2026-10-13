# Factorized initial RNG streams — alternate research prior

**Scope:** initial `RunStart` → `MapChoice` transition of the Silent
prototype only. This is an *explicitly different stochastic game* from
the normal deterministic run-seed model.

The standard prototype normally derives all six SplitMix64 stream
states from one run-seed string via SHA-256, so their apparent
independence must not be treated as a proved property of a native
one-seed distribution. In this experiment the initial states are
chosen **independently** as 64-bit unsigned integers.

`PrototypeFactorizedRunFactory.Create(initialStateHex)` accepts
exactly six 16-character hexadecimal state values keyed by
`map`, `combat`, `combat_targets`, `reward`, `shop` and `event`.
The returned hypothetical `RunStart` state holds the supplied RNG
bundle before any game RNG consumption. The ordinary
`start_run` action consumes the supplied bundle through the existing
game engine, preserving all dependent future RNG calls and cursors.
Ordinary resets (empty RNG bundle) retain the original run-seed path.

The JSONL bridge adds `reset_factorized_hypothetical`, accepting
`initial_streams` with the same six keys, and returns a separate
hypothetical handle plus capability ID
`prototype-independent-initial-streams-v1`. The reset response does
not expose the RNG state, and the existing player observation policy
continues to hide it.

## Verified start-of-run dependency graph

During the current prototype's `StartRun` transition:

- `combat` contributes the preselected Act 1 Overgrowth boss.
- `map` generates the visible Act 1 map, associated map metadata,
  and map-choice legal action menu.
- `combat_targets`, `reward`, `shop`, and `event` have no effect
  on any player-visible start-of-run output.

The C# test compares full public observations with only the
preselected boss field masked, legal menus, deterministic seed replay,
and varying unused stream states. It also checks the old
single-seed reset semantics are untouched.

With *discrete independent uniform priors* `U_m` and `U_c` on
initial map and combat RNG states, conditioning on the complete
post-`start_run` public frame factors:

```text
P(map_state, combat_state | visible map, visible boss)
 = P(map_state | visible map) * P(combat_state | visible boss)
```

Other four streams remain independent with their original priors
at this boundary. Consequently, the start-of-run posterior can
be sampled without evaluating the `|U_m| × |U_c|` Cartesian
product. Each accepted stream is advanced by its own actual
StartRun computation; independent future streams keep their
unconsumed initial state. A complete hypothetical state can then
be recreated by combining initial states and executing StartRun
once.

**This is not valid after arbitrary actions.** Later events may
couple stream-dependent consequences through combat, rewards,
shop/event decisions, and visible history. Filtering separate
stream supports independently after those interactions would
be incorrect.

## Statistical limits

This is a deliberately specified *factorized prior*, not a
verified native STS2 RNG law, and not a proof that the usual
one-string SHA-256 prototype seed prior is exactly factorized.
Small finite supports may still collapse under distinctive map
observations, although combat uncertainty can survive independently.

Follow-up work in the parent `sts2-ai` repository should build
a complete public `RunStart` + `MapChoice` posterior over finite
map/combat supports and independently sampled unused streams.
It must refuse to claim exactness for longer histories unless a
proper coupled posterior is supplied.

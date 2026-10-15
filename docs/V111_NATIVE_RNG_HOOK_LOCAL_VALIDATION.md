# Native v0.111 RNG Source Hook: Local Validation Protocol

**Branch:** `integration/fidelity-rng-on-silent-all-act-v111`.  
**Status:** Hook source is implemented, synthetic/standalone verification is in CI; actual installed-game compilation, hook attachment, and no-perturbation smoke **require a Windows/WSL game-local run**. Do not claim full native RNG parity before these gates.

## Source evidence used

Pinned read-only reference `Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20`:

- `src/Core/Random/Rng.cs` exposes `NextInt(int maxExclusive)`, `NextInt(int minInclusive,int maxExclusive)` and `ToSerializable()`. The two NextInt overloads each increment the RNG counter and return the original `MegaRandom.Next` result.
- `src/Core/Saves/SerializableRng.cs` exposes `counter,state0,state1,state2,state3`.
- `src/Core/Runs/RunRngSet.cs` exposes `GetRng(RunRngType)`; `src/Core/Random/PlayerRngSet.cs` exposes `GetRng(PlayerRngType)`.
- `Player.RunState.Rng` and `Player.PlayerRng` expose these **reference-identity** sources for single-player combats.

The runtime DLL SHA-256 configured by `STS2_FIDELITY_EXPECT_SHA256` remains authoritative; **verify the locally installed binary's API/signature before promotion**. The source mirror commit alone is no proof that a newer installed hotfix still matches.

## Hook behavior, scope and opt-in

The native mod's `NativeV111RngHook` uses Harmony prefix+postfix on exactly the two integer `NextInt` overloads. It copies the native serialized RNG state before/after and captures the method's already returned `__result`. **Neither prefix nor postfix requests another RNG outcome or changes its cursor.** Failure of hook installation leaves normal passive combat capture available; an observation error marks the sidecar incomplete and disables further RNG observations.

The hook is separately gated: **both** `STS2_FIDELITY_CAPTURE=1` **and** `STS2_FIDELITY_RNG_HOOK=1` must be set in the **native Windows game process's environment**, along with `STS2_FIDELITY_EXPECT_SHA256=<locally computed actual SHA256>`. Set `STS2_FIDELITY_TRACE_DIR` to your existing `D:\\SteamLibrary\\sts2-inspector-traces` (Windows-visible path) if desired. Environment variables set only in an unrelated WSL shell are insufficient for a Windows Steam-launched game process.

Native logs distinguish `RNG_HOOK_STATUS=INSTALLED_BOUND_INT_OVERLOADS`, `PIN_OR_SCENE_REJECTED` and `INSTALL_FAILED`. The installed message additionally reports `rollmove_source_scope=INSTALLED` or `UNAVAILABLE`. The optional pinned-source `MonsterModel.RollMove(IEnumerable<Creature>)` scope tags `RunRng.MonsterAi` draws with the actual creature combat ID and the `combat.committed_intent.index_draw` site. The native action sidecar records `native_monster_ai_scope_installed`; scope absence preserves primitive coverage without invented intent attribution. Record the exact logs and failed source signatures if installation fails; the observer must never be promoted without native compile/runtime evidence.

Only already-started, single-player combats are observed. The first sidecar frame registers all **12 run + 3 player** RNG objects from the combat's native player/run, with separate object identities and local zero-based ordinals. RNG draws **before the first observed combat frame**, on another game thread, from independently constructed map/event/monster/content RNGs, and from non-NextInt primitives remain **unverified**. Every primitive event emitted so far carries `site_id=UNATTRIBUTED` because `Rng.NextInt` is itself a primitive API, not a validated mechanic-level call site. Registered streams have a real owner category (`run`, `player:0`); ephemeral streams are `UNATTRIBUTED`. Capturing these events is meaningful evidence without pretending hook or full eligibility completeness.

## Run local validation without touching the protected emulator checkout

Use the existing Windows game installation at `/mnt/d/SteamLibrary`. This isolated checkout lives entirely under `/tmp`, outside `~/projects`:

```fish
set runner (mktemp -d /tmp/sts2-native-rng.XXXXXX)
git clone --depth 1 --single-branch \\
    --branch integration/fidelity-rng-on-silent-all-act-v111 \\
    https://github.com/nn971/sts2-emulator.git "$runner"
cd "$runner"

# Locate the actual sts2.dll; set GAME_ROOT to the parent of its data_* directory.
find /mnt/d/SteamLibrary -type f -name sts2.dll
set -gx GAME_ROOT "/mnt/d/SteamLibrary/steamapps/common/Slay the Spire 2"
python tools/native_game_preflight.py "$GAME_ROOT/data_sts2_windows_x86_64"
scripts/build-native-inspector.fish "$GAME_ROOT"
```

Adjust `GAME_ROOT` to the path containing the `data_sts2_windows_x86_64` directory shown by `find`; the illustrated Steam common folder name is not an asserted local path. The native mod build writes only to the temporary checkout and installs the updated mod under the actual game root, never `~/projects/sts2-emulator`.

Restart the Windows game with the four environment values above, open a single-player Silent combat, play/finish a few cards and at least one enemy turn, and check the game log for `RNG_HOOK_STATUS=INSTALLED_BOUND_INT_OVERLOADS`.

Audit (in the isolated checkout):

```fish
set trace /mnt/d/SteamLibrary/sts2-inspector-traces
python tools/native_action_envelope_batch.py "$trace" \\
    --out "$trace/native-rng-hook-corpus.json"
jq '{
  native_rng_hook_installed_sidecars,
  native_monster_ai_scope_installed_sidecars,
  native_rng_source_calls,
  native_rng_unattributed_calls,
  traces: [.traces[] | {
    file, status, native_rng_hook_installed,
    native_monster_ai_scope_installed,
    native_rng_source_calls, native_rng_unattributed_calls,
    native_rng_source_coverage
  }]
}' "$trace/native-rng-hook-corpus.json"
```

For every new source-capturing sidecar, run the independent offline
xoshiro-state and bounded-result verifier:

```fish
for sidecar in /mnt/d/SteamLibrary/sts2-inspector-traces/*.actions.jsonl
    python tools/native_rng_boundary_verify.py "$sidecar" \\
        --out "$sidecar.rng-verification.json"
end
```

The corpus audit reports `native_rng_primitive_validated_calls` and
`native_rng_primitive_errors` automatically. A nonzero error count makes
the unified checker return `NATIVE_RNG_PRIMITIVE_MISMATCH`, including
when the corresponding emulator step is unsupported. Verified source calls
establish only the **observed primitive transition**, not complete source
hook coverage, semantic eligibility or original probability laws.

If a sidecar has `native_rng_hook_installed=true` but zero events, the audit says `HOOK_INSTALLED_NO_SOURCE_CALLS_OBSERVED`; this is **not** proof of no RNG consumption. Old sidecars remain auditable and should say `SOURCE_RNG_UNINSTRUMENTED`.

## No-perturbation and source fidelity checks

1. On the pinned executable, compare identical **seed / combat setup / player actions** with the RNG hook disabled versus enabled, with ordinary passive Inspector active in both. Compare independently captured game states, committed intents, HP, and terminal outcomes; any divergence is a failure needing investigation.
2. For each `native_rng_boundary`, require `cursor_after.counter = cursor_before.counter + 1`, a valid `selected_index`, and source owner identity or explicit `UNATTRIBUTED`.
3. Compare stream snapshots against the existing native RNG stream auditor (if captured on that pinned build). Check for **unaccounted** cursor increments and quantify pre-attachment, off-thread, context RNG and non-integer call sites.
4. Separately instrument mechanic-level legal candidate pools and causal hook entry/exit, without calling RNG. Until then, the native/emulator comparator returns `NATIVE_RNG_CALLSITE_UNATTRIBUTED` or bounded-index-only verdicts.

### Remaining implementation gates

- Audit native DLL signature and game process hook installation on the exact installed version.
- Cover pre-combat setup, context/local RNGs, float/double/Gaussian/shuffle composition, and multi-thread continuations.
- Record semantic site ID, eligible-pool order/weights, and exact action/choice parent; compare to the emulator provider journal by causal identity.
- Restore powers, relic/potion mutable state, skill counters and auto-play causes from independently verified before-state; verify complete combat parity.

This experiment changes only the Inspector integration branch on GitHub and the separate local native mod installation when the user explicitly runs the steps. **The developing all-Act branch and `~/projects/sts2-emulator` checkout remain untouched.**

## 2026-10-13: passive source-level scalar RNG capture (review branch)

The opt-in native observer additionally attempts independent Harmony
prefix/postfix probes on the pinned **zero-argument**
`Rng.NextDouble()` and `Rng.NextBool()` methods. Both methods consume
exactly one xoshiro256** output, using respectively the top 53 bits
divided by `2^53` and the game's bounded-`2` coin flip. The callbacks
copy `ToSerializable()` before/after and record the game's original
return value. Missing optional methods leave `NextInt` probes running.

Each new `native_rng_scalar_boundary` sidecar entry stores
`operation_kind=unit_double|boolean`, a typed selected value,
the actual native stream identity, source attribution/provenance,
action ID when available, and native cursor snapshots. The header reports
separately `native_rng_double_hook_installed` and
`native_rng_bool_hook_installed`. They are independent of the existing
bounded-integer hook installation flag.

`native_rng_boundary_verify.py` verifies both scalar outcomes using
the original xoshiro output, one-counter-step transition, and every
state word. Stream continuity now spans mixed integer, double and bool
calls; forward gaps still explicitly report uninstrumented advances.
`native_rng_boundary_compare.py` marks a native action containing
a scalar primitive as `UNSUPPORTED_NATIVE_RNG_OPERATION` until the
emulator has a corresponding typed provider boundary. Thus source
coverage cannot be silently mistaken for full emulator RNG parity.

Validate on the **actual pinned installed DLL** with `STS2_FIDELITY_RNG_HOOK=1`,
then inspect log fields `double_source_hook` and `bool_source_hook`
and corpus fields `native_rng_scalar_source_calls`,
`native_rng_stream_gaps`, `native_rng_stream_conflicts`.
Native exact-build compilation, attachment, and no-perturbation evidence
remain unverified by synthetic CI.

**Coverage boundary:** the range overloads, `NextFloat`,
unsigned/long draws, chaotic/context RNGs and pre-attachment calls
still require independent source capture or explicit gaps. The two
zero-argument new probes cover some Gaussian inner draws and boolean
effects, but do not prove every composed RNG caller was observed.

## 2026-10-15: NextFloat(float,float) source callback

Added **optional** `Rng.NextFloat(float min,float max)` Harmony
prefix/postfix on the dedicated review branch
`feature/v111-native-nextfloat-source-fidelity`. It records the unmodified
native return float, the original float32 bounds and exact 256-bit
xoshiro snapshots. The one-argument `NextFloat(max)` wrapper itself
is not patched: it delegates to `NextFloat(0f,max)` and must be counted
as only one native RNG call. Equality of bounds is allowed and consumes
one RNG step. `NextGaussianFloat` wraps Gaussian double and requires
separate analysis; this hook does not claim to cover all RNG primitives.

The sidecar event retains `type=native_rng_scalar_boundary` with
`operation_kind=range_float`, `float_min`, `float_max` and
`selected_float`, the real actor when `RollMove` scope exists, and
`site_id=UNATTRIBUTED` until the actual weighted eligible move pool
is independently instrumented. The native float arithmetic is verified
as `float32(unit_xoshiro_double * float32(max-min) + min)`, so
binary32 conversion is tested and a changed float result fails closed.

New game log field: `float_source_hook=INSTALLED|UNAVAILABLE`.
New header field: `native_rng_float_hook_installed`.
Corpus field: `native_rng_float_source_calls`.
Primitive verifier field: `float_calls`.

See `docs/V111_NATIVE_GAME_WIDE_RNG_MECHANIC_TEST.md` for the precise
replay commands and acceptance conditions against the previously observed
nine `run.monster_ai` uninstrumented steps. Native pinned DLL compiler
and runtime attachment still require the user's game-local run.

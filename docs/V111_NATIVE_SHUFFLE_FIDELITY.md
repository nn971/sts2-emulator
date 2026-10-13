# Native v0.111 Shuffle Fidelity — Source Stages and Replay Gate

**Integration:** [PR #137](https://github.com/nn971/sts2-emulator/pull/137), on current Silent all-Act-based source branch. Active all-Act development, `main`, and `~/projects/sts2-emulator` remain untouched.

## Source-audited mechanics

The pinned `Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20` implements:

1. **Initial:** `CardPile.RandomizeOrderInternal(Player,Rng,CombatState)` invokes `_cards.UnstableShuffle(rng)`, a test RNG override if any, and then `Hook.ModifyShuffleOrder(..., isInitialShuffle:true)`.
2. **Recycle:** `CardPileCmd.Shuffle(PlayerChoiceContext,Player)` assembles `DiscardPile.Cards.ToList()`, appends `DrawPile.Cards.ToHashSet()`, then calls `StableShuffle(RunState.Rng.Shuffle)`, `Hook.ModifyShuffleOrder(..., isInitialShuffle:false)`, optional `DebugForcedTopCardOnNextShuffle` modification, and later asynchronous card movement. **StableShuffle sorts the collected card models by their native ModelId and upgrade level before Fisher-Yates.**
3. **Fisher-Yates:** `ListExtensions.UnstableShuffle` calls `rng.NextInt(n)` for `n = count, count-1, ..., 2`. Native `Rng.Shuffle` uses the same descending-bound primitive pattern. The primitive RNG cursor increments once per call.

The **ordered candidate models** and the **final post-hook card order** must be compared separately. The correct primitive RNG selection does not by itself prove the card permutation.

## Implemented on integration branch

- `NativeFisherYatesV111.Execute` in the emulator core is a source-authentic, injectable RNG mechanics primitive. Its result contains ordered card-instance input, **pre-hook** output and each typed provider request/decision on `run.shuffle`. It accepts native, scripted and controlled providers. The caller owns native model sorting and subsequent card/relic hooks. It is **not yet wired into every ordinary `PrototypeGameEngine.Step` shuffle site**.
- `NativeV111ShuffleHooks` installs three **optional**, gated native Harmony patches: `CardPile.RandomizeOrderInternal`, `CardPileCmd.Shuffle` and `Hook.ModifyShuffleOrder`. The patches observe real source card objects, choice bounds and original results; scope-wise reference identities distinguish multiple copies of the same card model.
- A bounded initial-source buffer retains **pre-first-frame initial shuffle** primitive choices, final pre/post hook ordering and exact RNG pre/post snapshots until the independent combat action sidecar opens. These observations have no native player action parent; their chronology is explicitly `recorded_before_first_frame`. In-combat recycle hooks record directly.
- `native_shuffle_operation` sidecar records carry `operation_id`, kind `initial|recycle`, `ordered_input`, `choices` (original result plus native four state words/counter), `pre_hook_order`, `post_hook_order`, and source model IDs. All per-operation tokens are unique, including duplicate model IDs. They are **local** identities, not stable cross-game card-instance IDs.
- `NativeShuffleOperationEvidenceV111.Validate`, `native_shuffle_operation_verify.py`, and the action corpus auditor verify all descending bounds, exact xoshiro transitions, cursor continuity, deterministic Fisher-Yates swaps, and preservation of card identities through the post-shuffle hook.
- The unified fidelity gate now reports **`NATIVE_SHUFFLE_SOURCE_MISMATCH`** separately from primitive RNG mismatches and unsupported emulator checkpoints.

The native source hook patches compile only when `STS2_NATIVE` is enabled with the pinned installed game assemblies. The stand-alone CI runs the contract, emulator primitive and offline Python tests; successful CI **does not establish native game-hook installation**.

## Strict acceptance progression

1. Existing uploaded Shrinker Beetle v0.111 capture: 18/18 exact native `run.shuffle` primitive calls, in two descending-bound groups of 9; still `site_id=UNATTRIBUTED`, because that capture predates the source shuffle patch. These 18 calls were not sufficient to reconstruct the native pre/post card ordering.
2. **Next capture gate:** locally build the updated Inspector against pinned `sts2.dll`. Launch with the already enabled `STS2_FIDELITY_CAPTURE=1` and `STS2_FIDELITY_RNG_HOOK=1`. Require `shuffle_source_scope=INSTALLED` in the mod log and `native_shuffle_scope_installed=true` in the new sidecar. One combat containing initial and recycle shuffles will provide source proof.
3. Verify source evidence offline:
   ```fish
   set trace /mnt/d/SteamLibrary/sts2-inspector-traces
   python tools/native_action_envelope_batch.py "$trace" \\
       --out "$trace/native-rng-shuffle-audit.json"
   jq '{ native_shuffle_scope_installed_sidecars, native_shuffle_source_operations, native_shuffle_verified_operations, native_shuffle_errors }' "$trace/native-rng-shuffle-audit.json"
   ```
4. **Emulator full mechanics gate** (still open): use native source order to reconstruct a precise before-state, map locally unique native card objects to emulator instance IDs, source-audit every after-shuffle hook/modification, and run actual `PrototypeGameEngine.Step` with matched RNG outcomes, comparing the ordered successor states. Defend replay / Weak / Shriek / Pinpoint blockers remain independent tasks.
5. **Initial HP/intent initialization gate** (still open): instrument `Creature.SetUniqueMonsterHpValue` and actual combat entry before the first frame; restore hidden state and persistent run/player stream owners. The source initial-shuffle buffer is the first pre-frame data path, **not yet complete combat initialization capture**.

## Verdict discipline

`OBSERVED_SHUFFLE_STAGES_VALID` proves the observed native source-local sequence (exactly reproduced source Fisher-Yates swaps and hook-permitted card permutations). It does **not** mean whole-combat or native/emulator parity, original conditional law, full RNG call coverage, or equal native seed cursors in counterfactual controlled mode. Shuffles with changed/missing card identities fail closed. Initial pre-frame capture remains bounded and should not silently discard records.

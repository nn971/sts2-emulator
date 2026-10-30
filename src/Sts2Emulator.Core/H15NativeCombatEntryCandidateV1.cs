namespace Sts2Emulator.Core;

/// <summary>
/// A first-turn combat-entry *candidate* with its private RNG cursors
/// replaced by a separately seeded experimental chance prior.
/// Supports both native-structure/prototype-RNG training roots and
/// opt-in native-combat RNG roots, without conflating their provenance.
///
/// This is intentionally not a fair public-history posterior. In particular,
/// arbitrary source-specific hidden correlations, previously revealed top
/// cards, and stochastic card/relic hooks still require audited support.
/// Never export this candidate as H15 fair PUCT supervision.
/// </summary>
public static class H15NativeCombatEntryCandidateV1
{
    public const string CandidateModelId =
        "h15-native-combat-entry-independent-candidate-unverified-v1";
    public const string PrototypeCandidateModelId =
        "h15-native-structure-prototype-rng-candidate-unverified-v1";
    public const string NativeCombatProviderClass = "native-combat-rng";
    public const string PrototypeCombatProviderClass = "prototype-combat-rng";
    public const string FairSearchCapabilityId = "unavailable";

    public static string ProviderClass(RunState source) =>
        source.World?.Combat?.NativeRng is null
            ? PrototypeCombatProviderClass : NativeCombatProviderClass;

    public static string ModelId(RunState source) =>
        ProviderClass(source) == NativeCombatProviderClass
            ? CandidateModelId : PrototypeCandidateModelId;

    public static RunState Build(RunState source, string independentSeed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(independentSeed);
        if (source.Phase != RunPhase.Combat ||
            source.World?.Combat is not { } sourceCombat)
            throw new NotSupportedException(
                "H15 candidate requires an active combat entry.");
        if (sourceCombat.NativeRng is not null &&
            source.NativeRunRng is null)
            throw new InvalidOperationException(
                "Native combat provider requires a persistent native run owner.");
        if (sourceCombat.Turn != 1 ||
            !sourceCombat.IsPlayerTurn ||
            sourceCombat.PendingChoice is not null ||
            sourceCombat.AutomaticPipelineContinuation is not null ||
            sourceCombat.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(
                "H15 candidate requires a settled first-turn player boundary.");
        if (source.ReferenceNatureState is not null ||
            source.Stochastic.SemanticOtherChance is not null)
            throw new NotSupportedException(
                "H15 candidate cannot replace an already active Nature provider.");

        // The public projection serves as an invariant. No source RNG cursor,
        // physical hidden deck order or unknown future draw may change the
        // already-visible player state or currently committed enemy intent.
        var view = new PrototypeAiEnvironment();
        var before = view.ObserveH8(source);

        // A prototype-RNG native-structure run has no NativeCombatRng
        // sidecar. Leave that absence explicit and retain its mechanics.
        // A v111 configured run may have a run-owned native sidecar before
        // any native combat provider is active; replace it independently too.
        var independentNative = source.NativeRunRng is null
            ? null : NativeRngV111.CreateBundle(independentSeed);
        static RngStreamState IndependentContext(
            RngStreamState old, string seed) =>
            NativeRngV111.CreateStream(
                old.StreamId,
                NativeRngV111.HashString(seed + "\n" + old.StreamId));

        NativeCombatRngV111? independentCombatRng = null;
        if (sourceCombat.NativeRng is { } originalNative)
        {
            var originalFormation = originalNative.Formation;
            var formation = originalFormation with
            {
                EncounterRng = IndependentContext(
                    originalFormation.EncounterRng, independentSeed),
                MonsterContexts = originalFormation.MonsterContexts.Select(context =>
                    context with
                    {
                        Rng = IndependentContext(context.Rng, independentSeed)
                    }).ToArray()
            };
            independentCombatRng = originalNative with
            {
                RunStreams = independentNative!.Fork(),
                Formation = formation
            };
        }

        // Scrub the physical draw ordering as well as the semantic
        // provider's ordering. Remaining physical instances are unchanged.
        // Subsequent supported draws must be resolved through the Nature deck.
        var drawPile = (long[])sourceCombat.DrawPile.Clone();
        var shuffle = new Random(unchecked((int)NativeRngV111.HashString(
            independentSeed + "\ncombat.draw_pile")));
        for (var i = drawPile.Length - 1; i > 0; i--)
        {
            var j = shuffle.Next(i + 1);
            (drawPile[i], drawPile[j]) = (drawPile[j], drawPile[i]);
        }

        var withoutDeck = sourceCombat.Fork() with
        {
            SemanticDeck = null
        };
        var rekeyedCombat = withoutDeck with
        {
            DrawPile = drawPile,
            NativeRng = independentCombatRng
        };
        var candidateDeck = SemanticDeckEngineV111.BeginNature(
            drawPile.Reverse().ToArray());
        rekeyedCombat = SemanticDrawPileAdapterV111.InitializeSemanticDrawPile(
            rekeyedCombat, candidateDeck);

        var candidate = source.Fork() with
        {
            Rng = PrototypeRng.CreateBundle(independentSeed),
            NativeRunRng = independentNative?.Fork(),
            ReferenceNatureState = null,
            World = source.World with { Combat = rekeyedCombat }
        };

        PrototypeStateInvariants.Validate(candidate);
        var after = view.ObserveH8(candidate);
        if (CanonicalJson.Sha256(before.Observation) !=
                CanonicalJson.Sha256(after.Observation) ||
            !before.LegalActions.Select(x => x.ActionId).SequenceEqual(
                after.LegalActions.Select(x => x.ActionId)) ||
            !before.LegalActions.Select(x => x.Kind).SequenceEqual(
                after.LegalActions.Select(x => x.Kind)))
            throw new InvalidOperationException(
                "H15 independent candidate changed the public combat decision.");

        return candidate;
    }
}

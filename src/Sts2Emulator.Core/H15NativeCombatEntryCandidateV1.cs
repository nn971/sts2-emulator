using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// A source-native combat-entry *candidate* with its live source RNG cursors
/// replaced by a separately seeded experimental chance prior.
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
    public const string FairSearchCapabilityId = "unavailable";

    public static RunState Build(RunState source, string independentSeed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(independentSeed);
        if (source.Phase != RunPhase.Combat ||
            source.World?.Combat is not { } sourceCombat ||
            source.NativeRunRng is null ||
            sourceCombat.NativeRng is null)
            throw new NotSupportedException(
                "H15 native candidate requires a live native combat entry.");
        if (sourceCombat.Turn != 1 ||
            !sourceCombat.IsPlayerTurn ||
            sourceCombat.PendingChoice is not null ||
            sourceCombat.AutomaticPipelineContinuation is not null ||
            sourceCombat.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(
                "H15 native candidate requires a settled first-turn player boundary.");
        if (source.ReferenceNatureState is not null ||
            source.Stochastic.SemanticOtherChance is not null)
            throw new NotSupportedException(
                "H15 candidate cannot replace an already active Nature provider.");

        // The public projection serves as an invariant. No source RNG cursor,
        // physical hidden deck order or unknown future draw may change the
        // already-visible player state or currently committed enemy intent.
        var view = new PrototypeAiEnvironment();
        var before = view.ObserveH8(source);

        var independentNative = NativeRngV111.CreateBundle(independentSeed);
        static RngStreamState IndependentContext(
            RngStreamState old, string seed) =>
            NativeRngV111.CreateStream(
                old.StreamId,
                NativeRngV111.HashString(seed + "\n" + old.StreamId));

        var originalFormation = sourceCombat.NativeRng.Formation;
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

        // Scrub the native physical draw ordering as well as the semantic
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
            NativeRng = sourceCombat.NativeRng with
            {
                RunStreams = independentNative.Fork(),
                Formation = formation
            }
        };
        var candidateDeck = SemanticDeckEngineV111.BeginNature(
            drawPile.Reverse().ToArray());
        rekeyedCombat = SemanticDrawPileAdapterV111.InitializeSemanticDrawPile(
            rekeyedCombat, candidateDeck);

        var candidate = source.Fork() with
        {
            Rng = PrototypeRng.CreateBundle(independentSeed),
            NativeRunRng = independentNative.Fork(),
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

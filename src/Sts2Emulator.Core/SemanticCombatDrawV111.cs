using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// A provenance-replayable, narrowly scoped semantic draw segment. The source
/// is a supplied concrete combat snapshot whose unrevealed draw-pile
/// instances are available, but whose exact order has not been committed by
/// the semantic Nature provider. No legacy swap tickets are created.
/// </summary>
public sealed record SemanticCombatDrawV111(
    RunState Origin, RunState Run, int RequestedCount, int RemainingCount,
    bool FromHandDraw)
{
    public const string SchemaId = "sts2-v111-semantic-real-combat-draw-v1";
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public SemanticDeckEventV111? Pending =>
        Run.World?.Combat?.SemanticDeck is { } deck
            ? SemanticDeckEngineV111.Inspect(deck) : null;
    [JsonIgnore] public bool AtBoundary => Pending is null && RemainingCount == 0;
    [JsonIgnore] public SemanticDeckChoiceV111[] Transcript =>
        Run.World?.Combat?.SemanticDeck?.Transcript ?? [];

    public SemanticCombatDrawV111 Fork() => this with
    {
        Origin = Origin.Fork(), Run = Run.Fork()
    };
}

public static class SemanticCombatDrawEngineV111
{
    public static SemanticCombatDrawV111 Begin(
        RunState source, int cardCount, bool fromHandDraw = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (cardCount <= 0) throw new ArgumentOutOfRangeException(nameof(cardCount));
        if (source.Phase != RunPhase.Combat ||
            source.World?.Combat is not { } combat ||
            combat.SemanticDeck is not null ||
            combat.PendingChoice is not null ||
            combat.AutomaticPipelineContinuation is not null)
            throw new InvalidOperationException(
                "Expected an ordinary pre-draw combat without a pending choice.");
        var baseRun = source.Fork();
        var world = baseRun.World!;
        var attached = baseRun with
        {
            World = world with
            {
                Combat = world.Combat! with
                {
                    SemanticDeck = SemanticDeckEngineV111.BeginNature(
                        world.Combat!.DrawPile)
                }
            }
        };
        RunState advanced;
        int remaining;
        using (ControlledChanceGuardV111.Enter())
            (advanced, remaining) =
                PrototypeGameEngine.BeginSemanticCombatDraw(
                    attached, cardCount, fromHandDraw);
        var result = new SemanticCombatDrawV111(
            baseRun, advanced, cardCount, remaining, fromHandDraw);
        Validate(result);
        return result;
    }

    /// <summary>Nature chooses a physical instance at the saved draw site;
    /// the normal mechanical DrawCards routine then performs the draw,
    /// cost checks and CardDrawn hooks before another Nature request.</summary>
    public static SemanticCombatDrawV111 Resolve(
        SemanticCombatDrawV111 state, string fingerprint, int selectedIndex)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic combat draw fingerprint.");
        var run = state.Run.Fork();
        var world = run.World!;
        var combat = world.Combat!;
        var deck = combat.SemanticDeck!;
        if (deck.Pending is null || state.RemainingCount <= 0)
            throw new InvalidOperationException("No semantic draw card pending.");
        var resolvedDeck = SemanticDeckEngineV111.Resolve(
            deck, deck.Fingerprint, selectedIndex);
        run = run with
        {
            World = world with
            {
                Combat = combat with { SemanticDeck = resolvedDeck }
            }
        };
        RunState advanced;
        int remaining;
        using (ControlledChanceGuardV111.Enter())
            (advanced, remaining) =
                PrototypeGameEngine.BeginSemanticCombatDraw(
                    run, state.RemainingCount, state.FromHandDraw);
        var next = state with { Run = advanced, RemainingCount = remaining };
        Validate(next);
        return next;
    }

    public static string Save(SemanticCombatDrawV111 state)
    {
        Validate(state);
        return JsonSerializer.Serialize(state);
    }

    /// <summary>Full replay from the supplied source combat and each
    /// semantic Nature decision, with exact resulting RunState comparison.
    /// An altered pending event or transcript fails even if locally valid.</summary>
    public static SemanticCombatDrawV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var stored = JsonSerializer.Deserialize<SemanticCombatDrawV111>(json)
            ?? throw new InvalidDataException("Null semantic combat draw.");
        Validate(stored);
        var reconstructed = Begin(
            stored.Origin, stored.RequestedCount, stored.FromHandDraw);
        foreach (var decision in stored.Transcript)
        {
            if (reconstructed.Pending is not { } pending ||
                pending.EventId != decision.EventId ||
                pending.Ordinal != decision.Ordinal ||
                pending.SiteId != decision.SiteId ||
                !pending.CandidateCardInstanceIds.SequenceEqual(
                    decision.CandidateCardInstanceIds) ||
                pending.Purpose != decision.Purpose ||
                pending.TopOffset != decision.TopOffset)
                throw new InvalidDataException(
                    "Semantic draw transcript disagrees with replayed mechanics.");
            reconstructed = Resolve(
                reconstructed, reconstructed.Fingerprint, decision.SelectedIndex);
        }
        if (CanonicalJson.Sha256(reconstructed) != CanonicalJson.Sha256(stored))
            throw new InvalidDataException(
                "Semantic combat draw snapshot fails complete provenance replay.");
        return reconstructed;
    }

    public static void Validate(SemanticCombatDrawV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.RequestedCount <= 0 ||
            state.RemainingCount < 0 ||
            state.RemainingCount > state.RequestedCount ||
            state.Origin.Phase != RunPhase.Combat ||
            state.Origin.World?.Combat is not { } originCombat ||
            originCombat.SemanticDeck is not null ||
            originCombat.AutomaticPipelineContinuation is not null ||
            originCombat.PendingChoice is not null ||
            state.Run.Phase != RunPhase.Combat ||
            state.Run.World?.Combat is not { } combat ||
            combat.SemanticDeck is not { } deck ||
            combat.PendingChoice is not null ||
            combat.AutomaticPipelineContinuation is not null ||
            (state.RemainingCount > 0) != (deck.Pending is not null))
            throw new InvalidDataException("Invalid semantic combat draw stage.");
        SemanticDeckEngineV111.Validate(deck);
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Semantic provider state and physical draw-pile membership differ.");
    }
}

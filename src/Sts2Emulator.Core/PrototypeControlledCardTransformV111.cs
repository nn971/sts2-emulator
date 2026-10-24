namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static string[] EligibleCombatTransformPool(
        string originalCardId, CombatState combat)
    {
        var pool = originalCardId.StartsWith(
                "proto.colorless.", StringComparison.Ordinal)
            ? ColorlessCombatGenerationPool(combat)
            : PrototypeContent.RewardCardPool;
        return pool.Where(id =>
        {
            var candidate = PrototypeContent.Card(id);
            return id != originalCardId && candidate.CanBeGeneratedInCombat
                && candidate.MechanicsImplemented
                && candidate.Rarity is PrototypeCardRarity.Common
                    or PrototypeCardRarity.Uncommon
                    or PrototypeCardRarity.Rare;
        }).ToArray();
    }

    private static CombatState MakeControlledCardTransformChoice(
        CombatState combat,
        PendingCombatChoiceState originalChoice,
        long[] selectedCardInstanceIds,
        string[]? replacementCardIds = null,
        int nextTransformIndex = 0)
    {
        if (!ControlledChanceGuardV111.Active ||
            originalChoice.ChoiceId != "select_cards" ||
            originalChoice.Selection.Resolution !=
                PrototypeCardSelectionResolutionKind.TransformRandom ||
            selectedCardInstanceIds.Length == 0 ||
            nextTransformIndex < 0 ||
            nextTransformIndex >= selectedCardInstanceIds.Length)
            throw new InvalidOperationException(
                "Controlled transform requires a live random-transform selection.");

        var replacements = replacementCardIds is null
            ? new string[selectedCardInstanceIds.Length]
            : (string[])replacementCardIds.Clone();
        var instanceId = selectedCardInstanceIds[nextTransformIndex];
        var original = RequireCombatCard(combat, instanceId);
        if (PrototypeCardRules.IsEternal(original))
            throw new InvalidOperationException(
                "Eternal combat cards cannot be transformed.");
        var candidates = EligibleCombatTransformPool(original.CardId, combat);
        if (candidates.Length == 0)
            throw new NotSupportedException(
                "No eligible native-pool solo transform targets.");

        var transform = new PrototypeControlledCardTransformV111(
            originalChoice.Fork(),
            (long[])selectedCardInstanceIds.Clone(),
            replacements, nextTransformIndex, candidates);
        return combat with
        {
            PendingChoice = originalChoice with
            {
                ChoiceId = "controlled_card_transform",
                CandidateCardInstanceIds = [],
                ControlledCardTransform = transform
            }
        };
    }

    internal static bool ValidateControlledCardTransform(
        CombatState combat,
        PrototypeControlledCardTransformV111 transform)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(transform);
        var pending = combat.PendingChoice;
        if (pending is not { ChoiceId: "controlled_card_transform" } ||
            pending.ControlledCardTransform is null ||
            transform.OriginalChoice.ChoiceId != "select_cards" ||
            transform.OriginalChoice.Selection.Resolution !=
                PrototypeCardSelectionResolutionKind.TransformRandom ||
            transform.SelectedCardInstanceIds is not { Length: > 0 } selected ||
            selected.Any(id => id <= 0) || selected.Distinct().Count() != selected.Length ||
            transform.ReplacementCardIds is not { } replacements ||
            replacements.Length != selected.Length ||
            transform.NextTransformIndex < 0 ||
            transform.NextTransformIndex >= selected.Length ||
            transform.CandidateCardIds is not { Length: > 0 } candidates ||
            selected.Any(id => !GetZone(combat,
                transform.OriginalChoice.Selection.SourceZone).Contains(id)))
            return false;

        var restoredPending = pending with
        {
            ChoiceId = transform.OriginalChoice.ChoiceId,
            CandidateCardInstanceIds =
                (long[])transform.OriginalChoice.CandidateCardInstanceIds.Clone(),
            ControlledCardTransform = null
        };
        if (CanonicalJson.Sha256(restoredPending) !=
            CanonicalJson.Sha256(transform.OriginalChoice))
            return false;

        for (var index = 0; index < replacements.Length; index++)
        {
            var original = RequireCombatCard(combat, selected[index]);
            if (PrototypeCardRules.IsEternal(original))
                return false;
            var pool = EligibleCombatTransformPool(original.CardId, combat);
            if (pool.Length == 0 ||
                (index < transform.NextTransformIndex
                    ? string.IsNullOrWhiteSpace(replacements[index]) ||
                      !pool.Contains(replacements[index], StringComparer.Ordinal)
                    : !string.IsNullOrEmpty(replacements[index])))
                return false;
        }

        var currentOriginal = RequireCombatCard(
            combat, selected[transform.NextTransformIndex]);
        return candidates.SequenceEqual(
            EligibleCombatTransformPool(currentOriginal.CardId, combat));
    }

    internal static RunState ResumeControlledCardTransform(
        RunState state, string selectedCardId)
        => ResumeControlledCardTransformWithBoundary(
            state, selectedCardId).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledCardTransformWithBoundary(
            RunState state, string selectedCardId)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled transform requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ?? throw new InvalidOperationException(
            "Missing combat for controlled card transform.");
        var pending = combat.PendingChoice ?? throw new InvalidOperationException(
            "No controlled card transform is pending.");
        var transform = pending.ControlledCardTransform ??
            throw new InvalidOperationException(
                "Pending combat choice is not a controlled card transform.");
        if (!ValidateControlledCardTransform(combat, transform))
            throw new InvalidDataException(
                "Controlled transform continuation is not source-reachable.");
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedCardId);
        if (!transform.CandidateCardIds.Contains(
                selectedCardId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Selected transform identity is outside the saved source pool.");

        var replacements = (string[])transform.ReplacementCardIds.Clone();
        replacements[transform.NextTransformIndex] =
            selectedCardId;
        var nextIndex = transform.NextTransformIndex + 1;
        if (nextIndex < transform.SelectedCardInstanceIds.Length)
        {
            combat = MakeControlledCardTransformChoice(
                combat, transform.OriginalChoice,
                transform.SelectedCardInstanceIds, replacements, nextIndex);
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                state with { World = world with { Combat = combat } });
        }

        var replacementMap = transform.SelectedCardInstanceIds
            .Select((instanceId, index) =>
                new KeyValuePair<long, string>(instanceId, replacements[index]))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        combat = combat with
        {
            PendingChoice = transform.OriginalChoice.Fork()
        };
        state = state with { World = world with { Combat = combat } };
        return ResolvePendingChoiceWithBoundary(
            state, transform.SelectedCardInstanceIds,
            replacementMap, selectionAlreadyResolved: true);
    }
}

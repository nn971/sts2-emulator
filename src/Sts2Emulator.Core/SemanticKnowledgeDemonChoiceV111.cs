using System.Text.Json;

namespace Sts2Emulator.Core;

public sealed record SemanticKnowledgeDemonChoiceCandidateV111(
    string ActionId, string CardId, string Kind, string PayloadFingerprint);

/// <summary>A source action choice made while the automatic enemy pipeline
/// is suspended at Curse of Knowledge.</summary>
public sealed record SemanticKnowledgeDemonChoiceEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, int EnemyInstanceId, int EnemyTurn,
    int CurseIndex, string[] OfferedCardIds,
    SemanticKnowledgeDemonChoiceCandidateV111[] Candidates)
{
    private string[]? _offeredCardIds = CloneOptional(OfferedCardIds);
    private SemanticKnowledgeDemonChoiceCandidateV111[]? _candidates =
        CloneCandidates(Candidates);

    public string[] OfferedCardIds
    {
        get => CloneOptional(_offeredCardIds)!;
        init => _offeredCardIds = CloneOptional(value);
    }
    public SemanticKnowledgeDemonChoiceCandidateV111[] Candidates
    {
        get => CloneCandidates(_candidates)!;
        init => _candidates = CloneCandidates(value);
    }

    public const string CurrentSchemaId =
        "sts2-v111-semantic-knowledge-demon-choice-v1";

    public SemanticKnowledgeDemonChoiceEventV111 Fork() => this with
    {
        OfferedCardIds = _offeredCardIds!,
        Candidates = _candidates!
    };

    public void Validate()
    {
        var offeredCardIds = _offeredCardIds;
        var candidates = _candidates;
        if (SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            EnemyInstanceId < 0 || EnemyTurn < 1 || CurseIndex is < 0 or > 2 ||
            offeredCardIds is not { Length: 2 } ||
            !offeredCardIds.SequenceEqual(
                PrototypeNativeHiveKnowledgeDemonChoice.Options(CurseIndex)) ||
            candidates is not { Length: 2 } ||
            candidates.Any(candidate => candidate is null ||
                string.IsNullOrWhiteSpace(candidate.ActionId) ||
                string.IsNullOrWhiteSpace(candidate.CardId) ||
                string.IsNullOrWhiteSpace(candidate.Kind) ||
                string.IsNullOrWhiteSpace(candidate.PayloadFingerprint)) ||
            !candidates.Select(candidate => candidate.CardId)
                .SequenceEqual(offeredCardIds) ||
            candidates.Select(candidate => candidate.ActionId).Distinct()
                .Count() != candidates.Length ||
            SupportFingerprint != CanonicalJson.Sha256(new
            {
                EnemyInstanceId,
                EnemyTurn,
                CurseIndex,
                OfferedCardIds = offeredCardIds,
                Candidates = candidates
            }) ||
            EventId != CanonicalJson.Sha256(new
            {
                SchemaId,
                BoundaryFingerprint,
                SupportFingerprint
            }))
            throw new InvalidDataException(
                "Invalid semantic Knowledge Demon choice event.");
    }

    private static string[]? CloneOptional(string[]? values) => values is null
        ? null
        : (string[])values.Clone();

    private static SemanticKnowledgeDemonChoiceCandidateV111[]?
        CloneCandidates(SemanticKnowledgeDemonChoiceCandidateV111[]? values) =>
            values?.Select(candidate => candidate is null
                ? null!
                : candidate with { }).ToArray();
}

public sealed record SemanticKnowledgeDemonChoiceDecisionV111(
    SemanticKnowledgeDemonChoiceEventV111 Event, string ActionId,
    string SelectedCardId)
{
    public SemanticKnowledgeDemonChoiceDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (!Event.Candidates.Any(candidate =>
                candidate.ActionId == ActionId &&
                candidate.CardId == SelectedCardId))
            throw new InvalidDataException(
                "Knowledge Demon selection is outside its source support.");
    }
}

public static class SemanticKnowledgeDemonChoiceEngineV111
{
    public static SemanticKnowledgeDemonChoiceEventV111? DescribePending(
        RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var combat = source.World?.Combat;
        var pending = combat?.PendingKnowledgeDemonChoice;
        if (pending is null)
            return null;
        if (source.Phase != RunPhase.Combat || combat is not
                { IsPlayerTurn: false, PendingChoice: null,
                    AutomaticPipelineContinuation: { } continuation } ||
            !continuation.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.ResolveEnemyActions) ||
            pending.EnemyTurn != combat.Turn ||
            combat.KnowledgeDemonChoiceResolvedTurn == combat.Turn ||
            pending.CurseIndex is < 0 or > 2 ||
            !pending.OptionCardIds.SequenceEqual(
                PrototypeNativeHiveKnowledgeDemonChoice.Options(
                    pending.CurseIndex)))
            throw new InvalidDataException(
                "Knowledge Demon choice is outside its source pipeline cursor.");

        var enemy = combat.Enemies.SingleOrDefault(candidate =>
            candidate.InstanceId == pending.EnemyInstanceId &&
            candidate.EnemyId == PrototypeNativeHiveBosses.KnowledgeId &&
            candidate.Hp > 0 && candidate.PlannedMoveIndex == 0)
            ?? throw new InvalidDataException(
                "Pending Knowledge Demon no longer owns its move choice.");
        if ((enemy.MoveUseCounts?.GetValueOrDefault("curse_of_knowledge") ?? 0)
                != pending.CurseIndex)
            throw new InvalidDataException(
                "Knowledge Demon choice differs from its committed move index.");

        PrototypeStateInvariants.Validate(source);
        var actions = new PrototypeAiEnvironment().Observe(source).LegalActions;
        var candidates = actions.Select(action =>
        {
            if (action.Kind != "choose_knowledge_curse")
                throw new InvalidDataException(
                    "Knowledge Demon source exposed an unexpected action.");
            var cardId = action.Payload.Deserialize<ChooseKnowledgeCursePayload>()
                ?.CardId ?? throw new InvalidDataException(
                    "Knowledge Demon action omitted its selected card.");
            return new SemanticKnowledgeDemonChoiceCandidateV111(
                action.ActionId, cardId, action.Kind,
                CanonicalJson.Sha256(action.Payload));
        }).ToArray();
        if (candidates.Length != 2 ||
            !candidates.Select(candidate => candidate.CardId)
                .SequenceEqual(pending.OptionCardIds))
            throw new InvalidDataException(
                "Knowledge Demon source actions differ from its ordered offer.");

        var boundary = CanonicalJson.Sha256(source);
        var support = CanonicalJson.Sha256(new
        {
            EnemyInstanceId = pending.EnemyInstanceId,
            EnemyTurn = pending.EnemyTurn,
            CurseIndex = pending.CurseIndex,
            OfferedCardIds = pending.OptionCardIds,
            Candidates = candidates
        });
        var schema = SemanticKnowledgeDemonChoiceEventV111.CurrentSchemaId;
        var result = new SemanticKnowledgeDemonChoiceEventV111(
            schema,
            CanonicalJson.Sha256(new
            {
                SchemaId = schema,
                BoundaryFingerprint = boundary,
                SupportFingerprint = support
            }), boundary, support, pending.EnemyInstanceId,
            pending.EnemyTurn, pending.CurseIndex,
            (string[])pending.OptionCardIds.Clone(), candidates);
        result.Validate();
        return result;
    }

    public static SemanticKnowledgeDemonChoiceDecisionV111 Select(
        RunState source, SemanticKnowledgeDemonChoiceEventV111 pending,
        string actionId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        pending.Validate();
        var current = DescribePending(source);
        if (current is null || CanonicalJson.Sha256(current) !=
                CanonicalJson.Sha256(pending))
            throw new InvalidDataException(
                "Stale semantic Knowledge Demon choice event.");
        var candidate = ValidateCurrentAction(source, pending, actionId);

        var decision = new SemanticKnowledgeDemonChoiceDecisionV111(
            pending.Fork(), actionId, candidate.CardId);
        decision.Validate();
        return decision;
    }

    public static (RunState Run,
        SemanticKnowledgeDemonChoiceDecisionV111 Decision) Resolve(
        RunState source, SemanticKnowledgeDemonChoiceEventV111 pending,
        string actionId)
    {
        var decision = Select(source, pending, actionId);
        return (Resume(source, decision), decision);
    }

    public static RunState Resume(
        RunState source, SemanticKnowledgeDemonChoiceDecisionV111 decision)
        => ResumeWithBoundary(source, decision).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeWithBoundary(
            RunState source,
            SemanticKnowledgeDemonChoiceDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var current = DescribePending(source);
        if (current is null || CanonicalJson.Sha256(current) !=
                CanonicalJson.Sha256(ownedDecision.Event))
            throw new InvalidDataException(
                "Stale semantic Knowledge Demon choice decision.");
        _ = ValidateCurrentAction(source, current, ownedDecision.ActionId,
            ownedDecision.SelectedCardId);

        var resumed = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine.ResolveKnowledgeDemonChoiceWithBoundary(
                source.Fork(), GameAction.Create(
                    "choose_knowledge_curse",
                    new ChooseKnowledgeCursePayload(
                        ownedDecision.SelectedCardId))),
            allowEngineOwnedPrototypeReward: true);
        if (resumed.Run.Phase is not (
                RunPhase.Combat or RunPhase.Reward or RunPhase.Terminal) ||
            resumed.Run.World?.Combat?.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_ACTION site=knowledge_demon.choice " +
                "operation=unexpected_resume_result");
        return resumed;
    }

    private static SemanticKnowledgeDemonChoiceCandidateV111
        ValidateCurrentAction(
            RunState source, SemanticKnowledgeDemonChoiceEventV111 pending,
            string actionId, string? selectedCardId = null)
    {
        var candidate = pending.Candidates.SingleOrDefault(item =>
            item.ActionId == actionId) ?? throw new InvalidOperationException(
                "Action is outside the Knowledge Demon choice support.");
        var action = new PrototypeAiEnvironment().Observe(source).LegalActions
            .SingleOrDefault(item => item.ActionId == actionId) ??
            throw new InvalidOperationException(
                "Knowledge Demon action is no longer legal.");
        if (action.Kind != candidate.Kind ||
            CanonicalJson.Sha256(action.Payload) !=
                candidate.PayloadFingerprint ||
            (selectedCardId is not null &&
                candidate.CardId != selectedCardId))
            throw new InvalidDataException(
                "Knowledge Demon action payload changed during resolution.");
        return candidate;
    }
}

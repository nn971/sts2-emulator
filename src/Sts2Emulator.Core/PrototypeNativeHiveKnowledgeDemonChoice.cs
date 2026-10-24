namespace Sts2Emulator.Core;

/// <summary>
/// KnowledgeDemon.CurseOfKnowledge (pinned v0.111.0): the two displayed
/// status cards are immediately resolved via IChoosable.OnChosen and are
/// never generated into hand, discard or the persistent deck.
/// </summary>
public static class PrototypeNativeHiveKnowledgeDemonChoice
{
    public const string DisintegrationCardId =
        "proto.native.hive.knowledge.disintegration";
    public const string MindRotCardId =
        "proto.native.hive.knowledge.mind_rot";
    public const string SlothCardId =
        "proto.native.hive.knowledge.sloth";
    public const string WasteAwayCardId =
        "proto.native.hive.knowledge.waste_away";

    public const string DisintegrationPowerId =
        "proto.native.hive.knowledge.disintegration_power";
    public const string MindRotPowerId =
        "proto.native.hive.knowledge.mind_rot_power";
    public const string SlothPowerId =
        "proto.native.hive.knowledge.sloth_power";
    public const string WasteAwayPowerId =
        "proto.native.hive.knowledge.waste_away_power";

    public static string[] Options(int curseIndex) =>
        curseIndex switch
        {
            0 => [DisintegrationCardId, MindRotCardId],
            1 => [DisintegrationCardId, SlothCardId],
            2 => [DisintegrationCardId, WasteAwayCardId],
            _ => throw new ArgumentOutOfRangeException(nameof(curseIndex),
                "Curse of Knowledge has precisely three selectable sets.")
        };

    public static PrototypeCardDefinition[] Cards => CardDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneCardDefinition)
        .ToArray();

    private static readonly PrototypeCardDefinition[] CardDefinitions =
    [
        new(DisintegrationCardId, "Disintegration", -1,
            PrototypeCardTarget.None, [], Rarity: PrototypeCardRarity.Status,
            Type: PrototypeCardType.Status, Unplayable: true,
            RewardEligible: false, CanBeGeneratedInCombat: false,
            MaxUpgradeLevel: 0),
        new(MindRotCardId, "Mind Rot", -1,
            PrototypeCardTarget.None, [], Rarity: PrototypeCardRarity.Status,
            Type: PrototypeCardType.Status, Unplayable: true,
            RewardEligible: false, CanBeGeneratedInCombat: false,
            MaxUpgradeLevel: 0),
        new(SlothCardId, "Sloth", -1,
            PrototypeCardTarget.None, [], Rarity: PrototypeCardRarity.Status,
            Type: PrototypeCardType.Status, Unplayable: true,
            RewardEligible: false, CanBeGeneratedInCombat: false,
            MaxUpgradeLevel: 0),
        new(WasteAwayCardId, "Waste Away", -1,
            PrototypeCardTarget.None, [], Rarity: PrototypeCardRarity.Status,
            Type: PrototypeCardType.Status, Unplayable: true,
            RewardEligible: false, CanBeGeneratedInCombat: false,
            MaxUpgradeLevel: 0)
    ];

    public static PrototypePowerDefinition[] Powers => PowerDefinitions
        .Select(PrototypeDefinitionCopiesV111.ClonePowerDefinition)
        .ToArray();

    private static readonly PrototypePowerDefinition[] PowerDefinitions =
    [
        new(DisintegrationPowerId, "Disintegration", 0, [], IsDebuff: true),
        new(MindRotPowerId, "Mind Rot", 0, [], IsDebuff: true,
            HandDrawBonusPerStack: -1),
        // Native SlothPower holds a three-card limit, not three stacks
        // of a separate modifier.
        new(SlothPowerId, "Sloth", 0, [], IsDebuff: true,
            MaxCardsPlayablePerTurn: 3),
        new(WasteAwayPowerId, "Waste Away", 0, [], IsDebuff: true,
            EnergyAfterResetPerStack: -1)
    ];
}

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// Suspend before the precommitted Curse of Knowledge move. The normal
    /// enemy action is resumed after the choice on the *same* turn, so its
    /// move usage/AI successor advance exactly once.
    /// </summary>
    private static CombatState BeginKnowledgeDemonChoice(
        CombatState combat)
    {
        if (combat.KnowledgeDemonChoiceResolvedTurn == combat.Turn
            || combat.PendingKnowledgeDemonChoice is not null)
            return combat;
        var enemy = combat.Enemies.FirstOrDefault(candidate =>
            candidate.Hp > 0
            && candidate.EnemyId == PrototypeNativeHiveBosses.KnowledgeId
            && candidate.EnemyActionSkipsRemaining == 0
            && !candidate.SkipNextEnemyAction
            && candidate.PlannedMoveIndex == 0);
        if (enemy is null)
            return combat;
        var curseIndex =
            enemy.MoveUseCounts?.GetValueOrDefault("curse_of_knowledge") ?? 0;
        if (curseIndex is < 0 or > 2)
            throw new InvalidOperationException(
                "Knowledge Demon attempted a fourth Curse of Knowledge.");
        return combat with
        {
            PendingKnowledgeDemonChoice =
                new PrototypeKnowledgeDemonChoiceState(
                    enemy.InstanceId, combat.Turn, curseIndex,
                    PrototypeNativeHiveKnowledgeDemonChoice.Options(curseIndex))
        };
    }

    private static IReadOnlyList<GameAction> GetKnowledgeDemonChoiceActions(
        PrototypeKnowledgeDemonChoiceState choice) =>
        choice.OptionCardIds.Select(id =>
            GameAction.Create("choose_knowledge_curse",
                new ChooseKnowledgeCursePayload(id))).ToArray();

    private static RunState ResolveKnowledgeDemonChoice(
        RunState state, GameAction action)
        => ResolveKnowledgeDemonChoiceWithBoundary(state, action).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResolveKnowledgeDemonChoiceWithBoundary(
            RunState state, GameAction action)
    {
        RequireKind(action, "choose_knowledge_curse");
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Missing Knowledge Demon combat.");
        var pending = combat.PendingKnowledgeDemonChoice
            ?? throw new InvalidOperationException("No pending Knowledge Demon choice.");
        if (pending.EnemyTurn != combat.Turn || combat.IsPlayerTurn
            || combat.KnowledgeDemonChoiceResolvedTurn == combat.Turn)
            throw new InvalidOperationException(
                "Knowledge Demon choice has an invalid enemy-side turn.");
        var enemy = combat.Enemies.SingleOrDefault(candidate =>
            candidate.InstanceId == pending.EnemyInstanceId
            && candidate.EnemyId == PrototypeNativeHiveBosses.KnowledgeId
            && candidate.Hp > 0 && candidate.PlannedMoveIndex == 0)
            ?? throw new InvalidOperationException(
                "Knowledge Demon no longer owns the pending curse choice.");
        var index = enemy.MoveUseCounts?.GetValueOrDefault(
            "curse_of_knowledge") ?? 0;
        if (index != pending.CurseIndex
            || !pending.OptionCardIds.SequenceEqual(
                PrototypeNativeHiveKnowledgeDemonChoice.Options(index)))
            throw new InvalidOperationException(
                "Knowledge Demon choice no longer matches the committed move.");

        var chosen = action.ReadPayload<ChooseKnowledgeCursePayload>().CardId;
        if (!pending.OptionCardIds.Contains(chosen, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Chosen Knowledge Demon card was not offered.");
        var (powerId, amount) = chosen switch
        {
            PrototypeNativeHiveKnowledgeDemonChoice.DisintegrationCardId =>
                (PrototypeNativeHiveKnowledgeDemonChoice.DisintegrationPowerId,
                    6 + index),
            PrototypeNativeHiveKnowledgeDemonChoice.MindRotCardId =>
                (PrototypeNativeHiveKnowledgeDemonChoice.MindRotPowerId, 1),
            PrototypeNativeHiveKnowledgeDemonChoice.SlothCardId =>
                (PrototypeNativeHiveKnowledgeDemonChoice.SlothPowerId, 3),
            PrototypeNativeHiveKnowledgeDemonChoice.WasteAwayCardId =>
                (PrototypeNativeHiveKnowledgeDemonChoice.WasteAwayPowerId, 1),
            _ => throw new InvalidOperationException("Invalid Knowledge Demon card.")
        };
        combat = ApplyPlayerPower(combat, powerId, amount) with
        {
            PendingKnowledgeDemonChoice = null,
            KnowledgeDemonChoiceResolvedTurn = combat.Turn
        };
        if (combat.AutomaticPipelineContinuation is null)
            throw new InvalidOperationException(
                "Knowledge Demon choice lost its automatic-step continuation.");
        return ResumeAutomaticPipelineContinuation(
            state with { World = world with { Combat = combat } });
    }
}

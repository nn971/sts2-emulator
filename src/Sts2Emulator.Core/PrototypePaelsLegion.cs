namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static (CombatState Combat, int Amount) ModifyPlayerBlockGainWithLegion(
        CombatState combat,
        int amount,
        bool fromCard,
        long? sourceCardInstanceId)
    {
        var modified = ModifyPlayerBlockGain(combat, amount, fromCard, sourceCardInstanceId);
        if (!fromCard || sourceCardInstanceId is null || modified <= 0)
            return (combat, modified);

        var relics = (CombatRelicState[])combat.RelicStates.Clone();
        var changed = false;
        for (var index = 0; index < relics.Length; index++)
        {
            var relic = relics[index];
            if (relic.IsMelted || relic.RelicId != NativePaelRewardsV111.LegionId
                || relic.PaelsLegionCooldown > 0)
                continue;

            // Pael's Legion's ModifyBlockMultiplicative doubles player card
            // block while ready. AfterModifyingBlockAmount remembers the
            // particular card play only when the final gain is positive.
            modified *= 2;
            if (relic.PaelsLegionAffectedCardPlaySourceId is null
                || relic.PaelsLegionAffectedCardPlaySourceId == sourceCardInstanceId)
            {
                relics[index] = relic with
                { PaelsLegionAffectedCardPlaySourceId = sourceCardInstanceId };
                changed = true;
            }
        }

        return (changed ? combat with { Relics = relics } : combat, modified);
    }

    private static CombatState CompletePaelsLegionCardPlay(
        CombatState combat,
        long sourceCardInstanceId)
    {
        var relics = (CombatRelicState[])combat.RelicStates.Clone();
        var changed = false;
        for (var index = 0; index < relics.Length; index++)
        {
            var relic = relics[index];
            if (relic.IsMelted || relic.RelicId != NativePaelRewardsV111.LegionId
                || relic.PaelsLegionAffectedCardPlaySourceId != sourceCardInstanceId)
                continue;
            relics[index] = relic with
            {
                PaelsLegionCooldown = 2,
                PaelsLegionTriggeredBlockLastTurn = true,
                PaelsLegionAffectedCardPlaySourceId = null
            };
            changed = true;
        }
        return changed ? combat with { Relics = relics } : combat;
    }

    private static CombatState TickPaelsLegionAtPlayerTurnStart(CombatState combat)
    {
        var relics = (CombatRelicState[])combat.RelicStates.Clone();
        var changed = false;
        for (var index = 0; index < relics.Length; index++)
        {
            var relic = relics[index];
            if (relic.IsMelted || relic.RelicId != NativePaelRewardsV111.LegionId)
                continue;
            relics[index] = relic with
            {
                PaelsLegionCooldown = Math.Max(0, relic.PaelsLegionCooldown - 1),
                PaelsLegionTriggeredBlockLastTurn = false
            };
            changed = true;
        }
        return changed ? combat with { Relics = relics } : combat;
    }
}

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
            var firstCardBlockMultiplier = PrototypeContent.Relic(
                relic.RelicId).FirstCardBlockMultiplierPerCombat;
            if (relic.IsMelted
                || firstCardBlockMultiplier <= 1
                || relic.FirstCardBlockMultiplierUsedThisCombat
                || (relic.FirstCardBlockMultiplierTriggeringCardInstanceId
                    is { } triggeringCard
                    && triggeringCard != sourceCardInstanceId))
            {
                continue;
            }

            modified = checked(modified * firstCardBlockMultiplier);
            relics[index] = relic with
            {
                FirstCardBlockMultiplierTriggeringCardInstanceId =
                    sourceCardInstanceId
            };
            changed = true;
        }

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

    private static CombatState CompleteFirstCardBlockMultiplier(
        CombatState combat,
        long sourceCardInstanceId)
    {
        var relics = (CombatRelicState[])combat.RelicStates.Clone();
        var changed = false;
        for (var index = 0; index < relics.Length; index++)
        {
            var relic = relics[index];
            if (relic.IsMelted
                || PrototypeContent.Relic(relic.RelicId)
                    .FirstCardBlockMultiplierPerCombat <= 1
                || relic.FirstCardBlockMultiplierTriggeringCardInstanceId
                    != sourceCardInstanceId)
            {
                continue;
            }

            relics[index] = relic with
            {
                FirstCardBlockMultiplierTriggeringCardInstanceId = null,
                FirstCardBlockMultiplierUsedThisCombat = true
            };
            changed = true;
        }

        return changed ? combat with { Relics = relics } : combat;
    }

    private static (CombatState Combat, int Amount)
        ModifyEnemyDebuffWithUnsettlingLamp(
            CombatState combat,
            long sourceCardInstanceId,
            int amount)
    {
        var relics = (CombatRelicState[])combat.RelicStates.Clone();
        var changed = false;
        for (var index = 0; index < relics.Length; index++)
        {
            var relic = relics[index];
            var multiplier = PrototypeContent.Relic(relic.RelicId)
                .FirstCardDebuffMultiplierPerCombat;
            if (relic.IsMelted
                || multiplier <= 1
                || relic.FirstCardDebuffMultiplierUsedThisCombat
                || (relic.FirstCardDebuffMultiplierTriggeringCardInstanceId
                    is { } triggeringCard
                    && triggeringCard != sourceCardInstanceId))
            {
                continue;
            }

            amount = checked(amount * multiplier);
            relics[index] = relic with
            {
                FirstCardDebuffMultiplierTriggeringCardInstanceId =
                    sourceCardInstanceId
            };
            changed = true;
        }

        return (changed ? combat with { Relics = relics } : combat,
            amount);
    }

    private static CombatState CompleteFirstCardDebuffMultiplier(
        CombatState combat,
        long sourceCardInstanceId)
    {
        var relics = (CombatRelicState[])combat.RelicStates.Clone();
        var changed = false;
        for (var index = 0; index < relics.Length; index++)
        {
            var relic = relics[index];
            if (relic.IsMelted
                || PrototypeContent.Relic(relic.RelicId)
                    .FirstCardDebuffMultiplierPerCombat <= 1
                || relic.FirstCardDebuffMultiplierTriggeringCardInstanceId
                    != sourceCardInstanceId)
            {
                continue;
            }

            relics[index] = relic with
            {
                FirstCardDebuffMultiplierTriggeringCardInstanceId = null,
                FirstCardDebuffMultiplierUsedThisCombat = true
            };
            changed = true;
        }

        return changed ? combat with { Relics = relics } : combat;
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

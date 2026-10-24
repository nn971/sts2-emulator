namespace Sts2Emulator.Core;

public sealed partial record CombatState
{
    private static void ValidateCombatPotions(
        PlayerState player,
        CombatState combat)
    {
        var occupiedSlots = player.PotionSlots
            .Select((potion, slot) => (Potion: potion, Slot: slot))
            .Where(item => item.Potion is not null)
            .ToArray();

        if (combat.PotionStates.Length != occupiedSlots.Length)
        {
            throw new InvalidOperationException(
                "Occupied persistent potion slots are not represented exactly once in combat.");
        }

        if (combat.PotionStates.Select(potion => potion.Slot).Distinct().Count()
            != combat.PotionStates.Length)
        {
            throw new InvalidOperationException("Combat potion slots are duplicated.");
        }

        foreach (var combatPotion in combat.PotionStates)
        {
            if (combatPotion.Slot < 0 || combatPotion.Slot >= player.PotionSlots.Length)
            {
                throw new InvalidOperationException("Combat potion has an invalid slot.");
            }

            var persistent = player.PotionSlots[combatPotion.Slot]
                ?? throw new InvalidOperationException(
                    "Combat potion points to an empty persistent slot.");
            if (!StringComparer.Ordinal.Equals(combatPotion.PotionId, persistent.PotionId))
            {
                throw new InvalidOperationException(
                    "Combat potion disagrees with its persistent origin.");
            }

            _ = PrototypeContent.Potion(combatPotion.PotionId);
        }
    }

    private static void ValidateCombatRelics(
        PlayerState player,
        CombatState combat)
    {
        if (combat.RelicStates.Length != player.Relics.Length)
        {
            throw new InvalidOperationException(
                "Persistent relics are not represented exactly once in combat.");
        }

        var expectedPets = NativePaelRewardsV111.CreateCombatPets(player);
        var actualPets = combat.Pets ?? Array.Empty<PrototypeCombatPetState>();
        if (!expectedPets.SequenceEqual(actualPets))
            throw new InvalidOperationException("Combat pet identity or saved skin disagrees with owned relics.");

        if (combat.CounterState.ManualCardsPlayedThisTurn < 0
            || combat.CounterState.ManualCardsPlayedThisTurn > combat.CounterState.CardsPlayedThisTurn)
            throw new InvalidOperationException("Manual card-play counter is inconsistent with total plays.");
        if (combat.PotionExtraTurnsPending < 0)
            throw new InvalidOperationException(
                "Pending potion extra turns cannot be negative.");
        if (combat.PaelsEyeExtraTurnPending
            && !combat.RelicStates.Any(relic => relic.RelicId == NativePaelRewardsV111.EyeId
                && !relic.IsMelted && !relic.PaelsEyeUsedThisCombat))
            throw new InvalidOperationException("Pending Pael's Eye turn has no eligible relic instance.");
        if (combat.RelicStates.Any(relic => relic.PaelsEyeUsedThisCombat
            && relic.RelicId != NativePaelRewardsV111.EyeId))
            throw new InvalidOperationException("Pael's Eye usage state belongs to a different relic.");

        foreach (var relic in combat.RelicStates)
        {
            if (relic.PersistentIndex < 0 || relic.PersistentIndex >= player.Relics.Length)
            {
                throw new InvalidOperationException("Combat relic has an invalid persistent index.");
            }

            var persistent = player.Relics[relic.PersistentIndex];
            if (relic.HadLeftoverEnergy && relic.RelicId != NativePaelRewardsV111.TearsId)
                throw new InvalidOperationException("Leftover-energy state belongs only to Pael's Tears.");
            if (relic.RelicId == NativePaelRewardsV111.LegionId
                ? relic.PaelsLegionCooldown is < 0 or > 2
                : relic.PaelsLegionCooldown != 0
                    || relic.PaelsLegionTriggeredBlockLastTurn
                    || relic.PaelsLegionAffectedCardPlaySourceId is not null)
                throw new InvalidOperationException("Pael's Legion combat state is invalid.");
            if (relic.PaelsLegionAffectedCardPlaySourceId is { } sourceId
                && !combat.Cards.Any(card => card.InstanceId == sourceId))
                throw new InvalidOperationException("Pael's Legion references an unknown card play.");
            if (!StringComparer.Ordinal.Equals(relic.RelicId, persistent.RelicId)
                || relic.IsMelted != persistent.IsMelted)
            {
                throw new InvalidOperationException("Combat relic disagrees with its persistent origin.");
            }

            var definition = PrototypeContent.Relic(relic.RelicId);
            var triggerCount = (definition.Triggers ?? Array.Empty<PrototypeRelicTriggerSpec>()).Length;
            if (relic.TriggerCounts.Length != triggerCount
                || relic.TriggerCounts.Any(count => count < 0))
            {
                throw new InvalidOperationException("Combat relic trigger counters are invalid.");
            }
        }

        if (combat.RelicStates.Select(relic => relic.PersistentIndex).Distinct().Count()
            != combat.RelicStates.Length)
        {
            throw new InvalidOperationException("Combat relic persistent indices are duplicated.");
        }
    }

    private static void ValidatePowerOwner(
        string owner,
        PrototypePowerInstanceState[] powers)
    {
        foreach (var power in powers)
        {
            var definition = PrototypeContent.Power(
                power.PowerId);
            if (power.Stacks == 0
                || (power.Stacks < 0
                    && !definition.AllowNegative))
            {
                throw new InvalidOperationException(
                    $"{owner} power '{power.PowerId}' has invalid stacks {power.Stacks}.");
            }
        }

        foreach (var group in powers.GroupBy(
                     power => power.PowerId,
                     StringComparer.Ordinal))
        {
            var definition = PrototypeContent.Power(group.Key);
            if (!definition.IsInstanced && group.Count() > 1)
            {
                throw new InvalidOperationException(
                    $"{owner} stacking power '{group.Key}' occurs more than once.");
            }
        }

        foreach (var power in powers)
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.RequiresCardPayload != (power.CardPayload is not null))
            {
                throw new InvalidOperationException(
                    $"{owner} power '{power.PowerId}' has invalid card-payload state.");
            }

            if (power.CardPayload is not null)
            {
                _ = PrototypeContent.Card(power.CardPayload.CardId);
            }
        }
    }


    public void Validate(PlayerState player)
    {
        var combat = this;
        combat.Stochastic.Validate(combat);
        combat.AutomaticPipelineContinuation?.Validate(combat);
        if (combat.ToricShields is { } shields
            && shields.Any(shield =>
                shield.BlockAmount <= 0
                || shield.ClearsRemaining is < 1 or > 2))
        {
            throw new InvalidOperationException(
                "Toric Toughness delayed block must be positive and expire within two clears.");
        }

        if (combat.Turn <= 0)
        {
            throw new InvalidOperationException($"Combat turn must be positive, got {combat.Turn}.");
        }

        if (combat.Energy < 0 || combat.PlayerBlock < 0)
        {
            throw new InvalidOperationException("Combat energy/block cannot be negative.");
        }

        if (combat.Enemies.Any(enemy => enemy.Hp < 0 || enemy.Block < 0))
        {
            throw new InvalidOperationException("Enemy HP/block cannot be negative.");
        }

        if (combat.Enemies.Any(enemy =>
                enemy.StunFollowUpMoveIndex is { } moveIndex
                && (moveIndex < 0
                    || moveIndex >= PrototypeContent.Enemy(enemy.EnemyId)
                        .Moves.Length)))
        {
            throw new InvalidOperationException(
                "Stunned enemy follow-up move is outside its move set.");
        }

        if (combat.KnowledgeDemonChoiceResolvedTurn < 0
            || combat.KnowledgeDemonChoiceResolvedTurn > combat.Turn)
            throw new InvalidOperationException(
                "Knowledge Demon resolved-choice turn is invalid.");
        if (combat.PendingKnowledgeDemonChoice is { } curse)
        {
            if (combat.IsPlayerTurn || combat.PendingChoice is not null
                || combat.AutomaticPipelineContinuation is null
                || curse.EnemyTurn != combat.Turn
                || combat.KnowledgeDemonChoiceResolvedTurn == combat.Turn
                || curse.CurseIndex is < 0 or > 2
                || !curse.OptionCardIds.SequenceEqual(
                    PrototypeNativeHiveKnowledgeDemonChoice.Options(
                        curse.CurseIndex))
                || !combat.Enemies.Any(enemy =>
                    enemy.InstanceId == curse.EnemyInstanceId
                    && enemy.EnemyId == PrototypeNativeHiveBosses.KnowledgeId
                    && enemy.Hp > 0 && enemy.PlannedMoveIndex == 0
                    && (enemy.MoveUseCounts?.GetValueOrDefault(
                        "curse_of_knowledge") ?? 0) == curse.CurseIndex))
                throw new InvalidOperationException(
                    "Pending Knowledge Demon curse choice is stale or malformed.");
        }

        if (combat.AutoPlayTargetRolls is { } rolls
            && rolls.Any(roll =>
                roll.CardInstanceId <= 0
                || roll.Turn <= 0 || roll.Turn > combat.Turn
                || roll.CandidateCount <= 0
                || roll.SelectedIndex < 0
                || roll.SelectedIndex >= roll.CandidateCount
                || !combat.Enemies.Any(enemy =>
                    enemy.InstanceId == roll.TargetEnemyInstanceId)))
            throw new InvalidOperationException(
                "Recorded Sly auto-play target roll has invalid identity or bounds.");

        var known = combat.Cards.Select(card => card.InstanceId).ToHashSet();
        if (known.Count != combat.Cards.Length)
        {
            throw new InvalidOperationException("Combat card instance IDs are not unique.");
        }

        if (combat.NextCardInstanceId <= known.DefaultIfEmpty(0).Max())
        {
            throw new InvalidOperationException("Next combat card instance ID is not fresh.");
        }

        var persistentIds = player.Deck.Select(card => card.InstanceId).ToHashSet();
        var persistentCombatCards = combat.Cards
            .Where(card => card.PersistentCardInstanceId is not null)
            .ToArray();
        var representedPersistentIds = persistentCombatCards
            .Select(card => card.PersistentCardInstanceId!.Value)
            .ToArray();

        if (representedPersistentIds.Distinct().Count()
                != representedPersistentIds.Length
            || persistentCombatCards.Any(card =>
                card.PersistentCardInstanceId is null
                || !persistentIds.Contains(card.PersistentCardInstanceId.Value)))
        {
            throw new InvalidOperationException(
                "Persistent deck cards have invalid combat provenance.");
        }

        if (combat.Cards.Any(card => card.IsTemporary != (card.PersistentCardInstanceId is null)))
        {
            throw new InvalidOperationException("Combat temporary-card provenance is inconsistent.");
        }

        foreach (var card in persistentCombatCards)
        {
            var persistent = player.Deck.Single(item =>
                item.InstanceId == card.PersistentCardInstanceId!.Value);
            if (!StringComparer.Ordinal.Equals(card.CardId, persistent.CardId)
                || card.UpgradeLevel
                    + card.SuppressedUpgradeLevels
                    < persistent.UpgradeLevel
                || card.Enchantment != persistent.Enchantment)
            {
                throw new InvalidOperationException(
                    "Combat card no longer matches its persistent origin.");
            }
        }

        var representedPersistentSet = representedPersistentIds.ToHashSet();
        var transformedPersistentIds = combat.TransformedPersistentCardIds
            ?? Array.Empty<long>();
        if (transformedPersistentIds.Length
                != transformedPersistentIds.Distinct().Count()
            || transformedPersistentIds.Any(id =>
                !persistentIds.Contains(id)
                || representedPersistentSet.Contains(id)))
        {
            throw new InvalidOperationException(
                "Combat card transformation provenance is inconsistent.");
        }

        var transformedPersistentSet = transformedPersistentIds.ToHashSet();
        foreach (var persistent in player.Deck.Where(card =>
                     !representedPersistentSet.Contains(card.InstanceId)
                     && !transformedPersistentSet.Contains(card.InstanceId)))
        {
            if (PrototypeContent.Card(persistent.CardId).Type
                != PrototypeCardType.Power)
            {
                throw new InvalidOperationException(
                    "Only resolved Power or transformed cards may leave the combat card scope.");
            }
        }

        ValidatePowerOwner("player", combat.PlayerPowers);

        foreach (var power in combat.PlayerPowers)
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.SourceBoundToEnemy)
            {
                if (power.SourceEnemyInstanceId is not
                    { } sourceEnemyId)
                {
                    throw new InvalidOperationException(
                        "Source-bound player power is missing its enemy source.");
                }

                var source = combat.Enemies.FirstOrDefault(enemy =>
                    enemy.InstanceId == sourceEnemyId);
                if (source is null || source.Hp <= 0)
                {
                    throw new InvalidOperationException(
                        "Source-bound player power references a missing or defeated enemy.");
                }
            }
            else if (power.SourceEnemyInstanceId is not null)
            {
                throw new InvalidOperationException(
                    "Non-source-bound player power carries an enemy source.");
            }
        }

        foreach (var card in combat.Cards)
        {
            if (card.Affliction?.SourceEnemyInstanceId is not
                { } sourceEnemyId)
            {
                continue;
            }

            var source = combat.Enemies.FirstOrDefault(enemy =>
                enemy.InstanceId == sourceEnemyId);
            if (source is null)
                throw new InvalidOperationException(
                    "Source-owned card Affliction refers to an unknown enemy.");
            // Two later-act card afflictions are installed by *enemy*
            // powers, not player powers. Galvanized persists on the card
            // after Globe Head dies, but its damage hook no longer fires.
            // Vital Spark's Tainted instead clears when the source dies.
            var enemyOwnedAffliction = card.Affliction.Kind switch
            {
                PrototypeCardAfflictionKind.Galvanized =>
                    source.PowerStates.Any(power =>
                        PrototypeContent.Power(power.PowerId)
                            .GalvanizePowerCardsPerStack > 0)
                    || source.Hp <= 0,
                PrototypeCardAfflictionKind.Tainted =>
                    source.Hp > 0 && source.PowerStates.Any(power =>
                        power.PowerId == PrototypeNativeHiveElites.VitalSparkId
                        && power.Stacks > 0),
                _ => false
            };
            var hasMatchingSourcePower =
                enemyOwnedAffliction
                || combat.PlayerPowers.Any(power =>
                {
                    if (power.SourceEnemyInstanceId != sourceEnemyId)
                        return false;
                    return PrototypeContent.Power(power.PowerId)
                        .SourceBoundCardAffliction == card.Affliction.Kind;
                });
            if (!hasMatchingSourcePower)
                throw new InvalidOperationException(
                    "Source-owned card Affliction has no matching active power.");
        }

        foreach (var enemy in combat.Enemies)
        {
            ValidatePowerOwner($"enemy {enemy.InstanceId}", enemy.PowerStates);
        }

        var allPowers = combat.PlayerPowers
            .Concat(combat.Enemies.SelectMany(enemy => enemy.PowerStates))
            .ToArray();

        ValidateCombatRelics(player, combat);
        ValidateCombatPotions(player, combat);

        var allSubscriberOrders = allPowers
            .Select(power => power.ApplicationOrder)
            .Concat(combat.RelicStates.Select(relic => relic.ApplicationOrder))
            .ToArray();

        if (allSubscriberOrders.Distinct().Count() != allSubscriberOrders.Length)
        {
            throw new InvalidOperationException(
                "Event-subscriber application order must be globally unique.");
        }

        if (allSubscriberOrders.Any(order =>
            order <= 0 || order >= combat.NextPowerApplicationOrder))
        {
            throw new InvalidOperationException("Event-subscriber application order is invalid.");
        }

        if (combat.NextPowerApplicationOrder <= allSubscriberOrders.DefaultIfEmpty(0).Max())
        {
            throw new InvalidOperationException("Next event-subscriber application order is not fresh.");
        }

        var cardZones = combat.Mechanics.CardZones;
        var zoneLocations = Enum.GetValues<PrototypeCardZone>()
            .SelectMany(zone => cardZones.CardsIn(zone).Select(id =>
                (Id: id, Location: zone.ToString())))
            .ToList();
        var zones = zoneLocations
            .Select(item => item.Id)
            .ToArray();

        var suspendedSources = new List<long>();
        if (combat.PendingChoice is
                {
                    SourceCardInstanceId: { } pendingSource,
                    SourceCardAlreadyMoved: false
                } pendingChoice &&
            !(pendingChoice.ControlledDrawCost is { } drawCost &&
              pendingSource == drawCost.CardInstanceId &&
              combat.Hand.Contains(pendingSource)))
        {
            if (!suspendedSources.Contains(pendingSource))
                suspendedSources.Add(pendingSource);
        }

        for (var continuation =
                 combat.PendingChoice?.OuterChoiceContinuation;
             continuation is not null;
             continuation = continuation.Parent)
        {
            if (!continuation.SourceCardAlreadyMoved
                && continuation.SourceCardInstanceId is
                    { } continuationSource)
            {
                if (!suspendedSources.Contains(continuationSource))
                    suspendedSources.Add(continuationSource);
            }
        }

        var represented = zones
            .Concat(suspendedSources)
            .ToArray();

        if (represented.Any(cardId => !known.Contains(cardId)))
        {
            throw new InvalidOperationException("Combat zone contains an unknown combat card instance.");
        }

        if (represented.Length != represented.Distinct().Count())
        {
            var suspendedLocations = suspendedSources
                .Select((id, index) =>
                    (Id: id, Location: $"SuspendedSource[{index}]"));
            var locations = zoneLocations
                .Concat(suspendedLocations)
                .GroupBy(item => item.Id)
                .Where(group => group.Count() > 1)
                .Select(group =>
                {
                    var card = combat.Cards.Single(item =>
                        item.InstanceId == group.Key);
                    return $"{group.Key}:{card.CardId} ({string.Join(", ", group.Select(item => item.Location))})";
                })
                .ToArray();
            throw new InvalidOperationException(
                "A card instance occurs in multiple combat zones/continuations: "
                + string.Join("; ", locations));
        }

        if (represented.Length != known.Count)
        {
            var missing = known.Except(represented).Order().ToArray();
            var missingCards = missing.Select(id =>
            {
                var card = combat.Cards.Single(item => item.InstanceId == id);
                return $"{id}:{card.CardId}";
            });
            throw new InvalidOperationException(
                "Combat cards are not fully represented by zones/continuations: "
                + string.Join(", ", missingCards));
        }

        if (combat.ChoiceCardIds.Length > 0
            && (combat.PendingChoice is null
                || combat.PendingChoice.Selection.SourceZone
                    != PrototypeCardZone.ChoicePool))
        {
            throw new InvalidOperationException(
                "Combat choice pool exists without a matching pending choice.");
        }

        if (combat.PendingChoice is not null)
        {
            var pending = combat.PendingChoice;
            if (!pending.HasValidControlledPayloadShape())
                throw new InvalidOperationException(
                    "Controlled combat choice carries a mismatched or overlapping payload.");
            if (pending.ControlledTarget is not null)
            {
                if (!SemanticTargetChoiceV111.HasValidLiveSupport(
                        combat, pending))
                    throw new InvalidOperationException(
                        "Controlled random-target continuation is malformed.");
            }
            if (pending.ControlledCardShortlist is { } cardShortlist)
            {
                var automatic = combat.AutomaticPipelineContinuation;
                if (pending.OuterChoiceContinuation is null ||
                    combat.PendingKnowledgeDemonChoice is not null ||
                    (automatic is null && !combat.IsPlayerTurn) ||
                    !PrototypeGameEngine.ValidateControlledCardShortlist(
                        combat, cardShortlist))
                    throw new InvalidOperationException(
                        "Controlled card-shortlist continuation is malformed.");
            }
            if (pending.ControlledRandomCost is { } randomCost)
            {
                var automatic = combat.AutomaticPipelineContinuation;
                if (pending.OuterChoiceContinuation is null ||
                    combat.PendingKnowledgeDemonChoice is not null ||
                    (automatic is null && !combat.IsPlayerTurn) ||
                    randomCost.Operation.Kind is not (
                        PrototypeCombatEffectKind
                            .RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed or
                        PrototypeCombatEffectKind
                            .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed) ||
                    !PrototypeGameEngine.ValidateControlledRandomCost(
                        combat, randomCost))
                    throw new InvalidOperationException(
                        "Controlled random-hand-cost continuation is malformed.");
            }
            if (pending.ControlledGeneration is { } generatedCard)
            {
                var automatic = combat.AutomaticPipelineContinuation;
                if (pending.OuterChoiceContinuation is null ||
                    combat.PendingKnowledgeDemonChoice is not null ||
                    (automatic is null && !combat.IsPlayerTurn) ||
                    !PrototypeGameEngine.ValidateControlledGenerationSupport(
                        generatedCard))
                    throw new InvalidOperationException(
                        "Controlled generated-card continuation is malformed.");
            }
            if (pending.ControlledDrawInsertion is { } drawInsertion)
            {
                var automatic = combat.AutomaticPipelineContinuation;
                if (pending.OuterChoiceContinuation is null ||
                    combat.PendingKnowledgeDemonChoice is not null ||
                    (automatic is null && !combat.IsPlayerTurn) ||
                    pending.Selection.SourceZone != PrototypeCardZone.Hand ||
                    pending.Selection.MinSelections != 0 ||
                    pending.Selection.MaxSelections != 0 ||
                    drawInsertion.AutomaticHandDrawRemaining is not null &&
                        (pending.OuterChoiceContinuation.Operations.Length != 0 ||
                         pending.OuterChoiceContinuation.PendingDiscardEvents.Length != 0 ||
                         pending.OuterChoiceContinuation.PendingSlyCardInstanceIds.Length != 0 ||
                         pending.OuterChoiceContinuation.CompletionEvents.Length != 0 ||
                         pending.OuterChoiceContinuation.Parent is not null ||
                         pending.OuterChoiceContinuation.EventDispatchContinuation is not null) ||
                    !PrototypeGameEngine.ValidateControlledDrawInsertion(
                        combat, drawInsertion))
                    throw new InvalidOperationException(
                        "Controlled draw-insertion continuation is malformed.");
            }
            if (pending.ControlledPoolShuffle is { } poolShuffle)
            {
                var automatic = combat.AutomaticPipelineContinuation;
                if (pending.OuterChoiceContinuation is null ||
                    combat.PendingKnowledgeDemonChoice is not null ||
                    (automatic is null && !combat.IsPlayerTurn) ||
                    !PrototypeGameEngine.ValidateControlledPoolShuffle(
                        combat, poolShuffle, pending.SourceCardInstanceId))
                    throw new InvalidOperationException(
                        "Controlled pool-shuffle continuation is malformed.");
            }
            if (pending.ControlledPotionGeneration is { } potionGeneration)
            {
                var automatic = combat.AutomaticPipelineContinuation;
                if (pending.OuterChoiceContinuation is null ||
                    combat.PendingKnowledgeDemonChoice is not null ||
                    (automatic is null && !combat.IsPlayerTurn) ||
                    !PrototypeGameEngine.ValidateControlledPotionGeneration(
                        combat, potionGeneration,
                        pending.SourceCardInstanceId) ||
                    pending.Selection.SourceZone != PrototypeCardZone.Hand ||
                    pending.Selection.MinSelections != 0 ||
                    pending.Selection.MaxSelections != 0 ||
                    pending.CandidateCardInstanceIds.Length != 0 ||
                    pending.Continuation.Length != 0 ||
                    pending.CompletionEvents.Length != 0)
                    throw new InvalidOperationException(
                        "Controlled potion-generation continuation is malformed.");
            }
            if (pending.ControlledCardTransform is { } cardTransform)
            {
                if (!PrototypeGameEngine.ValidateControlledCardTransform(
                        combat, cardTransform) ||
                    pending.CandidateCardInstanceIds.Length != 0)
                    throw new InvalidOperationException(
                        "Controlled card-transform continuation is malformed.");
            }
            if (pending.SemanticStratagemDrawSelection)
            {
                bool hasStandaloneDraw = pending.Continuation.Any(operation =>
                    operation.Kind == PrototypeCombatEffectKind.DrawCards &&
                    operation.StandaloneSemanticDrawSegment);
                for (var outer = pending.OuterChoiceContinuation;
                     !hasStandaloneDraw && outer is not null;
                     outer = outer.Parent)
                    if (outer.Operations.Any(operation =>
                            operation.Kind ==
                                PrototypeCombatEffectKind.DrawCards &&
                            operation.StandaloneSemanticDrawSegment))
                    {
                        hasStandaloneDraw = true;
                        break;
                    }
                int expectedStratagemPicks =
                    PrototypeGameEngine.ExpectedStratagemSelectionCount(
                        combat);
                if (pending.ChoiceId != "select_cards" ||
                    pending.Selection.SourceZone !=
                        PrototypeCardZone.DrawPile ||
                    pending.Selection.MinSelections != expectedStratagemPicks ||
                    pending.Selection.MaxSelections != expectedStratagemPicks ||
                    !hasStandaloneDraw || expectedStratagemPicks <= 0)
                    throw new InvalidOperationException(
                        "Standalone Stratagem selection is malformed.");
            }
            if (pending.AutomaticSemanticStratagemDrawSelection)
            {
                int expectedStratagemPicks =
                    PrototypeGameEngine.ExpectedStratagemSelectionCount(
                        combat);
                if (pending.ChoiceId != "select_cards" ||
                    pending.Selection.SourceZone !=
                        PrototypeCardZone.DrawPile ||
                    pending.Selection.MinSelections != expectedStratagemPicks ||
                    pending.Selection.MaxSelections != expectedStratagemPicks ||
                    expectedStratagemPicks <= 0 ||
                    combat.Stochastic.SemanticDeck is not
                        { Mode: SemanticDeckStateV111.NatureMode,
                            Pending: null } ||
                    !PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat))
                    throw new InvalidOperationException(
                        "Automatic Stratagem selection is malformed.");
            }
            if (pending.StandaloneSemanticDrawHookChoice)
            {
                bool hasStandaloneDrawResume = pending.Continuation.Any(
                    operation =>
                        operation.Kind == PrototypeCombatEffectKind.DrawCards &&
                        operation.StandaloneSemanticDrawSegment);
                for (var outer = pending.OuterChoiceContinuation;
                     !hasStandaloneDrawResume && outer is not null;
                     outer = outer.Parent)
                    if (outer.Operations.Any(operation =>
                            operation.Kind ==
                                PrototypeCombatEffectKind.DrawCards &&
                            operation.StandaloneSemanticDrawSegment))
                    {
                        hasStandaloneDrawResume = true;
                        break;
                    }
                if (!hasStandaloneDrawResume || !combat.IsPlayerTurn ||
                    combat.AutomaticPipelineContinuation is not null ||
                    combat.Stochastic.SemanticDeck is not
                        { Mode: SemanticDeckStateV111.NatureMode,
                            Pending: null })
                    throw new InvalidOperationException(
                        "Standalone CardDrawn hook choice is malformed.");
            }
            if (pending.AutomaticSemanticDrawHookChoice)
            {
                var pipelineCursor = combat.AutomaticPipelineContinuation;
                bool semanticBeforeHandDraw = pipelineCursor is not null &&
                    pipelineCursor.Stochastic.IsAtSemanticBeforeHandDraw(
                        pipelineCursor.Mechanics);
                if (!(PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat) ||
                      semanticBeforeHandDraw) ||
                    combat.Stochastic.SemanticDeck is not
                        { Mode: SemanticDeckStateV111.NatureMode,
                            Pending: null })
                    throw new InvalidOperationException(
                        "Automatic CardDrawn hook choice is malformed.");
            }
            var savedChoiceOperations = new List<PrototypeQueuedOperation>(
                pending.Continuation);
            for (var outer = pending.OuterChoiceContinuation;
                 outer is not null; outer = outer.Parent)
                savedChoiceOperations.AddRange(outer.Operations);
            if (savedChoiceOperations.Any(operation =>
                    operation.DrawEventDepth is < 0 ||
                    operation.DeferredDrawnCardInstanceIds is { } deferredIds &&
                    (deferredIds.Distinct().Count() != deferredIds.Length ||
                     deferredIds.Any(cardId => !combat.Cards.Any(card =>
                         card.InstanceId == cardId))) ||
                    operation.DrawEqualSelectionCount is { } selectionDrawCount &&
                        (operation.Kind != PrototypeCombatEffectKind.DrawCards ||
                         selectionDrawCount <= 0 ||
                         selectionDrawCount !=
                             (operation.DeferredDrawnCardInstanceIds?.Length ?? 0) +
                             operation.Amount)))
                throw new InvalidOperationException(
                    "Saved draw continuation has invalid deferred card metadata.");
            var draw = pending.ControlledSemanticDraw;
            var isSemanticDrawChoice =
                pending.ChoiceId == "controlled_semantic_draw";
            var isEffectDrawCostChoice =
                pending.ChoiceId == "controlled_draw_cost" &&
                pending.ControlledDrawCost is not null && draw is not null;
            if (draw is not null)
            {
                var continuation = pending.OuterChoiceContinuation;
                var topAutoplayOperation = continuation?.Operations
                    .FirstOrDefault();
                var isTopAutoplayDraw = isSemanticDrawChoice &&
                    topAutoplayOperation?.Kind ==
                        PrototypeCombatEffectKind.AutoPlayTopDrawCards;
                var stagedTopAutoplayCards = isTopAutoplayDraw
                    ? topAutoplayOperation?.DeferredDrawnCardInstanceIds ?? []
                    : [];
                bool supportedPipelineSemanticDraw =
                    (isSemanticDrawChoice || isEffectDrawCostChoice) &&
                    PrototypeGameEngine.IsSupportedAutomaticPipelineSemanticDrawContinuation(
                        combat, pending);
                var deck = combat.Stochastic.SemanticDeck;
                if (combat.IsPlayerTurn is false ||
                    (combat.AutomaticPipelineContinuation is not null &&
                     !supportedPipelineSemanticDraw) ||
                    combat.PendingKnowledgeDemonChoice is not null ||
                    draw.RemainingCount < 0 || draw.EventDepth < 0 ||
                    draw.DrawEqualSelectionCount is { } selectionDrawCount &&
                        (selectionDrawCount <= 0 ||
                         selectionDrawCount !=
                             (draw.DeferredKeywordCardInstanceIds?.Length ?? 0) +
                             draw.RemainingCount) ||
                    (isSemanticDrawChoice && draw.RemainingCount == 0) ||
                    draw.DeferredKeywordCardInstanceIds?.Distinct().Count() !=
                        draw.DeferredKeywordCardInstanceIds?.Length ||
                    (draw.DeferredKeywordCardInstanceIds ?? []).Any(cardId =>
                        !combat.Cards.Any(card =>
                            card.InstanceId == cardId)) ||
                    continuation is null ||
                    (isSemanticDrawChoice &&
                     (pending.SourceCardInstanceId !=
                          continuation.SourceCardInstanceId ||
                      pending.SourceCardDestination !=
                          continuation.SourceCardDestination)) ||
                    pending.Selection.SourceZone != PrototypeCardZone.Hand ||
                    pending.Selection.MinSelections != 0 ||
                    pending.Selection.MaxSelections != 0 ||
                    pending.Selection.SequentialOptional ||
                    pending.Selection.DrawEqualToSelectionsOnCompletion ||
                    pending.Selection.RemoveUnselectedFromSource ||
                    pending.Selection.Resolution !=
                        (isSemanticDrawChoice
                            ? PrototypeCardSelectionResolutionKind.Preserve
                            : PrototypeCardSelectionResolutionKind.MoveToDiscard) ||
                    pending.CandidateCardInstanceIds.Length != 0 ||
                    pending.Continuation.Length != 0 ||
                    pending.CompletionEvents.Length != 0 ||
                    deck is not { Mode: SemanticDeckStateV111.NatureMode } ||
                    (isSemanticDrawChoice &&
                     (deck.Pending is not
                        { Purpose: "draw", TopOffset: 0 })) ||
                    (isTopAutoplayDraw &&
                     (draw.FromHandDraw || topAutoplayOperation is null ||
                      topAutoplayOperation.Amount != draw.RemainingCount ||
                      stagedTopAutoplayCards.Distinct().Count() !=
                          stagedTopAutoplayCards.Length ||
                      stagedTopAutoplayCards.Any(id =>
                          !combat.PlayCardIds.Contains(id)))) ||
                    (isEffectDrawCostChoice &&
                     (pending.ControlledDrawCost is not { } cost ||
                      pending.SourceCardDestination !=
                          PrototypeCardZone.Hand ||
                      pending.SourceCardInstanceId != cost.CardInstanceId ||
                      cost.RemainingCount != draw.RemainingCount ||
                      cost.FromHandDraw != draw.FromHandDraw ||
                      cost.EventDepth != draw.EventDepth ||
                      deck.Pending is not null ||
                      !PrototypeGameEngine.ValidateControlledDrawCost(
                          player, combat, cost))) ||
                    (!isSemanticDrawChoice && !isEffectDrawCostChoice))
                    throw new InvalidOperationException(
                        "Controlled semantic draw checkpoint is malformed.");
                SemanticDrawPileAdapterV111.ValidateSynchronized(combat);
                if (isSemanticDrawChoice)
                {
                    var drawEvent = SemanticDeckEngineV111.Inspect(deck!);
                    if (drawEvent is null ||
                        drawEvent.CandidateCardInstanceIds.Length == 0 ||
                        drawEvent.CandidateCardInstanceIds.Any(cardId =>
                            !combat.DrawPile.Contains(cardId)))
                        throw new InvalidOperationException(
                            "Controlled semantic draw has no live Nature support.");
                }
            }
            var sourceZone = cardZones.CardsIn(
                pending.Selection.SourceZone);

            if (pending.CandidateCardInstanceIds.Length
                != pending.CandidateCardInstanceIds.Distinct().Count())
            {
                throw new InvalidOperationException("Pending choice contains duplicate candidates.");
            }

            if (pending.CandidateCardInstanceIds.Any(cardId => !sourceZone.Contains(cardId)))
            {
                throw new InvalidOperationException("Pending choice candidate left its declared source zone.");
            }

            var sequentialSelected =
                pending.SequentialSelectedCardInstanceIds
                ?? Array.Empty<long>();
            if (!pending.Selection.SequentialOptional
                && sequentialSelected.Length > 0)
            {
                throw new InvalidOperationException(
                    "Non-sequential pending choice carries sequential selections.");
            }

            if (sequentialSelected.Length
                != sequentialSelected.Distinct().Count())
            {
                throw new InvalidOperationException(
                    "Sequential pending choice contains duplicate accumulated selections.");
            }

            if (sequentialSelected.Any(cardId =>
                    !sourceZone.Contains(cardId))
                || sequentialSelected.Any(cardId =>
                    pending.CandidateCardInstanceIds.Contains(cardId)))
            {
                throw new InvalidOperationException(
                    "Sequential pending choice has an invalid accumulated selection.");
            }

            var availableSelectionCapacity =
                pending.CandidateCardInstanceIds.Length
                + sequentialSelected.Length;
            if (pending.Selection.MinSelections < 0
                || pending.Selection.MaxSelections
                    < pending.Selection.MinSelections
                || pending.Selection.MaxSelections
                    > availableSelectionCapacity
                || sequentialSelected.Length
                    > pending.Selection.MaxSelections
                || (!pending.Selection.SequentialOptional
                    && pending.Selection.MaxSelections
                        > pending.CandidateCardInstanceIds.Length))
            {
                throw new InvalidOperationException(
                    "Pending choice has an invalid selection range.");
            }


            for (var continuation = pending.OuterChoiceContinuation;
                 continuation is not null;
                 continuation = continuation.Parent)
            {
                if (continuation.EnemyDefeatFollowUp is { } defeatFollowUp &&
                    (defeatFollowUp.Operation.Kind !=
                        PrototypeCombatEffectKind.DamageEnemy ||
                     defeatFollowUp.EventDepth < 0 ||
                     defeatFollowUp.Operation.TargetEnemyId is not
                        { } defeatedEnemyId ||
                     !combat.Enemies.Any(enemy =>
                        enemy.InstanceId == defeatedEnemyId &&
                        enemy.Hp <= 0) ||
                     (defeatFollowUp.FatalEligible &&
                      defeatFollowUp.Operation.ExtraCardRewardsOnFatal <= 0 &&
                      defeatFollowUp.Operation.GoldOnFatal <= 0 &&
                      (defeatFollowUp.Operation.PlayerPowerOnFatalId is null ||
                       defeatFollowUp.Operation.PlayerPowerOnFatalAmount == 0))))
                {
                    throw new InvalidOperationException(
                        "Nested choice has an invalid enemy-defeat follow-up.");
                }

                if (continuation.PendingSlyCardInstanceIds.Any(
                        cardId => !combat.DiscardPile.Contains(cardId)))
                {
                    throw new InvalidOperationException(
                        "Nested choice continuation has a pending Sly card outside Discard.");
                }

                if (continuation.PendingDiscardEvents.Any(combatEvent =>
                        combatEvent.SourceCardInstanceId is
                            { } cardId
                        && !combat.DiscardPile.Contains(cardId)))
                {
                    throw new InvalidOperationException(
                        "Nested choice continuation has a pending discard event for a card outside Discard.");
                }
            }
        }
    }
}

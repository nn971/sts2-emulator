namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static RunState StartCombat(
        RunState state,
        PrototypeRoomType roomType,
        PrototypeEncounterDefinition? forcedEncounter = null)
    {
        // Keep entry draws isolated until their cursor is published with the
        // corresponding encounter and combat state.
        var prototypeRng = state.Stochastic.PrototypeRng;
        var nativeRunRng = state.Stochastic.NativeRunRng;
        var world = RequireWorld(state);
        PrototypeEncounterDefinition encounter;

        if (forcedEncounter is { } scriptedEncounter)
        {
            encounter = scriptedEncounter;
        }
        else if (world.Act == 1
            && world.ActOneRegion == PrototypeActOneRegion.Underdocks)
        {
            var selected = PickUnderdocksEncounter(
                world, roomType, prototypeRng);
            encounter = selected.Encounter;
            world = selected.World;
        }
        else
        {
        var overgrowthBoss =
            TryPickOvergrowthBossEncounter(
                world,
                roomType);
        if (overgrowthBoss is not null)
        {
            encounter = overgrowthBoss;
        }
        else
        {
            var overgrowthElite =
                TryPickOvergrowthEliteEncounter(
                    world,
                    roomType,
                    prototypeRng);
            if (overgrowthElite is not null)
            {
                encounter = overgrowthElite.Value.Encounter;
                world = overgrowthElite.Value.World;
            }
            else
            {
                var overgrowthWeak =
                TryPickOvergrowthWeakEncounter(
                    world,
                    roomType,
                    prototypeRng);
            if (overgrowthWeak is not null)
            {
                encounter =
                    overgrowthWeak.Value.Encounter;
                world = overgrowthWeak.Value.World;
            }
            else
            {
                var overgrowthNormal =
                    TryPickOvergrowthNormalEncounter(
                        world,
                        roomType,
                        prototypeRng);
            if (overgrowthNormal is not null)
            {
                encounter =
                    overgrowthNormal.Value.Encounter;
                world = overgrowthNormal.Value.World;
            }
            else
            {
                var encounters = PrototypeContent.Encounters
                .Where(candidate =>
                    candidate.RoomType == roomType
                    && world.Act >= candidate.MinAct
                    && world.Act <= candidate.MaxAct
                    && world.Floor >= candidate.MinFloor
                    && world.Floor <= candidate.MaxFloor
                    && candidate.Weight > 0)
                .ToArray();

            if (encounters.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No prototype encounter is eligible for {roomType} " +
                    $"at act {world.Act}, floor {world.Floor}.");
            }

            var previousEncounter = world.EncounterIds.LastOrDefault();
            if (previousEncounter is not null && encounters.Length > 1)
            {
                var withoutImmediateRepeat = encounters
                    .Where(candidate =>
                        !StringComparer.Ordinal.Equals(
                            candidate.Id,
                            previousEncounter))
                    .ToArray();
                if (withoutImmediateRepeat.Length > 0)
                {
                    encounters = withoutImmediateRepeat;
                }
            }

                    encounter = PickWeightedEncounter(
                        encounters,
                        prototypeRng);
                }
            }
        }
        }

        }

        state = ApplyBeforeCombatStartRelics(
            state, prototypeRng, ref nativeRunRng);
        state = state with
        {
            Player = RemoveExpiredTeaRelics(state.Player)
        };
        var emberTeaIndex = FindChargedTeaRelic(
            state.Player, PrototypeNativeGloryEvents.EmberTeaId);
        var emberTeaActive = emberTeaIndex >= 0;
        if (emberTeaActive)
        {
            state = state with
            {
                Player = ConsumeTeaCharge(
                    state.Player, emberTeaIndex)
            };
        }

        var teaSet = PrototypeNativeFakeMerchant.ConsumeTeaSetCharges(
            state.Player);
        state = state with { Player = teaSet.Player };

        var combatRelics = state.Player.Relics
            .Select((relic, index) =>
            {
                var definition = PrototypeContent.Relic(relic.RelicId);
                return new CombatRelicState(
                    PersistentIndex: index,
                    RelicId: relic.RelicId,
                    ApplicationOrder: index + 1L,
                    TriggerCounts: CreateRelicTriggerCounts(relic, definition),
                    IsMelted: relic.IsMelted);
            })
            .ToArray();

        var combatPotions = state.Player.PotionSlots
            .Select((potion, slot) => potion is null
                ? null
                : new CombatPotionState(
                    Slot: slot,
                    PotionId: potion.PotionId,
                    State: potion.PersistentState.Clone()))
            .Where(potion => potion is not null)
            .Select(potion => potion!)
            .ToArray();

        var nextPowerApplicationOrder = combatRelics.Length + 1L;
        // Opt-in v111 live entry: source-audited weak, normal,
        // elite and boss initial formations, each with independent
        // encounter RNG. Unsupported entries must never fall back
        // to the prototype combat RNG.
        var nativePunchOffCombat =
            state.Configuration?.UsesExperimentalNativeWeakCombatEntry == true
            && encounter.Id == PrototypeNativePunchOff.EncounterId;
        NativeEncounterFormationV111? nativeFormation = null;
        if (nativePunchOffCombat)
        {
            nativeFormation =
                PrototypeNativePunchOff.PrepareNativeCombatFormation(
                    state, world, encounter);
        }
        else if (state.Configuration?.UsesExperimentalNativeWeakCombatEntry
            == true)
        {
            nativeFormation =
                NativeActOneEliteBossCombatEntryV111.IsSupported(encounter.Id)
                    ? NativeActOneEliteBossCombatEntryV111.Prepare(
                        state, world, encounter)
                    : NativeActOneNormalCombatEntryV111.IsSupported(encounter.Id)
                        ? NativeActOneNormalCombatEntryV111.Prepare(
                            state, world, encounter)
                        : NativeActOneWeakCombatEntryV111.Prepare(
                            state, world, encounter);
        }
        var enemySpecs = nativeFormation?.Enemies
            ?? encounter.ResolveEnemySpecs(
                prototypeRng,
                "encounter:" + encounter.Id + ":floor:" + world.Floor);
        var instanceIdByFormationPosition =
            enemySpecs
                .Select((enemySpec, index) =>
                    new
                    {
                        enemySpec.FormationPosition,
                        InstanceId = index + 1
                    })
                .ToDictionary(
                    item => item.FormationPosition,
                    item => item.InstanceId);
        // Some native encounters coordinate their monsters' initial
        // move-state positions with one encounter-local roll. Keep that
        // property declarative and deterministic within the emulator.
        var openingAiStates = encounter.CyclicOpeningAiStateIds;
        if (openingAiStates is { Length: 0 })
        {
            throw new InvalidOperationException(
                $"Encounter '{encounter.Id}' has an empty cyclic opening AI list.");
        }
        var fixedTrailingOpenings =
            encounter.FixedTrailingOpeningAiStateIds
            ?? Array.Empty<string>();
        if (openingAiStates is null && fixedTrailingOpenings.Length > 0)
        {
            throw new InvalidOperationException(
                $"Encounter '{encounter.Id}' defines fixed trailing openings without a cyclic prefix.");
        }
        if (openingAiStates is not null
            && (fixedTrailingOpenings.Length == 0
                ? enemySpecs.Length > openingAiStates.Length
                : enemySpecs.Length
                    != openingAiStates.Length + fixedTrailingOpenings.Length))
        {
            throw new InvalidOperationException(
                $"Encounter '{encounter.Id}' cyclic/fixed opening count does not match its formation.");
        }
        // Some encounters (CorpseSlugsWeak) initialize every monster's
        // starter state as part of GenerateMonsters(), not combat AI.
        // Consume the encounter's own opening roll exactly once above.
        // All other cyclic native formations remain strictly gated.
        if (nativeFormation is not null && openingAiStates is not null
            && nativeFormation.EncounterOpeningOffset is null)
            throw new NotSupportedException(
                "Experimental native encounter lacks its audited opening rotation.");
        var openingOffset = openingAiStates is null
            ? 0
            : nativeFormation?.EncounterOpeningOffset
                ?? ResolveLegacyRandomIndex(
                    prototypeRng,
                    "combat.encounter.formation.index_draw",
                    "encounter:" + encounter.Id + ":floor:" + world.Floor
                        + ":opening_rotation",
                    Enumerable.Range(0, openingAiStates.Length));
        var enemies = enemySpecs
            .Select((enemySpec, index) =>
            {
                var definition = PrototypeContent.Enemy(
                    enemySpec.EnemyId);
                var hpRange = definition.HpRangeAt(
                    world.Act,
                    state.Ascension);
                // Native HP is rolled later from the persistent run.niche
                // stream. Do not consume the prototype combat RNG here.
                var hp = nativeFormation is not null
                    ? hpRange.Max
                    : hpRange.Min == hpRange.Max
                        ? hpRange.Min
                        : ResolveLegacyRandomIndex(
                            prototypeRng,
                            NativeCombatRngEntryV111.InitialUniqueHpSiteId,
                            "creature:" + (index + 1),
                            Enumerable.Range(
                                hpRange.Min,
                                checked(hpRange.Max - hpRange.Min + 1)));
                var powers = (definition.StartingPowers ?? Array.Empty<PrototypeStartingPowerSpec>())
                    .Select(power =>
                    {
                        _ = PrototypeContent.Power(power.PowerId);
                        // PhrogParasite.AfterAddedToRoom applies FOUR
                        // Infested stacks, even though the old prototype
                        // recorded one. Correct only the source-pinned
                        // native elite; retain default training semantics.
                        var stacks = nativeFormation?.NativeEncounterEntry
                                == "PhrogParasiteElite"
                            && enemySpec.EnemyId == "proto.enemy.phrog_parasite"
                            && power.PowerId == "proto.power.infested"
                                ? 4
                                : power.StacksAt(state.Ascension);
                        return new PrototypePowerInstanceState(
                            power.PowerId,
                            stacks,
                            nextPowerApplicationOrder++);
                    })
                    .Where(power => power.Stacks > 0)
                    .ToArray();

                return new EnemyCombatState(
                    InstanceId: index + 1,
                    EnemyId: enemySpec.EnemyId,
                    Hp: hp,
                    MaxHp: hp,
                    Block: powers.Sum(power =>
                        power.Stacks * PrototypeContent.Power(power.PowerId)
                            .EnemyStartingBlockPerStack),
                    MoveIndex: 0,
                    Statuses: new Dictionary<string, int>(StringComparer.Ordinal),
                    Powers: powers,
                    FormationPosition:
                        enemySpec.FormationPosition,
                    SlotName: enemySpec.SlotName,
                    AiStateId: openingAiStates is null
                        ? null
                        : index < openingAiStates.Length
                            ? openingAiStates[(openingOffset + index)
                                % openingAiStates.Length]
                            : fixedTrailingOpenings[
                                index - openingAiStates.Length],
                    NonSummonMovesUntilEligible:
                        definition.NonSummonMovesBeforeEligible,
                    LeaderEnemyInstanceId:
                        enemySpec.LeaderFormationPosition is
                            { } leaderFormationPosition
                            ? instanceIdByFormationPosition[
                                leaderFormationPosition]
                            : null);
            })
            .ToArray();

        if (nativeFormation is not null
            && encounter.UniqueEvenEnemyHpWithinEncounter)
            throw new NotSupportedException(
                "Native unique-even-HP encounter hooks are not migrated.");
        if (encounter.UniqueEvenEnemyHpWithinEncounter)
        {
            var occupiedHp = new HashSet<int>();
            for (var i = 0; i < enemies.Length; i++)
            {
                var range = PrototypeContent.Enemy(
                    enemies[i].EnemyId).HpRangeAt(world.Act, state.Ascension);
                var minimumEven = range.Min + (range.Min % 2);
                var maximumEven = range.Max - (range.Max % 2);
                if (minimumEven > maximumEven)
                    throw new InvalidOperationException(
                        "Segment HP interval has no even value.");
                var candidate = enemies[i].Hp + (enemies[i].Hp % 2);
                if (candidate > maximumEven)
                    candidate = minimumEven;
                // Native Decimillipede's AfterAddedToRoom advances
                // duplicate HP by two and wraps around its even range.
                while (!occupiedHp.Add(candidate))
                {
                    candidate += 2;
                    if (candidate > maximumEven)
                        candidate = minimumEven;
                }
                enemies[i] = enemies[i] with { Hp = candidate };
            }
        }

        var nextCombatCardId = 1L;
        var combatCards = state.Player.Deck
            .Select(card => new CombatCardInstance(
                InstanceId: nextCombatCardId++,
                PersistentCardInstanceId: card.InstanceId,
                CardId: card.CardId,
                UpgradeLevel: card.UpgradeLevel,
                IsTemporary: false,
                State: card.PersistentState.Clone(),
                Enchantment: card.Enchantment))
            .ToArray();

        var innateCards = combatCards
            .Where(IsInnate)
            .Select(card => card.InstanceId)
            .ToArray();
        var drawPile = combatCards
            .Where(card => !IsInnate(card))
            .Select(card => card.InstanceId)
            .ToArray();
        var nativeCombatEntryRng = nativeFormation is null
            ? null
            : nativeRunRng ??
                throw new NotSupportedException(
                    "UNSUPPORTED_NATIVE_COMBAT site=combat.initial_shuffle " +
                    "operation=missing_run_stream_owner");
        var shuffleStreams = nativeCombatEntryRng ?? prototypeRng;
        IRandomIndexProviderV111 shuffleProvider = nativeCombatEntryRng is null
            ? new LegacyPrototypeRandomIndexProviderV111()
            : new NativeRandomIndexProviderV111();
        var entryCardModels = nativeCombatEntryRng is not null &&
            RandomBoundaryJournalV111.HasActiveJournal
                ? combatCards.ToDictionary(card => card.InstanceId,
                    card => card.CardId)
                : null;
        var originalDrawPileLabels = entryCardModels is null
            ? null
            : NativePoolShuffleLabels(entryCardModels, drawPile);
        var initialDrawPileDecisions = NativeFisherYatesV111.ShuffleInPlace(
            drawPile, shuffleStreams, shuffleProvider,
            nativeCombatEntryRng is null ? "combat" : "run.shuffle",
            "player:0", NativeFisherYatesV111.SiteId);
        if (entryCardModels is not null && originalDrawPileLabels is not null)
            SemanticPoolShuffleChoiceV111.RecordNativeAudit(
                originalDrawPileLabels,
                NativePoolShuffleLabels(entryCardModels, drawPile),
                initialDrawPileDecisions, drawPile.Length,
                SemanticPoolShuffleChoiceV111.NativeCombatEntryShuffleOperation,
                null);
        var boomingConchAtElite = roomType == PrototypeRoomType.Elite
            && state.Player.ActiveRelics.Any(relic =>
                relic.RelicId ==
                    PrototypeNativeOvergrowthEvents.NeowRelicId(
                        "BoomingConch"));
        var combat = new CombatState(
            Turn: 1,
            Energy: EnergyPerTurn(state.Player)
                + (boomingConchAtElite ? 1 : 0)
                + teaSet.Bonus,
            PlayerBlock: 0,
            Hand: innateCards,
            DrawPile: drawPile,
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies: enemies,
            NextCardInstanceId: nextCombatCardId,
            Cards: combatCards,
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: nextPowerApplicationOrder,
            Relics: combatRelics,
            Potions: combatPotions,
            Act: world.Act,
            Ascension: state.Ascension,
            Pets: NativePaelRewardsV111.CreateCombatPets(state.Player),
            CurrentMapNodeId: world.Map.CurrentNodeId)
        {
            ConfiguredColorlessGenerationFullyUnlocked =
                state.Configuration?.Profile.FullyUnlocked,
            ConfiguredUnlockedColorlessEpochs =
                state.Configuration?.Profile.UnlockedColorlessEpochs
        };

        var teaOfDiscourtesy = ApplyTeaOfDiscourtesyBeforeCombat(
            state.Player, combat, prototypeRng);
        state = state with { Player = teaOfDiscourtesy.Player };
        combat = teaOfDiscourtesy.Combat;

        if (emberTeaActive)
        {
            combat = ApplyPlayerPower(
                combat, "proto.power.strength", 2);
        }
        var giryaStrength = PrototypeNativeRestSiteRelics
            .CombatStrengthBonus(state.Player);
        if (giryaStrength > 0)
        {
            combat = ApplyPlayerPower(
                combat, "proto.power.strength", giryaStrength);
        }

        // Bind the native owner before combat entry hooks and the first
        // intent commitment. HP and all initial enemy maxima now use
        // the advancing run.niche stream rather than the prototype RNG.
        if (nativeFormation is not null)
        {
            combat = NativeCombatRngEntryV111.AttachAndRollInitialHp(
                combat, nativeFormation, nativeCombatEntryRng
                    ?? throw new NotSupportedException(
                        "UNSUPPORTED_NATIVE_COMBAT site=combat.initial_hp " +
                        "operation=missing_run_stream_owner"));
        }

        combat = NativeGloryAncientsV111.ApplyFurCoatCombatStart(
            combat, state.Player);

        if (enemies.Any(enemy => enemy.EnemyId ==
            PrototypeNativeHiveBosses.RocketId))
            combat = ApplyPlayerPower(combat,
                PrototypeNativeHiveBosses.SurroundedId, 1);

        // Vital Spark afflicts every existing Skill before the opening
        // draw, without overriding a different native affliction.
        combat = RefreshNativeHiveVitalSpark(combat);
        combat = RefreshNativeGloryGalvanic(combat);

        // Intents must be committed before an opening player choice is visible.
        combat = GrantEnemyAllyBlockOnPlayerTurnStart(combat);
        var entryWorld = world.AppendEncounterId(encounter.Id) with
        {
            Combat = combat
        };
        var entryStochastic = RunStochasticStateV111.WithPrototypeRng(
            state.Stochastic, prototypeRng);
        entryStochastic = RunStochasticStateV111.WithNativeRunRng(
            entryStochastic,
            combat.Stochastic.NativeRng?.RunStreams ?? nativeRunRng);
        SemanticInitialIntentCaptureV111.CaptureAndSuspend(state with
        {
            Stochastic = entryStochastic,
            World = entryWorld,
            Phase = RunPhase.Combat
        });
        combat = CommitEnemyIntents(
            combat, prototypeRng,
            NativeCombatRngEntryV111.CommittedIntentSiteId);
        // Return the latest native run/player cursors to their persistent
        // owner; later Step() calls continue from these precise positions.
        var nextStochastic = RunStochasticStateV111.WithPrototypeRng(
            entryStochastic, prototypeRng);
        nextStochastic = RunStochasticStateV111.WithNativeRunRng(
            nextStochastic,
            combat.Stochastic.NativeRng?.RunStreams ?? nativeRunRng);
        state = state with { Stochastic = nextStochastic };

        return ResumeSemanticInitialCombatEntry(state with
        {
            World = entryWorld with { Combat = combat },
            Phase = RunPhase.Combat
        }, combat);
    }

    internal static RunState ResumeSemanticInitialCombatEntry(
        RunState source, CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(combat);
        var world = RequireWorld(source);
        if (source.Phase != RunPhase.Combat ||
            world.Combat is null || combat.Act != world.Act)
            throw new InvalidOperationException(
                "Initial intent continuation has an invalid combat-entry source.");
        var combatNativeRng = combat.Stochastic.NativeRng;
        var nativeStreams = combatNativeRng?.RunStreams
            ?? source.Stochastic.NativeRunRng;
        var advanced = source with
        {
            Stochastic = RunStochasticStateV111.WithNativeRunRng(
                source.Stochastic, nativeStreams),
            World = world with { Combat = combat.Fork() }
        };
        if (nativeStreams is not null && combatNativeRng is not null)
            NativeRunRngV111.Validate(advanced);
        advanced = ApplyOpeningRoyalPoison(advanced);
        return ResumeAutomaticPipelineContinuation(
            SetOpeningCombatContinuation(advanced,
                PrototypeOpeningCombatStage.PlayerSideStart)).Run;
    }

    private static PrototypeEncounterDefinition?
        TryPickOvergrowthBossEncounter(
            RunWorldState world,
            PrototypeRoomType roomType)
    {
        if (roomType != PrototypeRoomType.Boss
            || world.Act != 1
            || world.ActOneRegion
                != PrototypeActOneRegion.Overgrowth)
        {
            return null;
        }

        var pool = world.Stochastic.ActOneEncounterPool;
        if (pool is null
            || pool.Region
                != PrototypeActOneRegion.Overgrowth
            || pool.BossEncounterId is null)
        {
            return null;
        }

        if (!PrototypeContent.OvergrowthBossEncounterPool.Contains(
                pool.BossEncounterId,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unknown Overgrowth boss encounter '{pool.BossEncounterId}'.");
        }

        return PrototypeContent.Encounter(
            pool.BossEncounterId);
    }

    private static (
        PrototypeEncounterDefinition Encounter,
        RunWorldState World)?
        TryPickOvergrowthEliteEncounter(
            RunWorldState world,
            PrototypeRoomType roomType,
            RngBundle prototypeRng)
    {
        if (roomType != PrototypeRoomType.Elite
            || world.Act != 1
            || world.ActOneRegion
                != PrototypeActOneRegion.Overgrowth)
        {
            return null;
        }

        var pool = world.Stochastic.ActOneEncounterPool;
        if (pool is null
            || pool.Region
                != PrototypeActOneRegion.Overgrowth)
        {
            return null;
        }

        var remaining =
            pool.RemainingEliteEncounterIds
            ?? (string[])PrototypeContent
                .OvergrowthEliteEncounterPool
                .Clone();
        if (remaining.Length == 0)
        {
            remaining = (string[])PrototypeContent
                .OvergrowthEliteEncounterPool
                .Clone();
        }

        var previous = world.EncounterIds
            .Reverse()
            .FirstOrDefault(id =>
                PrototypeContent.OvergrowthEliteEncounterPool.Contains(
                    id,
                    StringComparer.Ordinal));
        var eligible = previous is null
            ? remaining
            : remaining
                .Where(id =>
                    !StringComparer.Ordinal.Equals(
                        id,
                        previous))
                .ToArray();
        if (eligible.Length == 0)
        {
            eligible = remaining;
        }

        var selectedIndex = ResolveLegacyRandomIndex(
            prototypeRng,
            "combat.encounter.overgrowth_elite.index_draw",
            "act:1:floor:" + world.Floor + ":pool:"
                + CanonicalJson.Sha256(eligible),
            Enumerable.Range(0, eligible.Length));
        var selectedId = eligible[selectedIndex];
        var encounter = PrototypeContent.Encounter(
            selectedId);
        var nextRemaining = remaining
            .Where(id =>
                !StringComparer.Ordinal.Equals(
                    id,
                    selectedId))
            .ToArray();

        return (
            encounter,
            world with
            {
                Stochastic = RunWorldStochasticStateV111
                    .CommitActOneEncounterPool(
                        world, pool with
                        {
                            RemainingEliteEncounterIds = nextRemaining
                        })
            });
    }

    private static (
        PrototypeEncounterDefinition Encounter,
        RunWorldState World)?
        TryPickOvergrowthWeakEncounter(
            RunWorldState world,
            PrototypeRoomType roomType,
            RngBundle prototypeRng)
    {
        if (roomType != PrototypeRoomType.Combat
            || world.Act != 1
            || world.ActOneRegion
                != PrototypeActOneRegion.Overgrowth)
        {
            return null;
        }

        var pool = world.Stochastic.ActOneEncounterPool;
        if (pool is null
            || pool.Region
                != PrototypeActOneRegion.Overgrowth
            || pool.OrdinaryCombatsStarted >= 3)
        {
            return null;
        }

        if (pool.RemainingWeakEncounterIds.Length == 0)
        {
            throw new InvalidOperationException(
                "Overgrowth weak encounter pool exhausted before three ordinary combats.");
        }

        var selectedIndex = ResolveLegacyRandomIndex(
            prototypeRng,
            "combat.encounter.overgrowth_weak.index_draw",
            "act:1:floor:" + world.Floor + ":pool:"
                + CanonicalJson.Sha256(pool.RemainingWeakEncounterIds),
            Enumerable.Range(0, pool.RemainingWeakEncounterIds.Length));
        var selectedId =
            pool.RemainingWeakEncounterIds[selectedIndex];
        var encounter = PrototypeContent.Encounter(
            selectedId);

        var remaining = pool.RemainingWeakEncounterIds
            .Where((_, index) => index != selectedIndex)
            .ToArray();
        var nextPool = pool with
        {
            OrdinaryCombatsStarted =
                pool.OrdinaryCombatsStarted + 1,
            RemainingWeakEncounterIds = remaining
        };

        return (
            encounter,
            world with
            {
                Stochastic = RunWorldStochasticStateV111
                    .CommitActOneEncounterPool(world, nextPool)
            });
    }

    private static (
        PrototypeEncounterDefinition Encounter,
        RunWorldState World)?
        TryPickOvergrowthNormalEncounter(
            RunWorldState world,
            PrototypeRoomType roomType,
            RngBundle prototypeRng)
    {
        if (roomType != PrototypeRoomType.Combat
            || world.Act != 1
            || world.ActOneRegion
                != PrototypeActOneRegion.Overgrowth)
        {
            return null;
        }

        var pool = world.Stochastic.ActOneEncounterPool;
        if (pool is null
            || pool.Region
                != PrototypeActOneRegion.Overgrowth
            || pool.OrdinaryCombatsStarted < 3)
        {
            return null;
        }

        var remaining =
            pool.RemainingNormalEncounterIds
            ?? (string[])PrototypeContent
                .OvergrowthNormalEncounterPool
                .Clone();
        if (remaining.Length == 0)
        {
            throw new InvalidOperationException(
                "Overgrowth normal encounter bag is exhausted.");
        }

        var previous = world.EncounterIds.LastOrDefault();
        var eligible = previous is null
            ? remaining
            : remaining
                .Where(candidate =>
                    CanFollowOvergrowthEncounter(
                        previous,
                        candidate))
                .ToArray();
        if (eligible.Length == 0)
        {
            eligible = remaining;
        }

        var selectedIndex = ResolveLegacyRandomIndex(
            prototypeRng,
            "combat.encounter.overgrowth_normal.index_draw",
            "act:1:floor:" + world.Floor + ":pool:"
                + CanonicalJson.Sha256(eligible),
            Enumerable.Range(0, eligible.Length));
        var selectedId = eligible[selectedIndex];
        var encounter = PrototypeContent.Encounter(
            selectedId);
        var nextRemaining = remaining
            .Where(id =>
                !StringComparer.Ordinal.Equals(
                    id,
                    selectedId))
            .ToArray();

        return (
            encounter,
            world with
            {
                Stochastic = RunWorldStochasticStateV111
                    .CommitActOneEncounterPool(
                        world, pool with
                        {
                            OrdinaryCombatsStarted =
                                pool.OrdinaryCombatsStarted + 1,
                            RemainingNormalEncounterIds = nextRemaining
                        })
            });
    }

    private static bool CanFollowOvergrowthEncounter(
        string previous,
        string candidate)
    {
        static bool IsEither(
            string value,
            string first,
            string second) =>
            StringComparer.Ordinal.Equals(
                value,
                first)
            || StringComparer.Ordinal.Equals(
                value,
                second);

        if (IsEither(
                previous,
                "proto.encounter.fuzzy_wurm_crawler_weak",
                "proto.encounter.overgrowth_crawlers")
            && IsEither(
                candidate,
                "proto.encounter.fuzzy_wurm_crawler_weak",
                "proto.encounter.overgrowth_crawlers"))
        {
            return false;
        }

        if (IsEither(
                previous,
                "proto.encounter.shrinker_beetle_weak",
                "proto.encounter.overgrowth_crawlers")
            && IsEither(
                candidate,
                "proto.encounter.shrinker_beetle_weak",
                "proto.encounter.overgrowth_crawlers"))
        {
            return false;
        }

        var slimeFamily = new[]
        {
            "proto.encounter.slimes_weak",
            "proto.encounter.flyconid_normal",
            "proto.encounter.slimes_normal"
        };
        if (slimeFamily.Contains(
                previous,
                StringComparer.Ordinal)
            && slimeFamily.Contains(
                candidate,
                StringComparer.Ordinal))
        {
            return false;
        }

        if (IsEither(
                previous,
                "proto.encounter.flyconid_normal",
                "proto.encounter.snapping_jaxfruit_normal")
            && IsEither(
                candidate,
                "proto.encounter.flyconid_normal",
                "proto.encounter.snapping_jaxfruit_normal"))
        {
            return false;
        }

        return true;
    }

    private static PrototypeEncounterDefinition PickWeightedEncounter(
        PrototypeEncounterDefinition[] encounters,
        RngBundle rng)
    {
        var totalWeight = encounters.Sum(encounter => encounter.Weight);
        if (totalWeight <= 0)
        {
            throw new InvalidOperationException("Eligible encounter pool has no positive weight.");
        }

        var roll = ResolveLegacyBoundedInteger(
            rng,
            "combat.encounter.weighted_selection.ticket",
            "pool:" + CanonicalJson.Sha256(encounters.Select(encounter =>
                new { encounter.Id, encounter.Weight }).ToArray()),
            totalWeight);
        foreach (var encounter in encounters)
        {
            if (roll < encounter.Weight)
            {
                return encounter;
            }

            roll -= encounter.Weight;
        }

        throw new InvalidOperationException("Weighted encounter selection fell through.");
    }

    // Native RampartPower.AfterSideTurnStart fires once at the start
    // of every player side. Each living owner contributes its own
    // stacks to each living matching ally; defeated owners stop
    // granting Block. Block is reset by the existing enemy-side hook.
    internal static CombatState GrantEnemyAllyBlockOnPlayerTurnStart(
        CombatState combat)
    {
        var grants = combat.Enemies
            .Where(owner => owner.Hp > 0)
            .SelectMany(owner => owner.PowerStates)
            .Where(power => power.Stacks > 0)
            .Select(power => (
                Power: power,
                Definition: PrototypeContent.Power(power.PowerId)))
            .Where(item =>
                item.Definition.AllyBlockAtPlayerTurnStartPerStack > 0
                && item.Definition.AllyBlockTargetEnemyId is not null)
            .ToArray();
        if (grants.Length == 0)
        {
            return combat;
        }

        return combat with
        {
            Enemies = combat.Enemies.Select(enemy =>
            {
                if (enemy.Hp <= 0)
                {
                    return enemy;
                }

                var block = grants
                    .Where(item => StringComparer.Ordinal.Equals(
                        item.Definition.AllyBlockTargetEnemyId,
                        enemy.EnemyId))
                    .Sum(item => checked(
                        item.Power.Stacks
                        * item.Definition
                            .AllyBlockAtPlayerTurnStartPerStack));
                return block <= 0
                    ? enemy
                    : enemy with { Block = checked(enemy.Block + block) };
            }).ToArray()
        };
    }

    private static IReadOnlyList<GameAction> GetCombatActions(RunState state)
    {
        var combat = RequireWorld(state).Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");

        if (combat.PendingKnowledgeDemonChoice is { } enemyChoice)
            return GetKnowledgeDemonChoiceActions(enemyChoice);
        if (combat.PendingChoice is not null)
            return GetPendingChoiceActions(combat.PendingChoice);

        var actions = new List<GameAction>();

        foreach (var instanceId in combat.Hand)
        {
            var card = RequireCombatCard(combat, instanceId);
            var definition = RuntimeCardDefinition(card);
            if (card.Affliction?.Kind == PrototypeCardAfflictionKind.Bound
                && combat.BoundCardPlayedThisTurn)
                continue;
            if (!CanPlayAnotherCardThisTurn(state.Player, combat, card))
            {
                continue;
            }
            var isFreeByPower = IsCardFreeByPower(combat, definition);
            if (!definition.MechanicsImplemented
                || definition.Unplayable
                || (!isFreeByPower
                    && !IsCardAffordable(
                        combat,
                        card,
                        definition))
                || !EvaluateCombatPredicate(
                    definition.PlayCondition,
                    combat,
                    targetEnemyId: null))
            {
                continue;
            }

            var effectiveTarget = EffectiveCardTarget(
                combat,
                definition);
            if (effectiveTarget == PrototypeCardTarget.Enemy)
            {
                foreach (var enemy in combat.Enemies.Where(enemy => enemy.Hp > 0))
                {
                    actions.Add(GameAction.Create(
                        "play_card",
                        new PlayCardPayload(instanceId, enemy.InstanceId)));
                }
            }
            else
            {
                actions.Add(GameAction.Create(
                    "play_card",
                    new PlayCardPayload(instanceId, null)));
            }
        }

        for (var slot = 0; slot < state.Player.PotionSlots.Length; slot++)
        {
            var potion = state.Player.PotionSlots[slot];
            if (potion is null)
            {
                continue;
            }

            var definition = PrototypeContent.Potion(potion.PotionId);
            if (definition.AutomaticUsage)
            {
                continue;
            }

            if (definition.Target == PrototypeCardTarget.Enemy)
            {
                foreach (var enemy in combat.Enemies.Where(enemy => enemy.Hp > 0))
                {
                    actions.Add(GameAction.Create(
                        "use_potion",
                        new UsePotionPayload(slot, enemy.InstanceId)));
                }
            }
            else
            {
                actions.Add(GameAction.Create(
                    "use_potion",
                    new UsePotionPayload(slot, null)));
            }
        }

        actions.Add(GameAction.Empty("end_turn"));
        return actions;
    }

    private static RunState StepCombat(RunState state, GameAction action)
    {
        var combat = RequireWorld(state).Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");

        var next = combat.PendingKnowledgeDemonChoice is not null
            ? ResolveKnowledgeDemonChoice(state, action)
            : combat.PendingChoice is not null
                ? ResolvePendingChoice(state, action)
                : action.Kind switch
            {
                "play_card" =>
                    PlayCard(
                        state,
                        action.ReadPayload<PlayCardPayload>()),
                "use_potion" =>
                    UsePotion(
                        state,
                        action.ReadPayload<UsePotionPayload>()),
                "end_turn" => EndPlayerTurn(state),
                _ => throw new InvalidOperationException(
                    $"Unknown combat action '{action.Kind}'.")
            };

        if (next.Phase != RunPhase.Combat
            || next.Player.Hp > 0)
        {
            return next;
        }

        next = TryUseAutomaticDeathPrevention(next);
        return next.Player.Hp > 0
            ? next
            : EndRun(next, "defeat");
    }

    private static RunState PlayCard(RunState state, PlayCardPayload payload)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");
        var prototypeRng = state.Stochastic.PrototypeRng;

        if (!combat.Hand.Contains(payload.CardInstanceId))
        {
            throw new InvalidOperationException($"Card {payload.CardInstanceId} is not in hand.");
        }

        var card = RequireCombatCard(combat, payload.CardInstanceId);
        if (!CanPlayAnotherCardThisTurn(state.Player, combat, card))
        {
            throw new InvalidOperationException(
                "A player effect prevents playing this card this turn.");
        }
        var definition = RuntimeCardDefinition(card);
        if (!definition.MechanicsImplemented)
        {
            throw new NotSupportedException(
                $"Card '{definition.Name}' is catalogued but its combat mechanics are not implemented.");
        }

        if (definition.Unplayable)
        {
            throw new InvalidOperationException(
                $"Card {payload.CardInstanceId} is unplayable.");
        }

        var isFreeByPower = IsCardFreeByPower(combat, definition);
        if (!isFreeByPower
            && !IsCardAffordable(
                combat,
                card,
                definition))
        {
            throw new InvalidOperationException($"Card {payload.CardInstanceId} is unaffordable.");
        }

        if (!EvaluateCombatPredicate(
                definition.PlayCondition,
                combat,
                payload.TargetEnemyId))
        {
            throw new InvalidOperationException(
                $"Card {payload.CardInstanceId} does not satisfy its play condition.");
        }

        ValidateTarget(
            EffectiveCardTarget(combat, definition),
            payload.TargetEnemyId,
            combat);
        combat = ResolveKaiserCrabFacing(combat, payload.TargetEnemyId);
        var energySpent = isFreeByPower
            ? 0
            : ResolveCardEnergySpent(
                combat,
                card,
                definition);

        combat = SetZone(
            combat, PrototypeCardZone.Hand,
            combat.Hand.Where(id => id != payload.CardInstanceId).ToArray())
            with { Energy = combat.Energy - energySpent };
        NativeEngineEventJournalV111.Record(
            "resource", "energy_spent",
            sourceCardInstanceId: payload.CardInstanceId,
            amount: energySpent);
        NativeEngineEventJournalV111.Record(
            "card", "card_play_started",
            sourceCardInstanceId: payload.CardInstanceId,
            targetEnemyInstanceId: payload.TargetEnemyId);

        if (card.Enchantment is
                { Kind: PrototypeCardEnchantmentKind.Sown } sown
            && !card.EnchantmentTriggeredThisCombat)
        {
            combat = combat with
            {
                Energy = combat.Energy + Math.Max(0, sown.Amount),
                Cards = combat.Cards.Select(item =>
                    item.InstanceId == card.InstanceId
                        ? item with { EnchantmentTriggeredThisCombat = true }
                        : item).ToArray()
            };
        }

        if (isFreeByPower)
        {
            combat = ConsumeMatchingFreeCardPower(combat, definition.Type);
        }

        var playCountResult =
            ResolveCardPlayCountAndConsumeModifiers(
                combat,
                card,
                definition.Type);
        combat = playCountResult.Combat;

        var sourceDestination = CardExhaustsOnUse(
                definition, card)
            ? PrototypeCardZone.ExhaustPile
            : PrototypeCardZone.DiscardPile;

        var effectEnergySpent =
            definition.Cost.Kind == PrototypeCardCostKind.X
                ? energySpent
                    + state.Player.ActiveRelics.Sum(relic =>
                        PrototypeContent.Relic(relic.RelicId)
                            .XValueBonus)
                : energySpent;

        var series = new PrototypeCardPlaySeriesState(
            SourceCardInstanceId: payload.CardInstanceId,
            SourceCardDestination: sourceDestination,
            TargetEnemyId: payload.TargetEnemyId,
            EnergySpent: effectEnergySpent,
            PlayCount: playCountResult.PlayCount,
            NextPlayIndex: 0,
            RemoveSourceCardOnCompletion:
                definition.Type == PrototypeCardType.Power);

        var resolved = ResolveCardPlaySeries(
            state.Player,
            combat,
            prototypeRng,
            series);
        combat = resolved.Combat;

        state = state with
        {
            Player = resolved.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { Combat = combat }
        };

        // An attack may kill the final enemy while its own effects still
        // require a card-selection choice (e.g. upgraded Dagger Throw).
        // Resolve that choice and its card-play continuation before firing
        // CombatWon: entering rewards here dispatches an unrelated event
        // against an existing pending choice and corrupts the continuation.
        if (combat.PendingChoice is not null)
        {
            return state;
        }

        return AllEnemiesDefeated(combat)
            ? EnterCombatReward(state)
            : state;
    }

    private static RunState UsePotion(RunState state, UsePotionPayload payload)
    {
        if (payload.Slot < 0 || payload.Slot >= state.Player.PotionSlots.Length)
        {
            throw new InvalidOperationException($"Potion slot {payload.Slot} is invalid.");
        }

        var potion = state.Player.PotionSlots[payload.Slot]
            ?? throw new InvalidOperationException($"Potion slot {payload.Slot} is empty.");
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");
        var combatPotion = combat.PotionStates.SingleOrDefault(item => item.Slot == payload.Slot)
            ?? throw new InvalidOperationException(
                $"Combat potion state for slot {payload.Slot} is missing.");
        var prototypeRng = state.Stochastic.PrototypeRng;
        var nativeRunRng = combat.Stochastic.NativeRng?.RunStreams.Fork()
            ?? state.Stochastic.NativeRunRng?.Fork();

        if (!StringComparer.Ordinal.Equals(potion.PotionId, combatPotion.PotionId))
        {
            throw new InvalidOperationException(
                $"Persistent/combat potion IDs disagree in slot {payload.Slot}.");
        }

        var definition = PrototypeContent.Potion(combatPotion.PotionId);
        if (definition.AutomaticUsage)
        {
            throw new InvalidOperationException(
                $"Potion '{definition.Name}' is automatic and cannot be used manually.");
        }

        ValidateTarget(definition.Target, payload.TargetEnemyId, combat);
        combat = ResolveKaiserCrabFacing(combat, payload.TargetEnemyId);

        var operations = new Queue<PrototypeQueuedOperation>();
        foreach (var effect in definition.Effects)
        {
            EnqueueEffectOperations(
                operations,
                effect,
                0,
                0,
                payload.TargetEnemyId,
                combat,
                sourceKind: PrototypeEffectSourceKind.Potion);
        }

        var playerAfterPotion =
            ConsumePotionSlot(
                state.Player,
                payload.Slot);
        playerAfterPotion =
            ApplyPotionRunEffects(
                playerAfterPotion,
                definition,
                prototypeRng,
                nativeRunRng);

        var combatAfterPotion = combat with
        {
            Potions = playerAfterPotion.PotionSlots
                .Select((item, slot) =>
                    item is null
                        ? null
                        : new CombatPotionState(
                            slot,
                            item.PotionId,
                            item.PersistentState
                                .Clone()))
                .Where(item => item is not null)
                .Select(item => item!)
                .ToArray()
        };
        if (nativeRunRng is not null &&
            combatAfterPotion.Stochastic.NativeRng is { } nativeCombat)
        {
            combatAfterPotion = combatAfterPotion with
            {
                Stochastic = CombatStochasticStateV111.WithNativeRng(
                    combatAfterPotion.Stochastic,
                    nativeCombat with
                    {
                        RunStreams = nativeRunRng.Fork()
                    })
            };
        }

        var resolved = ResolveOperations(
            playerAfterPotion,
            combatAfterPotion,
            operations,
            prototypeRng,
            completionEvents:
            [
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PotionUsed)
            ]);
        playerAfterPotion = resolved.Player;
        combatAfterPotion = resolved.Combat;
        if (definition.GrantsExtraTurn)
        {
            combatAfterPotion = combatAfterPotion with
            {
                PotionExtraTurnsPending =
                    combatAfterPotion.PotionExtraTurnsPending + 1
            };
        }

        var stochastic = RunStochasticStateV111.WithPrototypeRng(
            state.Stochastic, prototypeRng);
        var completedNativeRunRng =
            combatAfterPotion.Stochastic.NativeRng?.RunStreams
                ?? nativeRunRng;
        if (completedNativeRunRng is not null)
            stochastic = RunStochasticStateV111.WithNativeRunRng(
                stochastic, completedNativeRunRng);

        state = state with
        {
            Player = playerAfterPotion,
            Stochastic = stochastic,
            World = world with { Combat = combatAfterPotion }
        };

        if (combatAfterPotion.PendingChoice is not null)
        {
            return state;
        }

        return AllEnemiesDefeated(combatAfterPotion)
            ? EnterCombatReward(state)
            : state;
    }

    private static RunState TryUseAutomaticDeathPrevention(
        RunState state)
    {
        if (state.Player.Hp > 0
            || state.Phase != RunPhase.Combat)
        {
            return state;
        }

        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat death prevention has no combat state.");
        var prototypeRng = state.Stochastic.PrototypeRng;
        var prevented = TryUseAutomaticDeathPrevention(
            state.Player,
            combat,
            prototypeRng);

        return state with
        {
            Player = prevented.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with
            {
                Combat = prevented.Combat
            }
        };
    }

    private static (PlayerState Player, CombatState Combat)
        TryUseAutomaticDeathPrevention(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            int eventDepth = 0)
    {
        if (player.Hp > 0)
        {
            return (player, combat);
        }

        for (var slot = 0;
             slot < player.PotionSlots.Length;
             slot++)
        {
            var potion = player.PotionSlots[slot];
            if (potion is null)
            {
                continue;
            }

            var definition =
                PrototypeContent.Potion(
                    potion.PotionId);
            if (!definition.AutomaticUsage
                || definition.DeathPreventionHealPercent <= 0)
            {
                continue;
            }

            var slots =
                (PotionInstance?[])player.PotionSlots.Clone();
            slots[slot] = null;
            var healedHp = Math.Max(
                1,
                (player.MaxHp
                    * definition.DeathPreventionHealPercent)
                / 100);
            player = player with
            {
                Hp = Math.Min(
                    player.MaxHp,
                    healedHp),
                PotionSlots = slots
            };
            combat = combat with
            {
                Potions = combat.PotionStates
                    .Where(item =>
                        item.Slot != slot)
                    .ToArray()
            };

            var dispatched = DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PotionUsed),
                rng,
                eventDepth + 1);
            return (
                dispatched.Player,
                dispatched.Combat);
        }

        return (player, combat);
    }

    private static RunState EnterCombatReward(
        RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat victory has no combat state.");
        var prototypeRng = state.Stochastic.PrototypeRng;

        var won = DispatchCombatEvent(
            state.Player,
            combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CombatWon),
            prototypeRng);
        state = state with
        {
            Player = won.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with
            {
                Combat = won.Combat
            }
        };

        state = ApplyNativePersistentCombatEnd(state, world.ActiveRoom);
        prototypeRng = state.Stochastic.PrototypeRng;
        var madSciencePlayer = ApplyMadScienceImprovement(
            state.Player, combat, prototypeRng);
        state = state with
        {
            Player = madSciencePlayer,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng)
        };
        state = ApplyWarHammerEliteVictory(
            state, world.ActiveRoom);
        state = NativeAncientRewardsV111.ApplyCombatEnd(state);

        if (world.Event is { EventId: PrototypeNativeFakeMerchant.EventId }
            && !combat.Enemies.Any(enemy => enemy.Hp > 0))
        {
            return CompleteNativeFakeMerchant(state, combat);
        }

        if (world.Event is { } battleworn
            && battleworn.EventId ==
                PrototypeNativeGloryEvents.BattlewornDummyId
            && battleworn.NativePageIndex > 0)
        {
            return CompleteNativeBattlewornDummy(state, combat);
        }

        if (world.Event is { } punchOff
            && punchOff.EventId == PrototypeNativePunchOff.EventId
            && punchOff.NativePageIndex == 2)
        {
            return EnterPunchOffReward(state);
        }

        if (world.Event is { } lanternEvent
            && lanternEvent.EventId == PrototypeNativeHiveEvents.LanternKeyEventId
            && lanternEvent.NativePageIndex == 2)
        {
            // Native SpecialCardReward is optional: win the fight,
            // then take or skip the Lantern Key before leaving event.
            return state with
            {
                World = RequireWorld(state) with
                {
                    Combat = null,
                    Reward = new RewardState(
                        "Event:" + lanternEvent.EventId,
                        [PrototypeNativeHiveEvents.LanternKeyCardId],
                        null, null, false, true, true, false)
                },
                Phase = RunPhase.Reward
            };
        }

        if (world.Event is { } pendingEvent
            && pendingEvent.EventId == PrototypeNativeDenseVegetation.EventId
            && pendingEvent.NativePageIndex == 2)
        {
            // Native Dense Vegetation enters a four-Wriggler combat
            // without offering any postcombat reward.
            return CompleteRoomToMap(state with
            {
                World = RequireWorld(state) with { Combat = null }
            });
        }

        return EnterReward(state);
    }

    private static RunState EndPlayerTurn(RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");

        combat = ClearAutomaticPipelineContinuation(combat) with
        {
            IsPlayerTurn = false
        };
        state = state with
        {
            World = world with { Combat = combat }
        };

        return ResumeAutomaticPipelineContinuation(
            SetAutomaticPipelineContinuation(state, 0,
                pendingPostDispatchEventKind: null)).Run;
    }

    private static RunState ResumeAutomaticPipeline(
        RunState state,
        int startIndex,
        PrototypeCombatEventKind? pendingPostDispatchEventKind = null,
        bool stopBeforeSemanticHandDraw = false,
        PrototypeControlledEnemyActionContinuationV111?
            controlledEnemyActionReplay = null)
    {
        if (pendingPostDispatchEventKind is not null)
        {
            var previousTurn = RequireWorld(state).Combat?.Turn;
            state = ResolvePostDispatchAutomaticPolicy(
                state,
                pendingPostDispatchEventKind.Value);

            if (pendingPostDispatchEventKind == PrototypeCombatEventKind.PlayerTurnEnded
                && RequireWorld(state).Combat?.Turn != previousTurn)
                return state;

            if (RequireWorld(state).Combat?.PendingChoice is not null)
            {
                return SetAutomaticPipelineContinuation(
                    state,
                    startIndex,
                    pendingPostDispatchEventKind: null);
            }
        }

        for (var index = startIndex;
             index < PrototypeContent.Rules.EndTurnPipeline.Length;
             index++)
        {
            var automaticStep =
                PrototypeContent.Rules.EndTurnPipeline[index];
            // Opt-in semantic hand-draw handoff. Everything before this
            // source stage has already executed in the original engine.
            // The hand stage itself runs later via the genuine
            // ResolveAutomaticStep(DrawPlayerHand) continuation.
            if (ControlledChanceGuardV111.Active &&
                stopBeforeSemanticHandDraw &&
                automaticStep.Kind == PrototypeAutomaticStepKind.DrawPlayerHand)
                return SetAutomaticPipelineContinuation(
                    state, index, pendingPostDispatchEventKind: null);
            // A controlled end-turn preserves every preceding hook result.
            // The next-turn intent stage is a separate continuation boundary:
            // Nature commits its finite tickets before this stage's postwork.
            if (ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
                automaticStep.Kind == PrototypeAutomaticStepKind.AdvanceTurn)
                return SetAutomaticPipelineContinuation(
                    state, index, pendingPostDispatchEventKind: null);
            if (ControlledChanceGuardV111.Active &&
                automaticStep.Kind ==
                    PrototypeAutomaticStepKind.ResolveEnemyActions)
            {
                var replay = controlledEnemyActionReplay;
                if (replay is not null && replay.StepIndex != index)
                    throw new InvalidDataException(
                        "Enemy-action replay cursor points at another pipeline stage.");
                var checkpoint = replay?.Checkpoint ??
                    ClearAutomaticPipelineContinuation(state.Fork());
                var replayResult =
                    ControlledChanceContinuationExecutorV111.ResumeEnemyAction(
                        state,
                        replay?.Decisions ?? [],
                        checkpointState => ResolveAutomaticStep(
                            checkpointState, automaticStep),
                        replay?.DecisionCandidateLabels);
                if (replayResult.PendingChance is { } required)
                {
                    return SetControlledEnemyActionContinuation(
                        checkpoint, index, replay?.Decisions ?? [],
                        required.Request,
                        replay?.DecisionCandidateLabels,
                        required.CandidateLabels);
                }
                state = replayResult.Run ?? throw new InvalidDataException(
                    "Enemy-action replay returned neither a run nor a pending chance request.");
                controlledEnemyActionReplay = null;
            }
            else
            {
                state = ResolveAutomaticStep(state, automaticStep);
            }

            var currentCombat = RequireWorld(state).Combat
                ?? throw new InvalidOperationException(
                    "Combat unexpectedly disappeared.");

            // The draw continuation owns saved hand-draw progress and
            // pre-draw choices. Keep its real pipeline stage until that
            // owner reports that the boundary has resumed.
            if (MustSuspendAutomaticPipelineAfterStep(currentCombat))
                return state;

            if (currentCombat.PendingChoice is not null
                || currentCombat.PendingKnowledgeDemonChoice is not null)
            {
                return SetAutomaticPipelineContinuation(
                    state,
                    currentCombat.PendingKnowledgeDemonChoice is not null
                        ? index : index + 1,
                    automaticStep.Kind
                            == PrototypeAutomaticStepKind.DispatchCombatEvent
                        ? automaticStep.EventKind
                        : null);
            }

            if (automaticStep.Kind
                    == PrototypeAutomaticStepKind.DispatchCombatEvent
                && automaticStep.EventKind is not null)
            {
                var previousTurn = currentCombat.Turn;
                state = ResolvePostDispatchAutomaticPolicy(
                    state,
                    automaticStep.EventKind.Value);

                if (automaticStep.EventKind == PrototypeCombatEventKind.PlayerTurnEnded
                    && RequireWorld(state).Combat?.Turn != previousTurn)
                    return state;

                currentCombat = RequireWorld(state).Combat
                    ?? throw new InvalidOperationException(
                        "Combat unexpectedly disappeared.");
                if (currentCombat.PendingChoice is not null)
                {
                    return SetAutomaticPipelineContinuation(
                        state,
                        index + 1,
                        pendingPostDispatchEventKind: null);
                }
            }

            if (state.Player.Hp <= 0)
            {
                state = TryUseAutomaticDeathPrevention(
                    state);
                if (state.Player.Hp <= 0)
                {
                    return EndRun(state, "defeat");
                }
            }

            currentCombat = RequireWorld(state).Combat
                ?? throw new InvalidOperationException(
                    "Combat unexpectedly disappeared.");
            if (AllEnemiesDefeated(currentCombat))
            {
                return EnterCombatReward(state);
            }
        }

        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat unexpectedly disappeared.");
        return state with
        {
            World = world with
            {
                Combat = ClearAutomaticPipelineContinuation(combat)
            }
        };
    }

    /// <summary>
    /// Resume a saved automatic pipeline after its boundary outcome is
    /// committed, then settle its terminal combat reward boundary. Choice
    /// and semantic end-turn adapters share this handoff so cursor clearing,
    /// remaining stages, and reward timing stay consistent. Every return
    /// includes the engine-classified boundary with the resulting run.
    /// </summary>
    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeAutomaticPipelineContinuation(
        RunState state,
        bool stopBeforeSemanticHandDraw = false,
        PrototypeControlledEnemyActionContinuationV111?
            controlledEnemyActionReplay = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (controlledEnemyActionReplay is { } enemyActionReplay)
        {
            if (!ControlledChanceGuardV111.Active)
                throw new InvalidOperationException(
                    "Controlled enemy-action replay requires the RNG guard.");
            enemyActionReplay.Validate(
                PrototypeContent.Rules.EndTurnPipeline.Length);
            if (state.World?.Combat is not
                { PendingChoice: null,
                  PendingKnowledgeDemonChoice: null,
                  AutomaticPipelineContinuation: { } enemyActionCursor } ||
                !enemyActionCursor.Mechanics.IsAtStepIndex(
                    enemyActionReplay.StepIndex) ||
                enemyActionCursor.Stochastic !=
                    new PrototypeAutomaticPipelineStochasticStateV111() ||
                state.World?.Combat?.Stochastic.SemanticDeck?.Pending is not null ||
                CanonicalJson.Sha256(ClearAutomaticPipelineContinuation(state)) !=
                CanonicalJson.Sha256(enemyActionReplay.Checkpoint))
                throw new InvalidDataException(
                    "Enemy-action replay does not match the saved pipeline cursor.");
        }

        if (ResumeAutomaticHandDrawHookContinuation(
                state, stopBeforeSemanticHandDraw) is { } handDrawHookResume)
            return handDrawHookResume;

        if (state.World?.Combat is
            { PendingChoice: null,
              PendingKnowledgeDemonChoice: null,
              AutomaticPipelineContinuation: { } continuation } combat)
        {
            if (!continuation.IsReadyForAutomaticPipelineResume(combat))
                return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                    state);
            var world = RequireWorld(state);
            state = state with
            {
                World = world with
                {
                    Combat = ClearAutomaticPipelineContinuation(combat)
                }
            };
            state = continuation.Mechanics.OpeningStage is { } openingStage
                ? ResumeOpeningCombat(state, openingStage)
                : ResumeAutomaticPipeline(state,
                    continuation.Mechanics.RequireResumeStepIndex(),
                    continuation.Mechanics.PendingPostDispatchEventKind,
                    stopBeforeSemanticHandDraw:
                        stopBeforeSemanticHandDraw,
                    controlledEnemyActionReplay:
                        controlledEnemyActionReplay);
        }
        var completed = state.Phase == RunPhase.Combat &&
            state.World?.Combat is
                { PendingChoice: null,
                  PendingKnowledgeDemonChoice: null,
                  AutomaticPipelineContinuation: null } live &&
            live.Stochastic.SemanticDeck?.Pending is null &&
            AllEnemiesDefeated(live)
                ? EnterCombatReward(state)
                : state;
        return PrototypeAutomaticPipelineResumeResultV111.FromRun(
            completed);
    }

    /// <summary>
    /// Attach the semantic intent provider frame to the ordinary automatic
    /// pipeline cursor. The end-turn facade projects this value into its
    /// established serialized Intents field.
    /// </summary>
    internal static RunState SetControlledTurnIntentContinuation(
        RunState state, ControlledTurnIntentsV111? intents)
    {
        ArgumentNullException.ThrowIfNull(state);
        var world = state.World;
        var combat = world?.Combat;
        var pipeline = combat?.AutomaticPipelineContinuation;
        if (combat is null)
        {
            if (intents is null)
                return state;
            throw new InvalidOperationException(
                "Controlled turn intents require an automatic pipeline checkpoint.");
        }

        if (intents is null &&
            pipeline?.Stochastic.ControlledTurnIntents is null)
            return state;

        bool initialIntent = intents?.SiteId ==
            NativeCombatRngEntryV111.CommittedIntentSiteId;
        if (intents is not null)
        {
            if (state.Phase != RunPhase.Combat ||
                intents.SiteId != ControlledTurnIntentEngineV111.SiteId &&
                !initialIntent)
                throw new InvalidOperationException(
                    "Controlled turn intents use an unsupported mechanics site.");

            if (pipeline is null && initialIntent)
                pipeline = new PrototypeAutomaticPipelineContinuationState(
                    PrototypeContent.Rules.EndTurnPipeline.Length,
                    OpeningStage:
                        PrototypeOpeningCombatStage.InitialEnemyIntents);

            bool atInitialIntentStage = initialIntent &&
                pipeline?.Mechanics.IsAtOpeningBoundary(
                    PrototypeOpeningCombatStage.InitialEnemyIntents) == true;
            bool atAdvanceTurn = !initialIntent && pipeline is not null &&
                pipeline.Mechanics.IsAtStep(
                    PrototypeAutomaticStepKind.AdvanceTurn) &&
                !combat.IsPlayerTurn;
            if (!atInitialIntentStage && !atAdvanceTurn)
                throw new InvalidOperationException(
                    "Controlled turn intents require their suspended automatic-pipeline stage.");
        }

        if (pipeline is null)
            return state;

        var updatedPipeline = pipeline with
        {
            Stochastic = pipeline.Stochastic with
            {
                ControlledTurnIntents = intents?.Fork()
            }
        };
        var updatedCombat = AttachValidatedAutomaticPipelineContinuation(
            combat, updatedPipeline);
        return state with
        {
            World = world! with
            {
                Combat = updatedCombat
            }
        };
    }

    /// <summary>
    /// Resume after the shared pipeline owner has recorded all next-turn
    /// intent tickets. Earlier enemy actions/status hooks remain applied in
    /// the RunState. This method validates and consumes that saved intent
    /// frame, performs AdvanceTurn postwork once, and continues from the next
    /// real pipeline index under the same fail-closed RNG guard.
    /// </summary>
    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledTurnAfterIntents(
        RunState state,
        bool stopBeforeSemanticHandDraw = false)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled turn-intent resumption requires the RNG guard.");
        var world = RequireWorld(state);
        var original = world.Combat ??
            throw new InvalidOperationException("No combat to resume.");
        var continuation = original.AutomaticPipelineContinuation ??
            throw new InvalidOperationException("Missing saved turn pipeline cursor.");
        if (!continuation.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.AdvanceTurn))
            throw new InvalidOperationException("Not an AdvanceTurn chance boundary.");
        var intents = continuation.Stochastic.ControlledTurnIntents ??
            throw new InvalidOperationException(
                "Missing saved controlled turn-intent frame.");
        if (intents.SiteId != ControlledTurnIntentEngineV111.SiteId ||
            !intents.Complete)
            throw new InvalidOperationException(
                "AdvanceTurn requires a completed next-turn intent frame.");
        intents.Validate();

        var nextCursor = continuation.Mechanics.AdvancePastStep(
            PrototypeAutomaticStepKind.AdvanceTurn);
        var advanced = CompleteAdvanceTurnAfterCommittedIntents(
            ClearAutomaticPipelineContinuation(intents.Combat.Fork()));
        var resumedCursor = continuation with
        {
            Mechanics = nextCursor,
            Stochastic = continuation.Stochastic with
            {
                ControlledTurnIntents = null
            }
        };
        var advancedCombat = advanced.World?.Combat ??
            throw new InvalidDataException(
                "AdvanceTurn completion removed active combat unexpectedly.");
        var updatedAdvancedCombat =
            AttachValidatedAutomaticPipelineContinuation(
                advancedCombat, resumedCursor);
        advanced = advanced with
        {
            World = advanced.World! with
            {
                Combat = updatedAdvancedCombat
            }
        };
        var resumed = state with
        {
            World = world with { Combat = advanced }
        };
        return ResumeAutomaticPipelineContinuation(
            resumed, stopBeforeSemanticHandDraw:
                stopBeforeSemanticHandDraw);
    }

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledEnemyAction(
        RunState state, int selectedIndex)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled enemy actions require the RNG guard.");
        var combat = RequireWorld(state).Combat ??
            throw new InvalidOperationException(
                "No combat for the controlled enemy-action choice.");
        var replay = combat.AutomaticPipelineContinuation?
            .Stochastic.ControlledEnemyAction ?? throw new InvalidOperationException(
                "No controlled enemy-action continuation is pending.");
        replay.Validate(PrototypeContent.Rules.EndTurnPipeline.Length);
        if (CanonicalJson.Sha256(ClearAutomaticPipelineContinuation(state)) !=
            CanonicalJson.Sha256(replay.Checkpoint))
            throw new InvalidDataException(
                "Enemy-action checkpoint no longer matches the suspended run.");

        replay.PendingRequest.Validate();
        if (selectedIndex < 0 ||
            selectedIndex >= replay.PendingRequest.SupportCount)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        var decision = new RandomIndexDecisionV111(
            replay.PendingRequest with
            {
                OrderedCandidates =
                    (int[])replay.PendingRequest.OrderedCandidates.Clone()
            }, selectedIndex, "controlled-v111");
        decision.Validate();
        var resumed = replay with
        {
            Decisions = replay.Decisions.Append(decision).ToArray(),
            DecisionCandidateLabels = AppendCandidateLabels(
                replay.DecisionCandidateLabels,
                replay.Decisions.Length, replay.PendingCandidateLabels)
        };
        var checkpoint = replay.Checkpoint.Fork();
        var checkpointWorld = RequireWorld(checkpoint);
        var checkpointCombat = checkpointWorld.Combat ??
            throw new InvalidDataException(
                "Enemy-action replay checkpoint has no active combat.");
        var checkpointCombatWithCursor =
            AttachValidatedAutomaticPipelineContinuation(
                checkpointCombat,
                new PrototypeAutomaticPipelineContinuationState(
                    replay.StepIndex));
        checkpoint = checkpoint with
        {
            World = checkpointWorld with
            {
                Combat = checkpointCombatWithCursor
            }
        };
        return ResumeAutomaticPipelineContinuation(
            checkpoint, controlledEnemyActionReplay: resumed);
    }

    /// <summary>
    /// Resume a pending DrawPlayerHand recycle using Nature's pre-hook shuffle.
    /// The saved combat includes all preceding drawn cards, draw triggers and
    /// BeforeHandDraw effects. The original stage is never restarted.
    /// </summary>
    internal static RunState ResumeControlledDrawAfterRecycle(
        RunState state, long[] selectedPreHookOrder)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled recycle resumption requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("Missing combat for recycle.");
        var prototypeRng = state.Stochastic.PrototypeRng;
        var continuation = combat.AutomaticPipelineContinuation ??
            throw new InvalidOperationException("Missing draw continuation.");
        if (!continuation.Stochastic.TryGetControlledHandDrawRemainder(
                continuation.Mechanics, out int remainingHandDraw) ||
            remainingHandDraw <= 0 ||
            combat.DrawPile.Length != 0 ||
            combat.DiscardPile.Length == 0)
            throw new InvalidOperationException("Invalid suspended draw stage.");
        var eligible = combat.DiscardPile;
        if (selectedPreHookOrder is null ||
            selectedPreHookOrder.Length != eligible.Length ||
            !selectedPreHookOrder.Order().SequenceEqual(eligible.Order()))
            throw new InvalidOperationException(
                "Nature shuffle must preserve all eligible card identities.");

        var postHook = NativeShuffleStepReplayV111.CheckPostHook(
            ApplyPerfectFitShuffleOrder(combat, (long[])selectedPreHookOrder.Clone()));
        var semanticDeck = combat.Stochastic.SemanticDeck;
        if (semanticDeck is not null)
        {
            if (semanticDeck.Mode != SemanticDeckStateV111.NatureMode ||
                semanticDeck.Count != 0)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK " +
                    "site=combat.draw.recycle " +
                    "operation=legacy_recycle_with_provider_deck");
            combat = CommitNatureDrawPileRecycle(combat, postHook);
        }
        else
            combat = SemanticDrawPileAdapterV111.CommitLegacyRecycle(
                combat, postHook);
        var afterShuffleInsertions = ControlledAfterShuffleRelicInsertions(
            combat);
        if (afterShuffleInsertions.Length > 0 &&
            ControlledChanceGuardV111.Active &&
            !ControlledChanceGuardV111.EngineOwnedNativeProviderActive)
        {
            var insertionContinuation =
                new PrototypeChoiceResolutionContinuationState(
                    SourceCardInstanceId: null,
                    SourceCardDestination: PrototypeCardZone.DrawPile,
                    Operations: [],
                    PendingDiscardEvents: [],
                    PendingSlyCardInstanceIds: [],
                    CompletionEvents: []);
            combat = MakeControlledAfterShuffleInsertionChoice(
                combat, afterShuffleInsertions[0],
                afterShuffleInsertions.Skip(1).ToArray(), [], null,
                insertionContinuation, ordinal: 0,
                automaticHandDrawRemaining: remainingHandDraw);
            var suspended = state with
            {
                World = world with { Combat = combat }
            };
            PrototypeStateInvariants.Validate(suspended);
            return suspended;
        }
        combat = ApplyRelicAfterShuffleCards(combat, prototypeRng);
        state = state with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng)
        };
        if (ExpectedStratagemSelectionCount(combat) > 0)
            return BeginControlledRecycleStratagemSelection(
                state, state.Player, combat,
                remainingHandDraw);

        var player = state.Player;
        var drawn = DrawCards(player, combat, remainingHandDraw,
            prototypeRng, fromHandDraw: true,
            suspensionPolicy:
                PrototypeDrawCardsSuspensionPolicyV111
                    .ControlledLegacyAutomaticHandDraw(combat),
            automaticHandDrawStage: true);
        state = state with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng)
        };
        return ResumeSavedControlledAutomaticHandDraw(state, drawn);
    }

    private static CombatState CompleteAdvanceTurnAfterCommittedIntents(
        CombatState combat) => combat with
    {
        Turn = combat.Turn + 1,
        PlayerPowers = combat.PlayerPowers.Select(power =>
            PrototypeContent.Power(power.PowerId).SnapshotGenerationStacksAtTurnStart
                ? power with { StoredValue = power.Stacks } : power).ToArray(),
        Counters = AdvanceTurnCounters(combat.CounterState)
    };

    private static RunState SetAutomaticPipelineContinuation(
        RunState state,
        int nextStepIndex,
        PrototypeCombatEventKind? pendingPostDispatchEventKind)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat unexpectedly disappeared.");
        var continuation = new PrototypeAutomaticPipelineContinuationState(
            nextStepIndex, pendingPostDispatchEventKind);
        var updatedCombat = AttachValidatedAutomaticPipelineContinuation(
            combat, continuation);
        return state with
        {
            World = world with
            {
                Combat = updatedCombat
            }
        };
    }

    internal static CombatState AttachValidatedAutomaticPipelineContinuation(
        CombatState combat,
        PrototypeAutomaticPipelineContinuationState continuation)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(continuation);
        var updatedCombat = combat with
        {
            AutomaticPipelineContinuation = continuation
        };
        continuation.Validate(updatedCombat);
        return updatedCombat;
    }

    private static RunState SetControlledEnemyActionContinuation(
        RunState checkpoint,
        int stepIndex,
        RandomIndexDecisionV111[] decisions,
        RandomIndexRequestV111 pendingRequest,
        string[][]? decisionCandidateLabels,
        string[]? pendingCandidateLabels)
    {
        if (checkpoint.World?.Combat is not { } originalCombat ||
            checkpoint.Phase != RunPhase.Combat || originalCombat.IsPlayerTurn)
            throw new InvalidOperationException(
                "Enemy-action suspension lost its pre-step combat state.");
        var savedCheckpoint = ClearAutomaticPipelineContinuation(checkpoint);
        var combat = RequireWorld(savedCheckpoint).Combat!;
        var replay = new PrototypeControlledEnemyActionContinuationV111(
            savedCheckpoint, stepIndex, decisions, pendingRequest,
            decisionCandidateLabels, pendingCandidateLabels);
        replay.Validate(PrototypeContent.Rules.EndTurnPipeline.Length);
        var continuation = new PrototypeAutomaticPipelineContinuationState(
            stepIndex, ControlledEnemyAction: replay);
        var updatedCombat = AttachValidatedAutomaticPipelineContinuation(
            combat, continuation);
        return savedCheckpoint with
        {
            World = savedCheckpoint.World! with
            {
                Combat = updatedCombat
            }
        };
    }

    private static string[][]? AppendCandidateLabels(
        string[][]? previousLabels, int previousDecisionCount,
        string[]? nextLabels)
    {
        if (previousLabels is null && nextLabels is null)
            return null;
        if (previousLabels is not null &&
            previousLabels.Length != previousDecisionCount)
            throw new InvalidDataException(
                "Enemy-action candidate-label prefix is malformed.");
        var aligned = previousLabels ?? Enumerable.Repeat<string[]>(
            null!, previousDecisionCount).ToArray();
        return aligned.Append(nextLabels is null
            ? null! : (string[])nextLabels.Clone()).ToArray();
    }

    private static RunState ClearAutomaticPipelineContinuation(RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException(
                "Combat unexpectedly disappeared.");
        return combat.AutomaticPipelineContinuation is null
            ? state
            : state with
            {
                World = world with
                {
                    Combat = ClearAutomaticPipelineContinuation(combat)
                }
            };
    }

    internal static CombatState ClearAutomaticPipelineContinuation(
        CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        return combat.AutomaticPipelineContinuation is null
            ? combat
            : combat with { AutomaticPipelineContinuation = null };
    }

    private static RunState ResolveImbuedAutoPlay(RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Imbued autoplay requires an active combat.");
        var operations = new List<PrototypeQueuedOperation>();
        foreach (var card in combat.Cards
                     .Where(card => card.Enchantment is
                         { Kind: PrototypeCardEnchantmentKind.Imbued }
                         && !card.EnchantmentTriggeredThisCombat)
                     .OrderBy(card => card.PersistentCardInstanceId))
        {
            var zone = combat.Hand.Contains(card.InstanceId)
                ? PrototypeCardZone.Hand
                : combat.DrawPile.Contains(card.InstanceId)
                    ? PrototypeCardZone.DrawPile
                    : combat.DiscardPile.Contains(card.InstanceId)
                        ? PrototypeCardZone.DiscardPile
                        : (PrototypeCardZone?)null;
            if (zone is null)
                continue;

            combat = MoveCards(
                combat, zone.Value, PrototypeCardZone.PlayPile,
                [card.InstanceId]);
            combat = combat with
            {
                Cards = combat.Cards.Select(item =>
                    item.InstanceId == card.InstanceId
                        ? item with { EnchantmentTriggeredThisCombat = true }
                        : item).ToArray()
            };
            operations.Add(new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.AutoPlayCombatCard, 0,
                SourceKind: PrototypeEffectSourceKind.Card,
                CardInstanceId: card.InstanceId));
        }

        if (operations.Count == 0)
            return state;

        var prototypeRng = state.Stochastic.PrototypeRng;
        var result = ResolveOperations(state.Player, combat,
            new Queue<PrototypeQueuedOperation>(operations), prototypeRng,
            moveSourceCardOnCompletion: false);
        return state with
        {
            Player = result.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { Combat = result.Combat }
        };
    }

    private static RunState ResolvePostDispatchAutomaticPolicy(
        RunState state,
        PrototypeCombatEventKind eventKind)
    {
        if (eventKind == PrototypeCombatEventKind.OpeningPlayerTurnStarted)
        {
            var openingWorld = RequireWorld(state);
            var prototypeRng = state.Stochastic.PrototypeRng;
            var opening = DispatchCombatEvent(state.Player, openingWorld.Combat!,
                new PrototypeCombatEvent(eventKind),
                prototypeRng, allowSuspension: true);
            return state with
            {
                Player = opening.Player,
                Stochastic = RunStochasticStateV111.WithPrototypeRng(
                    state.Stochastic, prototypeRng),
                World = openingWorld with { Combat = opening.Combat }
            };
        }
        // The pinned native HistoryCourse.AfterAutoPrePlayPhaseEntered
        // runs after the start-of-turn hooks. Reuse the existing
        // automatic-pipeline resume protocol for any choice suspensions.
        if (eventKind == PrototypeCombatEventKind.AutoPrePlayPhaseEntered)
            return ResolveHistoryCourseAutoPlay(state);

        if (eventKind == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            var turnWorld = RequireWorld(state);
            var turnCombat = turnWorld.Combat
                ?? throw new InvalidOperationException("Combat unexpectedly disappeared.");
            var eyeExtraTurn = turnCombat.PaelsEyeExtraTurnPending;
            if (!eyeExtraTurn && turnCombat.PotionExtraTurnsPending <= 0)
                return state;

            var relics = turnCombat.RelicStates;
            if (eyeExtraTurn)
            {
                var eyeIndex = Array.FindIndex(relics, relic =>
                    relic.RelicId == NativePaelRewardsV111.EyeId
                    && !relic.IsMelted);
                if (eyeIndex < 0)
                    throw new InvalidOperationException("Pael's Eye turn continuation has no active relic.");
                relics = (CombatRelicState[])relics.Clone();
                relics[eyeIndex] = relics[eyeIndex] with
                { PaelsEyeUsedThisCombat = true };
            }
            turnCombat = ClearAutomaticPipelineContinuation(turnCombat);
            turnCombat = turnCombat with
            {
                Relics = relics,
                PaelsEyeExtraTurnPending = false,
                PotionExtraTurnsPending = Math.Max(
                    0, turnCombat.PotionExtraTurnsPending
                        - (eyeExtraTurn ? 0 : 1))
            };
            var resetBlockIndex = Array.FindIndex(PrototypeContent.Rules.EndTurnPipeline,
                step => step.Kind == PrototypeAutomaticStepKind.ResetPlayerBlock);
            if (resetBlockIndex < 0)
                throw new InvalidOperationException("Turn pipeline has no player block reset stage.");
            // An extra player turn skips the enemy turn, so do not run the
            // normal AdvanceTurn intent commitment (or consume its RNG).
            turnCombat = turnCombat with
            {
                Turn = turnCombat.Turn + 1,
                PlayerPowers = turnCombat.PlayerPowers.Select(power =>
                    PrototypeContent.Power(power.PowerId).SnapshotGenerationStacksAtTurnStart
                        ? power with { StoredValue = power.Stacks } : power).ToArray(),
                Counters = AdvanceTurnCounters(turnCombat.CounterState)
            };
            return ResumeAutomaticPipelineContinuation(
                SetAutomaticPipelineContinuation(state with
                { World = turnWorld with { Combat = turnCombat } },
                    resetBlockIndex, pendingPostDispatchEventKind: null)).Run;
        }

        if (eventKind != PrototypeCombatEventKind.PlayerTurnStarted)
        {
            return state;
        }

        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat unexpectedly disappeared.");
        var discardCount = combat.PlayerPowers.Sum(power =>
            PrototypeContent.Power(power.PowerId)
                .DiscardAfterPlayerTurnStartPerStack
            * power.Stacks);

        if (discardCount <= 0 || combat.Hand.Length == 0)
        {
            return state;
        }

        var effectiveCount = Math.Min(
            discardCount,
            combat.Hand.Length);
        combat = combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "select_cards",
                SourceCardInstanceId: null,
                SourceCardDestination:
                    PrototypeCardZone.DiscardPile,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand,
                    effectiveCount,
                    effectiveCount,
                    PrototypeCardSelectionResolutionKind
                        .MoveToDiscard),
                CandidateCardInstanceIds:
                    (long[])combat.Hand.Clone(),
                Continuation:
                    Array.Empty<PrototypeQueuedOperation>(),
                CompletionEvents:
                    Array.Empty<PrototypeCombatEvent>())
        };

        return state with
        {
            World = world with { Combat = combat }
        };
    }

    private static RunState ResolveAutomaticStep(
        RunState state,
        PrototypeAutomaticStep automaticStep,
        AutomaticHandDrawSourceResultContextV111
            handDrawResultContext =
                AutomaticHandDrawSourceResultContextV111.OrdinaryPipeline)
    {
        if (!Enum.IsDefined(handDrawResultContext) ||
            handDrawResultContext ==
                AutomaticHandDrawSourceResultContextV111
                    .ResumedSemanticBeforeHandDraw ||
            (handDrawResultContext !=
                AutomaticHandDrawSourceResultContextV111.OrdinaryPipeline &&
             automaticStep.Kind != PrototypeAutomaticStepKind.DrawPlayerHand))
            throw new InvalidDataException(
                "Automatic hand-draw result context does not match its pipeline step.");
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");
        if (handDrawResultContext ==
                AutomaticHandDrawSourceResultContextV111
                    .DeferredSemanticStage &&
            (!ControlledChanceGuardV111.Active ||
             combat.Stochastic.SemanticDeck is null))
            throw new InvalidDataException(
                "Deferred semantic hand draw has no active provider.");
        var player = state.Player;
        var prototypeRng = state.Stochastic.PrototypeRng;
        RunState PublishPrototypeRng(RunState current) => current with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                current.Stochastic, prototypeRng)
        };

        switch (automaticStep.Kind)
        {
            case PrototypeAutomaticStepKind.ResolvePlayerEndTurnHandEffects:
            {
                var eye = combat.RelicStates.FirstOrDefault(relic =>
                    relic.RelicId == NativePaelRewardsV111.EyeId
                    && !relic.IsMelted && !relic.PaelsEyeUsedThisCombat);
                var whisperingEarring = player.ActiveRelics.Any(relic =>
                    relic.RelicId == "proto.relic.whispering_earring");
                if (eye is not null && (combat.Turn != 1 || !whisperingEarring)
                    && combat.CounterState.ManualCardsPlayedThisTurn == 0)
                {
                    // Whispering Earring makes the first turn count as a played
                    // turn in the source implementation. Its autoplay opener is
                    // not modeled here, so keep the explicit suppression rule.
                    combat = combat with { PaelsEyeExtraTurnPending = true };
                    var exhausted = ResolveOperations(player, combat,
                        new Queue<PrototypeQueuedOperation>([
                            new(PrototypeCombatEffectKind.ExhaustHand, 1)
                        ]), prototypeRng);
                    player = exhausted.Player;
                    combat = exhausted.Combat;
                }
                // PaelsTears.BeforeSideTurnEnd precedes hand end effects.
                // Capture once at this existing stage so suspended pipeline
                // cursors keep their historical indices.
                if (combat.RelicStates.Any(relic => relic.RelicId == NativePaelRewardsV111.TearsId && !relic.IsMelted))
                    combat = combat with { Relics = combat.RelicStates.Select(relic =>
                        relic.RelicId == NativePaelRewardsV111.TearsId && !relic.IsMelted
                            ? relic with { HadLeftoverEnergy = combat.Energy > 0 }
                            : relic).ToArray() };
                foreach (var instanceId in combat.Hand)
                {
                    var card = RequireCombatCard(
                        combat,
                        instanceId);
                    var definition =
                        PrototypeContent.Card(card.CardId);
                    if (definition.EndTurnGoldLossIfInHand > 0)
                    {
                        player = player with
                        {
                            Gold = Math.Max(0, player.Gold
                                - definition.EndTurnGoldLossIfInHand)
                        };
                    }
                    // Trial Shame/Doubt apply one debuff while in hand.
                    // Newly created Weak/Frail survives the imminent
                    // PlayerTurnEnded power-decrement stage, following
                    // native SkipNextDurationTick semantics.
                    var cursePower = card.CardId switch
                    {
                        PrototypeNativeGloryEvents.ShameId => "proto.power.frail",
                        PrototypeNativeGloryEvents.DoubtId => "proto.power.weak",
                        _ => null
                    };
                    if (cursePower is not null)
                    {
                        var alreadyPresent = combat.PlayerPowers.Any(power =>
                            power.PowerId == cursePower);
                        combat = ApplyPlayerPower(combat, cursePower, 1);
                        if (!alreadyPresent)
                        {
                            combat = combat with
                            {
                                PlayerPowers = combat.PlayerPowers
                                    .Select(power => power.PowerId == cursePower
                                        ? power with { SkipNextPlayerTurnEnd = true }
                                        : power).ToArray()
                            };
                        }
                    }

                    // Regret snapshots the count of cards in hand
                    // before side-turn end, dealing unblockable
                    // unpowered damage equal to that count.
                    var handDamage =
                        card.CardId == PrototypeNativeGloryEvents.RegretId
                            ? combat.Hand.Length
                            : definition.EndTurnDamageIfInHand;
                    var damage =
                        ApplyPlayerIncomingDamageCap(
                            combat,
                            Math.Max(
                                0,
                                handDamage
                                    + (card.CardId == PrototypeNativeGloryBosses.WitherId
                                        ? 3 * card.WitherFakeUpgradeLevel : 0)));
                    if (damage <= 0)
                    {
                        continue;
                    }

                    var absorbed = definition.EndTurnDamageUnblockable
                        || card.CardId == PrototypeNativeGloryEvents.RegretId
                        ? 0
                        : Math.Min(combat.PlayerBlock, damage);
                    combat = combat with
                    {
                        PlayerBlock =
                            combat.PlayerBlock - absorbed
                    };
                    player = player with
                    {
                        Hp = Math.Max(
                            0,
                            player.Hp
                            - Math.Max(
                                0,
                                damage - absorbed))
                    };

                    if (player.Hp <= 0)
                    {
                        break;
                    }
                }

                break;
            }

            case PrototypeAutomaticStepKind.DispatchCombatEvent:
                if (automaticStep.EventKind is null)
                {
                    throw new InvalidOperationException("Combat-event pipeline step is missing an event kind.");
                }

                if (automaticStep.EventKind
                    == PrototypeCombatEventKind.PlayerTurnStarted)
                {
                    combat = combat with { IsPlayerTurn = true };
                }

                var dispatched = DispatchCombatEvent(
                    player,
                    combat,
                    new PrototypeCombatEvent(automaticStep.EventKind.Value),
                    prototypeRng,
                    allowSuspension: true);
                player = dispatched.Player;
                combat = dispatched.Combat;

                // DisintegrationPower.AfterSideTurnEndLate applies an
                // unpowered, blockable hit at the end of the player's
                // side-turn. It is not an Attack and uses no enemy
                // strength/damage modifiers.
                if (automaticStep.EventKind ==
                    PrototypeCombatEventKind.PlayerTurnEnded)
                {
                    var disintegration = combat.PlayerPowers
                        .Where(power => power.PowerId ==
                            PrototypeNativeHiveKnowledgeDemonChoice
                                .DisintegrationPowerId)
                        .Sum(power => Math.Max(0, power.Stacks));
                    if (disintegration > 0)
                    {
                        var absorbed = Math.Min(
                            combat.PlayerBlock, disintegration);
                        combat = combat with
                        {
                            PlayerBlock = combat.PlayerBlock - absorbed
                        };
                        var loss = ApplyPlayerHpLoss(
                            player,
                            combat,
                            disintegration - absorbed);
                        player = loss.Player;
                        combat = loss.Combat;
                    }
                }
                break;

            case PrototypeAutomaticStepKind.DiscardPlayerHand:
            {
                combat = ClearTemporaryCardCosts(
                    combat,
                    PrototypeTemporaryCardCostExpiry.EndOfTurn);

                var ethereal = combat.Hand
                    .Where(instanceId =>
                        CardHasKeyword(
                            combat,
                            instanceId,
                            PrototypeCardKeyword.Ethereal))
                    .ToArray();
                var etherealSet = ethereal.ToHashSet();
                var preserveHand =
                    combat.PlayerPowers.Any(power =>
                        power.Stacks > 0
                        && PrototypeContent.Power(power.PowerId)
                            .PreventsHandDiscard)
                    || player.ActiveRelics.Any(relic =>
                        PrototypeContent.Relic(relic.RelicId)
                            .PreventsHandDiscard);
                var retained = combat.Hand
                    .Where(instanceId =>
                        !etherealSet.Contains(instanceId)
                        && (preserveHand
                            || CardHasKeyword(
                                combat,
                                instanceId,
                                PrototypeCardKeyword.Retain)))
                    .ToArray();
                var retainedSet = retained.ToHashSet();
                var discarded = combat.Hand
                    .Where(instanceId =>
                        !etherealSet.Contains(instanceId)
                        && !retainedSet.Contains(instanceId))
                    .ToArray();
                var discardEvents = discarded.Select(cardInstanceId =>
                {
                    var card = RequireCombatCard(combat, cardInstanceId);
                    return new PrototypeCombatEvent(
                        PrototypeCombatEventKind.CardDiscarded,
                        SourceCardInstanceId: cardInstanceId,
                        CardId: card.CardId);
                }).ToArray();

                combat = SemanticDrawPileAdapterV111.ApplyZoneTransition(
                    combat,
                    combat.Mechanics.CardZones.WithZoneChanges(
                        new Dictionary<PrototypeCardZone, long[]>
                        {
                            [PrototypeCardZone.ExhaustPile] = combat.ExhaustPile
                                .Concat(ethereal).ToArray(),
                            [PrototypeCardZone.DiscardPile] = combat.DiscardPile
                                .Concat(discarded).ToArray(),
                            [PrototypeCardZone.Hand] = retained
                        }));

                foreach (var cardInstanceId in ethereal)
                {
                    var exhaustedCard = RequireCombatCard(combat, cardInstanceId);
                    var exhausted = DispatchCombatEvent(
                        player,
                        combat,
                        new PrototypeCombatEvent(
                            PrototypeCombatEventKind.CardExhausted,
                            SourceCardInstanceId: cardInstanceId,
                            CardId: exhaustedCard.CardId),
                        prototypeRng);
                    player = exhausted.Player;
                    combat = exhausted.Combat;
                }

                var discardHooks = ResumeChoiceResolutionContinuation(
                    player,
                    combat,
                    prototypeRng,
                    new PrototypeChoiceResolutionContinuationState(
                        SourceCardInstanceId: null,
                        SourceCardDestination: PrototypeCardZone.DiscardPile,
                        Operations: [],
                        PendingDiscardEvents: discardEvents,
                        PendingSlyCardInstanceIds: [],
                        CompletionEvents: [],
                        CompletePlayerHandCleanup: true),
                    resumeCardPlaySeries: false);
                player = discardHooks.Player;
                combat = discardHooks.Combat;

                break;
            }

            case PrototypeAutomaticStepKind.DispatchEnemyStatusStage:
                if (automaticStep.Stage is null)
                {
                    throw new InvalidOperationException("Enemy-status pipeline step is missing a stage.");
                }

                if (automaticStep.Stage.Value
                    == PrototypeTurnStage.EnemyTurnStart)
                {
                    combat = combat with { IsPlayerSideActive = false };
                }

                // Sandpit kills the affected player as it reaches
                // zero at the enemy side-start. Native player/pet
                // animation is outside the emulator scope.
                if (automaticStep.Stage.Value ==
                        PrototypeTurnStage.EnemyTurnStart
                    && combat.Turn > 1
                    && combat.Enemies.Any(enemy =>
                        enemy.Hp > 0 && enemy.PowerStates.Any(power =>
                            power.PowerId ==
                                PrototypeNativeHiveBosses.SandpitId
                            && power.Stacks == 1)))
                    player = player with { Hp = 0 };

                combat = ResolveEnemyStatusStage(
                    combat,
                    automaticStep.Stage.Value,
                    prototypeRng);
                if (automaticStep.Stage.Value
                    == PrototypeTurnStage.EnemyTurnEnd)
                {
                    combat = DecrementPlayerPowers(
                        combat,
                        definition =>
                            definition
                                .DecrementAtEnemyTurnEnd);
                    // Native TaintedPower expires after the enemy side-turn
                    // (all stacks simultaneously, not one per turn).
                    combat = combat with
                    {
                        PlayerPowers = combat.PlayerPowers
                            .Where(power => power.PowerId !=
                                PrototypeNativeHiveElites.TaintedPowerId)
                            .ToArray()
                    };
                }

                combat = ResolveEnemyDeathSummons(
                    combat,
                    prototypeRng,
                    enemyActionSkips:
                        automaticStep.Stage.Value
                            == PrototypeTurnStage.EnemyTurnStart
                            ? 2
                            : 1);
                break;

            case PrototypeAutomaticStepKind.ResetEnemyBlock:
                combat = combat with
                {
                    Enemies = combat.Enemies
                        .Select(enemy => enemy with
                        {
                            Block = enemy.PowerStates.Any(power =>
                                power.Stacks > 0
                                && PrototypeContent.Power(power.PowerId)
                                    .PreventsEnemyBlockClear)
                                    ? enemy.Block : 0
                        })
                        .ToArray()
                };
                break;

            case PrototypeAutomaticStepKind.ResolveEnemyActions:
            {
                combat = BeginKnowledgeDemonChoice(combat);
                if (combat.PendingKnowledgeDemonChoice is not null)
                    break;
                var result = ResolveEnemyActions(
                    player,
                    combat,
                    world.Act,
                    state.Ascension,
                    prototypeRng);
                player = result.Player;
                combat = result.Combat;
                break;
            }

            case PrototypeAutomaticStepKind.AdvanceTurn:
                // The controlled pipeline suspends *before* this stage.
                combat = CompleteAdvanceTurnAfterCommittedIntents(
                    CommitEnemyIntents(
                        combat, prototypeRng,
                        NativeCombatRngEntryV111.NextTurnIntentSiteId));
                break;

            case PrototypeAutomaticStepKind.ResetPlayerBlock:
            {
                var preventsBlockClear = combat.PlayerPowers.Any(power =>
                    power.Stacks > 0
                    && PrototypeContent.Power(power.PowerId)
                        .PreventsPlayerBlockClear);
                if (!preventsBlockClear)
                {
                    combat = combat with { PlayerBlock = 0 };
                }

                var toricShields = combat.ToricShields
                    ?? Array.Empty<PrototypeToricShield>();
                var toricBlock = toricShields.Sum(item =>
                    item.BlockAmount);
                combat = combat with
                {
                    ToricShields = toricShields
                        .Where(item => item.ClearsRemaining > 1)
                        .Select(item => item with
                        {
                            ClearsRemaining = item.ClearsRemaining - 1
                        })
                        .ToArray(),
                    PlayerBlock = combat.PlayerBlock + toricBlock
                };

                var delayedBlock = combat.PlayerPowers.Sum(power =>
                    PrototypeContent.Power(power.PowerId)
                        .BlockAfterClearPerStack * power.Stacks);
                if (delayedBlock > 0)
                {
                    combat = combat with
                    {
                        PlayerBlock = combat.PlayerBlock + delayedBlock
                    };
                }

                combat = RemovePlayerPowers(
                    combat,
                    definition => definition.RemoveAfterBlockClear);
                break;
            }

            case PrototypeAutomaticStepKind.RefreshPlayerEnergy:
            {
                var preserveUnusedEnergy =
                    player.ActiveRelics.Any(relic =>
                        PrototypeContent.Relic(relic.RelicId)
                            .PreserveUnusedEnergy);
                combat = combat with
                {
                    Energy = preserveUnusedEnergy
                        ? combat.Energy + EnergyPerTurn(player, combat.Turn)
                        : EnergyPerTurn(player, combat.Turn)
                };
                var delayedEnergy = combat.PlayerPowers.Sum(power =>
                {
                    var definition = PrototypeContent.Power(power.PowerId);
                    return definition.EnergyAfterResetPerStack * power.Stacks
                        + (power.Stacks > 0
                            ? definition.EnergyBonusWhileActive
                            : 0);
                });
                if (delayedEnergy != 0)
                {
                    combat = combat with
                    {
                        Energy = Math.Max(0, combat.Energy + delayedEnergy)
                    };
                }

                combat = DecrementPlayerPowers(
                    combat,
                    definition => definition.DecrementAfterEnergyReset);

                combat = RemovePlayerPowers(
                    combat,
                    definition => definition.RemoveAfterEnergyReset);
                var sideStarted = DispatchCombatEvent(player, combat,
                    new PrototypeCombatEvent(PrototypeCombatEventKind.PlayerSideTurnStarted),
                    prototypeRng, allowSuspension: true);
                player = sideStarted.Player;
                combat = TickPaelsLegionAtPlayerTurnStart(sideStarted.Combat);
                break;
            }

            case PrototypeAutomaticStepKind.DrawPlayerHand:
            {
                var prepared = PrepareAutomaticHandDrawStage(
                    state, handDrawResultContext);
                state = prepared.Run;
                handDrawResultContext = prepared.ResultContext;
                prototypeRng = state.Stochastic.PrototypeRng;
                world = RequireWorld(state);
                player = state.Player;
                combat = world.Combat!;
                if (prepared.DispatchBeforeHandDraw)
                {
                    var beforeHandDraw = DispatchCombatEvent(
                        player,
                        combat,
                        new PrototypeCombatEvent(
                            PrototypeCombatEventKind.BeforeHandDraw),
                        prototypeRng,
                        allowSuspension: ControlledChanceGuardV111.Active &&
                            (combat.Stochastic.SemanticDeck is
                                { Mode: SemanticDeckStateV111.NatureMode } ||
                             (!ControlledChanceGuardV111
                                  .EngineOwnedNativeProviderActive &&
                              combat.Stochastic.SemanticDeck is null)));
                    player = beforeHandDraw.Player;
                    combat = beforeHandDraw.Combat;
                    if (combat.PendingChoice?.ControlledRandomPileCard is not null)
                    {
                        return CommitAutomaticHandDrawProgress(
                            PublishPrototypeRng(state), player, combat,
                            PrototypeAutomaticHandDrawProgressV111
                                .SemanticBeforeHandDraw(),
                            createCheckpoint: true);
                    }
                    if (ControlledChanceGuardV111.Active &&
                        combat.PendingChoice is not null)
                    {
                        var pending = combat.PendingChoice;
                        if (pending.ControlledTarget is not null ||
                            pending.ControlledGeneration is not null ||
                            pending.ControlledPotionGeneration is not null ||
                            pending.ControlledDrawCost is not null ||
                            pending.ControlledSemanticDraw is not null ||
                            pending.ControlledDrawInsertion is not null ||
                            pending.ControlledRandomCost is not null ||
                            pending.ControlledCardShortlist is not null ||
                            (pending.ControlledPoolShuffle is not null &&
                             !IsAutomaticPipelineSemanticPoolShuffleContinuation(
                                 combat, pending)) ||
                            pending.ControlledRandomPileCard is not null ||
                            pending.SemanticStratagemDrawSelection ||
                            pending.AutomaticSemanticStratagemDrawSelection)
                            throw new NotSupportedException(
                                "UNSUPPORTED_SEMANTIC_DECK site=combat.BeforeHandDraw " +
                                "operation=nested_controlled_choice");
                        bool semanticBeforeHandDraw = combat.Stochastic.SemanticDeck is
                            { Mode: SemanticDeckStateV111.NatureMode };
                        if (!semanticBeforeHandDraw &&
                            (ControlledChanceGuardV111
                                 .EngineOwnedNativeProviderActive ||
                             combat.Stochastic.SemanticDeck is not null))
                            throw new NotSupportedException(
                                "UNSUPPORTED_CONTROLLED_CHANCE " +
                                "site=combat.BeforeHandDraw " +
                                "operation=nested_choice");
                        var progress = semanticBeforeHandDraw
                            ? PrototypeAutomaticHandDrawProgressV111
                                .SemanticBeforeHandDraw()
                            : PrototypeAutomaticHandDrawProgressV111
                                .ControlledBeforeHandDrawHookChoice();
                        combat = combat with
                        {
                            PendingChoice = semanticBeforeHandDraw
                                ? pending with
                                {
                                    AutomaticSemanticDrawHookChoice = true
                                }
                                : pending
                        };
                        return CommitAutomaticHandDrawProgress(
                            PublishPrototypeRng(state), player, combat, progress,
                            createCheckpoint: true);
                    }
                }
                combat = ReturnLastTurnReboundCardsToHand(combat);

                var pollinousTurn = ApplyPollinousCoreHandDraw(player);
                player = pollinousTurn.Player;
                var handDrawBonus =
                    pollinousTurn.Bonus
                    + (player.ActiveRelics.Any(relic =>
                        relic.RelicId == NativeGloryAncientsV111.FiddleId)
                        ? 2 : 0)
                    + combat.PlayerPowers.Sum(power =>
                        PrototypeContent.Power(power.PowerId)
                            .HandDrawBonusPerStack
                        * power.Stacks)
                    + combat.PlayerPowers.Sum(power =>
                        power.Stacks > 0
                            ? PrototypeContent.Power(power.PowerId)
                                .HandDrawBonusWhileActive
                            : 0)
                    + PrototypeNativeOrobasRelics.HandDrawBonusForTurn(
                        player, combat.Turn);
                int requestedHandDraw = Math.Max(0,
                    PrototypeContent.Rules.HandSize + handDrawBonus);
                var drawn = DrawCards(
                    player,
                    combat,
                    requestedHandDraw,
                    prototypeRng,
                    fromHandDraw: true,
                    suspensionPolicy:
                        PrototypeDrawCardsSuspensionPolicyV111
                            .AutomaticHandDraw(combat),
                    automaticHandDrawStage: true);
                return ResumeAutomaticHandDrawSourceResult(
                    state, drawn, requestedHandDraw, prototypeRng,
                    handDrawResultContext);
            }

            case PrototypeAutomaticStepKind.ResolveLateRelicAutoplay:
                state = ResolveWhisperingEarringAutoPlay(state);
                prototypeRng = state.Stochastic.PrototypeRng;
                world = RequireWorld(state);
                combat = world.Combat!;
                player = state.Player;
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        return state with
        {
            Player = player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { Combat = combat }
        };
    }

    private static PrototypeCombatCounters AdvanceTurnCounters(
        PrototypeCombatCounters counters) => counters with
    {
        CardsPlayedThisTurn = 0,
        ManualCardsPlayedThisTurn = 0,
        AttacksPlayedLastTurn = counters.AttacksPlayedThisTurn,
        AttacksPlayedThisTurn = 0,
        SkillsPlayedThisTurn = 0,
        CardsDiscardedThisTurn = 0,
        PlayedCardIdsLastTurn = counters.PlayedCardIdsThisTurn,
        PlayedCardIdsThisTurn = Array.Empty<long>(),
        PlayedCardTagsThisTurn = Array.Empty<string>(),
        LastOriginalAttackLastTurn = counters.LastOriginalAttackThisTurn?.Fork(),
        LastOriginalAttackThisTurn = null
    };

    private static PrototypeQueuedOperation StratagemChoiceOperation(
        CombatState combat, bool semanticStandaloneDraw = false,
        bool semanticAutomaticDraw = false)
    {
        var amount = ExpectedStratagemSelectionCount(combat);
        return new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.ChooseCards, 0,
            Selection: new PrototypeCardSelectionSpec(
                PrototypeCardZone.DrawPile,
                amount, amount,
                PrototypeCardSelectionResolutionKind.MoveToHand),
            SemanticStratagemDrawSelection: semanticStandaloneDraw,
            AutomaticSemanticStratagemDrawSelection:
                semanticAutomaticDraw);
    }

    private static CombatState ReturnLastTurnReboundCardsToHand(
        CombatState combat)
    {
        var lastTurn = combat.CounterState.PlayedCardIdsLastTurn
            ?? Array.Empty<long>();
        foreach (var id in lastTurn.Distinct())
        {
            if (combat.Hand.Contains(id))
            {
                continue;
            }

            var card = combat.Cards.FirstOrDefault(c => c.InstanceId == id);
            if (card is null || card.CardId is not
                ("proto.colorless.bolas" or "proto.colorless.thrumming_hatchet"))
            {
                continue;
            }

            if (!PrototypeContent.Rules.HasHandCapacity(
                    combat.Hand.Length))
            {
                break;
            }

            var fromDraw = combat.DrawPile.Contains(id);
            var fromDiscard = combat.DiscardPile.Contains(id);
            var fromExhaust = combat.ExhaustPile.Contains(id);
            if (!fromDraw && !fromDiscard && !fromExhaust)
            {
                continue;
            }

            var nextZones = combat.Mechanics.CardZones.WithZoneChanges(
                new Dictionary<PrototypeCardZone, long[]>
                {
                    [PrototypeCardZone.DrawPile] = combat.DrawPile
                        .Where(x => x != id).ToArray(),
                    [PrototypeCardZone.DiscardPile] = combat.DiscardPile
                        .Where(x => x != id).ToArray(),
                    [PrototypeCardZone.ExhaustPile] = combat.ExhaustPile
                        .Where(x => x != id).ToArray(),
                    [PrototypeCardZone.Hand] = combat.Hand.Append(id).ToArray()
                });
            combat = fromDraw
                ? SemanticDrawPileAdapterV111.ApplyDrawPileRemoval(
                    combat, [id], nextZones)
                : SemanticDrawPileAdapterV111.ApplyZoneTransition(
                    combat, nextZones);
        }
        return combat;
    }

    private static CombatState ResolveEnemyStatusStage(
        CombatState combat,
        PrototypeTurnStage stage,
        RngBundle rng)
    {
        RequireSemanticNativeStreamOwner(
            combat, "combat.enemy_status_stage");
        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();

        for (var enemyIndex = 0; enemyIndex < enemies.Length; enemyIndex++)
        {
            var enemy = enemies[enemyIndex];
            var wasAlive = enemy.Hp > 0;
            if (enemy.Hp <= 0)
            {
                continue;
            }

            var statuses = new Dictionary<string, int>(enemy.Statuses, StringComparer.Ordinal);
            foreach (var pair in enemy.Statuses.ToArray())
            {
                var definition = PrototypeContent.Status(pair.Key);
                var stacks = pair.Value;

                if (definition.TriggerStage == stage)
                {
                    if (definition.TriggerKind
                        == PrototypeStatusTriggerKind
                            .DamageSelfByStacks)
                    {
                        var triggerCount =
                            ResolveEnemyStatusTriggerCount(
                                combat,
                                pair.Key,
                                stacks);
                        for (var trigger = 0;
                             trigger < triggerCount;
                             trigger++)
                        {
                            if (enemy.Hp <= 0
                                || stacks <= 0)
                            {
                                break;
                            }

                            var hpLoss =
                                ModifyEnemyHpLoss(
                                    enemy,
                                    stacks);
                            enemy = hpLoss.Enemy with
                            {
                                Hp = Math.Max(
                                    0,
                                    hpLoss.Enemy.Hp
                                    - hpLoss.HpLoss)
                            };

                            if (enemy.Hp > 0)
                            {
                                stacks -=
                                    definition
                                        .DecayOnTrigger;
                            }
                        }
                    }
                    else
                    {
                        stacks -=
                            definition.DecayOnTrigger;
                    }
                }

                if (definition.DecayStage == stage)
                {
                    stacks -= definition.DecayAtStage;
                }

                if (stacks > 0)
                {
                    statuses[pair.Key] = stacks;
                }
                else
                {
                    statuses.Remove(pair.Key);
                }
            }

            // Expiring an enemy-side countdown can wake its owner and
            // remove companion protection *before* other powers contribute
            // end-of-turn Block (native Asleep/Plating ordering).
            string? aiAfterExpiry = null;
            var powersToRemoveOnExpiry = new HashSet<string>(
                StringComparer.Ordinal);
            if (stage == PrototypeTurnStage.EnemyTurnEnd)
            {
                foreach (var power in enemy.PowerStates)
                {
                    var definition = PrototypeContent.Power(power.PowerId);
                    if (definition.EnemyStacksDecayAtSideTurnEnd > 0
                        && power.Stacks <=
                            definition.EnemyStacksDecayAtSideTurnEnd)
                    {
                        aiAfterExpiry ??=
                            definition.OwnerAiStateOnPowerExpiry;
                        foreach (var id in definition
                            .RemoveOwnerPowersOnPowerExpiry
                            ?? Array.Empty<string>())
                        {
                            powersToRemoveOnExpiry.Add(id);
                        }
                    }
                }
            }

            enemy = InterceptEnemyLethalDeath(enemy);
            var strengthAtSideTurnEnd = 0;
            // Expiring temporary debuffs restore their original Strength
            // *after* the enemy has acted, even though the source power
            // itself is removed at this stage.
            var temporaryStrengthRestoration =
                stage == PrototypeTurnStage.EnemyTurnEnd
                    ? enemy.PowerStates
                        .Where(power =>
                            PrototypeContent.Power(power.PowerId)
                                .RemoveAtEnemyTurnEnd)
                        .Sum(power => power.Stacks
                            * PrototypeContent.Power(power.PowerId)
                                .EnemyStrengthRestoreAtSideTurnEndPerStack)
                    : 0;
            var blockAtSideTurnEnd = 0;
            var powers = stage == PrototypeTurnStage.EnemyTurnEnd
                ? enemy.PowerStates
                    .Where(power =>
                        !PrototypeContent.Power(power.PowerId)
                            .RemoveAtEnemyTurnEnd
                        && !powersToRemoveOnExpiry.Contains(power.PowerId))
                    .Select(power =>
                    {
                        var definition = PrototypeContent.Power(power.PowerId);
                        if (power.SkipNextEnemySideTurnEnd)
                        {
                            // Newly applied enemy Ritual must not activate
                            // on the same enemy turn it was applied.
                            return power with
                            {
                                SkipNextEnemySideTurnEnd = false
                            };
                        }

                        strengthAtSideTurnEnd +=
                            power.Stacks
                            * definition.EnemyStrengthAtSideTurnEndPerStack;
                        blockAtSideTurnEnd +=
                            power.Stacks
                            * definition.EnemyBlockAtSideTurnEndPerStack;
                        return power with
                        {
                            Stacks = power.Stacks -
                                definition.EnemyStacksDecayAtSideTurnEnd
                        };
                    })
                    .Where(power => power.Stacks > 0
                        || PrototypeContent.Power(power.PowerId)
                            .AllowNegative)
                    .ToArray()
                : stage == PrototypeTurnStage.EnemyTurnStart
                  && combat.Turn > 1
                    ? enemy.PowerStates
                        .Select(power => power with
                        {
                            Stacks = power.Stacks -
                                PrototypeContent.Power(power.PowerId)
                                    .EnemyStacksDecayAtSideTurnStartAfterFirst
                        })
                        .Where(power => power.Stacks > 0
                            || PrototypeContent.Power(power.PowerId)
                                .AllowNegative)
                        .ToArray()
                    : enemy.PowerStates;

            // Native BattlewornDummyTimeLimitPower: the dummy
            // escapes when its third enemy-side turn ends, and never
            // grants the selected reward on such a timeout.
            var timedOut = stage == PrototypeTurnStage.EnemyTurnEnd
                && enemy.Hp > 0
                && enemy.PowerStates.Any(power =>
                    power.PowerId ==
                        PrototypeNativeGloryEvents.BattlewornTimeLimitId
                    && power.Stacks <= 1);
            enemies[enemyIndex] = enemy with
            {
                Hp = timedOut ? 0 : enemy.Hp,
                Escaped = timedOut || enemy.Escaped,
                Statuses = statuses,
                Powers = powers,
                AiStateId = aiAfterExpiry ?? enemy.AiStateId,
                Block = enemy.Block + blockAtSideTurnEnd,
                SharedSummonUsedThisTurn =
                    stage == PrototypeTurnStage.EnemyTurnStart
                        ? false : enemy.SharedSummonUsedThisTurn,
                HpLossBudgetUsed =
                    stage == PrototypeTurnStage.EnemyTurnStart
                        ? 0 : enemy.HpLossBudgetUsed,
                GainedReactiveBlockThisTurn =
                    stage == PrototypeTurnStage.EnemyTurnEnd
                        ? false : enemy.GainedReactiveBlockThisTurn
            };

            var totalStrengthRestoration =
                strengthAtSideTurnEnd + temporaryStrengthRestoration;
            if (enemy.Hp > 0 && totalStrengthRestoration != 0)
            {
                var strengthened = ApplyEnemyPowerToState(
                    combat, enemies[enemyIndex],
                    "proto.power.strength", totalStrengthRestoration,
                    ignoreDebuffPrevention: true);
                combat = strengthened.Combat;
                enemies[enemyIndex] = strengthened.Enemy;
            }
            if (wasAlive && enemies[enemyIndex].Hp <= 0
                && !enemies[enemyIndex].Escaped)
            {
                // Scheduled poison damage is processed directly in this
                // stage rather than through LoseEnemyHp. It must still
                // dispatch ally-death powers before other enemies act.
                combat = ResolveAllyDeathPowers(
                    combat with { Enemies = enemies }, enemy.InstanceId);
                enemies = combat.Enemies
                    .Select(item => item.Fork())
                    .ToArray();
            }

            if (stage == PrototypeTurnStage.EnemyTurnEnd
                && enemies[enemyIndex].Hp > 0)
            {
                var demiseHpLoss = enemies[enemyIndex].PowerStates.Sum(
                    power => Math.Max(0, power.Stacks)
                        * PrototypeContent.Power(power.PowerId)
                            .EnemyHpLossAtSideTurnEndPerStack);
                if (demiseHpLoss > 0)
                {
                    var lost = LoseEnemyHp(
                        combat with { Enemies = enemies },
                        enemies[enemyIndex].InstanceId,
                        demiseHpLoss);
                    combat = lost.Combat;
                    enemies = combat.Enemies
                        .Select(item => item.Fork())
                        .ToArray();
                    if (lost.Defeated)
                    {
                        combat = CleanupSourceBoundPowersForDefeatedEnemies(
                            combat);
                        combat = ResolveEnemyDeathSummons(combat, rng);
                        enemies = combat.Enemies
                            .Select(item => item.Fork())
                            .ToArray();
                    }
                }
            }
        }

        combat = combat with { Enemies = enemies };
        return CleanupSourceBoundPowersForDefeatedEnemies(
            combat);
    }

    // Commit each enemy's action at combat entry and at the end of each
    // enemy turn. Importantly, this consumes the combat RNG once, at a
    // transition boundary; repeated public observations never reroll moves.
    internal sealed record NativeCommittedIntentAuditV111(
        CombatState Combat, RandomIndexDecisionV111[] Choices,
        SemanticIntentAuditEntryV111[] WeightedOutcomes);

    internal static CombatState CommitEnemyIntents(
        CombatState combat, RngBundle rng,
        string siteId = NativeCombatRngEntryV111.CommittedIntentSiteId) =>
        CommitEnemyIntentsCore(combat, rng, null, null, siteId);

    internal static NativeCommittedIntentAuditV111 CommitEnemyIntentsWithAudit(
        CombatState combat, RngBundle rng,
        string siteId = NativeCombatRngEntryV111.CommittedIntentSiteId)
    {
        if (combat.Stochastic.NativeRng is null)
            throw new InvalidOperationException("Native intent audit requires attached native RNG.");
        var decisions = new List<RandomIndexDecisionV111>();
        var weightedOutcomes = new List<SemanticIntentAuditEntryV111>();
        var result = CommitEnemyIntentsCore(
            combat, rng, decisions, weightedOutcomes, siteId);
        return new NativeCommittedIntentAuditV111(
            result, decisions.ToArray(), weightedOutcomes.ToArray());
    }

    private static CombatState CommitEnemyIntentsCore(
        CombatState combat, RngBundle rng,
        List<RandomIndexDecisionV111>? audit,
        List<SemanticIntentAuditEntryV111>? weightedOutcomes, string siteId)
    {
        if (!StochasticSiteRegistryV111.CombatIntentSiteIds.Contains(
                siteId, StringComparer.Ordinal))
            throw new NotSupportedException(
                $"UNSUPPORTED_RANDOM_SITE site={siteId} " +
                "operation=enemy_intent_commit.");
        var ownedNative = combat.Stochastic.NativeRng?.Fork();
        var formation = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        var provider = new NativeRandomIndexProviderV111();
        bool captureWeightedOutcomes = ownedNative is not null &&
            (weightedOutcomes is not null ||
             RandomBoundaryJournalV111.HasActiveJournal);
        int occurrence = 0;
        int sampledIntentTicketCount = 0;
        int weightedOutcomeCount = 0;
        for (int index = 0; index < formation.Length; index++)
        {
            var enemy = formation[index];
            SemanticIntentCandidateV111[]? pendingCandidates = null;
            Func<int, int> nextIndex = ownedNative is null
                ? maxExclusive => ResolveLegacyBoundedInteger(
                    rng,
                    siteId,
                    "creature:" + enemy.InstanceId,
                    maxExclusive)
                : maxExclusive =>
                {
                    var ordinal = occurrence;
                    var request = new RandomIndexRequestV111(
                        siteId, "run.monster_ai",
                        "creature:" + enemy.InstanceId, ordinal,
                        [], OperationKind: "bounded_integer",
                        BoundedCount: maxExclusive);
                    var result = StochasticProviderScopeV111.ResolveIndex(
                        provider, request, ownedNative.RunStreams);
                    audit?.Add(result);
                    occurrence++;
                    sampledIntentTicketCount++;
                    if (captureWeightedOutcomes &&
                        pendingCandidates is { Length: > 0 })
                    {
                        var totalWeight = checked(pendingCandidates.Sum(
                            candidate => candidate.RawWeight));
                        if (totalWeight != maxExclusive)
                            throw new InvalidDataException(
                                "Native intent weights disagree with the primitive ticket range.");
                        var support = new SemanticIntentSupportV111(
                            request.OwnerId, pendingCandidates, totalWeight);
                        var boundaryFingerprint = CanonicalJson.Sha256(new
                        {
                            SiteId = siteId,
                            EnemyIndex = index,
                            Formation = formation,
                            Request = request
                        });
                        var semanticEvent = SemanticIntentChoiceV111.CreateEvent(
                            boundaryFingerprint, request, support);
                        var selectedCandidate = SemanticIntentChoiceV111
                            .SelectBySourceTicket(
                                semanticEvent, result.SelectedIndex);
                        var outcome = new SemanticIntentOutcomeV111(
                            semanticEvent, selectedCandidate.IntentId,
                            result.SelectedIndex);
                        var auditEntry = new SemanticIntentAuditEntryV111(
                            outcome, result);
                        auditEntry.Validate();
                        weightedOutcomes?.Add(auditEntry);
                        RandomBoundaryJournalV111
                            .RecordWeightedIntentOutcome(auditEntry);
                        weightedOutcomeCount++;
                    }
                    pendingCandidates = null;
                    return result.SelectedIndex;
                };
            var chosen = EvaluateEnemyIntentChoice(
                formation, index, ownedNative?.Formation.NativeEncounterEntry,
                nextIndex,
                !captureWeightedOutcomes
                    ? null
                    : candidates => pendingCandidates = candidates);
            formation[index] = ApplyEnemyIntentOutcome(
                enemy, chosen.MoveIndex, chosen.NextAiStateId);
        }
        if (captureWeightedOutcomes &&
            sampledIntentTicketCount != weightedOutcomeCount)
            throw new InvalidDataException(
                "Native intent audit did not describe every sampled ticket as a weighted outcome.");
        return combat with
        {
            Enemies = formation,
            Stochastic = CombatStochasticStateV111.WithNativeRng(
                combat.Stochastic, ownedNative)
        };
    }

    /// <summary>
    /// Commits a semantic intent outcome after any provider has selected it.
    /// Native sampling and controlled Nature therefore apply the same planned
    /// move, next AI state, and stun-follow-up transition.
    /// </summary>
    internal static EnemyCombatState ApplyEnemyIntentOutcome(
        EnemyCombatState enemy, int? moveIndex, string? nextAiStateId) =>
        enemy with
        {
            PlannedMoveIndex = moveIndex,
            PlannedNextAiStateId = nextAiStateId,
            StunFollowUpMoveIndex =
                moveIndex.HasValue && enemy.StunFollowUpMoveIndex.HasValue
                    ? null : enemy.StunFollowUpMoveIndex,
            StunFollowUpAiStateId =
                moveIndex.HasValue && enemy.StunFollowUpMoveIndex.HasValue
                    ? null : enemy.StunFollowUpAiStateId
        };

    /// <summary>
    /// Pure source-shaped intent selector shared by the actual game engine and
    /// serializable controlled Nature. No mutation, hook replay or RNG draw
    /// occurs except through the caller's supplied bounded ticket callback.
    /// </summary>
    internal static (int? MoveIndex, string? NextAiStateId) EvaluateEnemyIntentChoice(
        IReadOnlyList<EnemyCombatState> formation,
        int index, string? nativeEntry, Func<int, int> nextIndex,
        Action<SemanticIntentCandidateV111[]>? inspectCandidates = null)
    {
        var enemy = formation[index];
        var definition = PrototypeContent.Enemy(enemy.EnemyId);
        if (enemy.Hp <= 0 || enemy.SkipNextEnemyAction
            || enemy.EnemyActionSkipsRemaining > 0 || definition.Moves.Length == 0)
            return (null, null);
        if (enemy.StunFollowUpMoveIndex is { } stunFollowUp)
        {
            if (stunFollowUp < 0 || stunFollowUp >= definition.Moves.Length)
                throw new InvalidOperationException(
                    $"Enemy {enemy.EnemyId} has an invalid stunned follow-up move.");
            return (stunFollowUp, enemy.StunFollowUpAiStateId ?? enemy.AiStateId);
        }
        var evaluating = nativeEntry == "InkletsNormal"
            && enemy.EnemyId == "proto.enemy.inklet"
            && enemy.SlotName is null
                ? enemy with
                {
                    SlotName = enemy.FormationPosition switch
                    {
                        0 => "left",
                        1 => "middle",
                        2 => "right",
                        _ => null
                    }
                }
                : nativeEntry == "TheKinBoss"
                    && enemy.EnemyId == "proto.enemy.kin_follower"
                    ? enemy with
                    {
                        SlotName = enemy.SlotName switch
                        {
                            "slot1" => "dance",
                            "slot2" => "quick",
                            _ => throw new InvalidOperationException(
                                "Unknown native KinFollower starting slot.")
                        }
                    }
                    : enemy;
        var selected = SelectEnemyMove(
            definition, evaluating, formation, nextIndex, inspectCandidates);
        int moveIndex = Array.IndexOf(definition.Moves, selected.Move);
        if (moveIndex < 0)
            throw new InvalidOperationException(
                $"Enemy '{enemy.EnemyId}' selected an unknown move.");
        return (moveIndex, selected.NextAiStateId);
    }

    private static (
        PrototypeEnemyMoveDefinition Move,
        string? NextAiStateId)
        SelectEnemyMove(
            PrototypeEnemyDefinition definition,
            EnemyCombatState enemy,
            IReadOnlyList<EnemyCombatState> formation,
            RngBundle rng) =>
        SelectEnemyMove(definition, enemy, formation,
            maxExclusive => ResolveLegacyBoundedInteger(
                rng,
                "combat.committed_intent.index_draw",
                "creature:" + enemy.InstanceId,
                maxExclusive));

    private static (
        PrototypeEnemyMoveDefinition Move,
        string? NextAiStateId)
        SelectEnemyMove(
            PrototypeEnemyDefinition definition,
            EnemyCombatState enemy,
            IReadOnlyList<EnemyCombatState> formation,
            Func<int, int> nextIndex,
        Action<SemanticIntentCandidateV111[]>? inspectCandidates = null)
    {
        if (definition.Moves.Length == 0)
        {
            throw new InvalidOperationException(
                $"Enemy '{definition.Id}' has no moves.");
        }

        switch (definition.MovePolicy)
        {
            case PrototypeEnemyMovePolicy.SequentialLoop:
            {
                if (definition.MoveLoopStartIndex < 0
                    || definition.MoveLoopStartIndex
                        >= definition.Moves.Length)
                {
                    throw new InvalidOperationException(
                        $"Enemy '{definition.Id}' has invalid move-loop start index {definition.MoveLoopStartIndex}.");
                }

                var moveIndex = enemy.MoveIndex;
                if (moveIndex >= definition.Moves.Length)
                {
                    var loopLength =
                        definition.Moves.Length
                        - definition.MoveLoopStartIndex;
                    moveIndex =
                        definition.MoveLoopStartIndex
                        + ((moveIndex
                            - definition.MoveLoopStartIndex)
                           % loopLength);
                }

                return (
                    definition.Moves[moveIndex],
                    enemy.AiStateId);
            }

            case PrototypeEnemyMovePolicy.UniformRandomAfterOpener:
            {
                var openingMoves =
                    definition.OpeningMoveIndices
                    ?? [definition.OpeningMoveIndex];

                if (openingMoves.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Enemy '{definition.Id}' has an empty opening move sequence.");
                }

                foreach (var openingMoveIndex in openingMoves)
                {
                    if (openingMoveIndex < 0
                        || openingMoveIndex
                            >= definition.Moves.Length)
                    {
                        throw new InvalidOperationException(
                            $"Enemy '{definition.Id}' has invalid opening move index {openingMoveIndex}.");
                    }
                }

                if (enemy.MoveIndex < openingMoves.Length)
                {
                    return (
                        definition.Moves[
                            openingMoves[enemy.MoveIndex]],
                        enemy.AiStateId);
                }

                if (definition.RandomMovePoolStartIndex < 0
                    || definition.RandomMovePoolStartIndex
                        >= definition.Moves.Length)
                {
                    throw new InvalidOperationException(
                        $"Enemy '{definition.Id}' has invalid random-pool start index {definition.RandomMovePoolStartIndex}.");
                }

                var candidates = definition.Moves
                    .Skip(definition.RandomMovePoolStartIndex)
                    .Where(move =>
                        move.MaxConsecutiveUses <= 0
                        || !StringComparer.Ordinal.Equals(
                            enemy.LastMoveId,
                            move.Id)
                        || enemy.ConsecutiveMoveUses
                            < move.MaxConsecutiveUses)
                    .ToArray();
                if (candidates.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Enemy '{definition.Id}' has no legal random moves.");
                }

                inspectCandidates?.Invoke(candidates.Select((move, position) =>
                    new SemanticIntentCandidateV111(
                        "move:" + move.Id + ":index:" + position,
                        move.Id,
                        Array.IndexOf(definition.Moves, move),
                        1, position)).ToArray());
                return (
                    candidates[nextIndex(candidates.Length)],
                    enemy.AiStateId);
            }

            case PrototypeEnemyMovePolicy.StateMachine:
                return SelectEnemyStateMachineMove(
                    definition,
                    enemy,
                    formation,
                    nextIndex,
                    inspectCandidates);

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(definition.MovePolicy));
        }
    }

    private static (
        PrototypeEnemyMoveDefinition Move,
        string? NextAiStateId)
        SelectEnemyStateMachineMove(
            PrototypeEnemyDefinition definition,
            EnemyCombatState enemy,
            IReadOnlyList<EnemyCombatState> formation,
            Func<int, int> nextIndex,
            Action<SemanticIntentCandidateV111[]>? inspectCandidates = null)
    {
        var ai = definition.Ai
            ?? throw new InvalidOperationException(
                $"Enemy '{definition.Id}' uses StateMachine policy without an AI definition.");
        if (ai.States.Length == 0)
        {
            throw new InvalidOperationException(
                $"Enemy '{definition.Id}' has an empty AI state machine.");
        }

        var states = ai.States.ToDictionary(
            state => state.Id,
            StringComparer.Ordinal);
        var stateId = enemy.AiStateId
            ?? ai.InitialStateId;

        for (var depth = 0; depth < 32; depth++)
        {
            if (!states.TryGetValue(
                    stateId,
                    out var state))
            {
                throw new InvalidOperationException(
                    $"Enemy '{definition.Id}' AI references missing state '{stateId}'.");
            }

            switch (state.Kind)
            {
                case PrototypeEnemyAiStateKind.Move:
                {
                    if (state.MoveIndex is null
                        || state.MoveIndex.Value < 0
                        || state.MoveIndex.Value
                            >= definition.Moves.Length)
                    {
                        throw new InvalidOperationException(
                            $"Enemy '{definition.Id}' AI move state '{state.Id}' has invalid move index.");
                    }

                    return (
                        definition.Moves[
                            state.MoveIndex.Value],
                        state.NextStateId);
                }

                case PrototypeEnemyAiStateKind.Random:
                {
                    var branches =
                        state.Branches
                        ?? Array.Empty<
                            PrototypeEnemyAiBranch>();
                    var legal = branches
                        .Where(branch =>
                            branch.Weight > 0
                            && IsEnemyAiBranchLegal(
                                definition,
                                enemy,
                                states,
                                branch,
                                formation))
                        .ToArray();
                    if (legal.Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"Enemy '{definition.Id}' AI random state '{state.Id}' has no legal branches.");
                    }

                    var totalWeight =
                        legal.Sum(branch =>
                            branch.Weight);
                    if (inspectCandidates is not null)
                    {
                        var start = 0;
                        var support = legal.Select((branch, branchIndex) =>
                        {
                            // IsEnemyAiBranchLegal already verified the
                            // branch targets a concrete Move state.
                            var moveIndex = states[branch.TargetStateId]
                                .MoveIndex!.Value;
                            var candidate = new SemanticIntentCandidateV111(
                                "state:" + state.Id + ":branch:" +
                                    branch.TargetStateId + ":index:" + branchIndex,
                                definition.Moves[moveIndex].Id, moveIndex,
                                branch.Weight, start);
                            start = checked(start + branch.Weight);
                            return candidate;
                        }).ToArray();
                        inspectCandidates(support);
                    }
                    var roll = nextIndex(totalWeight);
                    var selected = legal[^1];
                    foreach (var branch in legal)
                    {
                        if (roll < branch.Weight)
                        {
                            selected = branch;
                            break;
                        }

                        roll -= branch.Weight;
                    }

                    stateId =
                        selected.TargetStateId;
                    break;
                }

                case PrototypeEnemyAiStateKind.Conditional:
                {
                    var branches =
                        state.ConditionalBranches
                        ?? Array.Empty<
                            PrototypeEnemyAiConditionalBranch>();
                    var selected = branches.FirstOrDefault(
                        branch => EnemyAiConditionMatches(
                            enemy,
                            formation,
                            branch));
                    if (selected is null)
                    {
                        throw new InvalidOperationException(
                            $"Enemy '{definition.Id}' AI conditional state '{state.Id}' has no matching branch.");
                    }

                    if (!states.TryGetValue(
                            selected.TargetStateId,
                            out var target))
                    {
                        throw new InvalidOperationException(
                            $"Enemy '{definition.Id}' AI conditional branch targets missing state '{selected.TargetStateId}'.");
                    }

                    // The native Exoskeleton fourth slot branches to
                    // RAND rather than directly to a MoveState.
                    // Continue resolving the graph until a concrete
                    // move is committed, using the same RNG and without
                    // sampling anything during Observe().
                    stateId = target.Id;
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(state.Kind));
            }
        }

        throw new InvalidOperationException(
            $"Enemy '{definition.Id}' AI exceeded the state-resolution depth limit.");
    }

    internal static bool EnemyAiConditionMatches(
        EnemyCombatState enemy,
        IReadOnlyList<EnemyCombatState> formation,
        PrototypeEnemyAiConditionalBranch branch)
    {
        var living = formation
            .Where(item => item.Hp > 0)
            .OrderBy(item => item.FormationPosition)
            .ThenBy(item => item.InstanceId)
            .ToArray();
        var isAlone = living.Length == 1
            && living[0].InstanceId == enemy.InstanceId;
        var isFront = living.Length > 0
            && living[0].InstanceId == enemy.InstanceId;

        // Knowledge Demon tracks the number of curse rounds in its
        // owner-local move-use log, independent of observed intents.
        var moveThreshold = branch.Value?.Split(':');
        var used = moveThreshold is { Length: 2 }
            ? enemy.MoveUseCounts?.GetValueOrDefault(moveThreshold[0]) ?? 0
            : 0;
        var hasThreshold = moveThreshold is { Length: 2 }
            && int.TryParse(moveThreshold[1], out _);

        return branch.Condition switch
        {
            PrototypeEnemyAiConditionKind.MoveUsedFewerThan =>
                hasThreshold
                && used < int.Parse(moveThreshold![1]),
            PrototypeEnemyAiConditionKind.MoveUsedAtLeast =>
                hasThreshold
                && used >= int.Parse(moveThreshold![1]),
            PrototypeEnemyAiConditionKind.HpAtLeastHalf =>
                enemy.MaxHp > 0 && enemy.Hp >= enemy.MaxHp / 2,
            PrototypeEnemyAiConditionKind.HpBelowHalf =>
                enemy.MaxHp > 0 && enemy.Hp < enemy.MaxHp / 2,
            PrototypeEnemyAiConditionKind.HasLivingEnemyId =>
                branch.Value is not null && formation.Any(item =>
                    item.Hp > 0 && item.EnemyId == branch.Value),
            PrototypeEnemyAiConditionKind.LacksLivingEnemyId =>
                branch.Value is not null && !formation.Any(item =>
                    item.Hp > 0 && item.EnemyId == branch.Value),
            PrototypeEnemyAiConditionKind.IsAlone =>
                isAlone,
            PrototypeEnemyAiConditionKind.IsFront =>
                isFront,
            PrototypeEnemyAiConditionKind.IsNotFront =>
                !isFront,
            PrototypeEnemyAiConditionKind.SlotNameEquals =>
                branch.Value is not null
                && StringComparer.Ordinal.Equals(
                    enemy.SlotName,
                    branch.Value),
            PrototypeEnemyAiConditionKind.IsOffBalance =>
                enemy.IsOffBalance,
            PrototypeEnemyAiConditionKind.IsNotOffBalance =>
                !enemy.IsOffBalance,
            PrototypeEnemyAiConditionKind.HasEnemyPower =>
                branch.Value is not null
                && enemy.PowerStates.Any(power =>
                    power.PowerId == branch.Value && power.Stacks > 0),
            PrototypeEnemyAiConditionKind.LacksEnemyPower =>
                branch.Value is not null
                && !enemy.PowerStates.Any(power =>
                    power.PowerId == branch.Value && power.Stacks > 0),
            // GetTeammatesOf includes the acting monster.
            PrototypeEnemyAiConditionKind.LivingEnemiesAtMost =>
                int.TryParse(branch.Value, out var maxAlive)
                && living.Length <= maxAlive,
            PrototypeEnemyAiConditionKind.LivingEnemiesGreaterThan =>
                int.TryParse(branch.Value, out var minAlive)
                && living.Length > minAlive,
            _ => throw new ArgumentOutOfRangeException(
                nameof(branch.Condition))
        };
    }

    private static bool IsEnemyAiBranchLegal(
        PrototypeEnemyDefinition definition,
        EnemyCombatState enemy,
        IReadOnlyDictionary<
            string,
            PrototypeEnemyAiStateDefinition> states,
        PrototypeEnemyAiBranch branch,
        IReadOnlyList<EnemyCombatState> formation)
    {
        if (!states.TryGetValue(
                branch.TargetStateId,
                out var target)
            || target.Kind
                != PrototypeEnemyAiStateKind.Move
            || target.MoveIndex is null
            || target.MoveIndex.Value < 0
            || target.MoveIndex.Value
                >= definition.Moves.Length)
        {
            throw new InvalidOperationException(
                $"Enemy '{definition.Id}' AI branch targets invalid move state '{branch.TargetStateId}'.");
        }

        var move = definition.Moves[
            target.MoveIndex.Value];

        if (branch.RequiresAvailableSummon)
        {
            var summon = move.Effects.FirstOrDefault(effect =>
                effect.Kind == PrototypeEnemyEffectKind.SummonEnemy);
            var slots = summon?.SummonSlotNames;
            if (definition.MaxCoordinatedSummons <= 0
                || enemy.NonSummonMovesUntilEligible > 0
                || enemy.SharedSummonsUsed >= definition.MaxCoordinatedSummons
                || enemy.SharedSummonUsedThisTurn
                || slots is not { Length: > 0 }
                || !slots.Any(slot => !formation.Any(candidate =>
                    candidate.Hp > 0
                    && StringComparer.Ordinal.Equals(candidate.SlotName, slot))))
            {
                return false;
            }
        }

        return branch.RepeatRule switch
        {
            PrototypeEnemyAiRepeatRule
                .CanRepeatForever => true,
            PrototypeEnemyAiRepeatRule
                .CannotRepeat =>
                !StringComparer.Ordinal.Equals(
                    enemy.LastMoveId,
                    move.Id),
            PrototypeEnemyAiRepeatRule
                .CanRepeatXTimes =>
                branch.MaxTimes > 0
                && (!StringComparer.Ordinal.Equals(
                        enemy.LastMoveId,
                        move.Id)
                    || enemy.ConsecutiveMoveUses
                        < branch.MaxTimes),
            PrototypeEnemyAiRepeatRule
                .UseOnlyOnce =>
                (enemy.MoveUseCounts?.GetValueOrDefault(
                    move.Id) ?? 0) == 0,
            _ => throw new ArgumentOutOfRangeException(
                nameof(branch.RepeatRule))
        };
    }

    private static (PlayerState Player, CombatState Combat) ResolveEnemyActions(
        PlayerState player,
        CombatState combat,
        int act,
        int ascension,
        RngBundle rng)
    {
        RequireSemanticNativeStreamOwner(
            combat, "combat.resolve_enemy_actions");
        var enemies = combat.Enemies
            .Select(enemy => enemy.Fork())
            .ToList();
        var randomDiscardInsertionOrdinal = 0;
        var randomDrawInsertionOrdinal = 0;
        var block = combat.PlayerBlock;
        var hp = player.Hp;
        var gold = player.Gold;

        for (var index = 0; index < enemies.Count; index++)
        {
            var enemy = enemies[index];
            var enemyCombatId = combat.Stochastic.NativeRng is { } nativeRng
                ? index < nativeRng.Formation.MonsterContexts.Length
                    ? nativeRng.Formation.MonsterContexts[index].CombatId
                    : throw new InvalidDataException(
                        "Native enemy action lost its creature RNG owner.")
                : enemy.InstanceId;
            var definition = PrototypeContent.Enemy(enemy.EnemyId);

            if (enemy.Hp <= 0)
            {
                // ReattachPower: a deceased segment remains in combat,
                // untargetable. It first spends its DEAD move, then
                // reattaches for 25 HP on a later enemy action if at
                // least one other segment remains alive. Its next
                // normal intent is chosen after reattachment.
                if (enemy.PowerStates.Any(power =>
                        power.PowerId == PrototypeNativeHiveElites.ReattachId)
                    && enemies.Any(other =>
                        other.InstanceId != enemy.InstanceId
                        && other.Hp > 0
                        && other.PowerStates.Any(power =>
                            power.PowerId ==
                                PrototypeNativeHiveElites.ReattachId)))
                {
                    enemy = enemy.ReattachDeadMoveResolved
                        ? enemy with
                        {
                            Hp = Math.Min(definition.HpRangeAt(act,
                                ascension).Max,
                                enemy.PowerStates
                                    .Where(power => power.PowerId ==
                                        PrototypeNativeHiveElites.ReattachId)
                                    .Sum(power => power.Stacks)),
                            Block = 0,
                            LastMoveId = "reattach",
                            ReattachDeadMoveResolved = false,
                            AiStateId = "reattach_random",
                            Statuses = new Dictionary<string, int>(
                                StringComparer.Ordinal)
                        }
                        : enemy with
                        {
                            LastMoveId = "dead",
                            ReattachDeadMoveResolved = true
                        };
                    enemies[index] = enemy;
                    continue;
                }

                var leaderAlive = enemy.LeaderEnemyInstanceId is not
                        { } leaderId
                    || enemies.Any(candidate =>
                        candidate.InstanceId == leaderId
                        && candidate.Hp > 0);
                if (definition.RevivesOnEnemyTurn && leaderAlive)
                {
                    var hpRange = definition.HpRangeAt(
                        act,
                        ascension);
                    enemy = enemy with
                    {
                        Hp = hpRange.Max,
                        Block = 0,
                        LastMoveId = "revive",
                        Statuses =
                            new Dictionary<string, int>(
                                StringComparer.Ordinal)
                    };
                    enemies[index] = enemy;
                }

                continue;
            }

            if (enemy.EnemyActionSkipsRemaining > 0)
            {
                enemies[index] = enemy with
                {
                    EnemyActionSkipsRemaining =
                        enemy.EnemyActionSkipsRemaining - 1,
                    SkipNextEnemyAction = false
                };
                continue;
            }

            if (enemy.SkipNextEnemyAction)
            {
                enemies[index] = enemy with
                {
                    SkipNextEnemyAction = false
                };
                continue;
            }

            if (definition.Moves.Length == 0)
            {
                continue;
            }

            // Normal gameplay has a precommitted move. A fallback is kept
            // solely for legacy/manual combat states created without the
            // initial intent-commit step; seeded runs never use it.
            var selection = enemy.PlannedMoveIndex is { } selectedIndex
                ? (Move: definition.Moves[selectedIndex],
                   NextAiStateId: enemy.PlannedNextAiStateId)
                : SelectEnemyMove(definition, enemy, enemies, rng);
            var move = selection.Move;
            // BowlbugRock.DizzyMove unstuns the model and clears its
            // persistent off-balance condition before its next Headbutt.
            if (enemy.EnemyId == "proto.native.hive.bowlbug_rock"
                && move.Id == "dizzy")
            {
                enemy = enemy with { IsOffBalance = false };
            }
            // Vigor applies to every hit of one attack command, then
            // consumes the stacks that existed when the command began.
            // Vigor gained by this move is never consumed by that move.
            var consumeOnAttackOrders = move.Effects.Any(effect =>
                    effect.Kind == PrototypeEnemyEffectKind.DamagePlayer
                    && effect.IsAttack)
                ? enemy.PowerStates
                    .Where(power => power.Stacks > 0
                        && PrototypeContent.Power(power.PowerId)
                            .ConsumeAfterEnemyAttack)
                    .Select(power => power.ApplicationOrder)
                    .ToHashSet()
                : new HashSet<long>();

            foreach (var effect in move.Effects)
            {
                if (effect.OwnerPowerStackConditionId is { } requiredPower)
                {
                    var stackCount = enemy.PowerStates
                        .Where(power => power.PowerId == requiredPower)
                        .Sum(power => power.Stacks);
                    if (effect.OwnerPowerStacksLessThan is { } upper
                        && stackCount >= upper)
                        continue;
                    if (effect.OwnerPowerStacksAtLeast is { } lower
                        && stackCount < lower)
                        continue;
                }
                var amount = effect.UseStoredEnemyDamage
                    ? enemy.StoredEnemyDamage
                    : checked(effect.AmountAt(act, ascension)
                        + effect.ExtraAmountPerPriorMoveUse
                        * (enemy.MoveUseCounts?.GetValueOrDefault(move.Id) ?? 0));
                var repetitions = effect.RepetitionsAt(ascension);
                if (enemy.EnemyId == PrototypeNativeGloryBosses.TestSubjectId
                    && move.Id == "multi_claw"
                    && effect.Kind == PrototypeEnemyEffectKind.DamagePlayer)
                    repetitions += enemy.ExtraMultiClawCount;
                var unblockedAttackHits = 0;
                for (var repetition = 0; repetition < repetitions; repetition++)
                {
                    switch (effect.Kind)
                    {
                        case PrototypeEnemyEffectKind.DamagePlayer:
                        {
                            var retaliation = PlayerAttackRetaliation(combat);
                            if (retaliation > 0 && enemy.Hp > 0)
                            {
                                var retaliationBlocked = Math.Min(
                                    enemy.Block,
                                    retaliation);
                                var hpLoss = ModifyEnemyHpLoss(
                                    enemy,
                                    Math.Max(
                                        0,
                                        retaliation - retaliationBlocked));
                                enemy = hpLoss.Enemy with
                                {
                                    Block = enemy.Block - retaliationBlocked,
                                    Hp = Math.Max(
                                        0,
                                        hpLoss.Enemy.Hp - hpLoss.HpLoss)
                                };
                            }

                            var damage = EnemyHitDamage(
                                enemy,
                                combat,
                                effect,
                                act,
                                ascension,
                                enemy.MoveUseCounts?.GetValueOrDefault(move.Id) ?? 0);
                            // Surrounded modifies attacks from the arm
                            // opposite the side the player is facing.
                            var facing = combat.PlayerPowers.FirstOrDefault(power =>
                                power.PowerId ==
                                    PrototypeNativeHiveBosses.SurroundedId);
                            if (facing is not null
                                && (facing.StoredValue == 0
                                    && enemy.PowerStates.Any(power =>
                                        power.PowerId ==
                                            PrototypeNativeHiveBosses.BackLeftId)
                                    || facing.StoredValue == 1
                                    && enemy.PowerStates.Any(power =>
                                        power.PowerId ==
                                            PrototypeNativeHiveBosses.BackRightId)))
                                damage = (int)Math.Floor(damage * 1.5m);

                            var absorbed = Math.Min(
                                block,
                                Math.Max(0, damage));
                            block -= absorbed;
                            var unblocked = Math.Max(0, damage - absorbed);
                            // ImbalancedPower.AfterDamageGiven: a fully
                            // blocked attack leaves Bowlbug Rock off-balance.
                            // Headbutt then commits Dizzy on the NEXT
                            // enemy turn rather than rerolling in Observe.
                            if (effect.IsAttack && damage > 0
                                && unblocked == 0
                                && enemy.PowerStates.Any(power =>
                                    power.Stacks > 0
                                    && power.PowerId ==
                                        "proto.native.hive.imbalanced"))
                            {
                                enemy = enemy with { IsOffBalance = true };
                            }
                            if (effect.IsAttack && unblocked > 0 && hp > 0)
                            {
                                unblockedAttackHits++;
                            }
                            var aliveBeforeHit = hp > 0;
                            hp = Math.Max(0, hp - unblocked);
                            if (effect.IsAttack && unblocked > 0
                                && aliveBeforeHit)
                            {
                                // PaperCutsPower.AfterDamageGiven is per
                                // powered attack hit, after ordinary Block
                                // absorption. LoseMaxHp damages the player
                                // down to the new cap before clamping the
                                // cap itself to at least one.
                                var maxHpLoss = enemy.PowerStates.Sum(power =>
                                    Math.Max(0, power.Stacks)
                                    * PrototypeContent.Power(power.PowerId)
                                        .PlayerMaxHpLossOnUnblockedAttackHitPerStack);
                                if (maxHpLoss > 0)
                                {
                                    var rawMaxHp = player.MaxHp - maxHpLoss;
                                    hp = Math.Max(0, Math.Min(hp, rawMaxHp));
                                    player = player with
                                    {
                                        MaxHp = Math.Max(1, rawMaxHp)
                                    };
                                }
                            }
                            if (effect.IsAttack && unblocked > 0
                                && combat.PlayerPowers.Any(power =>
                                    power.Stacks > 0
                                    && PrototypeContent.Power(power.PowerId)
                                        .LethalAfterUnblockedPoweredAttack))
                            {
                                // The Gambit deliberately kills its owner
                                // upon the first unblocked powered hit.
                                hp = 0;
                            }
                            break;
                        }

                        case PrototypeEnemyEffectKind.KillSelf:
                            enemy = enemy with { Hp = 0, Block = 0 };
                            break;

                        case PrototypeEnemyEffectKind.StealPlayerCard:
                        {
                            // Thieving Hopper only steals persistent cards
                            // presently in the DRAW or DISCARD piles. A card
                            // in hand/exhaust/temporary play cannot be stolen.
                            var eligible = combat.DrawPile
                                .Concat(combat.DiscardPile)
                                .Select(id => combat.Cards.FirstOrDefault(
                                    card => card.InstanceId == id))
                                .Where(card => card is not null
                                    && card.PersistentCardInstanceId is not null
                                    && player.Deck.Any(persistent =>
                                        persistent.InstanceId
                                            == card.PersistentCardInstanceId))
                                .Select(card => card!)
                                .ToArray();
                            if (eligible.Length == 0)
                            {
                                break;
                            }

                            // Source priority: Uncommon; then Common,
                            // Rare or Event; then Basic or Quest; then
                            // Ancient/Imbued. An Imbued Skill stays in the
                            // final class regardless of printed rarity.
                            static int TheftPriority(CombatCardInstance card)
                            {
                                if (card.Enchantment?.Kind
                                    == PrototypeCardEnchantmentKind.Imbued)
                                    return 3;
                                var rarity = PrototypeContent.Card(
                                    card.CardId).Rarity;
                                return rarity switch
                                {
                                    PrototypeCardRarity.Uncommon => 0,
                                    PrototypeCardRarity.Common
                                        or PrototypeCardRarity.Rare
                                        or PrototypeCardRarity.Event => 1,
                                    PrototypeCardRarity.Basic
                                        or PrototypeCardRarity.Quest => 2,
                                    PrototypeCardRarity.Ancient => 3,
                                    _ => 4
                                };
                            }

                            var best = eligible.Min(TheftPriority);
                            var candidates = eligible.Where(card =>
                                TheftPriority(card) == best).ToArray();
                            // Native uses RunRng.CombatCardGeneration,
                            // which has no matching prototype substream.
                            // This stream is explicitly an approximation.
                            var choice = ResolveRandomCombatCardIndex(
                                combat, rng,
                                StochasticSiteRegistryV111
                                    .CombatEnemyTheftCardSelectSiteId,
                                "combat",
                                candidates.Select(card => card.InstanceId)
                                    .ToArray(),
                                "enemy:" + enemy.InstanceId +
                                    ":effect:steal_player_card",
                                nativeStreamId:
                                    "run.combat_card_generation");
                            combat = choice.Combat;
                            var chosen = choice.SelectedCardInstanceId is
                                { } selectedCardInstanceId
                                ? candidates.SingleOrDefault(card =>
                                    card.InstanceId == selectedCardInstanceId)
                                    ?? throw new InvalidDataException(
                                        "Saved enemy theft identity is outside the live eligible support.")
                                : candidates[choice.SelectedIndex];
                            var persistentId =
                                chosen.PersistentCardInstanceId!.Value;
                            var persistentCard = player.Deck.Single(card =>
                                card.InstanceId == persistentId);
                            var chosenFromDraw = combat.DrawPile.Contains(
                                chosen.InstanceId);
                            player = player with
                            {
                                Deck = player.Deck.Where(card =>
                                    card.InstanceId != persistentId)
                                    .ToArray()
                            };
                            var zoneChanges = new Dictionary<PrototypeCardZone, long[]>
                            {
                                [PrototypeCardZone.DrawPile] = combat.DrawPile
                                    .Where(id => id != chosen.InstanceId).ToArray(),
                                [PrototypeCardZone.DiscardPile] = combat.DiscardPile
                                    .Where(id => id != chosen.InstanceId).ToArray()
                            };
                            var nextZones = combat.Mechanics.CardZones
                                .WithZoneChanges(zoneChanges);
                            combat = (chosenFromDraw
                                ? SemanticDrawPileAdapterV111
                                    .ApplyDrawPileRemoval(
                                        combat, [chosen.InstanceId], nextZones,
                                        removedFromCombatInstanceIds:
                                            [chosen.InstanceId])
                                : SemanticDrawPileAdapterV111
                                    .ApplyZoneIdentityDelta(
                                        combat, nextZones, [],
                                        [chosen.InstanceId],
                                        removedCombatCardInstanceIds:
                                            [chosen.InstanceId])) with
                            {
                                NextPowerApplicationOrder =
                                    combat.NextPowerApplicationOrder + 1
                            };
                            enemy = enemy with
                            {
                                StolenCards = enemy.StolenCards
                                    is { } existing
                                        ? existing.Append(persistentCard)
                                            .ToArray()
                                        : [persistentCard],
                                Powers = enemy.PowerStates.Append(
                                    new PrototypePowerInstanceState(
                                        "proto.native.hive.swipe", 1,
                                        combat.NextPowerApplicationOrder - 1))
                                    .ToArray()
                            };
                            break;
                        }

                        case PrototypeEnemyEffectKind.StealPlayerGold:
                        {
                            var stolen = Math.Min(Math.Max(0, amount), gold);
                            gold -= stolen;
                            enemy = enemy with
                            {
                                StolenGold = checked(enemy.StolenGold + stolen)
                            };
                            break;
                        }

                        case PrototypeEnemyEffectKind.EscapeEnemy:
                            enemy = enemy with
                            {
                                Hp = 0,
                                Block = 0,
                                Escaped = true
                            };
                            break;

                        case PrototypeEnemyEffectKind.GainBlock:
                            enemy = enemy with { Block = enemy.Block + Math.Max(0, amount) };
                            break;

                        case PrototypeEnemyEffectKind.GrantBlockToEnemyType:
                            if (effect.EnemyId is null)
                                throw new InvalidOperationException(
                                    "Ally Block grant must specify an enemy type.");
                            for (var allyIndex = 0;
                                 allyIndex < enemies.Count;
                                 allyIndex++)
                            {
                                var ally = enemies[allyIndex];
                                if (ally.Hp > 0 && ally.EnemyId == effect.EnemyId)
                                    enemies[allyIndex] = ally with
                                    {
                                        Block = checked(ally.Block
                                            + Math.Max(0, amount))
                                    };
                            }
                            break;

                        case PrototypeEnemyEffectKind.HealSelf:
                            enemy = enemy with
                            {
                                Hp = Math.Min(
                                    definition.HpRangeAt(act, ascension).Max,
                                    enemy.Hp + Math.Max(0, amount))
                            };
                            break;

                        case PrototypeEnemyEffectKind.StoreEnemyPowerAsDamage:
                            if (effect.PowerId is null)
                            {
                                throw new InvalidOperationException(
                                    "Stored-damage effect requires a power ID.");
                            }
                            enemy = enemy with
                            {
                                StoredEnemyDamage = enemy.PowerStates
                                    .Where(power => power.PowerId == effect.PowerId)
                                    .Sum(power => power.Stacks),
                                Powers = enemy.PowerStates
                                    .Where(power => power.PowerId != effect.PowerId)
                                    .ToArray()
                            };
                            break;

                        case PrototypeEnemyEffectKind.ApplyPlayerPower:
                            if (effect.PowerId is null)
                            {
                                throw new InvalidOperationException(
                                    "Enemy apply-power effect is missing a power ID.");
                            }

                            combat = ApplyPlayerPower(
                                combat,
                                effect.PowerId,
                                amount,
                                sourceEnemyInstanceId: enemy.InstanceId);
                            break;

                        case PrototypeEnemyEffectKind.ApplyEnemyPower:
                            if (effect.PowerId is null)
                            {
                                throw new InvalidOperationException(
                                    "Enemy self-power effect is missing a power ID.");
                            }

                            var selfPower = ApplyEnemyPowerToState(
                                combat,
                                enemy,
                                effect.PowerId,
                                amount);
                            combat = selfPower.Combat;
                            enemy = selfPower.Enemy;
                            break;

                        case PrototypeEnemyEffectKind.RemoveEnemyPower:
                            if (effect.PowerId is null)
                                throw new InvalidOperationException(
                                    "Enemy remove-power effect requires a power ID.");
                            enemy = enemy with
                            {
                                Powers = enemy.PowerStates.Where(power =>
                                    power.PowerId != effect.PowerId).ToArray()
                            };
                            break;

                        case PrototypeEnemyEffectKind.StealPlayerPower:
                        {
                            if (effect.PowerId is not
                                ("proto.power.strength" or "proto.power.dexterity"))
                                throw new InvalidOperationException(
                                    "Stat theft must specify Strength or Dexterity.");
                            var prior = combat.PlayerPowers
                                .Where(power => power.PowerId == effect.PowerId)
                                .Sum(power => power.Stacks);
                            combat = ApplyPlayerPower(
                                combat, effect.PowerId, -amount,
                                sourceEnemyInstanceId: enemy.InstanceId);
                            var after = combat.PlayerPowers
                                .Where(power => power.PowerId == effect.PowerId)
                                .Sum(power => power.Stacks);
                            var stolen = Math.Max(0, prior - after);
                            var granted = ApplyEnemyPowerToState(
                                combat, enemy, effect.PowerId, amount);
                            combat = granted.Combat;
                            enemy = granted.Enemy;
                            var ownerPowerId =
                                effect.PowerId == "proto.power.strength"
                                    ? PrototypeNativeGloryNewNormals.PossessStrengthId
                                    : PrototypeNativeGloryNewNormals.PossessSpeedId;
                            enemy = enemy with
                            {
                                Powers = enemy.PowerStates.Select(power =>
                                    power.PowerId == ownerPowerId
                                        ? power with
                                        {
                                            StoredValue = checked(
                                                power.StoredValue + stolen)
                                        }
                                        : power).ToArray()
                            };
                            break;
                        }

                        case PrototypeEnemyEffectKind.ApplyAllEnemyPower:
                        {
                            if (effect.PowerId is null)
                                throw new InvalidOperationException(
                                    "Team power requires a power ID.");
                            for (var allyIndex = 0; allyIndex < enemies.Count; allyIndex++)
                            {
                                var ally = enemies[allyIndex].InstanceId ==
                                    enemy.InstanceId ? enemy : enemies[allyIndex];
                                if (ally.Hp <= 0)
                                    continue;
                                var applied = ApplyEnemyPowerToState(
                                    combat, ally, effect.PowerId, amount);
                                combat = applied.Combat;
                                enemies[allyIndex] = applied.Enemy;
                                if (ally.InstanceId == enemy.InstanceId)
                                    enemy = applied.Enemy;
                            }
                            break;
                        }

                        case PrototypeEnemyEffectKind.TransformEnemy:
                        {
                            if (effect.EnemyId is null)
                                throw new InvalidOperationException(
                                    "Enemy transformation requires a model ID.");
                            if (combat.Stochastic.NativeRng is not null)
                            {
                                if (combat.Stochastic.NativeRng.Formation.NativeEncounterEntry
                                        == "OvicopterNormal"
                                    && enemy.EnemyId ==
                                        PrototypeNativeHiveSummoningNormals.ToughEggId
                                    && effect.EnemyId ==
                                        PrototypeNativeHiveSummoningNormals.HatchlingId)
                                {
                                    // Native ToughEgg.Hatch preserves the
                                    // MonsterModel and CombatId. The proxy
                                    // HatchlingId selects its Nibble move,
                                    // without creating a second RNG context.
                                    combat = NativeCombatRngEntryV111.HatchToughEgg(
                                        combat, enemy, out var nativeHatchling);
                                    enemy = nativeHatchling;
                                    break;
                                }
                                throw new NotSupportedException(
                                    "Native creature transformation has not been audited; " +
                                    "do not reuse or reseed a native monster RNG context.");
                            }
                            var transformed = PrototypeContent.Enemy(effect.EnemyId);
                            var range = transformed.HpRangeAt(act, ascension);
                            var maxHp = range.Min == range.Max
                                ? range.Max
                                : ResolveLegacyRandomIndex(
                                    rng,
                                    NativeCombatRngEntryV111.ToughEggHatchHpSiteId,
                                    "creature:" + enemy.InstanceId,
                                    Enumerable.Range(
                                        range.Min,
                                        checked(range.Max - range.Min + 1)));
                            // ToughEgg.Hatch preserves its instance and
                            // Minion marker, but changes the combat body.
                            enemy = enemy with
                            {
                                EnemyId = effect.EnemyId,
                                Hp = maxHp,
                                MaxHp = maxHp,
                                Block = 0,
                                Statuses = new Dictionary<string, int>(
                                    StringComparer.Ordinal),
                                Powers = enemy.PowerStates.Where(power =>
                                    power.PowerId == "proto.power.minion")
                                    .ToArray(),
                                AiStateId = transformed.Ai?.InitialStateId
                            };
                            break;
                        }

                        case PrototypeEnemyEffectKind.GrantStrengthToOtherLivingEnemies:
                        {
                            for (var allyIndex = 0; allyIndex < enemies.Count; allyIndex++)
                            {
                                var ally = enemies[allyIndex];
                                if (ally.Hp <= 0 || ally.InstanceId == enemy.InstanceId)
                                    continue;
                                var applied = ApplyEnemyPowerToState(
                                    combat, ally, effect.PowerId
                                        ?? "proto.power.strength", amount);
                                combat = applied.Combat;
                                enemies[allyIndex] = applied.Enemy;
                            }
                            break;
                        }

                        case PrototypeEnemyEffectKind.ReviveTestSubject:
                        {
                            if (enemy.EnemyId != PrototypeNativeGloryBosses.TestSubjectId
                                || !enemy.BossPendingRevival || enemy.BossPhase >= 2)
                                throw new InvalidOperationException(
                                    "Test Subject respawn requires a pending earlier phase.");
                            var newPhase = enemy.BossPhase + 1;
                            var newHp = newPhase == 1
                                ? (ascension >= 8 ? 212 : 200)
                                : (ascension >= 8 ? 313 : 300);
                            enemy = enemy with
                            {
                                Hp = newHp,
                                MaxHp = newHp,
                                Block = 0,
                                BossPhase = newPhase,
                                BossPendingRevival = false,
                                ExtraMultiClawCount = 0,
                                AiStateId = newPhase == 1 ? "claw" : "lacerate",
                                Powers = enemy.PowerStates.Where(power =>
                                    newPhase < 2
                                    || power.PowerId is not (
                                        PrototypeNativeGloryBosses.AdaptableId
                                        or PrototypeNativeGloryBosses.PainfulStabsId))
                                    .ToArray()
                            };
                            var newPower = newPhase == 1
                                ? PrototypeNativeGloryBosses.PainfulStabsId
                                : PrototypeNativeGloryBosses.NemesisId;
                            var applied = ApplyEnemyPowerToState(
                                combat, enemy, newPower, 1);
                            combat = applied.Combat;
                            enemy = applied.Enemy;
                            break;
                        }

                        case PrototypeEnemyEffectKind.IntensifyWither:
                        {
                            if (enemy.EnemyId != PrototypeNativeGloryBosses.AeonglassId)
                                throw new InvalidOperationException(
                                    "Only Aeonglass can intensify Wither.");
                            enemy = enemy with
                            {
                                WitherUpgradeCount = enemy.WitherUpgradeCount + 1
                            };
                            // Publish the updated owner before the next
                            // AddCardsToDiscard command constructs its Withers.
                            // Newly generated cards must inherit this move's
                            // new fake-upgrade level.
                            enemies[index] = enemy;
                            combat = combat with
                            {
                                Enemies = enemies.ToArray(),
                                Cards = combat.Cards.Select(card =>
                                    card.CardId == PrototypeNativeGloryBosses.WitherId
                                        ? card with
                                        {
                                            WitherFakeUpgradeLevel =
                                                card.WitherFakeUpgradeLevel + 1
                                        }
                                        : card).ToArray()
                            };
                            break;
                        }

                        case PrototypeEnemyEffectKind.AddCardsToHand:
                            if (effect.CardId is null)
                            {
                                throw new InvalidOperationException(
                                    "Enemy hand-card effect is missing a card ID.");
                            }

                            combat = AddGeneratedEnemyCardsToHand(
                                combat, effect.CardId, amount);
                            break;

                        case PrototypeEnemyEffectKind.AddCardsToDiscard:
                            if (effect.CardId is null)
                            {
                                throw new InvalidOperationException(
                                    "Enemy add-card effect is missing a card ID.");
                            }

                            combat = AddGeneratedCardsToDiscard(
                                combat,
                                effect.CardId,
                                amount);
                            break;

                        case PrototypeEnemyEffectKind.RequireNativeChoice:
                            if (enemy.EnemyId != PrototypeNativeHiveBosses.KnowledgeId
                                || move.Id != "curse_of_knowledge"
                                || combat.KnowledgeDemonChoiceResolvedTurn != combat.Turn)
                                throw new InvalidOperationException(
                                    "Knowledge Demon curse move was executed without its blocking player choice.");
                            // The power was applied by the player's earlier
                            // selected IChoosable option. Advance the
                            // original committed enemy move exactly once.
                            break;

                        case PrototypeEnemyEffectKind.AddCardsToRandomDiscard:
                        {
                            if (effect.CardId is null)
                                throw new InvalidOperationException(
                                    "Enemy discard insertion lacks a card ID.");
                            for (var generated = 0; generated < amount; generated++)
                            {
                                var instance = new CombatCardInstance(
                                    combat.NextCardInstanceId, null,
                                    effect.CardId, 0, true,
                                    PrototypeJson.EmptyObject());
                                var insertion = ResolveRandomPilePosition(
                                    combat, rng, combat.DiscardPile,
                                    effect.CardId, operationKind: null,
                                    sourceCardInstanceId: null,
                                    ordinal: randomDiscardInsertionOrdinal++,
                                    siteId:
                                        StochasticSiteRegistryV111.CombatEnemyDiscardRandomInsertSiteId,
                                    requestOwnerPrefix:
                                        "creature:" + enemyCombatId +
                                        ":move:" + move.Id +
                                        ":effect:" + effect.Kind,
                                    pileName: "discard");
                                combat = insertion.Combat;
                                combat = SemanticDrawPileAdapterV111
                                    .ApplyNewCardZoneInsertionAtSourceGap(
                                        combat,
                                        PrototypeCardZone.DiscardPile,
                                        insertion.SelectedIndex,
                                        insertion.SelectedGapIdentity,
                                        instance);
                            }
                            break;
                        }

                        case PrototypeEnemyEffectKind.AddCardsToRandomDraw:
                            if (effect.CardId is null)
                            {
                                throw new InvalidOperationException(
                                    "Enemy draw-card effect is missing card ID.");
                            }

                            for (var generated = 0; generated < amount; generated++)
                            {
                                var instance = new CombatCardInstance(
                                    combat.NextCardInstanceId,
                                    null, effect.CardId, 0, true,
                                    PrototypeJson.EmptyObject());
                                var insertion = ResolveRandomPilePosition(
                                    combat, rng, combat.DrawPile,
                                    effect.CardId, operationKind: null,
                                    sourceCardInstanceId: null,
                                    ordinal: randomDrawInsertionOrdinal++,
                                    siteId:
                                        StochasticSiteRegistryV111.CombatEnemyDrawRandomInsertSiteId,
                                    requestOwnerPrefix:
                                        "creature:" + enemyCombatId +
                                        ":move:" + move.Id +
                                        ":effect:" + effect.Kind);
                                combat = insertion.Combat;
                                combat = SemanticDrawPileAdapterV111
                                    .ApplyNewCardZoneInsertionAtSourceGap(
                                        combat, PrototypeCardZone.DrawPile,
                                        insertion.SelectedIndex,
                                        insertion.SelectedGapIdentity,
                                        instance);
                            }
                            break;

                        case PrototypeEnemyEffectKind.SummonEnemy:
                        {
                            var summonId = effect.EnemyId;
                            if (combat.Stochastic.NativeRng is not null
                                && effect.SummonEnemyPool is { Length: > 0 })
                                throw new NotSupportedException(
                                    "Native summon pools need audited source RNG selection.");
                            if (effect.SummonEnemyPool is { Length: > 0 } pool)
                            {
                                // Native Fabricator._lastSpawned persists
                                // through deaths. Retired minions stay in the
                                // typed combat history with their leader ID.
                                var lastSpawned = enemies
                                    .Where(candidate =>
                                        candidate.LeaderEnemyInstanceId ==
                                            enemy.InstanceId)
                                    .OrderByDescending(candidate =>
                                        candidate.InstanceId)
                                    .FirstOrDefault()?.EnemyId;
                                var choices = pool.Where(id =>
                                    id != lastSpawned).ToArray();
                                if (choices.Length == 0)
                                    choices = pool;
                                var selectedSummonIndex = ResolveLegacyRandomIndex(
                                    rng,
                                    StochasticSiteRegistryV111
                                        .CombatEnemySummonPoolSelectSiteId,
                                    "enemy:" + enemy.InstanceId
                                        + ":move:" + move.Id
                                        + ":pool:" + CanonicalJson.Sha256(choices),
                                    Enumerable.Range(0, choices.Length),
                                    out var selectedSummonLabel,
                                    controlledCandidateLabels: choices.Select(
                                        (enemyId, poolSlot) =>
                                            "enemy:" + enemyId + ":slot:" +
                                            poolSlot.ToString(
                                                System.Globalization.CultureInfo.InvariantCulture))
                                        .ToArray());
                                summonId = selectedSummonLabel is { } label
                                    ? ResolveSelectedSummonIdentity(
                                        label, choices)
                                    : choices[selectedSummonIndex];
                            }
                            if (summonId is null)
                                throw new InvalidOperationException(
                                    "Enemy summon requires an enemy ID or pool.");

                            var summonedDefinition =
                                PrototypeContent.Enemy(summonId);
                            var hpRange =
                                summonedDefinition.HpRangeAt(
                                    act,
                                    ascension);
                            var nextEnemyId =
                                enemies.Count == 0
                                    ? 1
                                    : enemies.Max(item =>
                                        item.InstanceId) + 1;
                            // The native creation path rolls from
                            // run.niche after selecting a legal summon
                            // slot. Do not consume prototype combat RNG.
                            var summonedHp =
                                combat.Stochastic.NativeRng is not null
                                    ? hpRange.Max
                                    : hpRange.Min == hpRange.Max
                                        ? hpRange.Min
                                    : ResolveLegacyRandomIndex(
                                        rng,
                                        NativeCombatRngEntryV111.InitialUniqueHpSiteId,
                                        "creature:" + nextEnemyId,
                                        Enumerable.Range(
                                            hpRange.Min,
                                            checked(hpRange.Max
                                                - hpRange.Min + 1)));
                            var nextPowerOrder =
                                combat.NextPowerApplicationOrder;
                            var powers =
                                (summonedDefinition.StartingPowers
                                    ?? Array.Empty<
                                        PrototypeStartingPowerSpec>())
                                .Select(power =>
                                {
                                    _ = PrototypeContent.Power(
                                        power.PowerId);
                                    return new
                                        PrototypePowerInstanceState(
                                            power.PowerId,
                                            power.StacksAt(ascension),
                                            nextPowerOrder++);
                                })
                                .Where(power =>
                                    power.Stacks > 0)
                                .ToArray();
                            var summonSlots = effect.SummonSlotNames;
                            string? selectedSlot = null;
                            if (summonSlots is { Length: > 0 })
                            {
                                // LivingFog.Bloat calls
                                // Encounter.GetNextSlot(): FIRST unoccupied
                                // source slot (bomb1 -> bomb5). The
                                // prototype's default last-slot preference
                                // remains intact for all other encounters.
                                var sourceFirst = combat.Stochastic.NativeRng?.Formation
                                        .NativeEncounterEntry == "LivingFogNormal"
                                    && enemy.EnemyId == "proto.enemy.living_fog"
                                    && summonId == "proto.enemy.gas_bomb";
                                selectedSlot = (sourceFirst
                                        || effect.SelectFirstAvailableSummonSlot
                                        ? summonSlots
                                        : summonSlots.Reverse())
                                    .FirstOrDefault(slot =>
                                        !enemies.Any(item =>
                                            (item.Hp > 0
                                                || effect.ReserveSummonSlotAfterDeath)
                                            && StringComparer.Ordinal.Equals(
                                                item.SlotName, slot)));
                                if (selectedSlot is null)
                                {
                                    break;
                                }
                            }

                            var nextFormationPosition =
                                selectedSlot is null
                                    ? (enemies.Count == 0
                                        ? 0
                                        : enemies.Max(item =>
                                            item.FormationPosition) + 1)
                                    : effect.SummonSlotFormationPositions is
                                            { } sourcePositions
                                        ? sourcePositions[
                                            Array.IndexOf(summonSlots!, selectedSlot)]
                                        : Array.IndexOf(summonSlots!, selectedSlot);

                            var spawnedEnemy =
                                new EnemyCombatState(
                                    InstanceId: nextEnemyId,
                                    EnemyId: summonId,
                                    Hp: NativeGloryAncientsV111.IsFurCoatCombat(
                                        player, combat.Act,
                                        combat.CurrentMapNodeId)
                                            ? 1 : summonedHp,
                                    MaxHp: summonedHp,
                                    Block: 0,
                                    MoveIndex: 0,
                                    Statuses:
                                        new Dictionary<string, int>(
                                            StringComparer.Ordinal),
                                    Powers: powers,
                                    FormationPosition:
                                        nextFormationPosition,
                                    SlotName: selectedSlot,
                                    LeaderEnemyInstanceId:
                                        summonedDefinition.IsMinion
                                            ? enemy.InstanceId
                                            : null,
                                    SkipNextEnemyAction: true,
                                    NonSummonMovesUntilEligible:
                                        summonedDefinition.NonSummonMovesBeforeEligible,
                                    SharedSummonsUsed:
                                        definition.MaxCoordinatedSummons > 0
                                            ? enemy.SharedSummonsUsed + 1
                                            : 0);
                            if (combat.Stochastic.NativeRng is not null)
                            {
                                combat = NativeCombatRngEntryV111.AppendSummonedEnemy(
                                    combat, enemies, spawnedEnemy);
                                enemies = combat.Enemies.ToList();
                            }
                            else
                            {
                                enemies.Add(spawnedEnemy);
                            }
                            if (definition.MaxCoordinatedSummons > 0)
                            {
                                // Coordinated summons share a global
                                // squad budget and reserve this turn.
                                var used = enemy.SharedSummonsUsed + 1;
                                enemy = enemy with
                                {
                                    SharedSummonsUsed = used,
                                    SharedSummonUsedThisTurn = true
                                };
                                for (var otherIndex = 0;
                                     otherIndex < enemies.Count;
                                     otherIndex++)
                                {
                                    if (StringComparer.Ordinal.Equals(
                                            enemies[otherIndex].EnemyId,
                                            enemy.EnemyId))
                                    {
                                        enemies[otherIndex] = enemies[otherIndex] with
                                        {
                                            SharedSummonsUsed = used,
                                            SharedSummonUsedThisTurn = true
                                        };
                                    }
                                }
                            }
                            combat = combat with
                            {
                                NextPowerApplicationOrder =
                                    nextPowerOrder,
                                Enemies = enemies
                                    .Select(item => item.Fork())
                                    .ToArray()
                            };
                            break;
                        }

                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (hp <= 0 || enemy.Hp <= 0)
                    {
                        break;
                    }
                }

                if (unblockedAttackHits > 0 && enemy.Hp > 0
                    && enemy.EnemyId == PrototypeNativeGloryBosses.TestSubjectId
                    && enemy.BossPhase == 1
                    && enemy.PowerStates.Any(power =>
                        power.PowerId == PrototypeNativeGloryBosses.PainfulStabsId))
                {
                    var wounds = unblockedAttackHits * enemy.PowerStates
                        .Where(power => power.PowerId
                            == PrototypeNativeGloryBosses.PainfulStabsId)
                        .Sum(power => power.Stacks);
                    combat = AddGeneratedCardsToDiscard(
                        combat, "proto.status.wound", wounds);
                }

                if (unblockedAttackHits > 0 && enemy.Hp > 0)
                {
                    // The native Suck hook fires after a powered attack
                    // command and counts hits which dealt unblocked HP
                    // damage, not blocked hits or non-attack HP loss.
                    var strengthGain = enemy.PowerStates.Sum(power =>
                        power.Stacks
                        * PrototypeContent.Power(power.PowerId)
                            .StrengthPerUnblockedAttackHitPerStack)
                        * unblockedAttackHits;
                    if (strengthGain > 0)
                    {
                        var strengthened = ApplyEnemyPowerToState(
                            combat, enemy, "proto.power.strength",
                            strengthGain);
                        combat = strengthened.Combat;
                        enemy = strengthened.Enemy;
                    }
                }

                if (hp <= 0 || enemy.Hp <= 0)
                {
                    break;
                }
            }

            if (consumeOnAttackOrders.Count > 0)
            {
                enemy = enemy with
                {
                    Powers = enemy.PowerStates
                        .Where(power => !consumeOnAttackOrders.Contains(
                            power.ApplicationOrder))
                        .ToArray()
                };
            }

            var turnEndStrength =
                enemy.PowerStates.Sum(power =>
                    PrototypeContent.Power(power.PowerId)
                        .EnemyStrengthGainAtTurnEndPerStack
                    * power.Stacks);
            if (turnEndStrength != 0)
            {
                var strengthened = ApplyEnemyPowerToState(
                    combat,
                    enemy,
                    "proto.power.strength",
                    turnEndStrength);
                combat = strengthened.Combat;
                enemy = strengthened.Enemy;
            }

            if (enemy.EnemyId == PrototypeNativeGloryBosses.TestSubjectId
                && enemy.BossPhase >= 2
                && enemy.PowerStates.Any(power => power.PowerId
                    == PrototypeNativeGloryBosses.NemesisId))
            {
                var makeIntangible = !enemy.NemesisIntangibleNext;
                enemy = enemy with
                {
                    Powers = enemy.PowerStates.Where(power =>
                        power.PowerId != PrototypeNativeGloryBosses.NemesisIntangibleId)
                        .ToArray(),
                    NemesisIntangibleNext = makeIntangible
                };
                if (makeIntangible)
                {
                    var applied = ApplyEnemyPowerToState(
                        combat, enemy,
                        PrototypeNativeGloryBosses.NemesisIntangibleId, 1);
                    combat = applied.Combat;
                    enemy = applied.Enemy;
                }
            }

            var consecutiveUses =
                StringComparer.Ordinal.Equals(
                    enemy.LastMoveId,
                    move.Id)
                    ? enemy.ConsecutiveMoveUses + 1
                    : 1;
            var moveUseCounts =
                enemy.MoveUseCounts is null
                    ? new Dictionary<string, int>(
                        StringComparer.Ordinal)
                    : new Dictionary<string, int>(
                        enemy.MoveUseCounts,
                        StringComparer.Ordinal);
            moveUseCounts[move.Id] =
                moveUseCounts.GetValueOrDefault(
                    move.Id) + 1;

            if (enemy.EnemyId == PrototypeNativeGloryBosses.TestSubjectId
                && move.Id == "multi_claw")
                enemy = enemy with
                {
                    ExtraMultiClawCount = enemy.ExtraMultiClawCount + 1
                };
            enemy = InterceptEnemyLethalDeath(enemy);
            enemies[index] = enemy with
            {
                MoveIndex = enemy.MoveIndex + 1,
                NonSummonMovesUntilEligible = move.Effects.Any(effect =>
                    effect.Kind == PrototypeEnemyEffectKind.SummonEnemy)
                    ? enemy.NonSummonMovesUntilEligible
                    : Math.Max(0, enemy.NonSummonMovesUntilEligible - 1),
                LastMoveId = move.Id,
                ConsecutiveMoveUses = consecutiveUses,
                AiStateId = enemy.EnemyId == PrototypeNativeGloryBosses.TestSubjectId
                    && move.Id == "respawn" && enemy.BossPhase >= 2
                        ? "lacerate" : selection.NextAiStateId,
                MoveUseCounts = moveUseCounts,
                PlannedMoveIndex = null,
                PlannedNextAiStateId = null
            };
            if (hp <= 0)
            {
                break;
            }
        }

        combat = combat with
        {
            PlayerBlock = block,
            Enemies = enemies.ToArray()
        };
        combat = CleanupSourceBoundPowersForDefeatedEnemies(
            combat);
        combat = ResolveEnemyDeathSummons(
            combat,
            rng);

        return (
            player with { Hp = hp, Gold = gold },
            combat);
    }

    internal static CombatState ResolveEnemyDeathSummons(
        CombatState combat,
        RngBundle rng,
        int enemyActionSkips = 1)
    {
        var enemies = combat.Enemies
            .Select(enemy => enemy.Fork())
            .ToList();
        var nextPowerOrder =
            combat.NextPowerApplicationOrder;
        var changed = false;

        for (var index = 0;
             index < enemies.Count;
             index++)
        {
            var source = enemies[index];
            if (source.Hp > 0
                || source.DeathEffectsResolved)
            {
                continue;
            }

            var sourceDefinition =
                PrototypeContent.Enemy(source.EnemyId);
            var summons =
                sourceDefinition.DeathSummons
                ?? Array.Empty<
                    PrototypeEnemyDeathSummonSpec>();
            if (summons.Length == 0)
            {
                continue;
            }

            enemies[index] = source with
            {
                DeathEffectsResolved = true
            };
            changed = true;

            // SurprisePower.AfterDeath first CreateCreature(FatGremlin)
            // (assigning native CombatId and niche HP), then Add<SneakyGremlin>
            // (new CombatId, new niche roll), then Add(precreated Fat).
            // The legacy emulator lists Sneaky first: reorder only the
            // opt-in native creation calls, not legacy behavior.
            var nativeMerc = combat.Stochastic.NativeRng?.Formation.NativeEncounterEntry
                    == "GremlinMercNormal"
                && source.EnemyId == "proto.enemy.gremlin_merc";
            var orderedSummons = nativeMerc
                ? summons.Reverse().ToArray()
                : summons;
            var nativePhrog =
                combat.Stochastic.NativeRng?.Formation.NativeEncounterEntry
                    == "PhrogParasiteElite"
                && source.EnemyId == "proto.enemy.phrog_parasite";
            if (nativePhrog
                && (enemyActionSkips != 1
                    || summons.Length != 4
                    || summons.Select((summon, i) =>
                        summon.EnemyId == "proto.enemy.wriggler"
                        && summon.SlotName == $"wriggler{i + 1}")
                        .Any(matches => !matches)))
                throw new NotSupportedException(
                    "Native InfestedPower currently supports the audited " +
                    "player-turn death phase only; four fixed Wriggler " +
                    "slots with SPAWNED_MOVE are required.");
            if (nativeMerc
                && (summons.Length != 2
                    || summons[0].EnemyId != "proto.enemy.sneaky_gremlin"
                    || summons[1].EnemyId != "proto.enemy.fat_gremlin"))
                throw new NotSupportedException(
                    "Pinned native SurprisePower death summons have changed.");

            int? precreatedFatId = null;
            foreach (var summon in orderedSummons)
            {
                var definition =
                    PrototypeContent.Enemy(
                        summon.EnemyId);
                var hpRange = definition.HpRangeAt(
                    combat.Act,
                    combat.Ascension);
                var nextEnemyId =
                    enemies.Count == 0
                        ? 1
                        : enemies.Max(enemy =>
                            enemy.InstanceId) + 1;
                var hp =
                    combat.Stochastic.NativeRng is not null
                        ? hpRange.Max
                        : hpRange.Min == hpRange.Max
                            ? hpRange.Min
                            : ResolveLegacyRandomIndex(
                                rng,
                                NativeCombatRngEntryV111.InitialUniqueHpSiteId,
                                "creature:" + nextEnemyId,
                                Enumerable.Range(
                                    hpRange.Min,
                                    checked(hpRange.Max - hpRange.Min + 1)));
                var powers =
                    (definition.StartingPowers
                        ?? Array.Empty<
                            PrototypeStartingPowerSpec>())
                    .Select(power =>
                    {
                        _ = PrototypeContent.Power(
                            power.PowerId);
                        return new
                            PrototypePowerInstanceState(
                                power.PowerId,
                                power.StacksAt(combat.Ascension),
                                nextPowerOrder++);
                    })
                    .Where(power =>
                        power.Stacks > 0)
                    .ToArray();
                var spawnedEnemy =
                    new EnemyCombatState(
                        InstanceId: nextEnemyId,
                        EnemyId: summon.EnemyId,
                        Hp: hp,
                        MaxHp: hp,
                        Block: 0,
                        MoveIndex: 0,
                        Statuses:
                            new Dictionary<string, int>(
                                StringComparer.Ordinal),
                        Powers: summon.TransferStolenGold
                            && source.StolenGold > 0
                            ? powers.Append(new PrototypePowerInstanceState(
                                "proto.power.heist",
                                source.StolenGold,
                                nextPowerOrder++)).ToArray()
                            : powers,
                        FormationPosition:
                            summon.FormationPosition,
                        SlotName: summon.SlotName,
                        StolenGold: summon.TransferStolenGold
                            ? source.StolenGold : 0,
                        // Source InfestedPower sets StartStunned before
                        // CreatureCmd.Add. The Wriggler therefore has
                        // a genuine visible SPAWNED_MOVE StunIntent,
                        // not the prototype's silent skipped action.
                        AiStateId: nativePhrog ? "spawned_move" : null,
                        PlannedMoveIndex: nativePhrog ? 2 : null,
                        PlannedNextAiStateId: nativePhrog ? "init" : null,
                        EnemyActionSkipsRemaining: nativePhrog ? 0 :
                            summon.SkipEnemyActions == 1
                                ? Math.Max(0, enemyActionSkips)
                                : Math.Max(0, summon.SkipEnemyActions));
                if (combat.Stochastic.NativeRng is not null)
                {
                    combat = NativeCombatRngEntryV111.AppendSummonedEnemy(
                        combat, enemies, spawnedEnemy,
                        nativeMerc
                            && summon.EnemyId == "proto.enemy.sneaky_gremlin"
                            ? precreatedFatId
                            : null,
                        dyingStillAttachedId: source.InstanceId);
                    enemies = combat.Enemies.ToList();
                    if (nativeMerc
                        && summon.EnemyId == "proto.enemy.fat_gremlin")
                        precreatedFatId = spawnedEnemy.InstanceId;
                }
                else
                {
                    enemies.Add(spawnedEnemy);
                }
            }

            if (nativeMerc)
            {
                combat = NativeCombatRngEntryV111.SortGremlinMercDeathPair(
                    combat with { Enemies = enemies.ToArray() });
                enemies = combat.Enemies.ToList();
            }
        }

        return changed
            ? combat with
            {
                Enemies = enemies
                    .Select(enemy => enemy.Fork())
                    .ToArray(),
                NextPowerApplicationOrder =
                    nextPowerOrder
            }
            : combat;
    }

    private static void EnqueueEffectOperations(
        Queue<PrototypeQueuedOperation> operations,
        PrototypeCombatEffectSpec effect,
        int upgradeLevel,
        int energySpent,
        int? actionTargetEnemyId,
        CombatState combat,
        int powerStacks = 0,
        PrototypeEffectSourceKind sourceKind = PrototypeEffectSourceKind.System,
        bool isPoweredAttack = false,
        PrototypeCombatCardSnapshot? powerCardPayload = null,
        int? sourcePowerEnemyId = null,
        int sourcePowerStoredValue = 0,
        long? sourcePowerApplicationOrder = null)
    {
        // Snapshot hand-sensitive card conditions when their operations are
        // queued. Restlessness first draws cards and then gains energy, but
        // both effects depend on the hand at the moment the card is played.
        if (effect.Condition?.Kind
                == PrototypeCombatPredicateKind.HandEmptyAtEnqueue
            && combat.Hand.Length != 0)
        {
            return;
        }

        var queuedCondition = effect.Condition?.Kind
                == PrototypeCombatPredicateKind.HandEmptyAtEnqueue
            ? null
            : effect.Condition;
        var count = effect.CountKind is null
            or PrototypeCombatCountKind.TargetDebuffs
            ? 0
            : ResolveCombatCount(effect.CountKind.Value, combat);

        var repetitions = effect.RepetitionsAt(upgradeLevel, energySpent)
            + (effect.RepetitionsPerPowerStack * powerStacks)
            + (effect.RepetitionsPerCount * count);
        if (repetitions <= 0)
        {
            return;
        }

        var targets = effect.Target switch
        {
            PrototypeEffectTarget.ActionTargetEnemy => new int?[] { actionTargetEnemyId },
            PrototypeEffectTarget.AllEnemies => combat.Enemies
                .Where(enemy => enemy.Hp > 0)
                .Select(enemy => (int?)enemy.InstanceId)
                .ToArray(),
            PrototypeEffectTarget.RandomEnemy => new int?[] { null },
            PrototypeEffectTarget.SourcePowerOwnerEnemy =>
                sourcePowerEnemyId is null
                    ? throw new InvalidOperationException(
                        "Source-power-owner targeting requires an enemy power source.")
                    : new int?[] { sourcePowerEnemyId.Value },
            _ => throw new ArgumentOutOfRangeException()
        };

        if (targets.Length == 0)
        {
            if (effect.Target == PrototypeEffectTarget.AllEnemies)
            {
                return;
            }

            targets = new int?[] { null };
        }

        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            foreach (var targetEnemyId in targets)
            {
                operations.Enqueue(new PrototypeQueuedOperation(
                    effect.Kind,
                    (effect.UseSourcePowerStoredValue
                        ? sourcePowerStoredValue
                        : effect.AmountAt(upgradeLevel, energySpent))
                        + (effect.AmountPerPowerStack * powerStacks)
                        + ((effect.AmountPerCount
                            + (effect.AmountPerCountUpgradeDelta * upgradeLevel))
                            * (effect.CountKind == PrototypeCombatCountKind.TargetDebuffs
                                ? ResolveCombatCount(
                                    PrototypeCombatCountKind.TargetDebuffs,
                                    combat, targetEnemyId)
                                : count)),
                    targetEnemyId,
                    effect.StatusId,
                    effect.Selection is null
                        ? null
                        : effect.Selection with
                        {
                            MinSelections =
                                effect.Selection.MinSelections
                                + (effect.Selection.SelectionsPerPowerStack
                                    * powerStacks),
                            MaxSelections =
                                effect.Selection.MaxSelections
                                + (effect.Selection.SelectionsPerPowerStack
                                    * powerStacks)
                                + (effect.Selection.MaxSelectionsUpgradeDelta
                                    * upgradeLevel),
                            CopiesPerSelection = effect.Selection.CopiesPerSelection
                                + effect.Selection.CopiesPerSelectionUpgradeDelta * upgradeLevel
                        },
                    effect.CardId,
                    effect.PowerId,
                    effect.GeneratedCardUpgradeLevel
                        + (effect.GeneratedCardUpgradePerSourceUpgrade * upgradeLevel),
                    TargetMode: effect.Target,
                    SourceKind: sourceKind,
                    IsPoweredAttack: isPoweredAttack
                        && effect.Kind is
                            PrototypeCombatEffectKind.DamageEnemy
                            or PrototypeCombatEffectKind.DamageAllEnemiesRepeatPerKill,
                    Condition: queuedCondition,
                    SelectedCardPower: effect.SelectedCardPower is null
                        ? null
                        : new PrototypeSelectedCardPowerAction(
                            effect.SelectedCardPower.PowerId,
                            effect.SelectedCardPower.AmountAt(upgradeLevel),
                            effect.SelectedCardPower
                                .ClearAfflictionFromPayload,
                            effect.SelectedCardPower
                                .RestoreSuppressedUpgradesInPayload),
                    SelectedCardKeyword: effect.SelectedCardKeyword,
                    DrawnCardKeyword: effect.DrawnCardKeyword,
                    EventSourceCardKeyword: effect.EventSourceCardKeyword,
                    PowerCardPayload: powerCardPayload?.Fork(),
                    GeneratedCardEnchantment:
                        effect.GeneratedCardEnchantment is null
                            ? null
                            : effect.GeneratedCardEnchantment with { },
                    AutoPlaySourceZone:
                        effect.AutoPlaySourceZone,
                    RequiredCardTag:
                        effect.RequiredCardTag,
                    UpgradeAutoPlayedCardsBeforePlay:
                        effect.UpgradeAutoPlayedCardsOnSourceUpgrade
                        && upgradeLevel > 0,
                    ExtraCardRewardsOnFatal:
                        effect.ExtraCardRewardsOnFatal,
                    GoldOnFatal:
                        effect.GoldOnFatal
                        + (effect.GoldOnFatalUpgradeDelta * upgradeLevel),
                    GainBlockEqualToAttackDamage:
                        effect.GainBlockEqualToAttackDamage,
                    SplashUnpoweredAttackToOtherEnemies:
                        effect.SplashUnpoweredAttackToOtherEnemies,
                    AutoPlayRequiredCardType:
                        effect.AutoPlayRequiredCardType,
                    AutoPlayFallbackToUnplayable:
                        effect.AutoPlayFallbackToUnplayable,
                    SourcePowerApplicationOrder:
                        sourcePowerApplicationOrder,
                    PlayerGoldCost: effect.PlayerGoldCost,
                    GeneratedCharacterCardsFreeThisTurn:
                        effect.GeneratedCharacterCardsFreeThisTurn,
                    PowerStoredValue:
                        effect.PowerStoredValue
                        + (effect.PowerStoredValueUpgradeDelta * upgradeLevel),
                    PlayerPowerOnFatalId:
                        effect.PlayerPowerOnFatalId,
                    PlayerPowerOnFatalAmount:
                        effect.PlayerPowerOnFatalAmount,
                    GeneratedChoiceCardType:
                        effect.GeneratedChoiceCardType,
                    GeneratedChoiceCardsFreeThisTurn:
                        effect.GeneratedChoiceCardsFreeThisTurn,
                    GeneratedChoiceCardsUpgraded:
                        effect.GeneratedChoiceCardsUpgraded
                        || (effect.GeneratedChoiceCardsUpgradeWithSource
                            && upgradeLevel > 0),
                    GeneratedChoiceMustPick:
                        effect.GeneratedChoiceMustPick,
                    GeneratedChoiceCharacterCards:
                        effect.GeneratedChoiceCharacterCards,
                    GeneratedChoiceCardsRetain:
                        effect.GeneratedChoiceCardsRetain,
                    GeneratedChoiceUsesNativeCardGenerationRng:
                        effect.GeneratedChoiceUsesNativeCardGenerationRng,
                    SelectedCardTemporaryCost:
                        effect.SelectedCardTemporaryCost is null
                            ? null
                            : effect.SelectedCardTemporaryCost with { }));
            }
        }
    }

    private static (PlayerState Player, CombatState Combat) ResolveOperations(
        PlayerState player,
        CombatState combat,
        Queue<PrototypeQueuedOperation> operations,
        RngBundle rng,
        long? sourceCardInstanceId = null,
        PrototypeCardZone sourceCardDestination = PrototypeCardZone.DiscardPile,
        PrototypeCombatEvent[]? completionEvents = null,
        int eventDepth = 0,
        PrototypeCardPlaySeriesState? cardPlaySeries = null,
        bool moveSourceCardOnCompletion = true,
        bool removeSourceCardOnCompletion = false,
        bool sourceCardAlreadyMoved = false,
        long? eventSourceCardInstanceId = null,
        PrototypeEventDispatchContinuationState? eventDispatchContinuation = null)
    {
        RequireSemanticNativeStreamOwner(
            combat, "combat.resolve_operations");
        var randomTargetOrdinal = 0;
        var autoPlayTargetOrdinal = 0;
        var generatedCardOrdinal = 0;
        var drawInsertionOrdinal = 0;
        var effectContinuationContext = new EffectContinuationContext(
            SourceCardInstanceId: sourceCardInstanceId,
            SourceCardDestination: sourceCardDestination,
            CompletionEvents: completionEvents,
            CardPlaySeries: cardPlaySeries,
            MoveSourceCardOnCompletion: moveSourceCardOnCompletion,
            RemoveSourceCardOnCompletion: removeSourceCardOnCompletion,
            SourceCardAlreadyMoved: sourceCardAlreadyMoved,
            EventDispatchContinuation: eventDispatchContinuation);

        while (operations.Count > 0)
        {
            var operation = operations.Dequeue();
            var targetEnemyId = operation.TargetEnemyId;

            if (operation.TargetMode == PrototypeEffectTarget.RandomEnemy)
            {
                var liveEnemies = combat.Enemies
                    .Where(enemy => enemy.Hp > 0)
                    .Select(enemy => enemy.InstanceId)
                    .ToArray();

                if (liveEnemies.Length == 0)
                {
                    continue;
                }

                if (ControlledChanceGuardV111.Active)
                {
                    var suspended = new PrototypeChoiceResolutionContinuationState(
                        SourceCardInstanceId: sourceCardInstanceId,
                        SourceCardDestination: sourceCardDestination,
                        Operations: (new[]
                        {
                            operation with
                            {
                                TargetEnemyId = null,
                                TargetMode = PrototypeEffectTarget.ActionTargetEnemy
                            }
                        }).Concat(operations).ToArray(),
                        PendingDiscardEvents: [],
                        PendingSlyCardInstanceIds: [],
                        CompletionEvents: completionEvents ?? [],
                        CardPlaySeries: cardPlaySeries,
                        MoveSourceCardOnCompletion: moveSourceCardOnCompletion,
                        RemoveSourceCardOnCompletion: removeSourceCardOnCompletion,
                        SourceCardAlreadyMoved: sourceCardAlreadyMoved,
                        EventDispatchContinuation: eventDispatchContinuation?.Fork());
                    return (player, MakeControlledRandomTargetChoice(
                        combat, "effect_random_enemy", liveEnemies,
                        suspended));
                }
                var target = ResolveRandomEnemyTarget(
                    combat, rng, liveEnemies,
                    StochasticSiteRegistryV111.CombatEffectRandomTargetSiteId,
                    "combat_targets",
                    "effect_operation",
                    randomTargetOrdinal++);
                combat = target.Combat;
                targetEnemyId = liveEnemies[target.SelectedIndex];
            }

            if (!EvaluateCombatPredicate(
                    operation.Condition,
                    combat,
                    targetEnemyId))
            {
                continue;
            }

            if (ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
                operation.Kind ==
                    PrototypeCombatEffectKind.AcquireRandomCombatPotion &&
                operation.Amount > 0)
            {
                var continuation = new PrototypeChoiceResolutionContinuationState(
                    SourceCardInstanceId: sourceCardInstanceId,
                    SourceCardDestination: sourceCardDestination,
                    Operations: operations.ToArray(),
                    PendingDiscardEvents: [],
                    PendingSlyCardInstanceIds: [],
                    CompletionEvents: completionEvents ?? [],
                    CardPlaySeries: cardPlaySeries,
                    MoveSourceCardOnCompletion: moveSourceCardOnCompletion,
                    RemoveSourceCardOnCompletion: removeSourceCardOnCompletion,
                    SourceCardAlreadyMoved: sourceCardAlreadyMoved,
                    EventDispatchContinuation: eventDispatchContinuation?.Fork());
                return (player, MakeControlledPotionGenerationChoice(
                    combat, operation, continuation));
            }

            // Source TakeRandom/GetDistinctForCombat shuffles its entire
            // eligible pool, including entries beyond the eventual prefix.
            // Stop before the first swap, preserving the preceding hooks.
            if (ControlledChanceGuardV111.Active)
            {
                var continuation = new PrototypeChoiceResolutionContinuationState(
                    SourceCardInstanceId: sourceCardInstanceId,
                    SourceCardDestination: sourceCardDestination,
                    Operations: operations.ToArray(),
                    PendingDiscardEvents: [],
                    PendingSlyCardInstanceIds: [],
                    CompletionEvents: completionEvents ?? [],
                    CardPlaySeries: cardPlaySeries,
                    MoveSourceCardOnCompletion: moveSourceCardOnCompletion,
                    RemoveSourceCardOnCompletion: removeSourceCardOnCompletion,
                    SourceCardAlreadyMoved: sourceCardAlreadyMoved,
                    EventDispatchContinuation: eventDispatchContinuation?.Fork());
                if (TryMakeControlledPoolShuffleChoice(
                    combat, operation, sourceCardInstanceId,
                    continuation, out var suspended))
                    return (player, suspended);
            }

            // Draw-pile insertion has two Nature-owned stages when the card
            // itself is random: choose its source model, then its insertion
            // position. Fixed-card insertion needs only the position stage.
            if (ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
                operation.Amount > 0 &&
                operation.Kind is (
                    PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions
                    or PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile))
            {
                var tail = (operation.Amount > 1
                        ? new[] { operation with { Amount = operation.Amount - 1 } }
                        : Array.Empty<PrototypeQueuedOperation>())
                    .Concat(operations).ToArray();
                var continuation = new PrototypeChoiceResolutionContinuationState(
                    SourceCardInstanceId: sourceCardInstanceId,
                    SourceCardDestination: sourceCardDestination,
                    Operations: tail,
                    PendingDiscardEvents: [],
                    PendingSlyCardInstanceIds: [],
                    CompletionEvents: completionEvents ?? [],
                    CardPlaySeries: cardPlaySeries,
                    MoveSourceCardOnCompletion: moveSourceCardOnCompletion,
                    RemoveSourceCardOnCompletion: removeSourceCardOnCompletion,
                    SourceCardAlreadyMoved: sourceCardAlreadyMoved,
                    EventDispatchContinuation: eventDispatchContinuation?.Fork());
                return (player, MakeControlledDrawInsertionChoice(
                    combat, operation, continuation));
            }

            // Suspend before the first random hand-card selection or cost roll.
            // The saved tail and parent continuations resume exactly once.
            if (ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
                ControlledRandomCostCandidates(combat, operation) is
                    { Length: > 0 } eligibleCosts)
            {
                var continuation = new PrototypeChoiceResolutionContinuationState(
                    SourceCardInstanceId: sourceCardInstanceId,
                    SourceCardDestination: sourceCardDestination,
                    Operations: operations.ToArray(),
                    PendingDiscardEvents: [],
                    PendingSlyCardInstanceIds: [],
                    CompletionEvents: completionEvents ?? [],
                    CardPlaySeries: cardPlaySeries,
                    MoveSourceCardOnCompletion: moveSourceCardOnCompletion,
                    RemoveSourceCardOnCompletion: removeSourceCardOnCompletion,
                    SourceCardAlreadyMoved: sourceCardAlreadyMoved,
                    EventDispatchContinuation: eventDispatchContinuation?.Fork());
                return (player, MakeControlledRandomCostChoice(
                    combat, operation, eligibleCosts, continuation));
            }

            // The first truly random generated card in this operation is
            // an in-flight Nature boundary. Earlier queue operations and
            // combat hooks are already reflected in `combat`; only the
            // remaining operations are saved for later continuation.
            if (ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
                operation.Amount > 0 &&
                GeneratedCardModelPool(operation) is { } generationPool)
            {
                if (generationPool.Length == 0)
                    throw new NotSupportedException(
                        "UNSUPPORTED_CONTROLLED_CHANCE site=combat.generated_card " +
                        "operation=choose_index empty_eligible_pool");
                var tail = (operation.Amount > 1
                        ? new[] { operation with { Amount = operation.Amount - 1 } }
                        : Array.Empty<PrototypeQueuedOperation>())
                    .Concat(operations).ToArray();
                var continuation = new PrototypeChoiceResolutionContinuationState(
                    SourceCardInstanceId: sourceCardInstanceId,
                    SourceCardDestination: sourceCardDestination,
                    Operations: tail,
                    PendingDiscardEvents: [],
                    PendingSlyCardInstanceIds: [],
                    CompletionEvents: completionEvents ?? [],
                    CardPlaySeries: cardPlaySeries,
                    MoveSourceCardOnCompletion: moveSourceCardOnCompletion,
                    RemoveSourceCardOnCompletion: removeSourceCardOnCompletion,
                    SourceCardAlreadyMoved: sourceCardAlreadyMoved,
                    EventDispatchContinuation: eventDispatchContinuation?.Fork());
                return (player, MakeControlledGeneratedCardChoice(
                    combat, operation, generationPool, continuation));
            }

            if (ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
                operation.Kind == PrototypeCombatEffectKind.ChooseCards &&
                ControlledCardShortlistCandidates(combat, operation) is
                    { } shortlistCandidates)
            {
                var continuation = new PrototypeChoiceResolutionContinuationState(
                    SourceCardInstanceId: sourceCardInstanceId,
                    SourceCardDestination: sourceCardDestination,
                    Operations: operations.ToArray(),
                    PendingDiscardEvents: [],
                    PendingSlyCardInstanceIds: [],
                    CompletionEvents: completionEvents ?? [],
                    CardPlaySeries: cardPlaySeries,
                    MoveSourceCardOnCompletion: moveSourceCardOnCompletion,
                    RemoveSourceCardOnCompletion: removeSourceCardOnCompletion,
                    SourceCardAlreadyMoved: sourceCardAlreadyMoved,
                    EventDispatchContinuation: eventDispatchContinuation?.Fork());
                if (TryMakeControlledCardShortlistChoice(
                    combat, operation, shortlistCandidates,
                    continuation, out var suspended))
                    return (player, suspended);
            }

            NativeEngineEventJournalV111.Record(
                "effect_operation", operation.Kind.ToString(),
                sourceCardInstanceId: sourceCardInstanceId,
                targetEnemyInstanceId: targetEnemyId,
                amount: operation.Amount);
            switch (operation.Kind)
            {
                case PrototypeCombatEffectKind.DamageEnemy:
                    if (targetEnemyId is null)
                    {
                        throw new InvalidOperationException("Damage operation requires an enemy target.");
                    }

                    var fatalEligible =
                        (operation.ExtraCardRewardsOnFatal > 0
                            || operation.GoldOnFatal > 0
                            || (operation.PlayerPowerOnFatalId is not null
                                && operation.PlayerPowerOnFatalAmount != 0))
                        && ShouldEnemyDeathTriggerFatal(
                            combat,
                            targetEnemyId.Value);

                    if (operation.IsPoweredAttack && player.Hp <= 0)
                    {
                        break;
                    }

                    if (operation.IsPoweredAttack)
                    {
                        var enemyBeforeHit = combat.Enemies.SingleOrDefault(
                            enemy => enemy.InstanceId == targetEnemyId.Value)
                            ?? throw new InvalidOperationException(
                                $"Enemy {targetEnemyId.Value} is missing.");

                        if (enemyBeforeHit.Hp > 0)
                        {
                            var retaliation = EnemyAttackRetaliation(
                                combat,
                                targetEnemyId.Value);
                            if (retaliation > 0)
                            {
                                var absorbed = Math.Min(
                                    combat.PlayerBlock,
                                    retaliation);
                                combat = combat with
                                {
                                    PlayerBlock = combat.PlayerBlock - absorbed
                                };
                                var loss = ApplyPlayerHpLoss(
                                    player,
                                    combat,
                                    retaliation - absorbed);
                                player = loss.Player;
                                combat = loss.Combat;
                                if (player.Hp <= 0)
                                {
                                    var prevented =
                                        TryUseAutomaticDeathPrevention(
                                            player,
                                            combat,
                                            rng,
                                            eventDepth);
                                    player = prevented.Player;
                                    combat = prevented.Combat;
                                }
                            }
                        }
                    }

                    var damageAmount = operation.Amount;
                    if (operation.IsPoweredAttack)
                    {
                        damageAmount = ModifyPlayerAttackDamage(
                            combat,
                            sourceCardInstanceId,
                            targetEnemyId.Value,
                            damageAmount);
                        damageAmount = ModifyIncomingAttackDamage(
                            combat,
                            targetEnemyId.Value,
                            damageAmount);
                    }

                    var splashTarget = combat.Enemies.FirstOrDefault(e =>
                        e.InstanceId == targetEnemyId.Value);
                    var overkill = splashTarget is null ? 0
                        : Math.Max(0, Math.Max(0, damageAmount - splashTarget.Block)
                            - splashTarget.Hp);
                    var damageResult = DamageEnemyInternal(
                        combat,
                        targetEnemyId.Value,
                        damageAmount,
                        operation.IsPoweredAttack
                            ? MinimumPoweredAttackHpLoss(player) : 0,
                        operation.IsPoweredAttack);
                    combat = damageResult.Combat;
                    NativeEngineEventJournalV111.Record(
                        "effect_result", "enemy_damage",
                        sourceCardInstanceId: sourceCardInstanceId,
                        targetEnemyInstanceId: targetEnemyId.Value,
                        amount: damageResult.DamageDealt);
                    // PersonalHivePower.AfterDamageReceived fires on each
                    // powered hit, including a fully blocked hit. Every
                    // stack adds a generated Dazed into a random draw slot.
                    // It is a target-side reaction, not a card-play event.
                    var personalHiveStacks = 0;
                    if (operation.IsPoweredAttack && splashTarget is { Hp: > 0 })
                    {
                        personalHiveStacks = splashTarget.PowerStates
                            .Where(power => power.PowerId ==
                                PrototypeNativeHiveElites.PersonalHiveId)
                            .Sum(power => power.Stacks);
                        if (personalHiveStacks > 0 &&
                            ControlledChanceGuardV111.Active &&
                            !ControlledChanceGuardV111
                                .EngineOwnedNativeProviderActive)
                        {
                            // The hit and its earlier hooks are already
                            // committed. Suspend before the first Dazed
                            // insertion and retain only the remaining source
                            // operation queue plus this damage-stage frame;
                            // all post-hit triggers resume after insertion.
                            if (combat.Stochastic.SemanticDeck is
                                { Mode: SemanticDeckStateV111.NatureMode })
                            {
                                for (var position = 0;
                                     position <= combat.DrawPile.Length;
                                     position++)
                                    _ = SemanticDrawPileAdapterV111.SynchronizeSemanticDrawPileAfterInsertion(
                                        combat, position,
                                        combat.NextCardInstanceId);
                            }
                            var continuation =
                                new PrototypeChoiceResolutionContinuationState(
                                    SourceCardInstanceId: sourceCardInstanceId,
                                    SourceCardDestination: sourceCardDestination,
                                    Operations: operations.ToArray(),
                                    PendingDiscardEvents: [],
                                    PendingSlyCardInstanceIds: [],
                                    CompletionEvents: completionEvents ?? [],
                                    CardPlaySeries: cardPlaySeries,
                                    MoveSourceCardOnCompletion:
                                        moveSourceCardOnCompletion,
                                    RemoveSourceCardOnCompletion:
                                        removeSourceCardOnCompletion,
                                    SourceCardAlreadyMoved:
                                        sourceCardAlreadyMoved,
                                    EventDispatchContinuation:
                                        eventDispatchContinuation?.Fork());
                            var insertionOperation = new
                                PrototypeQueuedOperation(
                                    PrototypeCombatEffectKind
                                        .CreateGeneratedCardsAtRandomDrawPositions,
                                    personalHiveStacks,
                                    CardId: "proto.status.dazed");
                            var savedDamageContinuation =
                                new PrototypePersonalHiveDamageContinuationV111(
                                    operation with
                                    {
                                        TargetEnemyId = targetEnemyId.Value,
                                        TargetMode =
                                            PrototypeEffectTarget.ActionTargetEnemy
                                    },
                                    targetEnemyId.Value,
                                    splashTarget,
                                    damageAmount,
                                    overkill,
                                    damageResult.DamageDealt,
                                    damageResult.Defeated,
                                    fatalEligible,
                                    personalHiveStacks,
                                    eventDepth);
                            var ownerId = "enemy:" +
                                splashTarget.InstanceId +
                                ":power:personal_hive:source:" +
                                (sourceCardInstanceId?.ToString() ?? "none");
                            return (player, MakeControlledDrawInsertionChoice(
                                combat, insertionOperation, continuation,
                                requestSiteId:
                                    PrototypeControlledDrawInsertionV111
                                        .PersonalHiveDazedSiteId,
                                requestOwnerId: ownerId,
                                personalHiveDamageContinuation:
                                    savedDamageContinuation));
                        }
                        for (var n = 0; n < personalHiveStacks; n++)
                        {
                            var instance = new CombatCardInstance(
                                combat.NextCardInstanceId, null,
                                "proto.status.dazed", 0, true,
                                PrototypeJson.EmptyObject());
                            var insertion = ResolveRandomPilePosition(
                                combat, rng, combat.DrawPile,
                                instance.CardId, operationKind: null,
                                sourceCardInstanceId: sourceCardInstanceId,
                                ordinal: n,
                                siteId:
                                    PrototypeControlledDrawInsertionV111
                                        .PersonalHiveDazedSiteId,
                                requestOwnerPrefix:
                                    "enemy:" + splashTarget.InstanceId +
                                    ":power:personal_hive:source:" +
                                    (sourceCardInstanceId?.ToString() ?? "none"));
                            combat = insertion.Combat;
                            var slot = insertion.SelectedIndex;
                            combat = SemanticDrawPileAdapterV111
                                .ApplyNewDrawPileInsertion(
                                    combat, slot, instance);
                        }
                    }
                    var damageContinuation =
                        new PrototypePersonalHiveDamageContinuationV111(
                            operation with
                            {
                                TargetEnemyId = targetEnemyId.Value,
                                TargetMode =
                                    PrototypeEffectTarget.ActionTargetEnemy
                            },
                            targetEnemyId.Value,
                            splashTarget!,
                            damageAmount,
                            overkill,
                            damageResult.DamageDealt,
                            damageResult.Defeated,
                            fatalEligible,
                            personalHiveStacks,
                            eventDepth);
                    var effectContinuation = CreateEffectContinuation(
                        effectContinuationContext, operations);
                    var continuedDamage =
                        ContinueDamageEnemyAfterHit(
                            player, combat, rng, damageContinuation,
                            sourceCardInstanceId, operations,
                            effectContinuation);
                    player = continuedDamage.Player;
                    combat = continuedDamage.Combat;
                    if (combat.PendingChoice is not null)
                        return (player, combat);
                    break;

                case PrototypeCombatEffectKind.DamageAllEnemiesRepeatPerKill:
                {
                    var effectContinuation = CreateEffectContinuation(
                        effectContinuationContext, operations);
                    var repeatedDamage = ResolveDamageAllEnemiesRepeatPerKill(
                        player, combat, operation, rng,
                        sourceCardInstanceId, eventDepth,
                        effectContinuation);
                    player = repeatedDamage.Player;
                    combat = repeatedDamage.Combat;
                    if (combat.PendingChoice is not null)
                        return (player, combat);
                    break;
                }

                case PrototypeCombatEffectKind.LoseEnemyHp:
                {
                    if (targetEnemyId is null)
                    {
                        throw new InvalidOperationException(
                            "Enemy HP-loss operation requires a target.");
                    }

                    var hpLoss = LoseEnemyHp(
                        combat,
                        targetEnemyId.Value,
                        operation.Amount);
                    combat = hpLoss.Combat;
                    if (hpLoss.Defeated)
                    {
                        combat =
                            CleanupSourceBoundPowersForDefeatedEnemies(
                                combat);
                        combat = ResolveEnemyDeathSummons(
                            combat,
                            rng);
                    }

                    break;
                }

                case PrototypeCombatEffectKind.StunEnemy:
                {
                    if (targetEnemyId is null)
                    {
                        throw new InvalidOperationException(
                            "Enemy stun operation requires a target.");
                    }

                    var enemies = combat.Enemies
                        .Select(enemy => enemy.Fork()).ToArray();
                    var index = Array.FindIndex(enemies,
                        enemy => enemy.InstanceId == targetEnemyId.Value);
                    if (index < 0)
                    {
                        throw new InvalidOperationException(
                            $"Enemy {targetEnemyId.Value} is missing.");
                    }

                    if (enemies[index].Hp > 0)
                    {
                        var stunned = enemies[index];
                        var moveCount = PrototypeContent.Enemy(
                            stunned.EnemyId).Moves.Length;
                        enemies[index] = enemies[index] with
                        {
                            EnemyActionSkipsRemaining = Math.Max(
                                Math.Max(1, operation.Amount),
                                enemies[index].EnemyActionSkipsRemaining),
                            StunFollowUpMoveIndex =
                                stunned.StunFollowUpMoveIndex
                                    ?? stunned.PlannedMoveIndex
                                    ?? (moveCount > 0
                                        ? stunned.MoveIndex : null),
                            StunFollowUpAiStateId =
                                stunned.StunFollowUpAiStateId
                                    ?? stunned.PlannedNextAiStateId
                                    ?? stunned.AiStateId,
                            PlannedMoveIndex = null,
                            PlannedNextAiStateId = null
                        };
                        combat = combat with { Enemies = enemies };
                    }
                    break;
                }

                case PrototypeCombatEffectKind.GainPlayerBlock:
                {
                    var blockResult =
                        ModifyPlayerBlockGainWithLegion(
                            combat,
                            operation.Amount,
                            operation.SourceKind
                                == PrototypeEffectSourceKind.Card,
                            sourceCardInstanceId);
                    combat = blockResult.Combat with
                    {
                        PlayerBlock = blockResult.Combat.PlayerBlock
                            + Math.Max(0, blockResult.Amount)
                    };
                    break;
                }

                case PrototypeCombatEffectKind.MultiplyPlayerBlock:
                    combat = combat with
                    {
                        PlayerBlock =
                            combat.PlayerBlock
                            * Math.Max(0, operation.Amount)
                    };
                    break;

                case PrototypeCombatEffectKind.GainPlayerBlockFromEnemyStatusTotal:
                {
                    if (operation.StatusId is null)
                    {
                        throw new InvalidOperationException(
                            "Status-total block operation is missing a status ID.");
                    }

                    var statusTotal = combat.Enemies
                        .Where(enemy => enemy.Hp > 0)
                        .Sum(enemy =>
                            Math.Max(
                                0,
                                enemy.Statuses.GetValueOrDefault(
                                    operation.StatusId)));
                    var baseAmount =
                        operation.Amount + statusTotal;
                    var blockResult =
                        ModifyPlayerBlockGainWithLegion(
                            combat,
                            baseAmount,
                            operation.SourceKind == PrototypeEffectSourceKind.Card,
                            sourceCardInstanceId);
                    combat = blockResult.Combat with
                    {
                        PlayerBlock = blockResult.Combat.PlayerBlock
                            + Math.Max(0, blockResult.Amount)
                    };
                    break;
                }

                case PrototypeCombatEffectKind.GainToricToughnessBlock:
                {
                    var blockResult = ModifyPlayerBlockGainWithLegion(
                        combat,
                        operation.Amount,
                        operation.SourceKind
                            == PrototypeEffectSourceKind.Card,
                        sourceCardInstanceId);
                    var amount = Math.Max(0, blockResult.Amount);
                    combat = blockResult.Combat;
                    if (amount > 0)
                    {
                        combat = combat with
                        {
                            PlayerBlock = combat.PlayerBlock + amount,
                            ToricShields = (combat.ToricShields
                                ?? Array.Empty<PrototypeToricShield>())
                                .Append(new PrototypeToricShield(amount, 2))
                                .ToArray()
                        };
                    }
                    break;
                }

                case PrototypeCombatEffectKind.GainPlayerBlockAndApplyPowerFromActualGain:
                {
                    if (operation.PowerId is null)
                    {
                        throw new InvalidOperationException(
                            "Block-and-delay operation is missing a power ID.");
                    }

                    var blockResult =
                        ModifyPlayerBlockGainWithLegion(
                            combat,
                            operation.Amount,
                            operation.SourceKind
                                == PrototypeEffectSourceKind.Card,
                            sourceCardInstanceId);
                    var actualGain = Math.Max(0, blockResult.Amount);
                    combat = blockResult.Combat;
                    combat = combat with
                    {
                        PlayerBlock = combat.PlayerBlock + actualGain
                    };
                    if (actualGain > 0)
                    {
                        combat = ApplyPlayerPower(
                            combat,
                            operation.PowerId,
                            actualGain);
                    }

                    break;
                }

                case PrototypeCombatEffectKind.DrawCards:
                {
                    var drawProgress = ResolveEffectDrawOperation(
                        player, combat, operation, operations, rng,
                        eventDepth, effectContinuationContext);
                    player = drawProgress.Player;
                    combat = drawProgress.Combat;
                    if (drawProgress.ReturnFromOperations)
                        return (player, combat);
                    operations = drawProgress.Operations;
                    break;
                }

                case PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle:
                {
                    var shuffleProgress =
                        ResolveHandReturnDrawShuffleOperation(
                            player, combat, operation, operations, rng,
                            effectContinuationContext);
                    player = shuffleProgress.Player;
                    combat = shuffleProgress.Combat;
                    if (shuffleProgress.ReturnFromOperations)
                        return (player, combat);
                    operations = shuffleProgress.Operations;
                    break;
                }

                case PrototypeCombatEffectKind.ApplyEnemyStatus:
                    if (targetEnemyId is null || operation.StatusId is null)
                    {
                        throw new InvalidOperationException("Status operation requires target and status ID.");
                    }

                    var statusAmount = operation.Amount;
                    if (statusAmount > 0)
                    {
                        statusAmount = checked(statusAmount
                            + combat.RelicStates
                                .Where(relic => !relic.IsMelted)
                                .Select(relic => PrototypeContent.Relic(
                                    relic.RelicId))
                                .Where(relic => relic.AppliedEnemyStatusAmountBonusId
                                    == operation.StatusId)
                                .Sum(relic => relic.AppliedEnemyStatusAmountBonus));
                    }
                    combat = ApplyEnemyStatus(
                        combat,
                        targetEnemyId.Value,
                        operation.StatusId,
                        statusAmount);
                    break;

                case PrototypeCombatEffectKind.ExhaustHand:
                {
                    // Glowwater exhausts the entire current hand before its
                    // draw begins. Reuse the ordered event-continuation
                    // dispatcher so CardExhausted hooks run in hand order,
                    // including hooks which suspend for a player choice.
                    var exhausted = (long[])combat.Hand.Clone();
                    combat = SemanticDrawPileAdapterV111.ApplyZoneTransition(
                        combat,
                        combat.Mechanics.CardZones.WithZoneChanges(
                            new Dictionary<PrototypeCardZone, long[]>
                            {
                                [PrototypeCardZone.Hand] = [],
                                [PrototypeCardZone.ExhaustPile] = combat.ExhaustPile
                                    .Concat(exhausted).ToArray()
                            }));
                    if (exhausted.Length == 0)
                    {
                        break;
                    }

                    var exhaustEvents = exhausted.Select(id =>
                        new PrototypeCombatEvent(
                            PrototypeCombatEventKind.CardExhausted,
                            SourceCardInstanceId: id,
                            CardId: RequireCombatCard(combat, id).CardId))
                        .ToArray();
                    return ResumeChoiceResolutionContinuation(
                        player, combat, rng,
                        new PrototypeChoiceResolutionContinuationState(
                            SourceCardInstanceId: sourceCardInstanceId,
                            SourceCardDestination: sourceCardDestination,
                            Operations: operations.ToArray(),
                            PendingDiscardEvents: exhaustEvents,
                            PendingSlyCardInstanceIds: Array.Empty<long>(),
                            CompletionEvents: completionEvents
                                ?? Array.Empty<PrototypeCombatEvent>(),
                            CardPlaySeries: cardPlaySeries,
                            MoveSourceCardOnCompletion: moveSourceCardOnCompletion,
                            RemoveSourceCardOnCompletion: removeSourceCardOnCompletion,
                            SourceCardAlreadyMoved: sourceCardAlreadyMoved,
                            EventDispatchContinuation:
                                eventDispatchContinuation?.Fork()),
                        resumeCardPlaySeries: false);
                }

                case PrototypeCombatEffectKind.DiscardHand:
                {
                    var discarded = (long[])combat.Hand.Clone();
                    if (discarded.Length == 0)
                    {
                        break;
                    }

                    var slyCards = discarded
                        .Where(cardInstanceId =>
                            CardHasKeyword(
                                combat,
                                cardInstanceId,
                                PrototypeCardKeyword.Sly))
                        .ToArray();

                    combat = SemanticDrawPileAdapterV111.ApplyZoneTransition(
                        combat,
                        combat.Mechanics.CardZones.WithZoneChanges(
                            new Dictionary<PrototypeCardZone, long[]>
                            {
                                [PrototypeCardZone.Hand] = [],
                                [PrototypeCardZone.DiscardPile] = combat.DiscardPile
                                    .Concat(discarded).ToArray()
                            }));

                    var discardEvents = discarded
                        .Select(cardInstanceId =>
                        {
                            var discardedCard =
                                RequireCombatCard(
                                    combat,
                                    cardInstanceId);
                            return new PrototypeCombatEvent(
                                PrototypeCombatEventKind.CardDiscarded,
                                SourceCardInstanceId:
                                    cardInstanceId,
                                CardId:
                                    discardedCard.CardId);
                        })
                        .ToArray();

                    return ResumeChoiceResolutionContinuation(
                        player,
                        combat,
                        rng,
                        new PrototypeChoiceResolutionContinuationState(
                            SourceCardInstanceId:
                                sourceCardInstanceId,
                            SourceCardDestination:
                                sourceCardDestination,
                            Operations:
                                operations.ToArray(),
                            PendingDiscardEvents:
                                discardEvents,
                            PendingSlyCardInstanceIds:
                                slyCards,
                            CompletionEvents:
                                completionEvents
                                ?? Array.Empty<PrototypeCombatEvent>(),
                            CardPlaySeries:
                                cardPlaySeries,
                            MoveSourceCardOnCompletion:
                                moveSourceCardOnCompletion,
                            RemoveSourceCardOnCompletion:
                                removeSourceCardOnCompletion,
                            SourceCardAlreadyMoved:
                                sourceCardAlreadyMoved,
                            EventDispatchContinuation:
                                eventDispatchContinuation?.Fork()),
                        resumeCardPlaySeries: false);
                }

                case PrototypeCombatEffectKind.CreateDistinctColorlessCardsInHand:
                    combat = AddDistinctGeneratedColorlessCardsToHand(
                        combat, operation.Amount, rng,
                        sourceCardInstanceId is null
                            ? null
                            : RequireCombatCard(
                                combat, sourceCardInstanceId.Value).CardId,
                        sourceCardInstanceId);
                    break;

                case PrototypeCombatEffectKind.ChooseGeneratedCards:
                case PrototypeCombatEffectKind.ChooseGeneratedColorlessCards:
                {
                    var sourcePool = SourcePoolForShuffle(
                        combat, operation, sourceCardInstanceId)
                        ?? throw new InvalidOperationException(
                            "Generated-card choice has no source pool.");
                    var generated = AddGeneratedChoiceCards(
                        combat,
                        sourcePool,
                        operation.Amount,
                        operation.GeneratedChoiceCardsFreeThisTurn,
                        operation.GeneratedChoiceCardsUpgraded,
                        operation.Kind,
                        sourceCardInstanceId,
                        rng,
                        operation.GeneratedChoiceCardsRetain,
                        operation.GeneratedChoiceUsesNativeCardGenerationRng);
                    combat = generated.Combat;
                    if (generated.CardInstanceIds.Length == 0)
                    {
                        break;
                    }

                    combat = combat with
                    {
                        PendingChoice = new PendingCombatChoiceState(
                            ChoiceId: "select_cards",
                            SourceCardInstanceId: sourceCardInstanceId,
                            SourceCardDestination: sourceCardDestination,
                            Selection: new PrototypeCardSelectionSpec(
                                PrototypeCardZone.ChoicePool,
                                operation.GeneratedChoiceMustPick ? 1 : 0,
                                1,
                                PrototypeCardSelectionResolutionKind
                                    .MoveToHand,
                                RemoveUnselectedFromSource: true),
                            CandidateCardInstanceIds:
                                (long[])generated.CardInstanceIds.Clone(),
                            Continuation: operations.ToArray(),
                            CompletionEvents: completionEvents is null
                                ? Array.Empty<PrototypeCombatEvent>()
                                : (PrototypeCombatEvent[])completionEvents.Clone(),
                            CardPlaySeries: cardPlaySeries,
                            MoveSourceCardOnCompletion:
                                moveSourceCardOnCompletion,
                            RemoveSourceCardOnCompletion:
                                removeSourceCardOnCompletion,
                            SourceCardAlreadyMoved:
                                sourceCardAlreadyMoved,
                            EventDispatchContinuation:
                                eventDispatchContinuation?.Fork())
                    };
                    return (player, combat);
                }

                case PrototypeCombatEffectKind.EmpowerRandomDrawCardReplay:
                {
                    var pool = ControlledCardShortlistCandidates(
                        combat, operation) ?? throw new InvalidDataException(
                            "Random replay-card effect lost its source pool.");
                    if (pool.Length > 0)
                    {
                        (CombatState Combat, int SelectedIndex,
                            long? SelectedCardInstanceId) choice;
                        if (ControlledChanceGuardV111.Active &&
                            !ControlledChanceGuardV111
                                .EngineOwnedNativeProviderActive &&
                            pool.Length > 1)
                        {
                            var continuation =
                                new PrototypeChoiceResolutionContinuationState(
                                    SourceCardInstanceId: sourceCardInstanceId,
                                    SourceCardDestination: sourceCardDestination,
                                    Operations: operations.ToArray(),
                                    PendingDiscardEvents: [],
                                    PendingSlyCardInstanceIds: [],
                                    CompletionEvents: completionEvents ?? [],
                                    CardPlaySeries: cardPlaySeries,
                                    MoveSourceCardOnCompletion:
                                        moveSourceCardOnCompletion,
                                    RemoveSourceCardOnCompletion:
                                        removeSourceCardOnCompletion,
                                    SourceCardAlreadyMoved:
                                        sourceCardAlreadyMoved,
                                    EventDispatchContinuation:
                                        eventDispatchContinuation?.Fork());
                            if (TryMakeControlledCardShortlistChoice(
                                combat, operation, pool, continuation,
                                out var suspended))
                                return (player, suspended);
                        }
                        if (ControlledChanceGuardV111.Active &&
                            !ControlledChanceGuardV111
                                .EngineOwnedNativeProviderActive &&
                            pool.Length == 1)
                            choice = (combat, 0, null);
                        else
                            choice = ResolveRandomCombatCardIndex(
                                combat, rng,
                                StochasticSiteRegistryV111.CombatRandomReplayCardSelectSiteId,
                                "combat", pool,
                                "effect:" + operation.Kind + ":source:" +
                                    (sourceCardInstanceId?.ToString() ?? "none"));
                        combat = choice.Combat;
                        var selected = pool[choice.SelectedIndex];
                        combat = combat with
                        {
                            Cards = combat.Cards.Select(card =>
                                card.InstanceId == selected
                                    ? card with
                                    {
                                        ReplayCount = card.ReplayCount
                                            + Math.Max(0, operation.Amount)
                                    }
                                    : card).ToArray()
                        };
                    }
                    break;
                }

                case PrototypeCombatEffectKind.CreateRandomFreeCharacterCardInHand:
                {
                    // Chaos makes any eligible character card free for
                    // the current turn, independently of its normal cost.
                    var pool = GeneratedCardModelPool(operation)
                        ?? throw new InvalidOperationException(
                            "Free character-card generation has no source pool.");
                    if (operation.Amount > 0 && pool.Length == 0)
                        throw new NotSupportedException(
                            "Chaos has no supported single-player card pool.");
                    for (var index = 0; index < operation.Amount; index++)
                    {
                        var selection = ResolveGeneratedCardModelIndex(
                            combat, rng, pool, operation.Kind,
                            sourceCardInstanceId, generatedCardOrdinal++);
                        combat = selection.Combat;
                        var id = pool[selection.SelectedIndex];
                        combat = AddGeneratedCardCopies(combat,
                            new PrototypeCombatCardSnapshot(
                                id, 0, PrototypeJson.EmptyObject(),
                                TemporaryEnergyCost: new(
                                    0, PrototypeTemporaryCardCostExpiry.EndOfTurn
                                        | PrototypeTemporaryCardCostExpiry.WhenPlayed)),
                            1);
                    }
                    break;
                }

                case PrototypeCombatEffectKind.AddRandomZeroCostCardsToHand:
                {
                    var pool = GeneratedCardModelPool(operation)
                        ?? throw new InvalidOperationException(
                            "Zero-cost card generation has no source pool.");
                    if (pool.Length == 0 && operation.Amount > 0)
                    {
                        throw new NotSupportedException(
                            "No implemented zero-cost Silent card generation candidates.");
                    }
                    for (var n = 0; n < operation.Amount; n++)
                    {
                        var selection = ResolveGeneratedCardModelIndex(
                            combat, rng, pool, operation.Kind,
                            sourceCardInstanceId, generatedCardOrdinal++);
                        combat = selection.Combat;
                        var picked = pool[selection.SelectedIndex];
                        combat = AddGeneratedCardCopies(combat,
                            new PrototypeCombatCardSnapshot(
                                picked, operation.GeneratedCardUpgradeLevel,
                                PrototypeJson.EmptyObject()), 1);
                    }
                    break;
                }

                case PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand:
                {
                    // Anointed uses pile insertion rather than a normal draw:
                    // it must preserve card instances and avoid draw triggers.
                    var capacity = PrototypeContent.Rules
                        .RemainingHandCapacity(combat.Hand.Length);
                    var take = Math.Min(Math.Max(0, operation.Amount), capacity);
                    if (take == 0)
                    {
                        break;
                    }

                    var rareCards = combat.DrawPile
                        .Where(id => PrototypeContent.Card(
                            RequireCombatCard(combat, id).CardId).Rarity
                            == PrototypeCardRarity.Rare)
                        .ToArray();
                    if (rareCards.Length == 0)
                    {
                        break;
                    }

                    combat = ShuffleCombatCardPool(
                        combat, rng, rareCards,
                        StochasticSiteRegistryV111.CombatRandomRareDrawToHandShuffleSiteId,
                        "combat", "run.combat_card_selection",
                        "effect:" + operation.Kind + ":source:" +
                            (sourceCardInstanceId?.ToString() ?? "none") +
                            ":zone:draw",
                        operation.Kind, Math.Min(take, rareCards.Length),
                        sourceCardInstanceId);
                    var selected = rareCards.Take(take).ToArray();
                    var selectedIds = selected.ToHashSet();
                    var nextZones = combat.Mechanics.CardZones.WithZoneChanges(
                        new Dictionary<PrototypeCardZone, long[]>
                        {
                            [PrototypeCardZone.DrawPile] = combat.DrawPile
                                .Where(id => !selectedIds.Contains(id)).ToArray(),
                            [PrototypeCardZone.Hand] = combat.Hand
                                .Concat(selected).ToArray()
                        });
                    combat = SemanticDrawPileAdapterV111
                        .ApplyDrawPileRemoval(combat, selected, nextZones);
                    break;
                }

                case PrototypeCombatEffectKind.MoveRandomPowerDrawCardToHandFree:
                {
                    var powers = combat.DrawPile
                        .Where(id => RuntimeCardDefinition(
                            RequireCombatCard(combat, id)).Type
                            == PrototypeCardType.Power)
                        .ToArray();
                    var nonInnatePowers = powers
                        .Where(id => !IsInnate(
                            RequireCombatCard(combat, id)))
                        .ToArray();
                    var candidates = nonInnatePowers.Length > 0
                        ? nonInnatePowers
                        : powers;
                    if (candidates.Length == 0)
                        break;

                    if (ControlledChanceGuardV111.Active &&
                        !ControlledChanceGuardV111
                            .EngineOwnedNativeProviderActive)
                    {
                        if (combat.Stochastic.SemanticDeck is not
                            { Mode: SemanticDeckStateV111.NatureMode })
                            throw new NotSupportedException(
                                "UNSUPPORTED_SEMANTIC_DECK " +
                                "site=" + StochasticSiteRegistryV111
                                    .CombatJeweledMaskRandomPowerDrawCardSiteId +
                                " " +
                                "operation=requires_nature_draw_pile");
                        var continuation =
                            new PrototypeChoiceResolutionContinuationState(
                                SourceCardInstanceId: sourceCardInstanceId,
                                SourceCardDestination: sourceCardDestination,
                                Operations: operations.ToArray(),
                                PendingDiscardEvents: [],
                                PendingSlyCardInstanceIds: [],
                                CompletionEvents: completionEvents ?? [],
                                CardPlaySeries: cardPlaySeries,
                                MoveSourceCardOnCompletion:
                                    moveSourceCardOnCompletion,
                                RemoveSourceCardOnCompletion:
                                    removeSourceCardOnCompletion,
                                SourceCardAlreadyMoved:
                                    sourceCardAlreadyMoved,
                                EventDispatchContinuation:
                                    eventDispatchContinuation?.Fork());
                        return (player, MakeControlledRandomPileCardChoice(
                            combat, candidates, continuation));
                    }

                    var nativeRunStreams = combat.Stochastic.NativeRng?.RunStreams.Fork();
                    var streamId = nativeRunStreams is null
                        ? "combat"
                        : "run.combat_card_selection";
                    var streams = nativeRunStreams ?? rng;
                    IRandomIndexProviderV111 provider =
                        nativeRunStreams is null
                            ? new LegacyPrototypeRandomIndexProviderV111()
                            : new NativeRandomIndexProviderV111();
                    var request = new RandomIndexRequestV111(
                        StochasticSiteRegistryV111
                            .CombatJeweledMaskRandomPowerDrawCardSiteId,
                        streamId, "player:0", combat.Turn - 1,
                        Enumerable.Range(0, candidates.Length).ToArray());
                    var decision = StochasticProviderScopeV111.ResolveIndex(
                        provider, request, streams);
                    if (decision.ProviderMode == "native-v111" &&
                        RandomBoundaryJournalV111.HasActiveJournal &&
                        StochasticSiteRegistryV111
                            .IsCombatCardInstanceIndexAuditSite(
                                request.SiteId))
                        RandomBoundaryJournalV111.RecordCardInstanceIndexOutcome(
                            SemanticCardInstanceIndexAuditV111.Create(
                                decision, candidates));
                    var selectedIndex = decision.SelectedIndex;
                    var selectedId = candidates[selectedIndex];
                    combat = SetCardTemporaryEnergyCost(
                        combat,
                        selectedId,
                        new PrototypeTemporaryCardCost(
                            0,
                            PrototypeTemporaryCardCostExpiry.EndOfTurn
                            | PrototypeTemporaryCardCostExpiry.WhenPlayed));
                    var handHasSpace = PrototypeContent.Rules.HasHandCapacity(
                        combat.Hand.Length);
                    var zoneChanges = new Dictionary<PrototypeCardZone, long[]>
                    {
                        [PrototypeCardZone.DrawPile] = combat.DrawPile
                            .Where(id => id != selectedId).ToArray(),
                        [handHasSpace
                            ? PrototypeCardZone.Hand
                            : PrototypeCardZone.DiscardPile] = handHasSpace
                                ? combat.Hand.Append(selectedId).ToArray()
                                : combat.DiscardPile.Append(selectedId).ToArray()
                    };
                    var committedDraw =
                        SemanticDrawPileAdapterV111.ApplyDrawPileRemoval(
                            combat,
                            [selectedId],
                            combat.Mechanics.CardZones
                                .WithZoneChanges(zoneChanges));
                    combat = committedDraw with
                    {
                        Stochastic = CombatStochasticStateV111.WithNativeRng(
                            committedDraw.Stochastic,
                            nativeRunStreams is null
                                ? committedDraw.Stochastic.NativeRng
                                : committedDraw.Stochastic.NativeRng! with
                                {
                                    RunStreams = nativeRunStreams
                                })
                    };
                    break;
                }

                case PrototypeCombatEffectKind.ChooseCards:
                {
                    var selection = operation.Selection
                        ?? throw new InvalidOperationException("Choose-cards operation has no selection specification.");
                    var candidates = GetZone(combat, selection.SourceZone);
                    if (selection.Resolution == PrototypeCardSelectionResolutionKind.TransformRandom)
                        candidates = candidates.Where(id => !PrototypeCardRules.IsEternal(
                            RequireCombatCard(combat, id))).ToArray();
                    if (selection.RequireAttackOrPower)
                        candidates = candidates.Where(id => PrototypeContent.Card(
                            RequireCombatCard(combat, id).CardId).Type
                            is PrototypeCardType.Attack or PrototypeCardType.Power).ToArray();
                    if (selection.RequiredCardType is not null)
                    {
                        candidates = candidates
                            .Where(instanceId =>
                                PrototypeContent.Card(
                                    RequireCombatCard(combat, instanceId).CardId).Type
                                == selection.RequiredCardType.Value)
                            .ToArray();
                    }

                    if (selection.RandomCandidateCount > 0
                        && candidates.Length > selection.RandomCandidateCount)
                    {
                        if (ControlledChanceGuardV111.Active &&
                            !ControlledChanceGuardV111
                                .EngineOwnedNativeProviderActive)
                        {
                            var continuation =
                                new PrototypeChoiceResolutionContinuationState(
                                    SourceCardInstanceId: sourceCardInstanceId,
                                    SourceCardDestination: sourceCardDestination,
                                    Operations: operations.ToArray(),
                                    PendingDiscardEvents: [],
                                    PendingSlyCardInstanceIds: [],
                                    CompletionEvents: completionEvents ?? [],
                                    CardPlaySeries: cardPlaySeries,
                                    MoveSourceCardOnCompletion:
                                        moveSourceCardOnCompletion,
                                    RemoveSourceCardOnCompletion:
                                        removeSourceCardOnCompletion,
                                    SourceCardAlreadyMoved:
                                        sourceCardAlreadyMoved,
                                    EventDispatchContinuation:
                                        eventDispatchContinuation?.Fork());
                            if (TryMakeControlledPoolShuffleChoice(
                                    combat, operation, sourceCardInstanceId,
                                    continuation, out var suspended))
                                return (player, suspended);
                        }

                        // Native pile selectors may offer only a random
                        // shortlist. Filter by type before choosing it.
                        var shuffledCandidates = (long[])candidates.Clone();
                        combat = ShuffleCombatCardPool(
                            combat, rng, shuffledCandidates,
                            StochasticSiteRegistryV111.CombatRandomShortlistShuffleSiteId,
                            "combat", "run.combat_card_selection",
                            "card_selection:" + selection.SourceZone +
                                ":source:" +
                                (sourceCardInstanceId?.ToString() ?? "none"),
                            operation.Kind,
                            Math.Min(selection.RandomCandidateCount,
                                shuffledCandidates.Length),
                            sourceCardInstanceId);
                        candidates = shuffledCandidates
                            .Take(selection.RandomCandidateCount).ToArray();
                    }

                    if (selection.RequireEnergyCostingCard)
                    {
                        candidates = candidates
                            .Where(instanceId =>
                            {
                                var card =
                                    RequireCombatCard(
                                        combat,
                                        instanceId);
                                var definition =
                                    PrototypeContent.Card(
                                        card.CardId);
                                return !definition.Unplayable
                                    && (definition.Cost.Kind
                                            == PrototypeCardCostKind.X
                                        || (definition.Cost.Kind
                                                == PrototypeCardCostKind.Fixed
                                            && definition.Cost.Amount >= 0));
                            })
                            .ToArray();
                    }

                    var capacity = selection.Resolution
                            == PrototypeCardSelectionResolutionKind.MoveToHand
                        ? PrototypeContent.Rules.RemainingHandCapacity(
                            combat.Hand.Length)
                        : int.MaxValue;
                    var maxSelectable = Math.Min(
                        Math.Min(selection.MaxSelections, candidates.Length),
                        capacity);
                    if (candidates.Length == 0 || maxSelectable <= 0)
                    {
                        break;
                    }

                    var effective = selection with
                    {
                        MinSelections = Math.Min(selection.MinSelections, maxSelectable),
                        MaxSelections = maxSelectable
                    };

                    combat = combat with
                    {
                        PendingChoice = new PendingCombatChoiceState(
                            ChoiceId: "select_cards",
                            SourceCardInstanceId: sourceCardInstanceId,
                            SourceCardDestination: sourceCardDestination,
                            Selection: effective,
                            CandidateCardInstanceIds: (long[])candidates.Clone(),
                            Continuation: operations.ToArray(),
                            CompletionEvents: completionEvents is null
                                ? Array.Empty<PrototypeCombatEvent>()
                                : (PrototypeCombatEvent[])completionEvents.Clone(),
                            CardPlaySeries: cardPlaySeries,
                            MoveSourceCardOnCompletion:
                                moveSourceCardOnCompletion,
                            RemoveSourceCardOnCompletion:
                                removeSourceCardOnCompletion,
                            SourceCardAlreadyMoved:
                                sourceCardAlreadyMoved,
                            SelectedCardPower:
                                operation.SelectedCardPower,
                            SelectedCardKeyword:
                                operation.SelectedCardKeyword,
                            SelectedCardTemporaryCost:
                                operation.SelectedCardTemporaryCost is null
                                    ? null
                                    : operation.SelectedCardTemporaryCost with { },
                            EventDispatchContinuation:
                                eventDispatchContinuation?.Fork(),
                            SemanticStratagemDrawSelection:
                                operation.SemanticStratagemDrawSelection,
                            AutomaticSemanticStratagemDrawSelection:
                                operation.AutomaticSemanticStratagemDrawSelection)
                    };
                    return (player, combat);
                }

                case PrototypeCombatEffectKind.RemoveEnemyBlock:
                    if (targetEnemyId is null)
                    {
                        throw new InvalidOperationException(
                            "Remove-enemy-block operation requires a target.");
                    }

                    var blockBeforeRemoval = combat.Enemies.Single(enemy => enemy.InstanceId == targetEnemyId.Value).Block;
                    combat = SetEnemyBlock(
                        combat,
                        targetEnemyId.Value,
                        0);
                    if (blockBeforeRemoval > 0)
                        combat = ApplyPlayerEnemyBlockBrokenRelics(player, combat, targetEnemyId.Value);
                    break;

                case PrototypeCombatEffectKind.RemoveEnemyPower:
                    if (targetEnemyId is null
                        || operation.PowerId is null)
                    {
                        throw new InvalidOperationException(
                            "Remove-enemy-power operation requires a target and power ID.");
                    }

                    combat = RemoveEnemyPower(
                        combat,
                        targetEnemyId.Value,
                        operation.PowerId);
                    break;

                case PrototypeCombatEffectKind.TriggerEnemyStatus:
                    if (targetEnemyId is null
                        || operation.StatusId is null)
                    {
                        throw new InvalidOperationException(
                            "Trigger-enemy-status operation requires a target and status ID.");
                    }

                    combat = TriggerEnemyStatus(
                        combat,
                        targetEnemyId.Value,
                        operation.StatusId,
                        rng);
                    break;

                case PrototypeCombatEffectKind.ApplyPlayerPower:
                    if (operation.PowerId is null)
                    {
                        throw new InvalidOperationException("Apply-power operation is missing a power ID.");
                    }

                    combat = ApplyPlayerPower(
                        combat, operation.PowerId, operation.Amount,
                        storedValue: operation.PowerStoredValue);
                    break;

                case PrototypeCombatEffectKind.ApplyEnemyPower:
                    if (operation.PowerId is null || targetEnemyId is null)
                    {
                        throw new InvalidOperationException(
                            "Apply-enemy-power operation requires a power ID and enemy target.");
                    }

                    combat = ApplyEnemyPower(
                        combat,
                        targetEnemyId.Value,
                        operation.PowerId,
                        operation.Amount);
                    break;

                case PrototypeCombatEffectKind.HealPlayer:
                    player = player with
                    {
                        Hp = Math.Min(
                            player.MaxHp,
                            player.Hp + Math.Max(
                                0,
                                operation.Amount))
                    };
                    break;

                case PrototypeCombatEffectKind.UpgradeHandCards:
                    combat = combat with
                    {
                        Cards = combat.Cards
                            .Select(card =>
                            {
                                if (!combat.Hand.Contains(
                                        card.InstanceId)
                                    || card.UpgradeLevel > 0)
                                {
                                    return card;
                                }

                                var definition =
                                    PrototypeContent.Card(
                                        card.CardId);
                                if (definition.Type is
                                    PrototypeCardType.Status
                                    or PrototypeCardType.Curse)
                                {
                                    return card;
                                }

                                return card with
                                {
                                    UpgradeLevel =
                                        card.UpgradeLevel + 1
                                };
                            })
                            .ToArray()
                    };
                    break;

                case PrototypeCombatEffectKind.UpgradeAllCards:
                    combat = combat with
                    {
                        Cards = combat.Cards
                            .Select(card =>
                            {
                                if (card.InstanceId
                                    == sourceCardInstanceId
                                    || card.UpgradeLevel
                                        >= PrototypeContent.Card(card.CardId)
                                            .MaxUpgradeLevel)
                                {
                                    return card;
                                }

                                var definition =
                                    PrototypeContent.Card(card.CardId);
                                if (definition.Type is
                                    PrototypeCardType.Status
                                    or PrototypeCardType.Curse)
                                {
                                    return card;
                                }

                                return card with
                                {
                                    UpgradeLevel = card.UpgradeLevel + 1
                                };
                            })
                            .ToArray()
                    };
                    break;

                case PrototypeCombatEffectKind.DamagePlayer:
                {
                    var incoming =
                        ApplyPlayerIncomingDamageCap(
                            combat,
                            Math.Max(
                                0,
                                operation.Amount));
                    var absorbed = Math.Min(
                        combat.PlayerBlock,
                        incoming);
                    combat = combat with
                    {
                        PlayerBlock =
                            combat.PlayerBlock
                            - absorbed
                    };
                    var loss = ApplyPlayerHpLoss(
                        player,
                        combat,
                        incoming - absorbed);
                    player = loss.Player;
                    combat = loss.Combat;
                    if (player.Hp <= 0)
                    {
                        var prevented =
                            TryUseAutomaticDeathPrevention(
                                player,
                                combat,
                                rng,
                                eventDepth);
                        player = prevented.Player;
                        combat = prevented.Combat;
                    }
                    break;
                }

                case PrototypeCombatEffectKind.GainEnergy:
                    combat = combat with
                    {
                        Energy = combat.Energy + Math.Max(0, operation.Amount)
                    };
                    break;

                case PrototypeCombatEffectKind.BuffMaulDamage:
                    combat = combat with
                    {
                        Counters = combat.CounterState with
                        {
                            MaulDamageBonus = checked(
                                combat.CounterState.MaulDamageBonus
                                + Math.Max(0, operation.Amount))
                        }
                    };
                    break;

                case PrototypeCombatEffectKind.GainEnergyForGold:
                    if (operation.PlayerGoldCost < 0)
                        throw new InvalidOperationException("Energy purchase requires a nonnegative gold cost.");
                    // Evaluate funds at execution, after earlier ordered
                    // subscribers, rather than when the subscriber list forms.
                    if (player.Gold >= operation.PlayerGoldCost)
                    {
                        combat = combat with { Energy = combat.Energy + Math.Max(0, operation.Amount) };
                        player = player with { Gold = player.Gold - operation.PlayerGoldCost };
                    }
                    break;

                case PrototypeCombatEffectKind.LosePlayerMaxHp:
                {
                    var rawMaxHp = player.MaxHp - Math.Max(0, operation.Amount);
                    player = player with
                    {
                        MaxHp = Math.Max(1, rawMaxHp),
                        Hp = Math.Max(0, Math.Min(player.Hp, rawMaxHp))
                    };
                    if (player.Hp <= 0)
                    {
                        var prevented = TryUseAutomaticDeathPrevention(player, combat, rng, eventDepth);
                        player = prevented.Player;
                        combat = prevented.Combat;
                    }
                    break;
                }

                case PrototypeCombatEffectKind.ModifySourceCardEnergyCost:
                    if (sourceCardInstanceId is null)
                    {
                        throw new InvalidOperationException(
                            "Source-card cost mutation requires a source card.");
                    }

                    combat = ModifyCombatCardEnergyCost(
                        combat,
                        sourceCardInstanceId.Value,
                        operation.Amount);
                    break;

                case PrototypeCombatEffectKind.SetHandCardsEnergyCostUntilTurnEndOrPlayed:
                    combat = SetHandCardsTemporaryEnergyCost(
                        combat,
                        operation.Amount);
                    break;

                case PrototypeCombatEffectKind.ReduceHandCardCostsToOne:
                    // Base Enlightenment: expires when played or at turn end.
                    // Enlightenment+: the changed card retains cost 1
                    // through the combat, including future turns.
                    combat = SetHandCardsTemporaryEnergyCost(
                        combat, 1, reduceOnly: true,
                        expiry: operation.Amount > 0
                            ? PrototypeTemporaryCardCostExpiry.None
                            : PrototypeTemporaryCardCostExpiry.EndOfTurn
                                | PrototypeTemporaryCardCostExpiry.WhenPlayed);
                    break;

                case PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed:
                {
                    var candidates = combat.Hand
                        .Select(instanceId =>
                            RequireCombatCard(
                                combat,
                                instanceId))
                        .Where(card =>
                        {
                            var definition =
                                PrototypeContent.Card(
                                    card.CardId);
                            return !definition.Unplayable
                                && definition.Cost.Kind
                                    == PrototypeCardCostKind.Fixed
                                && ResolveFixedCardEnergyCost(
                                    combat,
                                    card,
                                    definition) > 0;
                        })
                        .ToArray();

                    if (candidates.Length > 0)
                    {
                        var selection = ResolveRandomHandCostChoice(
                            combat, rng, operation, sourceCardInstanceId,
                            candidates.Select(card => card.InstanceId).ToArray(),
                            selectedCardInstanceId: null,
                            boundedCount: null);
                        combat = selection.Combat;
                        var selected = candidates[selection.SelectedIndex];
                        combat = SetCardTemporaryEnergyCost(
                            combat,
                            selected.InstanceId,
                            new PrototypeTemporaryCardCost(
                                Math.Max(0, operation.Amount),
                                PrototypeTemporaryCardCostExpiry.EndOfTurn
                                | PrototypeTemporaryCardCostExpiry.WhenPlayed));
                    }

                    break;
                }

                case PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed:
                {
                    var upperBound = Math.Max(
                        1,
                        operation.Amount);
                    var eligibleCardIds = ControlledRandomCostCandidates(
                        combat, operation) ?? [];
                    var eligibleCursor = 0;
                    foreach (var cardInstanceId in combat.Hand)
                    {
                        var card = RequireCombatCard(
                            combat,
                            cardInstanceId);
                        var definition =
                            PrototypeContent.Card(
                                card.CardId);
                        if (definition.Unplayable
                            || definition.Cost.Kind
                                != PrototypeCardCostKind.Fixed
                            || definition.Cost.Amount < 0)
                        {
                            continue;
                        }

                        if (eligibleCursor >= eligibleCardIds.Length ||
                            eligibleCardIds[eligibleCursor] != cardInstanceId)
                            throw new InvalidOperationException(
                                "Random hand-cost support changed during source iteration.");
                        var selection = ResolveRandomHandCostChoice(
                            combat, rng, operation, sourceCardInstanceId,
                            eligibleCardIds,
                            selectedCardInstanceId: cardInstanceId,
                            boundedCount: upperBound);
                        combat = selection.Combat;
                        eligibleCursor++;
                        combat = SetCardTemporaryEnergyCost(
                            combat,
                            cardInstanceId,
                            new PrototypeTemporaryCardCost(
                                selection.SelectedIndex,
                                PrototypeTemporaryCardCostExpiry.EndOfTurn
                                | PrototypeTemporaryCardCostExpiry.WhenPlayed));
                    }

                    if (eligibleCursor != eligibleCardIds.Length)
                        throw new InvalidOperationException(
                            "Random hand-cost support was not fully consumed.");

                    break;
                }

                case PrototypeCombatEffectKind.ModifyEventSourceCardKeyword:
                    if (eventSourceCardInstanceId is null
                        || operation.EventSourceCardKeyword is null)
                    {
                        throw new InvalidOperationException(
                            "Event-source card keyword mutation requires a source card and keyword specification.");
                    }

                    combat = ApplyCardKeywordOverride(
                        combat,
                        eventSourceCardInstanceId.Value,
                        operation.EventSourceCardKeyword);
                    break;

                case PrototypeCombatEffectKind.MultiplyEnemyStatus:
                    if (targetEnemyId is null || operation.StatusId is null)
                    {
                        throw new InvalidOperationException(
                            "Multiply-status operation requires target and status ID.");
                    }

                    combat = MultiplyEnemyStatus(
                        combat,
                        targetEnemyId.Value,
                        operation.StatusId,
                        operation.Amount);
                    break;

                case PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile:
                {
                    var pool = RandomDrawAttackModelPool();
                    if (pool.Length == 0 && operation.Amount > 0)
                    {
                        throw new NotSupportedException(
                            "Metamorphosis has no implemented character Attacks.");
                    }

                    // CardFactory.GetForCombat samples WITH replacement.
                    // Route its model draw through CombatCardGeneration;
                    // the eligible pool remains the implemented Silent
                    // subset rather than the full unlocked roster.
                    for (int i = 0; i < operation.Amount; i++)
                    {
                        var modelSelection = ResolveGeneratedCardModelIndex(
                            combat, rng, pool, operation.Kind,
                            sourceCardInstanceId, generatedCardOrdinal++,
                            StochasticSiteRegistryV111.CombatRandomDrawAttackCardModelSiteId);
                        combat = modelSelection.Combat;
                        var selected = pool[modelSelection.SelectedIndex];
                        var id = combat.NextCardInstanceId;
                        var insertion = ResolveRandomPilePosition(
                            combat, rng, combat.DrawPile, selected,
                            operation.Kind, sourceCardInstanceId,
                            drawInsertionOrdinal++);
                        combat = insertion.Combat;
                        var cardInstance = new CombatCardInstance(
                            id, null, selected, 0, true,
                            PrototypeJson.EmptyObject(),
                            TemporaryEnergyCost:
                                new PrototypeTemporaryCardCost(0,
                                    PrototypeTemporaryCardCostExpiry.None));
                        combat = SemanticDrawPileAdapterV111
                            .ApplyNewDrawPileInsertion(
                                combat, insertion.SelectedIndex,
                                cardInstance);
                    }
                    break;
                }

                case PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions:
                {
                    if (operation.CardId is null)
                    {
                        throw new InvalidOperationException(
                            "Random draw-pile generation requires a card ID.");
                    }

                    _ = PrototypeContent.Card(operation.CardId);
                    for (var i = 0; i < Math.Max(0, operation.Amount); i++)
                    {
                        var id = combat.NextCardInstanceId;
                        var insertion = ResolveRandomPilePosition(
                            combat, rng, combat.DrawPile, operation.CardId,
                            operation.Kind, sourceCardInstanceId,
                            drawInsertionOrdinal++);
                        combat = insertion.Combat;
                        var cardInstance = new CombatCardInstance(
                            id, null, operation.CardId, 0, true,
                            PrototypeJson.EmptyObject());
                        combat = SemanticDrawPileAdapterV111
                            .ApplyNewDrawPileInsertion(
                                combat, insertion.SelectedIndex,
                                cardInstance);
                    }
                    break;
                }

                case PrototypeCombatEffectKind.CreateRandomCharacterAttackCardsInHand:
                case PrototypeCombatEffectKind.CreateRandomCharacterSkillCardsInHand:
                case PrototypeCombatEffectKind.CreateRandomCharacterPowerCardsInHand:
                {
                    var cardType = operation.Kind switch
                    {
                        PrototypeCombatEffectKind
                            .CreateRandomCharacterAttackCardsInHand =>
                            PrototypeCardType.Attack,
                        PrototypeCombatEffectKind
                            .CreateRandomCharacterSkillCardsInHand =>
                            PrototypeCardType.Skill,
                        PrototypeCombatEffectKind
                            .CreateRandomCharacterPowerCardsInHand =>
                            PrototypeCardType.Power,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    var skills = cardType == PrototypeCardType.Skill;
                    var pool = GeneratedCardModelPool(operation)
                        ?? throw new InvalidOperationException(
                            "Typed character-card generation has no source pool.");
                    if (pool.Length == 0 && operation.Amount > 0)
                    {
                        throw new NotSupportedException(
                            "No implemented solo character card generation pool.");
                    }
                    for (var i = 0; i < operation.Amount; i++)
                    {
                        var selection = ResolveGeneratedCardModelIndex(
                            combat, rng, pool, operation.Kind,
                            sourceCardInstanceId, generatedCardOrdinal++);
                        combat = selection.Combat;
                        var id = pool[selection.SelectedIndex];
                        combat = AddGeneratedCardCopies(combat,
                            new PrototypeCombatCardSnapshot(
                                id, 0, PrototypeJson.EmptyObject(),
                                TemporaryEnergyCost:
                                    skills
                                    || operation.GeneratedCharacterCardsFreeThisTurn
                                    ? new(0, PrototypeTemporaryCardCostExpiry.EndOfTurn)
                                    : null), 1);
                    }
                    break;
                }

                case PrototypeCombatEffectKind.CreateDistinctCharacterCommonCardsInHand:
                {
                    var count = operation.SourcePowerApplicationOrder is { } order
                        ? combat.PlayerPowers.Single(power => power.ApplicationOrder == order).StoredValue
                        : operation.Amount;
                    if (count <= 0) break;
                    var pool = SourcePoolForShuffle(
                        combat, operation, sourceCardInstanceId)
                        ?? throw new InvalidOperationException(
                            "Distinct Common-card generation has no source pool.");
                    // Native TakeRandom shuffles the whole filtered pool before
                    // taking a distinct prefix, even for a single generated card.
                    combat = ShuffleCombatCardPool(
                        combat, rng, pool,
                        StochasticSiteRegistryV111.CombatFullPoolShuffleSiteId,
                        "combat", "run.combat_card_generation",
                        "effect:" + operation.Kind + ":source:" +
                            (sourceCardInstanceId?.ToString() ?? "none"),
                        operation.Kind, Math.Min(count, pool.Length),
                        sourceCardInstanceId);
                    foreach (var id in pool.Take(count))
                        combat = AddGeneratedCardCopies(combat,
                            new PrototypeCombatCardSnapshot(id, 0, PrototypeJson.EmptyObject()), 1);
                    break;
                }

                case PrototypeCombatEffectKind.AcquireRandomCombatPotion:
                {
                    var pool = CombatPotionModelPool();
                    for (var i = 0; i < operation.Amount; i++)
                    {
                        if (ControlledChanceGuardV111.Active &&
                            !ControlledChanceGuardV111
                                .EngineOwnedNativeProviderActive)
                            throw new NotSupportedException(
                                "UNSUPPORTED_CONTROLLED_CHANCE " +
                                "site=combat.potion_generation.random_model " +
                                "operation=choose_index requires a saved potion continuation.");
                        var native = combat.Stochastic.NativeRng;
                        var streamId = native is null
                            ? "combat"
                            : "run.combat_potion_generation";
                        var streams = native?.RunStreams.Fork() ?? rng;
                        var stream = streams.GetStream(streamId);
                        var callOrdinal = StochasticProviderScopeV111
                            .GetNextOrdinal(streams, streamId) is { } scopedOrdinal
                                ? (ulong)scopedOrdinal
                                : (stream.CallCount ?? 0);
                        if (callOrdinal > int.MaxValue)
                            throw new NotSupportedException(
                                "Combat potion generation ordinal exceeds the request range.");
                        IRandomIndexProviderV111 provider = native is null
                            ? new LegacyPrototypeRandomIndexProviderV111()
                            : new NativeRandomIndexProviderV111();
                        var ownerId = "effect:" + operation.Kind + ":source:" +
                            (sourceCardInstanceId?.ToString() ?? "none") +
                            ":pool:" + CanonicalJson.Sha256(pool);
                        var request = new RandomIndexRequestV111(
                            StochasticSiteRegistryV111
                                .CombatPotionGenerationRandomModelSiteId,
                            streamId, ownerId, (int)callOrdinal,
                            Enumerable.Range(0, pool.Length).ToArray());
                        var decision = StochasticProviderScopeV111.ResolveIndex(
                            provider, request, streams);
                        if (decision.ProviderMode == "native-v111" &&
                            RandomBoundaryJournalV111.HasActiveJournal)
                            RandomBoundaryJournalV111.RecordPotionGenerationOutcome(
                                SemanticPotionGenerationNativeAuditV111.Create(
                                    decision, pool));
                        if (native is not null)
                            combat = combat with
                            {
                                Stochastic =
                                    CombatStochasticStateV111.WithNativeRng(
                                        combat.Stochastic,
                                        native with
                                        {
                                            RunStreams = streams
                                        })
                            };
                        var generated = pool[decision.SelectedIndex];
                        var slots = (PotionInstance?[])player.PotionSlots.Clone();
                        var firstEmpty = Array.FindIndex(slots, item => item is null);
                        if (firstEmpty < 0)
                        {
                            continue;
                        }

                        var potion = new PotionInstance(
                            generated, PrototypeJson.EmptyObject());
                        slots[firstEmpty] = potion;
                        player = player with { PotionSlots = slots };
                        combat = combat with
                        {
                            Potions = combat.PotionStates
                                .Append(new CombatPotionState(
                                    firstEmpty, generated,
                                    potion.PersistentState.Clone()))
                                .ToArray()
                        };
                    }
                    break;
                }

                case PrototypeCombatEffectKind.IncreaseSourcePowerStacks:
                {
                    if (operation.SourcePowerApplicationOrder is not { } order)
                    {
                        throw new InvalidOperationException(
                            "Power growth requires source application order.");
                    }

                    combat = combat with
                    {
                        PlayerPowers = combat.PlayerPowers.Select(power =>
                            power.ApplicationOrder == order
                                ? power with
                                {
                                    Stacks = power.Stacks
                                        + operation.Amount
                                }
                                : power).ToArray()
                    };
                    break;
                }

                case PrototypeCombatEffectKind.CreateCardsInHand:
                {
                    if (operation.CardId is null)
                    {
                        throw new InvalidOperationException("Create-card operation is missing a card ID.");
                    }

                    combat = AddGeneratedCardCopies(
                        combat,
                        new PrototypeCombatCardSnapshot(
                            operation.CardId,
                            operation.GeneratedCardUpgradeLevel,
                            PrototypeJson.EmptyObject(),
                            Enchantment:
                                operation.GeneratedCardEnchantment),
                        operation.Amount);
                    break;
                }

                case PrototypeCombatEffectKind.CreateCardsInHandFromPowerCardPayload:
                {
                    var snapshot = operation.PowerCardPayload
                        ?? throw new InvalidOperationException(
                            "Power-payload card generation requires a stored card snapshot.");
                    combat = AddGeneratedCardCopies(
                        combat,
                        snapshot,
                        operation.Amount);
                    break;
                }

                case PrototypeCombatEffectKind.CreateEventSourceCardCopyInHand:
                {
                    if (eventSourceCardInstanceId is null)
                    {
                        throw new InvalidOperationException(
                            "Event-source card copy requires a source card.");
                    }

                    var source = RequireCombatCard(
                        combat,
                        eventSourceCardInstanceId.Value);
                    var firstGeneratedCardId = combat.NextCardInstanceId;
                    combat = AddGeneratedCardCopies(
                        combat,
                        new PrototypeCombatCardSnapshot(
                            source.CardId,
                            source.UpgradeLevel,
                            source.State.Clone(),
                            source.CombatEnergyCostDelta,
                            source.TemporaryEnergyCost is null
                                ? null
                                : source.TemporaryEnergyCost with { },
                            source.KeywordOverrides is null
                                ? null
                                : source.KeywordOverrides
                                    .Select(item => item with { })
                                    .ToArray(),
                            source.ReplayCount,
                            source.Enchantment,
                            source.EnchantmentTriggeredThisCombat,
                            source.Affliction,
                            source.SuppressedUpgradeLevels),
                        operation.Amount);
                    if (operation.EventSourceCardKeyword is { } keywordOverride)
                    {
                        for (var generatedCardId = firstGeneratedCardId;
                             generatedCardId < combat.NextCardInstanceId;
                             generatedCardId++)
                        {
                            combat = ApplyCardKeywordOverride(
                                combat, generatedCardId, keywordOverride);
                        }
                    }
                    break;
                }

                case PrototypeCombatEffectKind.AutoPlayTaggedCardsFromZone:
                {
                    if (operation.AutoPlaySourceZone is null
                        || operation.RequiredCardTag is null)
                    {
                        throw new InvalidOperationException(
                            "Tagged autoplay requires a source zone and card tag.");
                    }

                    var sourceZone =
                        operation.AutoPlaySourceZone.Value;
                    var autoPlayIds = GetZone(
                            combat,
                            sourceZone)
                        .Where(instanceId =>
                        {
                            var candidate =
                                RequireCombatCard(
                                    combat,
                                    instanceId);
                            var candidateDefinition =
                                PrototypeContent.Card(
                                    candidate.CardId);
                            return (candidateDefinition.Tags
                                    ?? Array.Empty<string>())
                                .Contains(
                                    operation.RequiredCardTag,
                                    StringComparer.Ordinal);
                        })
                        .ToArray();

                    if (autoPlayIds.Length == 0)
                    {
                        break;
                    }

                    combat = MoveCards(
                        combat, sourceZone, PrototypeCardZone.PlayPile,
                        autoPlayIds);

                    operations = new Queue<
                        PrototypeQueuedOperation>(
                        autoPlayIds.Select(id =>
                            new PrototypeQueuedOperation(
                                PrototypeCombatEffectKind
                                    .AutoPlayCombatCard,
                                0,
                                TargetEnemyId:
                                    targetEnemyId,
                                SourceKind:
                                    operation.SourceKind,
                                UpgradeAutoPlayedCardsBeforePlay:
                                    operation
                                        .UpgradeAutoPlayedCardsBeforePlay,
                                CardInstanceId: id))
                        .Concat(operations));
                    break;
                }

                case PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone:
                {
                    if (operation.Amount <= 0
                        || AllEnemiesDefeated(combat))
                    {
                        break;
                    }

                    var zone = operation.AutoPlaySourceZone
                        ?? throw new InvalidOperationException(
                            "Random autoplay requires a source pile.");
                    var eligible = ControlledCardShortlistCandidates(
                        combat, operation) ?? throw new InvalidDataException(
                            "Random auto-play lost its eligible card pool.");

                    if (eligible.Length == 0)
                    {
                        break;
                    }

                    (CombatState Combat, int SelectedIndex,
                        long? SelectedCardInstanceId) selection;
                    if (ControlledChanceGuardV111.Active &&
                        !ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
                        eligible.Length > 1)
                    {
                        var continuation =
                            new PrototypeChoiceResolutionContinuationState(
                                SourceCardInstanceId: sourceCardInstanceId,
                                SourceCardDestination: sourceCardDestination,
                                Operations: operations.ToArray(),
                                PendingDiscardEvents: [],
                                PendingSlyCardInstanceIds: [],
                                CompletionEvents: completionEvents ?? [],
                                CardPlaySeries: cardPlaySeries,
                                MoveSourceCardOnCompletion:
                                    moveSourceCardOnCompletion,
                                RemoveSourceCardOnCompletion:
                                    removeSourceCardOnCompletion,
                                SourceCardAlreadyMoved:
                                    sourceCardAlreadyMoved,
                                EventDispatchContinuation:
                                    eventDispatchContinuation?.Fork());
                        if (TryMakeControlledCardShortlistChoice(
                            combat, operation, eligible, continuation,
                            out var suspended))
                            return (player, suspended);
                    }
                    if (ControlledChanceGuardV111.Active &&
                        !ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
                        eligible.Length == 1)
                        selection = (combat, 0, null);
                    else
                        selection = ResolveRandomCombatCardIndex(
                            combat, rng,
                            StochasticSiteRegistryV111
                                .CombatAutoplayRandomCardSelectSiteId,
                            "combat",
                            eligible,
                            "effect:" + operation.Kind + ":source:" +
                                (sourceCardInstanceId?.ToString() ?? "none") +
                                ":zone:" + zone);
                    combat = selection.Combat;
                    var selected = eligible[selection.SelectedIndex];
                    combat = MoveCards(
                        combat, zone, PrototypeCardZone.PlayPile,
                        [selected]);

                    // Re-evaluate the source pile after each autoplay,
                    // because the played card can draw or mutate cards.
                    operations = new Queue<PrototypeQueuedOperation>(
                        new[]
                        {
                            new PrototypeQueuedOperation(
                                PrototypeCombatEffectKind.AutoPlayCombatCard,
                                0, SourceKind: operation.SourceKind,
                                CardInstanceId: selected),
                            operation with { Amount = operation.Amount - 1 }
                        }.Concat(operations));
                    break;
                }

                case PrototypeCombatEffectKind.AutoPlayTopDrawCards:
                {
                    var semanticDeck = combat.Stochastic.SemanticDeck;
                    var natureAutoplay = semanticDeck is
                        { Mode: SemanticDeckStateV111.NatureMode };
                    if (natureAutoplay && !ControlledChanceGuardV111.Active)
                        throw new NotSupportedException(
                            "UNSUPPORTED_SEMANTIC_DECK " +
                            "site=combat.autoplay.top_draw " +
                            "operation=requires_controlled_draw_continuation");
                    if (natureAutoplay &&
                        combat.AutomaticPipelineContinuation is not null &&
                        !IsMayhemTopAutoplayPipelineOperation(
                            combat, operation))
                        throw new NotSupportedException(
                            "UNSUPPORTED_SEMANTIC_DECK " +
                            "site=combat.autoplay.top_draw " +
                            "operation=automatic_pipeline_continuation_unavailable");
                    if (semanticDeck is
                        { Mode: SemanticDeckStateV111.NativeMode })
                        SemanticDrawPileAdapterV111.ValidateSynchronized(
                            combat, requireIdle: true);
                    var count = Math.Max(
                        0,
                        operation.Amount);
                    var draw =
                        combat.DrawPile.ToList();
                    var discard =
                        combat.DiscardPile.ToList();
                    if (natureAutoplay)
                        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
                            combat,
                            "Nature semantic deck is missing before top-draw autoplay.",
                            "Nature semantic deck is stale before top-draw autoplay.");
                    var staged = natureAutoplay
                        ? (operation.DeferredDrawnCardInstanceIds ?? []).ToList()
                        : new List<long>();
                    if (staged.Distinct().Count() != staged.Count ||
                        staged.Any(id => !combat.PlayCardIds.Contains(id)))
                        throw new InvalidDataException(
                            "Top-draw autoplay continuation has stale staged cards.");
                    var previouslyStagedCount = staged.Count;
                    int committedStagedCount = previouslyStagedCount;
                    var resolvedTopCards = new List<
                        DrawPileTopCardResolutionV111>();

                    for (var index = 0;
                         index < count;
                         index++)
                    {
                        if (draw.Count == 0
                            && discard.Count > 0)
                        {
                            combat = CommitTopAutoplayDrawCheckpoint(
                                combat, draw, discard,
                                staged.Skip(committedStagedCount).ToArray(),
                                resolvedTopCards);
                            committedStagedCount = staged.Count;
                            semanticDeck = combat.Stochastic.SemanticDeck;
                            draw = combat.DrawPile.ToList();
                            discard = combat.DiscardPile.ToList();
                            resolvedTopCards.Clear();

                            if (semanticDeck?.Mode ==
                                SemanticDeckStateV111.NatureMode)
                            {
                                var recycled = discard.ToArray();
                                var nextPhysicalDrawPile =
                                    draw.Concat(recycled).ToArray();
                                combat = CommitNatureDrawPileRecycle(
                                    combat, nextPhysicalDrawPile);
                                semanticDeck = combat.Stochastic.SemanticDeck!;
                                draw = combat.DrawPile.ToList();
                                discard = combat.DiscardPile.ToList();
                                var afterShuffleInsertions =
                                    ControlledAfterShuffleRelicInsertions(
                                        combat);
                                if (afterShuffleInsertions.Length > 0)
                                {
                                    var resumeOperation = operation with
                                    {
                                        Amount = count - index,
                                        DeferredDrawnCardInstanceIds =
                                            (long[])staged.ToArray().Clone()
                                    };
                                    var continuation = new
                                        PrototypeChoiceResolutionContinuationState(
                                            SourceCardInstanceId:
                                                sourceCardInstanceId,
                                            SourceCardDestination:
                                                sourceCardDestination,
                                            Operations: new[] { resumeOperation }
                                                .Concat(operations).ToArray(),
                                            PendingDiscardEvents: [],
                                            PendingSlyCardInstanceIds: [],
                                            CompletionEvents:
                                                completionEvents ?? [],
                                            CardPlaySeries: cardPlaySeries,
                                            MoveSourceCardOnCompletion:
                                                moveSourceCardOnCompletion,
                                            RemoveSourceCardOnCompletion:
                                                removeSourceCardOnCompletion,
                                            SourceCardAlreadyMoved:
                                                sourceCardAlreadyMoved,
                                            EventDispatchContinuation:
                                                eventDispatchContinuation?.Fork());
                                    return (player,
                                        MakeControlledAfterShuffleInsertionChoice(
                                            combat,
                                            afterShuffleInsertions[0],
                                            afterShuffleInsertions.Skip(1)
                                                .ToArray(), [], null,
                                            continuation, ordinal: 0));
                                }
                            }
                            else if (semanticDeck is
                                { Mode: SemanticDeckStateV111.NativeMode })
                            {
                                combat = CommitNativeSemanticRecycle(
                                    combat, "combat.autoplay.top_draw");
                                semanticDeck = combat.Stochastic.SemanticDeck!;
                                draw = combat.DrawPile.ToList();
                                discard = combat.DiscardPile.ToList();
                            }
                            else
                            {
                                combat = CommitLegacyDrawPileRecycle(
                                    combat, rng);
                                semanticDeck = combat.Stochastic.SemanticDeck;
                                draw = combat.DrawPile.ToList();
                                discard = combat.DiscardPile.ToList();
                            }
                            discard.Clear();
                            if (semanticDeck is not
                                { Mode: SemanticDeckStateV111.NatureMode })
                            {
                                combat = ApplyRelicAfterShuffleCards(combat, rng);
                                semanticDeck = combat.Stochastic.SemanticDeck;
                                draw = combat.DrawPile.ToList();
                                discard = combat.DiscardPile.ToList();
                            }
                        }

                        if (draw.Count == 0)
                        {
                            break;
                        }

                        var resolvedTop =
                            SemanticDrawPileAdapterV111.ResolveTopCard(
                                semanticDeck, draw);
                        resolvedTopCards.Add(resolvedTop);
                        semanticDeck = resolvedTop.Deck;
                        if (resolvedTop.IsPending)
                        {
                            if (semanticDeck?.Mode !=
                                    SemanticDeckStateV111.NatureMode)
                                throw new InvalidDataException(
                                    "Only a Nature top-card reveal may suspend autoplay.");
                            combat = CommitTopAutoplayDrawCheckpoint(
                                combat, draw, discard,
                                staged.Skip(committedStagedCount).ToArray(),
                                resolvedTopCards);
                            var remainingCount = count - index;
                            var resumeOperation = operation with
                            {
                                Amount = remainingCount,
                                DeferredDrawnCardInstanceIds =
                                    (long[])staged.ToArray().Clone()
                            };
                            var continuation =
                                new PrototypeChoiceResolutionContinuationState(
                                    SourceCardInstanceId: sourceCardInstanceId,
                                    SourceCardDestination: sourceCardDestination,
                                    Operations: new[] { resumeOperation }
                                        .Concat(operations).ToArray(),
                                    PendingDiscardEvents: [],
                                    PendingSlyCardInstanceIds: [],
                                    CompletionEvents: completionEvents ?? [],
                                    CardPlaySeries: cardPlaySeries,
                                    MoveSourceCardOnCompletion:
                                        moveSourceCardOnCompletion,
                                    RemoveSourceCardOnCompletion:
                                        removeSourceCardOnCompletion,
                                    SourceCardAlreadyMoved:
                                        sourceCardAlreadyMoved,
                                    EventDispatchContinuation:
                                        eventDispatchContinuation?.Fork());
                            return (player, MakeControlledSemanticDrawChoice(
                                combat,
                                new PrototypeControlledSemanticDrawV111(
                                    remainingCount,
                                    FromHandDraw: false,
                                    EventDepth: eventDepth,
                                    DrawnCardKeyword: null),
                                continuation));
                        }
                        var cardId = resolvedTop.CardInstanceId!.Value;
                        draw = resolvedTop.RemainingPhysicalDrawPile.ToList();
                        staged.Add(cardId);
                    }

                    if (staged.Count == 0)
                    {
                        break;
                    }

                    combat = CommitTopAutoplayDrawCheckpoint(
                        combat, draw, discard,
                        staged.Skip(committedStagedCount).ToArray(),
                        resolvedTopCards);

                    operations = new Queue<
                        PrototypeQueuedOperation>(
                        staged.Select(id =>
                            new PrototypeQueuedOperation(
                                PrototypeCombatEffectKind
                                    .AutoPlayCombatCard,
                                0,
                                SourceKind:
                                    operation.SourceKind,
                                CardInstanceId: id))
                        .Concat(operations));
                    break;
                }

                case PrototypeCombatEffectKind.AutoPlayPlayableCardsFromHand:
                {
                    if (operation.Amount <= 0
                        || combat.Turn != 1
                        || !combat.IsPlayerTurn
                        || player.Hp <= 0
                        || AllEnemiesDefeated(combat))
                    {
                        break;
                    }

                    var playableCard = combat.Hand
                        .Select(id => RequireCombatCard(combat, id))
                        .FirstOrDefault(candidate =>
                        {
                            var definition = RuntimeCardDefinition(candidate);
                            return definition.MechanicsImplemented
                                && !definition.Unplayable
                                && CanPlayAnotherCardThisTurn(
                                    player, combat, candidate)
                                && IsCardAffordable(
                                    combat, candidate, definition);
                        });
                    if (playableCard is null)
                    {
                        break;
                    }

                    var playableDefinition =
                        RuntimeCardDefinition(playableCard);
                    int? targetEnemy = null;
                    if (EffectiveCardTarget(combat, playableDefinition)
                        == PrototypeCardTarget.Enemy)
                    {
                        targetEnemy = combat.Enemies.FirstOrDefault(enemy =>
                            enemy.Hp > 0 && !enemy.Escaped)?.InstanceId;
                        if (targetEnemy is null)
                        {
                            break;
                        }
                    }

                    var xValue = playableDefinition.Cost.Kind
                            == PrototypeCardCostKind.X
                        ? combat.Energy + player.ActiveRelics.Sum(relic =>
                            PrototypeContent.Relic(relic.RelicId)
                                .XValueBonus)
                        : 0;
                    var energyCost = playableDefinition.Cost.Kind
                            == PrototypeCardCostKind.X
                        ? combat.Energy
                        : ResolveFixedCardEnergyCost(
                            combat, playableCard, playableDefinition);
                    combat = combat with
                    {
                        Energy = Math.Max(0, combat.Energy - energyCost)
                    };
                    combat = MoveCards(
                        combat, PrototypeCardZone.Hand,
                        PrototypeCardZone.PlayPile,
                        [playableCard.InstanceId]);

                    var remaining = operation with
                    {
                        Amount = operation.Amount - 1
                    };
                    operations = new Queue<PrototypeQueuedOperation>(
                        new[]
                        {
                            new PrototypeQueuedOperation(
                                PrototypeCombatEffectKind.AutoPlayCombatCard,
                                0,
                                TargetEnemyId: targetEnemy,
                                SourceKind: operation.SourceKind,
                                CardInstanceId: playableCard.InstanceId,
                                EnergySpent: xValue),
                            remaining
                        }.Concat(operations));
                    break;
                }

                case PrototypeCombatEffectKind.AutoPlayCombatCard:
                {
                    if (operation.CardInstanceId is not
                        { } autoPlayId)
                    {
                        throw new InvalidOperationException(
                            "Autoplay-card operation is missing a card instance.");
                    }

                    if (!combat.PlayCardIds.Contains(
                            autoPlayId))
                    {
                        throw new InvalidOperationException(
                            $"Autoplay card {autoPlayId} is not staged in the play pile.");
                    }

                    var autoPlayCard =
                        RequireCombatCard(
                            combat,
                            autoPlayId);
                    if (operation
                            .UpgradeAutoPlayedCardsBeforePlay
                        && autoPlayCard.UpgradeLevel <= 0)
                    {
                        combat = combat with
                        {
                            Cards = combat.Cards
                                .Select(item =>
                                    item.InstanceId
                                        == autoPlayId
                                        ? item with
                                        {
                                            UpgradeLevel = 1
                                        }
                                        : item)
                                .ToArray()
                        };
                        autoPlayCard =
                            RequireCombatCard(
                                combat,
                                autoPlayId);
                    }

                    var autoPlayDefinition =
                        RuntimeCardDefinition(autoPlayCard);
                    // Unplayable cards have no on-play behavior. The autoplay
                    // stage still moves them to their destination, even when
                    // their *play* mechanics are deliberately unsupported (for
                    // example, persistent quest cards like Dowsing). Validate
                    // playable card mechanics only after this early exit.
                    var autoPlayDestination =
                        CardExhaustsOnUse(
                            autoPlayDefinition,
                            autoPlayCard)
                            ? PrototypeCardZone.ExhaustPile
                            : PrototypeCardZone.DiscardPile;

                    // Native Normality.ShouldPlay also guards
                    // auto-play (cards staged in the play pile) while
                    // Normality stays in hand.
                    if (autoPlayDefinition.Unplayable
                        || NormalityPreventsCardPlay(combat))
                    {
                        combat = MoveCards(
                            combat, PrototypeCardZone.PlayPile,
                            autoPlayDestination, [autoPlayId]);
                        break;
                    }

                    if (!autoPlayDefinition.MechanicsImplemented)
                    {
                        throw new NotSupportedException(
                            $"Autoplay cannot resolve unsupported card '{autoPlayDefinition.Name}'.");
                    }

                    int? autoPlayTarget =
                        operation.TargetEnemyId;
                    if (EffectiveCardTarget(
                            combat,
                            autoPlayDefinition)
                        == PrototypeCardTarget.Enemy)
                    {
                        var liveTargets =
                            combat.Enemies
                                .Where(enemy =>
                                    enemy.Hp > 0)
                                .Select(enemy =>
                                    enemy.InstanceId)
                                .ToArray();
                        if (liveTargets.Length == 0)
                        {
                            combat = MoveCards(
                                combat, PrototypeCardZone.PlayPile,
                                autoPlayDestination, [autoPlayId]);
                            break;
                        }

                        if (autoPlayTarget is null
                            || !liveTargets.Contains(
                                autoPlayTarget.Value))
                        {
                            if (ControlledChanceGuardV111.Active)
                            {
                                var continuation =
                                    new PrototypeChoiceResolutionContinuationState(
                                        SourceCardInstanceId: sourceCardInstanceId,
                                        SourceCardDestination: sourceCardDestination,
                                        Operations: (new[]
                                        {
                                            operation with
                                            {
                                                TargetEnemyId = null
                                            }
                                        }).Concat(operations).ToArray(),
                                        PendingDiscardEvents: [],
                                        PendingSlyCardInstanceIds: [],
                                        CompletionEvents: completionEvents ?? [],
                                        CardPlaySeries: cardPlaySeries,
                                        MoveSourceCardOnCompletion:
                                            moveSourceCardOnCompletion,
                                        RemoveSourceCardOnCompletion:
                                            removeSourceCardOnCompletion,
                                        SourceCardAlreadyMoved:
                                            sourceCardAlreadyMoved,
                                        EventDispatchContinuation:
                                            eventDispatchContinuation?.Fork());
                                return (player, MakeControlledRandomTargetChoice(
                                    combat, "autoplay_random_enemy", liveTargets,
                                    continuation,
                                    autoPlayCardInstanceId: autoPlayId));
                            }
                            var target = ResolveRandomEnemyTarget(
                                combat, rng, liveTargets,
                                StochasticSiteRegistryV111.CombatAutoplayRandomTargetSiteId,
                                "combat_targets",
                                "card:" + autoPlayId,
                                autoPlayTargetOrdinal++);
                            combat = target.Combat;
                            autoPlayTarget = liveTargets[
                                target.SelectedIndex];
                        }
                    }
                    else
                    {
                        autoPlayTarget = null;
                    }

                    combat = SetZone(
                        combat,
                        PrototypeCardZone.PlayPile,
                        combat.PlayCardIds
                            .Where(id =>
                                id != autoPlayId)
                            .ToArray());

                    var autoPlayCount =
                        ResolveCardPlayCountAndConsumeModifiers(
                            combat,
                            autoPlayCard,
                            autoPlayDefinition.Type);
                    combat = autoPlayCount.Combat;
                    var autoPlayXValue =
                        autoPlayDefinition.Cost.Kind
                            == PrototypeCardCostKind.X
                            ? operation.EnergySpent
                                ?? combat.Energy
                                    + player.ActiveRelics.Sum(relic =>
                                        PrototypeContent.Relic(
                                                relic.RelicId)
                                            .XValueBonus)
                            : 0;

                    var autoPlaySeries =
                        new PrototypeCardPlaySeriesState(
                            SourceCardInstanceId:
                                autoPlayId,
                            SourceCardDestination:
                                autoPlayDestination,
                            TargetEnemyId:
                                autoPlayTarget,
                            EnergySpent:
                                autoPlayXValue,
                            PlayCount:
                                autoPlayCount.PlayCount,
                            NextPlayIndex: 0,
                            RemoveSourceCardOnCompletion:
                                autoPlayDefinition.Type
                                    == PrototypeCardType.Power,
                            IsAutoPlay: true);

                    var autoPlayed =
                        ResolveCardPlaySeries(
                            player,
                            combat,
                            rng,
                            autoPlaySeries);
                    player = autoPlayed.Player;
                    combat = autoPlayed.Combat;

                    if (combat.PendingChoice
                        is not null)
                    {
                        var outerContinuation =
                            new PrototypeChoiceResolutionContinuationState(
                                SourceCardInstanceId:
                                    sourceCardInstanceId,
                                SourceCardDestination:
                                    sourceCardDestination,
                                Operations:
                                    operations.ToArray(),
                                PendingDiscardEvents:
                                    Array.Empty<
                                        PrototypeCombatEvent>(),
                                PendingSlyCardInstanceIds:
                                    Array.Empty<long>(),
                                CompletionEvents:
                                    completionEvents is null
                                        ? Array.Empty<
                                            PrototypeCombatEvent>()
                                        : (PrototypeCombatEvent[])
                                            completionEvents.Clone(),
                                CardPlaySeries:
                                    cardPlaySeries,
                                MoveSourceCardOnCompletion:
                                    moveSourceCardOnCompletion,
                                RemoveSourceCardOnCompletion:
                                    removeSourceCardOnCompletion,
                                SourceCardAlreadyMoved:
                                    sourceCardAlreadyMoved,
                                EventDispatchContinuation:
                                    eventDispatchContinuation?.Fork());
                        combat =
                            AttachOuterChoiceContinuation(
                                combat,
                                outerContinuation);
                        return (player, combat);
                    }

                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (combat.PendingChoice is not null)
                throw new InvalidOperationException(
                    "ResolveOperations cannot continue after a pending choice; " +
                    "the suspending operation must return its continuation.");
        }

        var sourceExhaustEvent =
            sourceCardInstanceId is not null
            && moveSourceCardOnCompletion
            && !sourceCardAlreadyMoved
            && !removeSourceCardOnCompletion
            && sourceCardDestination
                == PrototypeCardZone.ExhaustPile
                ? new PrototypeCombatEvent(
                    PrototypeCombatEventKind.CardExhausted,
                    SourceCardInstanceId:
                        sourceCardInstanceId.Value,
                    CardId: RequireCombatCard(
                        combat,
                        sourceCardInstanceId.Value)
                        .CardId)
                : null;

        if (sourceCardInstanceId is not null
            && moveSourceCardOnCompletion)
        {
            if (!sourceCardAlreadyMoved
                && !removeSourceCardOnCompletion)
            {
                var finalZone = sourceCardDestination;
                if (finalZone == PrototypeCardZone.DiscardPile)
                {
                    var source = RequireCombatCard(
                        combat, sourceCardInstanceId.Value);
                    var sourceType = PrototypeContent.Card(source.CardId).Type;
                    if (sourceType is PrototypeCardType.Attack
                        or PrototypeCardType.Skill)
                    {
                        var alreadyPlayed = combat.CounterState
                            .AttacksPlayedThisTurn
                            + combat.CounterState.SkillsPlayedThisTurn;
                        if (combat.PlayerPowers.Any(power =>
                            power.Stacks > alreadyPlayed
                            && PrototypeContent.Power(power.PowerId)
                                .PlayedAttacksAndSkillsReturnToDraw))
                        {
                            finalZone = PrototypeCardZone.DrawPile;
                        }
                    }
                }

                var destination = GetZone(combat, finalZone);
                combat = SetZone(
                    combat, finalZone,
                    destination.Append(sourceCardInstanceId.Value).ToArray());
            }

            combat = combat with { PendingChoice = null };
        }

        var completionEventArray =
            sourceExhaustEvent is null
                ? completionEvents
                    ?? Array.Empty<PrototypeCombatEvent>()
                : new[] { sourceExhaustEvent }
                    .Concat(
                        completionEvents
                        ?? Array.Empty<PrototypeCombatEvent>())
                    .ToArray();
        for (var completionEventIndex = 0;
             completionEventIndex < completionEventArray.Length;
             completionEventIndex++)
        {
            var dispatched = DispatchCombatEvent(
                player,
                combat,
                completionEventArray[completionEventIndex],
                rng,
                eventDepth + 1,
                allowSuspension: true);
            player = dispatched.Player;
            combat = dispatched.Combat;

            if (combat.PendingChoice is not null)
            {
                var outerContinuation =
                    new PrototypeChoiceResolutionContinuationState(
                        SourceCardInstanceId: sourceCardInstanceId,
                        SourceCardDestination: sourceCardDestination,
                        Operations:
                            Array.Empty<PrototypeQueuedOperation>(),
                        PendingDiscardEvents:
                            Array.Empty<PrototypeCombatEvent>(),
                        PendingSlyCardInstanceIds:
                            Array.Empty<long>(),
                        CompletionEvents:
                            completionEventArray
                                .Skip(completionEventIndex + 1)
                                .ToArray(),
                        CardPlaySeries: cardPlaySeries,
                        MoveSourceCardOnCompletion:
                            moveSourceCardOnCompletion,
                        RemoveSourceCardOnCompletion:
                            removeSourceCardOnCompletion,
                        SourceCardAlreadyMoved:
                            sourceCardAlreadyMoved
                            || (sourceCardInstanceId is not null
                                && moveSourceCardOnCompletion
                                && !removeSourceCardOnCompletion));
                combat = AttachOuterChoiceContinuation(
                    combat,
                    outerContinuation);
                return (player, combat);
            }
        }

        if (sourceCardInstanceId is not null
            && moveSourceCardOnCompletion)
        {
            combat = ClearTemporaryCardCost(
                combat,
                sourceCardInstanceId.Value,
                PrototypeTemporaryCardCostExpiry.WhenPlayed);
            combat = ClearTemporaryCardKeywordOverrides(
                combat,
                sourceCardInstanceId.Value,
                PrototypeCardKeywordOverrideExpiry.WhenPlayed);

            if (removeSourceCardOnCompletion)
            {
                combat = combat with
                {
                    Cards = combat.Cards
                        .Where(card =>
                            card.InstanceId != sourceCardInstanceId.Value)
                        .ToArray()
                };
            }
        }

        return (player, combat);
    }

    private static (PlayerState Player, CombatState Combat)
        ResolveDamageAllEnemiesRepeatPerKill(
            PlayerState player,
            CombatState combat,
            PrototypeQueuedOperation operation,
            RngBundle rng,
            long? sourceCardInstanceId,
            int eventDepth,
            PrototypeChoiceResolutionContinuationState effectContinuation,
            PrototypeRepeatedDamageContinuationV111? savedProgress = null)
    {
        if (operation.Kind !=
            PrototypeCombatEffectKind.DamageAllEnemiesRepeatPerKill)
            throw new ArgumentException(
                "Repeated damage continuation needs its source operation.",
                nameof(operation));

        var pendingRounds = savedProgress?.PendingRounds ?? 1;
        var roundTargets = savedProgress?.CurrentRoundTargetIds;
        var nextTargetIndex = savedProgress?.NextTargetIndex ?? 0;
        while (player.Hp > 0 &&
               (roundTargets is not null || pendingRounds > 0))
        {
            if (roundTargets is null)
            {
                pendingRounds--;
                roundTargets = combat.Enemies
                    .Where(enemy => enemy.Hp > 0)
                    .Select(enemy => enemy.InstanceId)
                    .ToArray();
                nextTargetIndex = 0;
                if (roundTargets.Length == 0)
                {
                    roundTargets = null;
                    continue;
                }
            }

            for (; nextTargetIndex < roundTargets.Length;
                 nextTargetIndex++)
            {
                var enemyId = roundTargets[nextTargetIndex];
                var enemyBeforeHit = combat.Enemies.Single(
                    enemy => enemy.InstanceId == enemyId);
                if (enemyBeforeHit.Hp <= 0)
                    continue;

                if (operation.IsPoweredAttack)
                {
                    var retaliation = EnemyAttackRetaliation(
                        combat, enemyId);
                    if (retaliation > 0)
                    {
                        var absorbed = Math.Min(
                            combat.PlayerBlock, retaliation);
                        combat = combat with
                        {
                            PlayerBlock = combat.PlayerBlock - absorbed
                        };
                        player = player with
                        {
                            Hp = Math.Max(0, player.Hp - Math.Max(
                                0, retaliation - absorbed))
                        };
                        if (player.Hp <= 0)
                        {
                            var prevented =
                                TryUseAutomaticDeathPrevention(
                                    player, combat, rng, eventDepth);
                            player = prevented.Player;
                            combat = prevented.Combat;
                            if (player.Hp <= 0)
                                break;
                        }
                    }
                }

                var damageAmount = operation.Amount;
                if (operation.IsPoweredAttack)
                {
                    damageAmount = ModifyPlayerAttackDamage(
                        combat, sourceCardInstanceId, enemyId,
                        damageAmount);
                    damageAmount = ModifyIncomingAttackDamage(
                        combat, enemyId, damageAmount);
                }

                var targetBlock = combat.Enemies.Single(
                    enemy => enemy.InstanceId == enemyId).Block;
                var damageResult = DamageEnemyInternal(
                    combat, enemyId, damageAmount,
                    operation.IsPoweredAttack
                        ? MinimumPoweredAttackHpLoss(player) : 0,
                    operation.IsPoweredAttack);
                combat = damageResult.Combat;
                if (targetBlock > 0 && damageAmount >= targetBlock)
                    combat = ApplyPlayerEnemyBlockBrokenRelics(
                        player, combat, enemyId);
                if (damageResult.Defeated)
                {
                    pendingRounds++;
                    combat = CleanupSourceBoundPowersForDefeatedEnemies(
                        combat);
                }

                var resumeProgress = new PrototypeRepeatedDamageContinuationV111(
                    operation, pendingRounds,
                    (int[])roundTargets.Clone(), nextTargetIndex + 1,
                    eventDepth);
                var damageEvents =
                    DispatchEnemyDamageEventsAfterHit(
                        player, combat, rng, operation, enemyId,
                        damageResult.DamageDealt,
                        damageResult.Defeated, fatalEligible: false,
                        eventDepth: eventDepth,
                        effectContinuation: effectContinuation,
                        remainingOperations:
                            effectContinuation.Operations,
                        repeatedDamageContinuation: resumeProgress);
                player = damageEvents.Player;
                combat = damageEvents.Combat;
                if (combat.PendingChoice is not null)
                    return (player, combat);
            }

            roundTargets = null;
            nextTargetIndex = 0;
        }

        return (player, combat);
    }

    private static (PlayerState Player, CombatState Combat)
        ContinueDamageEnemyAfterHit(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            PrototypePersonalHiveDamageContinuationV111 continuation,
            long? sourceCardInstanceId,
            Queue<PrototypeQueuedOperation> operations,
            PrototypeChoiceResolutionContinuationState
                effectContinuation)
    {
        var operation = continuation.Operation;
        var targetEnemyId = continuation.TargetEnemyId;
        var splashTarget = continuation.SplashTarget;
        var damageAmount = continuation.DamageAmount;
        var overkill = continuation.Overkill;
        var damageDealt = continuation.DamageDealt;
        var defeated = continuation.Defeated;

        // CurlUpPower records the attack's physical card source even on a
        // fully blocked hit, then fires AFTER that complete card play.
        if (operation.IsPoweredAttack &&
            sourceCardInstanceId is { } attackCardId &&
            splashTarget.Hp > 0)
        {
            combat = combat with
            {
                Enemies = combat.Enemies.Select(enemy =>
                    enemy.InstanceId == targetEnemyId && enemy.Hp > 0
                        ? enemy with
                        {
                            Powers = enemy.PowerStates.Select(power =>
                                power.PendingAttackingCardInstanceId is null &&
                                PrototypeContent.Power(power.PowerId)
                                    .EnemyBlockAfterAttackingCardPlayedPerStack > 0
                                    ? power with
                                    {
                                        PendingAttackingCardInstanceId =
                                            attackCardId
                                    }
                                    : power).ToArray()
                        }
                        : enemy).ToArray()
            };
        }
        if (splashTarget.Block > 0 && damageAmount >= splashTarget.Block)
            combat = ApplyPlayerEnemyBlockBrokenRelics(
                player, combat, targetEnemyId);
        if (operation.SplashUnpoweredAttackToOtherEnemies &&
            splashTarget.Hp > 0)
        {
            var echoAmount = Math.Max(0, damageAmount + overkill);
            var echoes = combat.Enemies
                .Where(enemy => enemy.Hp > 0 &&
                    enemy.InstanceId != targetEnemyId)
                .Select(enemy => new PrototypeQueuedOperation(
                    PrototypeCombatEffectKind.DamageEnemy,
                    echoAmount, TargetEnemyId: enemy.InstanceId));
            var queued = echoes.Concat(operations).ToArray();
            operations.Clear();
            foreach (var echo in queued) operations.Enqueue(echo);
        }

        if (operation.GainBlockEqualToAttackDamage)
        {
            var blockAmount = Math.Max(0, damageAmount);
            if (sourceCardInstanceId is { } blockCardId)
            {
                var blockEnchantment = RequireCombatCard(
                    combat, blockCardId).Enchantment;
                if (blockEnchantment is
                    { Kind: PrototypeCardEnchantmentKind.Nimble
                        or PrototypeCardEnchantmentKind.Goopy })
                {
                    blockAmount = Math.Max(0, checked(
                        blockAmount + blockEnchantment.Amount
                        - (blockEnchantment.Kind ==
                            PrototypeCardEnchantmentKind.Goopy ? 1 : 0)));
                }
            }
            var blockResult = ModifyPlayerBlockGainWithLegion(
                combat, blockAmount,
                fromCard: true, sourceCardInstanceId);
            combat = blockResult.Combat with
            {
                PlayerBlock = blockResult.Combat.PlayerBlock +
                    Math.Max(0, blockResult.Amount)
            };
        }
        if (operation.IsPoweredAttack && sourceCardInstanceId is not null &&
            damageDealt > 0)
        {
            var reactiveEnemies = combat.Enemies
                .Select(item => item.Fork()).ToArray();
            var reactiveIndex = Array.FindIndex(reactiveEnemies,
                item => item.InstanceId == targetEnemyId);
            if (reactiveIndex >= 0)
            {
                var target = reactiveEnemies[reactiveIndex];
                if (target.Hp > 0 && !target.GainedReactiveBlockThisTurn)
                {
                    var reactiveBlock = target.PowerStates.Sum(power =>
                        power.Stacks * PrototypeContent.Power(power.PowerId)
                            .EnemyBlockAfterFirstUnblockedCardAttackPerStack);
                    if (reactiveBlock > 0)
                    {
                        reactiveEnemies[reactiveIndex] = target with
                        {
                            Block = target.Block + reactiveBlock,
                            GainedReactiveBlockThisTurn = true
                        };
                        combat = combat with { Enemies = reactiveEnemies };
                    }
                }
            }
        }
        if (defeated)
            combat = CleanupSourceBoundPowersForDefeatedEnemies(combat);

        return DispatchEnemyDamageEventsAfterHit(
            player, combat, rng, operation, targetEnemyId, damageDealt,
            defeated, continuation.FatalEligible,
            continuation.EventDepth, effectContinuation,
            operations.ToArray());
    }

    private static (PlayerState Player, CombatState Combat)
        DispatchEnemyDamageEventsAfterHit(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            PrototypeQueuedOperation operation,
            int targetEnemyId,
            int damageDealt,
            bool defeated,
            bool fatalEligible,
            int eventDepth,
            PrototypeChoiceResolutionContinuationState effectContinuation,
            PrototypeQueuedOperation[] remainingOperations,
            PrototypeRepeatedDamageContinuationV111?
                repeatedDamageContinuation = null)
    {
        if (damageDealt > 0)
        {
            var damaged = DispatchCombatEvent(
                player, combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.EnemyDamaged,
                    TargetEnemyId: targetEnemyId,
                    Amount: damageDealt),
                rng, eventDepth + 1, allowSuspension: true);
            player = damaged.Player;
            combat = damaged.Combat;
            if (combat.PendingChoice is not null)
            {
                var suspended = effectContinuation with
                {
                    Operations =
                        (PrototypeQueuedOperation[])
                            remainingOperations.Clone(),
                    PendingDiscardEvents = defeated
                        ?
                        [new PrototypeCombatEvent(
                            PrototypeCombatEventKind.EnemyDefeated,
                            TargetEnemyId: targetEnemyId,
                            Amount: damageDealt)]
                        : [],
                    EnemyDefeatFollowUp = defeated
                        ? new PrototypeEnemyDefeatFollowUpV111(
                            operation, fatalEligible, eventDepth,
                            targetEnemyId)
                        : null,
                    DamageRepeatContinuation =
                        repeatedDamageContinuation?.Fork()
                };
                return (player, AttachOuterChoiceContinuation(
                    combat, suspended));
            }
        }

        if (!defeated)
            return (player, combat);

        var defeatedResult = DispatchCombatEvent(
            player, combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.EnemyDefeated,
                TargetEnemyId: targetEnemyId,
                Amount: damageDealt),
            rng, eventDepth + 1, allowSuspension: true);
        player = defeatedResult.Player;
        combat = defeatedResult.Combat;
        if (combat.PendingChoice is not null)
        {
            var suspended = effectContinuation with
            {
                Operations =
                    (PrototypeQueuedOperation[])remainingOperations.Clone(),
                PendingDiscardEvents = [],
                EnemyDefeatFollowUp =
                    new PrototypeEnemyDefeatFollowUpV111(
                        operation, fatalEligible, eventDepth,
                        targetEnemyId),
                DamageRepeatContinuation =
                    repeatedDamageContinuation?.Fork()
            };
            return (player, AttachOuterChoiceContinuation(
                combat, suspended));
        }

        var completedDefeat = CompleteEnemyDefeatFollowUp(
            player, combat, rng,
            new PrototypeEnemyDefeatFollowUpV111(
                operation, fatalEligible, eventDepth, targetEnemyId));
        return completedDefeat;
    }

    private static (PlayerState Player, CombatState Combat)
        CompleteEnemyDefeatFollowUp(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            PrototypeEnemyDefeatFollowUpV111 continuation)
    {
        combat = ResolveEnemyDeathSummons(combat, rng);
        var operation = continuation.Operation;
        if (continuation.FatalEligible)
        {
            if (operation.GoldOnFatal > 0)
                player = player with
                {
                    Gold = GoldAfterGain(player, operation.GoldOnFatal)
                };
            if (operation.ExtraCardRewardsOnFatal > 0)
                combat = combat with
                {
                    ExtraCardRewardsEarned = combat.ExtraCardRewardsEarned +
                        operation.ExtraCardRewardsOnFatal
                };
            if (operation.PlayerPowerOnFatalId is { } fatalPowerId &&
                operation.PlayerPowerOnFatalAmount != 0)
                combat = ApplyPlayerPower(
                    combat, fatalPowerId,
                    operation.PlayerPowerOnFatalAmount);
        }
        return (player, combat);
    }

    /// <summary>
    /// CardPileCmd.AddToCombatAndPreview(..., PileType.Hand) for enemy
    /// status injection. Generated cards are temporary, have no persistent
    /// deck version, and use stable combat instance identifiers.
    /// A full hand cannot accept another card; excess cards are put
    /// into discard by this emulator pending native overflow verification.
    /// </summary>
    private static CombatState AddGeneratedEnemyCardsToHand(
        CombatState combat,
        string cardId,
        int count)
    {
        _ = PrototypeContent.Card(cardId);
        for (var index = 0; index < Math.Max(0, count); index++)
        {
            var instance = new CombatCardInstance(
                InstanceId: combat.NextCardInstanceId,
                PersistentCardInstanceId: null,
                CardId: cardId,
                UpgradeLevel: 0,
                IsTemporary: true,
                State: PrototypeJson.EmptyObject(),
                WitherFakeUpgradeLevel: cardId == PrototypeNativeGloryBosses.WitherId
                    ? combat.Enemies.Where(enemy =>
                        enemy.EnemyId == PrototypeNativeGloryBosses.AeonglassId)
                        .Sum(enemy => enemy.WitherUpgradeCount)
                    : 0);
            instance = ApplyActiveSourceBoundAfflictionToCard(
                combat, instance);
            var fits = PrototypeContent.Rules.HasHandCapacity(
                combat.Hand.Length);
            var destination = fits
                ? PrototypeCardZone.Hand
                : PrototypeCardZone.DiscardPile;
            combat = SemanticDrawPileAdapterV111
                .ApplyNewCardZoneInsertion(
                    combat, destination, GetZone(combat, destination).Length,
                    instance);
        }

        return combat;
    }

    private static CombatState ApplyRelicAfterShuffleCards(CombatState combat, RngBundle rng)
    {
        var insertionOrdinal = 0;
        foreach (var relic in combat.RelicStates.Where(relic => !relic.IsMelted).OrderBy(relic => relic.ApplicationOrder))
        {
            var cardId = PrototypeContent.Relic(relic.RelicId).AfterShuffleGeneratedCardId;
            if (cardId is null)
                continue;
            var instanceId = combat.NextCardInstanceId;
            combat = AddGeneratedCardsToDiscard(combat, cardId, 1);
            if (ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive)
                throw new NotSupportedException(
                    "UNSUPPORTED_CONTROLLED_CHANCE " +
                    "site=" + StochasticSiteRegistryV111
                        .AfterShuffleRelicDrawPileInsertSiteId + " " +
                    "operation=bounded_integer requires a saved insertion continuation.");
            var insertion = ResolveRandomPilePosition(
                combat, rng, combat.DrawPile, cardId,
                PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
                sourceCardInstanceId: null, ordinal: insertionOrdinal++,
                siteId: StochasticSiteRegistryV111
                    .AfterShuffleRelicDrawPileInsertSiteId,
                requestOwnerPrefix:
                    "relic:" + relic.RelicId + ":application:" + relic.ApplicationOrder);
            combat = insertion.Combat;
            var draw = combat.DrawPile.ToList();
            draw.Insert(insertion.SelectedIndex, instanceId);
            var zoneChanges = new Dictionary<PrototypeCardZone, long[]>
            {
                [PrototypeCardZone.DrawPile] = draw.ToArray(),
                [PrototypeCardZone.DiscardPile] = combat.DiscardPile
                    .Where(id => id != instanceId).ToArray()
            };
            combat = SemanticDrawPileAdapterV111.ApplyDrawPileInsertion(
                combat,
                insertion.SelectedIndex,
                instanceId,
                combat.Mechanics.CardZones.WithZoneChanges(zoneChanges));
        }
        return combat;
    }

    private static CombatState AddGeneratedCardsToDiscard(
        CombatState combat,
        string cardId,
        int count)
    {
        _ = PrototypeContent.Card(cardId);
        for (var index = 0; index < Math.Max(0, count); index++)
        {
            var instance = new CombatCardInstance(
                InstanceId: combat.NextCardInstanceId,
                PersistentCardInstanceId: null,
                CardId: cardId,
                UpgradeLevel: 0,
                IsTemporary: true,
                State: PrototypeJson.EmptyObject(),
                WitherFakeUpgradeLevel: cardId == PrototypeNativeGloryBosses.WitherId
                    ? combat.Enemies.Where(enemy =>
                        enemy.EnemyId == PrototypeNativeGloryBosses.AeonglassId)
                        .Sum(enemy => enemy.WitherUpgradeCount)
                    : 0);
            instance = ApplyActiveSourceBoundAfflictionToCard(
                combat,
                instance);

            combat = SemanticDrawPileAdapterV111
                .ApplyNewCardZoneInsertion(
                    combat, PrototypeCardZone.DiscardPile,
                    combat.DiscardPile.Length, instance);
        }

        return combat;
    }

    private sealed record PrototypeGeneratedChoiceResult(
        CombatState Combat,
        long[] CardInstanceIds);

    private static CombatState AddDistinctGeneratedColorlessCardsToHand(
        CombatState combat, int count, RngBundle rng, string? sourceCardId,
        long? sourceCardInstanceId)
    {
        var capacity = PrototypeContent.Rules.RemainingHandCapacity(
            combat.Hand.Length);
        if (capacity == 0 || count <= 0)
            return combat;
        var candidates = DistinctColorlessGenerationPool(
            combat, sourceCardId);
        if (candidates.Length == 0)
        {
            return combat;
        }

        // Fully unlocked Native runs include the implemented epoch-gated
        // combat cards. Synthetic runs keep the conservative base pool.
        combat = ShuffleCombatCardPool(
            combat, rng, candidates,
            StochasticSiteRegistryV111.CombatFullPoolShuffleSiteId,
            "combat", "run.combat_card_generation",
            "effect:CreateDistinctColorlessCardsInHand:source:" +
                (sourceCardId ?? "none"),
            PrototypeCombatEffectKind.CreateDistinctColorlessCardsInHand,
            Math.Min(Math.Min(count, capacity), candidates.Length),
            sourceCardInstanceId);
        foreach (var cardId in candidates.Take(
                     Math.Min(Math.Min(count, capacity), candidates.Length)))
        {
            var instance = new CombatCardInstance(
                InstanceId: combat.NextCardInstanceId,
                PersistentCardInstanceId: null,
                CardId: cardId,
                UpgradeLevel: 0,
                IsTemporary: true,
                State: PrototypeJson.EmptyObject());
            instance = ApplyActiveSourceBoundAfflictionToCard(
                combat, instance);
            combat = SemanticDrawPileAdapterV111
                .ApplyNewCardZoneInsertion(
                    combat, PrototypeCardZone.Hand, combat.Hand.Length,
                    instance);
        }

        return combat;
    }

    private static PrototypeGeneratedChoiceResult AddGeneratedChoiceCards(
        CombatState combat,
        string[] candidates,
        int count,
        bool freeThisTurn,
        bool upgraded,
        PrototypeCombatEffectKind sourceOperationKind,
        long? sourceCardInstanceId,
        RngBundle rng,
        bool retain = false,
        bool useNativeCardGenerationRng = false)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Length == 0 || count <= 0)
        {
            return new PrototypeGeneratedChoiceResult(
                combat,
                Array.Empty<long>());
        }

        var selectedCount = Math.Min(count, candidates.Length);
        var useNativeProvider = useNativeCardGenerationRng &&
            combat.Stochastic.NativeRng is not null;
        // GetDistinctForCombat delegates to TakeRandom, which shuffles the
        // full filtered pool before taking a prefix. Preserve that RNG call
        // count even when the effect asks for only a few cards.
        combat = ShuffleCombatCardPool(
            combat, rng, candidates,
            StochasticSiteRegistryV111.CombatFullPoolShuffleSiteId,
            "combat", "run.combat_card_generation",
            "combat:generated_cards:first_instance:" +
                combat.NextCardInstanceId,
            sourceOperationKind, selectedCount, sourceCardInstanceId,
            forceLegacyProfile: !useNativeProvider);
        var selectedIds = candidates.Take(selectedCount).ToArray();
        var generatedIds = new List<long>(
            selectedIds.Length);
        foreach (var cardId in selectedIds)
        {
            var instance = new CombatCardInstance(
                InstanceId: combat.NextCardInstanceId,
                PersistentCardInstanceId: null,
                CardId: cardId,
                UpgradeLevel: upgraded ? 1 : 0,
                IsTemporary: true,
                State: PrototypeJson.EmptyObject(),
                TemporaryEnergyCost:
                    freeThisTurn
                        ? new PrototypeTemporaryCardCost(
                            0,
                            PrototypeTemporaryCardCostExpiry.EndOfTurn
                            | PrototypeTemporaryCardCostExpiry.WhenPlayed)
                        : null,
                KeywordOverrides: retain
                    ? [new PrototypeCardKeywordOverride(
                        PrototypeCardKeyword.Retain,
                        true,
                        PrototypeCardKeywordOverrideExpiry.None)]
                    : null);
            instance = ApplyActiveSourceBoundAfflictionToCard(
                combat,
                instance);

            generatedIds.Add(instance.InstanceId);
            combat = SemanticDrawPileAdapterV111
                .ApplyNewCardZoneInsertion(
                    combat, PrototypeCardZone.ChoicePool,
                    combat.ChoiceCardIds.Length, instance);
        }

        return new PrototypeGeneratedChoiceResult(combat, generatedIds.ToArray());
    }

    private static CombatState AddGeneratedCardCopies(
        CombatState combat,
        PrototypeCombatCardSnapshot snapshot,
        int count)
    {
        _ = PrototypeContent.Card(snapshot.CardId);
        for (var index = 0; index < Math.Max(0, count); index++)
        {
            var instance = new CombatCardInstance(
                InstanceId: combat.NextCardInstanceId,
                PersistentCardInstanceId: null,
                CardId: snapshot.CardId,
                UpgradeLevel: snapshot.UpgradeLevel,
                IsTemporary: true,
                State: snapshot.State.Clone(),
                CombatEnergyCostDelta: snapshot.CombatEnergyCostDelta,
                TemporaryEnergyCost: snapshot.TemporaryEnergyCost is null
                    ? null
                    : snapshot.TemporaryEnergyCost with { },
                KeywordOverrides: snapshot.KeywordOverrides is null
                    ? null
                    : snapshot.KeywordOverrides
                        .Select(item => item with { })
                        .ToArray(),
                ReplayCount: snapshot.ReplayCount,
                Enchantment: snapshot.Enchantment,
                EnchantmentTriggeredThisCombat:
                    snapshot.EnchantmentTriggeredThisCombat,
                Affliction: snapshot.Affliction,
                SuppressedUpgradeLevels:
                    snapshot.SuppressedUpgradeLevels);
            instance = ApplyActiveSourceBoundAfflictionToCard(
                combat,
                instance);

            var addToHand = PrototypeContent.Rules.HasHandCapacity(
                combat.Hand.Length);
            var destination = addToHand
                ? PrototypeCardZone.Hand
                : PrototypeCardZone.DiscardPile;
            combat = SemanticDrawPileAdapterV111
                .ApplyNewCardZoneInsertion(
                    combat, destination, GetZone(combat, destination).Length,
                    instance);
        }

        return combat;
    }

    private sealed record PrototypeCardPlayCountResult(
        CombatState Combat,
        int PlayCount);

    private static PrototypeCardPlayCountResult
        ResolveCardPlayCountAndConsumeModifiers(
            CombatState combat,
            CombatCardInstance card,
            PrototypeCardType cardType)
    {
        var modifiers = combat.PlayerPowers
            .Where(power =>
            {
                var definition = PrototypeContent.Power(power.PowerId);
                return power.Stacks > 0
                    && (definition.ReplayAnyCardType
                        || definition.ReplayCardType == cardType)
                    && definition.AdditionalPlayCount > 0;
            })
            .OrderBy(power => power.ApplicationOrder)
            .ToArray();

        var enchantmentReplay = card.Enchantment switch
        {
            {
                Kind: PrototypeCardEnchantmentKind.Spiral
            } enchantment =>
                Math.Max(0, enchantment.Amount),
            {
                Kind: PrototypeCardEnchantmentKind.Glam
            } enchantment
                when !card.EnchantmentTriggeredThisCombat =>
                Math.Max(0, enchantment.Amount),
            _ => 0
        };

        var relicIndices = Enumerable.Range(0, combat.RelicStates.Length)
            .Where(index =>
            {
                var relic = combat.RelicStates[index];
                return !relic.IsMelted
                    && !relic.UsedFirstCardPlayModifierThisCombat
                    && PrototypeContent.Relic(relic.RelicId)
                        .AdditionalCardPlayCountOncePerCombat > 0;
            }).ToArray();
        var relicReplay = relicIndices.Sum(index =>
            PrototypeContent.Relic(combat.RelicStates[index].RelicId)
                .AdditionalCardPlayCountOncePerCombat);
        if (relicIndices.Length > 0)
        {
            var relics = combat.RelicStates
                .Select(relic => relic.Fork()).ToArray();
            foreach (var relicIndex in relicIndices)
            {
                relics[relicIndex] = relics[relicIndex] with
                {
                    UsedFirstCardPlayModifierThisCombat = true
                };
            }
            combat = combat with { Relics = relics };
        }

        var playCount = 1
            + Math.Max(0, card.ReplayCount)
            + enchantmentReplay
            + relicReplay
            + modifiers.Sum(power =>
                PrototypeContent.Power(power.PowerId).AdditionalPlayCount);

        if (card.Enchantment is
                {
                    Kind: PrototypeCardEnchantmentKind.Glam
                }
            && enchantmentReplay > 0)
        {
            combat = combat with
            {
                Cards = combat.Cards
                    .Select(item =>
                        item.InstanceId == card.InstanceId
                            ? item with
                            {
                                EnchantmentTriggeredThisCombat = true
                            }
                            : item)
                    .ToArray()
            };
        }

        if (modifiers.Length == 0)
        {
            return new PrototypeCardPlayCountResult(combat, playCount);
        }

        var consumedOrders = modifiers
            .Where(power =>
                PrototypeContent.Power(power.PowerId)
                    .ConsumeOnMatchingPlayCountModification)
            .Select(power => power.ApplicationOrder)
            .ToHashSet();

        if (consumedOrders.Count > 0)
        {
            var powers = combat.PlayerPowers
                .Select(power =>
                {
                    if (!consumedOrders.Contains(power.ApplicationOrder))
                    {
                        return power;
                    }

                    return power with { Stacks = power.Stacks - 1 };
                })
                .Where(power => power.Stacks > 0)
                .ToArray();
            combat = combat with { PlayerPowers = powers };
        }

        return new PrototypeCardPlayCountResult(combat, playCount);
    }

    private static (PlayerState Player, CombatState Combat)
        ResolveCardPlaySeries(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            PrototypeCardPlaySeriesState series)
    {
        if (!series.HasRemainingExecutions)
        {
            return (player, combat);
        }

        var card = RequireCombatCard(
            combat,
            series.SourceCardInstanceId);
        var definition = RuntimeCardDefinition(card);
        if (series.NextPlayIndex == 0 && !series.RemoveSourceCardOnCompletion
            && series.SourceCardDestination == PrototypeCardZone.DiscardPile
            && combat.PlayerPowers.Any(power => power.Stacks > 0
                && PrototypeContent.Power(power.PowerId).ReturnNextDiscardedCardToDraw))
        {
            // Native result-location hooks run before OnPlay and apply once to
            // the physical card, including autoplay and repeated executions.
            combat = DecrementPlayerPowers(combat, power => power.ReturnNextDiscardedCardToDraw);
            series = series with { SourceCardDestination = PrototypeCardZone.DrawPile };
        }
        var powerApplicationOrderCeiling =
            combat.NextPowerApplicationOrder - 1;

        var operations = new Queue<PrototypeQueuedOperation>();
        foreach (var effect in definition.Effects)
        {
            var effectiveEffect =
                ShouldCardTargetAllEnemies(combat, definition)
                && effect.Kind == PrototypeCombatEffectKind.DamageEnemy
                    ? effect with
                    {
                        Target = PrototypeEffectTarget.AllEnemies
                    }
                    : effect;
            // Native Nimble adds Amount and Goopy adds Amount - 1
            // to base block before player-wide multipliers.
            if (card.Enchantment is
                    { Kind: PrototypeCardEnchantmentKind.Nimble or PrototypeCardEnchantmentKind.Goopy } blockEnchantment
                && effectiveEffect.Kind is
                    PrototypeCombatEffectKind.GainPlayerBlock
                    or PrototypeCombatEffectKind.GainToricToughnessBlock
                    or PrototypeCombatEffectKind.GainPlayerBlockFromEnemyStatusTotal
                    or PrototypeCombatEffectKind.GainPlayerBlockAndApplyPowerFromActualGain)
            {
                effectiveEffect = effectiveEffect with
                {
                    Amount = checked(effectiveEffect.Amount + blockEnchantment.Amount
                        - (blockEnchantment.Kind == PrototypeCardEnchantmentKind.Goopy ? 1 : 0))
                };
            }
            if (card.Enchantment?.Kind == PrototypeCardEnchantmentKind.TezcatarasEmber
                && definition.Type == PrototypeCardType.Attack
                && effectiveEffect.Kind == PrototypeCombatEffectKind.DamageEnemy)
            {
                effectiveEffect = effectiveEffect with { Amount = checked(effectiveEffect.Amount + 3) };
            }
            EnqueueEffectOperations(
                operations,
                effectiveEffect,
                card.UpgradeLevel,
                series.EnergySpent,
                series.TargetEnemyId,
                combat,
                sourceKind: PrototypeEffectSourceKind.Card,
                isPoweredAttack:
                    definition.Type == PrototypeCardType.Attack);
        }

        EnqueueCardEnchantmentOperations(
            operations,
            card,
            definition,
            series.TargetEnemyId,
            combat);

        if (series.NextPlayIndex == 0
            && card.Enchantment is
                { Kind: PrototypeCardEnchantmentKind.Swift, Amount: > 0 } swift
            && !card.EnchantmentTriggeredThisCombat)
        {
            combat = combat with
            {
                Cards = combat.Cards.Select(item =>
                    item.InstanceId == card.InstanceId
                        ? item with { EnchantmentTriggeredThisCombat = true }
                        : item).ToArray()
            };
            operations.Enqueue(new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.DrawCards, swift.Amount));
        }

        var nextSeries = series with
        {
            NextPlayIndex = series.NextPlayIndex + 1
        };
        var isLastExecution = !nextSeries.HasRemainingExecutions;

        var resolved = ResolveOperations(
            player,
            combat,
            operations,
            rng,
            series.SourceCardInstanceId,
            series.SourceCardDestination,
            [
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.CardPlayed,
                    SourceCardInstanceId:
                        series.SourceCardInstanceId,
                    CardId: card.CardId,
                    PowerApplicationOrderCeiling:
                        powerApplicationOrderCeiling,
                    IsAutoPlay: series.IsAutoPlay,
                    IsPoweredAttack: PrototypeContent.Card(card.CardId)
                        .Type == PrototypeCardType.Attack)
            ],
            cardPlaySeries: nextSeries,
            moveSourceCardOnCompletion: isLastExecution,
            removeSourceCardOnCompletion:
                series.RemoveSourceCardOnCompletion);

        if (isLastExecution && resolved.Combat.PendingChoice is null
            && resolved.Player.Hp > 0)
            resolved = (resolved.Player, CompletePaelsLegionCardPlay(
                resolved.Combat, series.SourceCardInstanceId));

        if (resolved.Combat.PendingChoice is not null
            || isLastExecution
            || resolved.Player.Hp <= 0)
        {
            if (isLastExecution
                && resolved.Combat.PendingChoice is null
                && resolved.Player.Hp > 0)
            {
                resolved = ApplyVigorousAfterCardPlayed(
                    resolved.Player,
                    resolved.Combat,
                    series.SourceCardInstanceId);
            }
            return resolved;
        }

        if (AllEnemiesDefeated(resolved.Combat))
        {
            var finalized = FinalizeInterruptedCardPlaySeries(
                resolved.Player,
                resolved.Combat,
                rng,
                nextSeries);
            return ApplyVigorousAfterCardPlayed(
                finalized.Player,
                finalized.Combat,
                series.SourceCardInstanceId);
        }

        return ResolveCardPlaySeries(
            resolved.Player,
            resolved.Combat,
            rng,
            nextSeries);
    }

    private static (PlayerState Player, CombatState Combat)
        ApplyVigorousAfterCardPlayed(
            PlayerState player,
            CombatState combat,
            long sourceCardInstanceId)
    {
        var card = RequireCombatCard(combat, sourceCardInstanceId);
        if (card.PersistentCardInstanceId is not { } persistentId
            || card.Enchantment is not
                {
                    Kind: PrototypeCardEnchantmentKind.Vigorous,
                    Disabled: false
                } vigorous)
        {
            return (player, combat);
        }

        var persistentCardExists = player.Deck.Any(deckCard =>
            deckCard.InstanceId == persistentId);
        if (persistentCardExists)
        {
            player = player with
            {
                Deck = player.Deck.Select(deckCard =>
                    deckCard.InstanceId == persistentId
                        ? deckCard with
                        {
                            Enchantment = vigorous with { Disabled = true }
                        }
                        : deckCard).ToArray()
            };
        }

        combat = combat with
        {
            Cards = combat.Cards.Select(combatCard =>
                combatCard.InstanceId == sourceCardInstanceId
                    ? combatCard with
                    {
                        Enchantment = vigorous with { Disabled = true }
                    }
                    : combatCard).ToArray()
        };
        return (player, combat);
    }

    private static (PlayerState Player, CombatState Combat)
        FinalizeInterruptedCardPlaySeries(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            PrototypeCardPlaySeriesState series) =>
        ResolveOperations(
            player,
            combat,
            new Queue<PrototypeQueuedOperation>(),
            rng,
            series.SourceCardInstanceId,
            series.SourceCardDestination,
            completionEvents: Array.Empty<PrototypeCombatEvent>(),
            cardPlaySeries: series,
            moveSourceCardOnCompletion: true,
            removeSourceCardOnCompletion:
                series.RemoveSourceCardOnCompletion);

    private static CombatState DecrementPlayerPowers(
        CombatState combat,
        Func<PrototypePowerDefinition, bool> predicate)
    {
        var powers = new List<PrototypePowerInstanceState>();
        var removed = new List<PrototypePowerInstanceState>();

        foreach (var power in combat.PlayerPowers)
        {
            var definition =
                PrototypeContent.Power(power.PowerId);
            if (!predicate(definition))
            {
                powers.Add(power);
                continue;
            }

            // Native Shame/Doubt skip the same turn's first duration
            // decrement when they create a previously absent debuff.
            if (power.SkipNextPlayerTurnEnd
                && definition.DecrementAtPlayerTurnEnd)
            {
                powers.Add(power with { SkipNextPlayerTurnEnd = false });
                continue;
            }

            var next = power with
            {
                Stacks = power.Stacks - 1
            };
            if (next.Stacks == 0
                || (next.Stacks < 0
                    && !definition.AllowNegative))
            {
                removed.Add(next);
            }
            else
            {
                powers.Add(next);
            }
        }

        var clearKinds = removed
            .Select(power =>
                PrototypeContent.Power(power.PowerId))
            .Where(definition =>
                definition.ClearAppliedCardAfflictionWhenRemoved
                && definition.AppliedCardAffliction is not null)
            .Select(definition =>
                definition.AppliedCardAffliction!.Value)
            .Where(kind =>
                !powers.Any(power =>
                    PrototypeContent.Power(power.PowerId)
                        .AppliedCardAffliction == kind))
            .ToHashSet();

        return combat with
        {
            PlayerPowers = powers.ToArray(),
            Cards = clearKinds.Count == 0
                ? combat.Cards
                : combat.Cards
                    .Select(card =>
                        card.Affliction is
                            {
                                SourceEnemyInstanceId: null
                            } affliction
                        && clearKinds.Contains(affliction.Kind)
                            ? card with { Affliction = null }
                            : card)
                    .ToArray()
        };
    }

    private static CombatState RemovePlayerPowers(
        CombatState combat,
        Func<PrototypePowerDefinition, bool> predicate)
    {
        var removed = combat.PlayerPowers
            .Where(power =>
                predicate(
                    PrototypeContent.Power(power.PowerId)))
            .ToArray();
        if (removed.Length == 0)
        {
            return combat;
        }

        var removedOrders = removed
            .Select(power => power.ApplicationOrder)
            .ToHashSet();
        var remaining = combat.PlayerPowers
            .Where(power =>
                !removedOrders.Contains(
                    power.ApplicationOrder))
            .ToArray();

        var clearKinds = removed
            .Select(power =>
                PrototypeContent.Power(power.PowerId))
            .Where(definition =>
                definition.ClearAppliedCardAfflictionWhenRemoved
                && definition.AppliedCardAffliction is not null)
            .Select(definition =>
                definition.AppliedCardAffliction!.Value)
            .Where(kind =>
                !remaining.Any(power =>
                    PrototypeContent.Power(power.PowerId)
                        .AppliedCardAffliction == kind))
            .ToHashSet();

        return combat with
        {
            PlayerPowers = remaining,
            Cards = clearKinds.Count == 0
                ? combat.Cards
                : combat.Cards
                    .Select(card =>
                        card.Affliction is
                            {
                                SourceEnemyInstanceId: null
                            } affliction
                        && clearKinds.Contains(affliction.Kind)
                            ? card with { Affliction = null }
                            : card)
                    .ToArray()
        };
    }

    private static int PlayerBlockBonus(CombatState combat) =>
        combat.PlayerPowers.Sum(power =>
            PrototypeContent.Power(power.PowerId).BlockBonusPerStack * power.Stacks);

    private static int ModifyPlayerBlockGain(
        CombatState combat,
        int amount,
        bool fromCard,
        long? sourceCardInstanceId = null)
    {
        var modified = fromCard
            ? ModifyPlayerCardBlock(
                combat,
                amount,
                sourceCardInstanceId)
            : amount;

        foreach (var power in combat.PlayerPowers
                     .Where(power => power.Stacks > 0))
        {
            var definition =
                PrototypeContent.Power(
                    power.PowerId);
            if (definition
                    .PlayerBlockGainDenominatorPerStack
                <= 0)
            {
                throw new InvalidOperationException(
                    $"Power '{definition.Id}' has invalid all-block denominator.");
            }

            for (var stack = 0;
                 stack < power.Stacks;
                 stack++)
            {
                modified =
                    (modified
                     * definition
                         .PlayerBlockGainNumeratorPerStack)
                    / definition
                        .PlayerBlockGainDenominatorPerStack;
            }
        }

        return modified;
    }

    private static int ModifyPlayerCardBlock(
        CombatState combat,
        int amount,
        long? sourceCardInstanceId = null)
    {
        if (sourceCardInstanceId is { } id)
        {
            var tags = PrototypeContent.Card(
                RequireCombatCard(combat, id).CardId).Tags
                ?? Array.Empty<string>();
            amount += combat.PlayerPowers.Where(power => power.Stacks > 0)
                .Sum(power =>
                {
                    var requiredTag = PrototypeContent.Power(
                        power.PowerId).BlockBonusRequiredCardTag;
                    return requiredTag is not null
                        && tags.Contains(requiredTag, StringComparer.Ordinal)
                        ? power.Stacks : 0;
                });
        }

        if (combat.PlayerPowers.Any(power =>
            power.Stacks > 0
            && PrototypeContent.Power(power.PowerId).PreventsCardBlock))
        {
            return 0;
        }

        var modified = amount + PlayerBlockBonus(combat);
        foreach (var power in combat.PlayerPowers.Where(power => power.Stacks > 0))
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.PlayerCardBlockDenominator <= 0)
            {
                throw new InvalidOperationException(
                    $"Power '{definition.Id}' has invalid player-card-block denominator.");
            }

            modified = (modified * definition.PlayerCardBlockNumerator)
                / definition.PlayerCardBlockDenominator;
        }

        return modified;
    }

    private static int ModifyPlayerOutgoingAttackDamage(
        CombatState combat,
        int damage)
    {
        var modified = damage;
        foreach (var power in combat.PlayerPowers.Where(power => power.Stacks > 0))
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.PlayerAttackDamageDenominator <= 0)
            {
                throw new InvalidOperationException(
                    $"Power '{definition.Id}' has invalid player-attack denominator.");
            }

            modified = (modified * definition.PlayerAttackDamageNumerator)
                / definition.PlayerAttackDamageDenominator;
            if (definition.PlayerAttackDamageMultiplierWhileActiveDenominator
                <= 0)
            {
                throw new InvalidOperationException(
                    $"Power '{definition.Id}' has invalid player attack multiplier denominator.");
            }

            modified = (modified
                * definition.PlayerAttackDamageMultiplierWhileActiveNumerator)
                / definition.PlayerAttackDamageMultiplierWhileActiveDenominator;
        }

        return modified;
    }

    // This is the single damage calculation used by both actual enemy hits
    // and public intent previews. It deliberately does not account for player
    // Block, and never consumes combat RNG.
    internal static int EnemyHitDamage(
        EnemyCombatState enemy,
        CombatState combat,
        PrototypeEnemyEffectSpec effect,
        int act,
        int ascension,
        int priorMoveUses = 0)
    {
        var baseDamage = effect.UseStoredEnemyDamage
            ? enemy.StoredEnemyDamage
            : checked(effect.AmountAt(act, ascension)
                + effect.ExtraAmountPerPriorMoveUse * priorMoveUses);
        var damage = baseDamage
            + (effect.ExtraAmountFromOwnerPowerId is { } statId
                ? enemy.PowerStates
                    .Where(power => power.PowerId == statId)
                    .Sum(power => power.Stacks)
                : 0)
            + enemy.PowerStates.Sum(power =>
                PrototypeContent.Power(power.PowerId)
                    .EnemyAttackDamageBonusPerStack * power.Stacks);
        foreach (var status in enemy.Statuses)
        {
            var definition = PrototypeContent.Status(status.Key);
            if (status.Key == "proto.status.weak")
            {
                if (!effect.IsAttack)
                    continue;

                var extraReductionPercent = combat.RelicStates
                    .Where(relic => !relic.IsMelted)
                    .Select(relic => PrototypeContent.Relic(relic.RelicId)
                        .WeakAttackDamageReductionPercent)
                    .Sum();
                if (extraReductionPercent > 0)
                {
                    var adjustedNumerator = Math.Max(0,
                        checked(100 * definition.OutgoingDamageNumerator
                            - extraReductionPercent
                                * definition.OutgoingDamageDenominator));
                    damage = checked(damage * adjustedNumerator)
                        / checked(100
                            * definition.OutgoingDamageDenominator);
                    continue;
                }
            }

            damage = (damage * definition.OutgoingDamageNumerator)
                / definition.OutgoingDamageDenominator;
        }
        foreach (var power in enemy.PowerStates.Where(power =>
            effect.IsAttack && power.Stacks > 0))
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.EnemyOutgoingPoweredAttackDenominator <= 0)
            {
                throw new InvalidOperationException(
                    $"Power '{definition.Id}' has invalid enemy outgoing-attack denominator.");
            }

            damage = (damage
                * definition.EnemyOutgoingPoweredAttackNumerator)
                / definition.EnemyOutgoingPoweredAttackDenominator;
        }

        return effect.IsAttack
            ? ModifyIncomingPlayerAttackDamage(combat, damage)
            : ApplyPlayerIncomingDamageCap(combat, damage);
    }

    private static int ApplyPlayerIncomingDamageCap(
        CombatState combat,
        int amount)
    {
        var caps = combat.PlayerPowers
            .Where(power => power.Stacks > 0)
            .Select(power =>
                PrototypeContent.Power(power.PowerId)
                    .PlayerIncomingDamageCap)
            .Where(cap => cap > 0)
            .ToArray();

        return caps.Length == 0
            ? amount
            : Math.Min(amount, caps.Min());
    }

    private static int ModifyIncomingPlayerAttackDamage(
        CombatState combat,
        int damage)
    {
        var modified = damage;
        foreach (var power in combat.PlayerPowers.Where(power => power.Stacks > 0))
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.PlayerIncomingAttackDamageDenominator <= 0)
            {
                throw new InvalidOperationException(
                    $"Power '{definition.Id}' has invalid incoming-player-attack denominator.");
            }

            modified =
                (modified
                 * definition.PlayerIncomingAttackDamageNumerator)
                / definition.PlayerIncomingAttackDamageDenominator;
        }

        // TaintedPower adds flat powered-attack damage after the
        // multiplicative incoming-damage modifiers.
        modified += combat.PlayerPowers
            .Where(power => power.PowerId ==
                PrototypeNativeHiveElites.TaintedPowerId)
            .Sum(power => Math.Max(0, power.Stacks));

        return ApplyPlayerIncomingDamageCap(
            combat,
            modified);
    }

    private static void EnqueueCardEnchantmentOperations(
        Queue<PrototypeQueuedOperation> operations,
        CombatCardInstance card,
        PrototypeCardDefinition definition,
        int? actionTargetEnemyId,
        CombatState combat)
    {
        if (card.Enchantment is not
            {
                Kind: PrototypeCardEnchantmentKind.Inky
            } inky
            || inky.Amount <= 0)
        {
            return;
        }

        var target = ShouldCardTargetAllEnemies(combat, definition)
            ? PrototypeEffectTarget.AllEnemies
            : PrototypeEffectTarget.ActionTargetEnemy;
        EnqueueEffectOperations(
            operations,
            new PrototypeCombatEffectSpec(
                PrototypeCombatEffectKind.ApplyEnemyStatus,
                inky.Amount,
                StatusId: "proto.status.weak",
                Target: target),
            upgradeLevel: 0,
            energySpent: 0,
            actionTargetEnemyId,
            combat,
            sourceKind: PrototypeEffectSourceKind.Card);
    }

    private static bool ShouldEnemyDeathTriggerFatal(
        CombatState combat,
        int enemyId)
    {
        var enemy = combat.Enemies.SingleOrDefault(
            item => item.InstanceId == enemyId)
            ?? throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");

        // ReattachPower only makes a segment's death fatal when
        // every other linked segment is already dead.
        if (enemy.PowerStates.Any(power =>
                power.PowerId == PrototypeNativeHiveElites.ReattachId)
            && combat.Enemies.Any(other =>
                other.InstanceId != enemy.InstanceId
                && other.Hp > 0
                && other.PowerStates.Any(power =>
                    power.PowerId == PrototypeNativeHiveElites.ReattachId)))
            return false;

        return enemy.PowerStates.All(power =>
            PrototypeContent.Power(power.PowerId)
                .OwnerDeathTriggersFatal);
    }

    private static PrototypeCardTarget EffectiveCardTarget(
        CombatState combat,
        PrototypeCardDefinition definition) =>
        ShouldCardTargetAllEnemies(combat, definition)
            ? PrototypeCardTarget.None
            : definition.Target;

    private static bool ShouldCardTargetAllEnemies(
        CombatState combat,
        PrototypeCardDefinition definition)
    {
        var tags = definition.Tags ?? Array.Empty<string>();
        return combat.PlayerPowers.Any(power =>
        {
            if (power.Stacks <= 0)
            {
                return false;
            }

            var powerDefinition =
                PrototypeContent.Power(power.PowerId);
            return powerDefinition.AllEnemyTargetCardTag is
                    { } requiredTag
                && tags.Contains(
                    requiredTag,
                    StringComparer.Ordinal);
        });
    }

    private static bool IsCardFreeByPower(
        CombatState combat,
        PrototypeCardDefinition card) =>
        combat.PlayerPowers.Any(power =>
        {
            var definition = PrototypeContent.Power(power.PowerId);
            return power.Stacks > 0
                && definition.FreeCardType == card.Type;
        });

    private static CombatState ConsumeMatchingFreeCardPower(
        CombatState combat,
        PrototypeCardType cardType)
    {
        var candidate = combat.PlayerPowers
            .Where(power =>
            {
                var definition = PrototypeContent.Power(power.PowerId);
                return power.Stacks > 0
                    && definition.FreeCardType == cardType
                    && definition.ConsumeOnMatchingCardPlay;
            })
            .OrderBy(power => power.ApplicationOrder)
            .FirstOrDefault();

        if (candidate is null)
        {
            return combat;
        }

        var powers = combat.PlayerPowers.ToList();
        var index = powers.FindIndex(power =>
            power.ApplicationOrder == candidate.ApplicationOrder);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Free-card power disappeared before consumption.");
        }

        if (powers[index].Stacks <= 1)
        {
            powers.RemoveAt(index);
        }
        else
        {
            powers[index] = powers[index] with
            {
                Stacks = powers[index].Stacks - 1
            };
        }

        return combat with { PlayerPowers = powers.ToArray() };
    }

    // As in native Normality.ShouldPlay, the owner is prohibited
    // after three card-play starts only while the curse is in hand.
    private static bool NormalityPreventsCardPlay(CombatState combat) =>
        combat.CounterState.CardsPlayedThisTurn >= 3
        && combat.Hand.Any(id =>
            RequireCombatCard(combat, id).CardId
                == PrototypeNativeHiveEvents.NormalityId);

    // Native PerfectFit.ModifyShuffleOrder inserts at the top of a
    // non-initial shuffle. Our engine draws from the LAST index, hence
    // the selected card(s) move to the end of the shuffled array.
    private static long[] ApplyPerfectFitShuffleOrder(
        CombatState combat, long[] shuffled)
    {
        var enchanted = shuffled.Where(id =>
            RequireCombatCard(combat, id).Enchantment?.Kind
                == PrototypeCardEnchantmentKind.PerfectFit).ToArray();
        if (enchanted.Length == 0)
        {
            return shuffled;
        }

        var selected = enchanted.ToHashSet();
        return shuffled.Where(id => !selected.Contains(id))
            .Concat(enchanted).ToArray();
    }

    private static bool CanPlayAnotherCardThisTurn(
        PlayerState player,
        CombatState combat,
        CombatCardInstance card)
    {
        if (combat.Hand.Any(instanceId => instanceId != card.InstanceId
                && PrototypeContent.Card(
                    RequireCombatCard(combat, instanceId).CardId)
                    .PreventsOtherManualCardPlaysWhileInHand))
        {
            return false;
        }

        // Native Normality.ShouldPlay only intervenes while the
        // unplayable Curse is physically in the player's hand. The
        // limit uses play-start counts, not energy or card draws.
        if (NormalityPreventsCardPlay(combat))
        {
            return false;
        }

        // Smoggy disallows playing a Skill while the card carries Smog.
        // Its affliction is cleared at the end of the player's turn.
        if (card.Affliction is { } affliction
            && combat.PlayerPowers.Any(power =>
                power.Stacks > 0
                && PrototypeContent.Power(power.PowerId)
                    .BlockPlayOfMatchingAffliction
                && PrototypeContent.Power(power.PowerId)
                    .SkillPlayAffliction == affliction.Kind))
        {
            return false;
        }

        // Native RingingPower.ShouldPlay checks the Ringing affliction
        // on the candidate card. A card already afflicted by something
        // else never receives Ringing and remains playable even after
        // the first card played that turn. Other cap powers still apply.
        var powerCaps = combat.PlayerPowers
            .Where(power => power.Stacks > 0
                && (power.PowerId != "proto.power.ringing"
                    || card.Affliction?.Kind
                        == PrototypeCardAfflictionKind.Ringing))
            .Select(power =>
                PrototypeContent.Power(power.PowerId)
                    .MaxCardsPlayablePerTurn);
        var relicCaps = player.ActiveRelics
            .Select(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .MaxCardsPlayablePerTurn);
        var caps = powerCaps
            .Concat(relicCaps)
            .Where(cap => cap > 0)
            .ToArray();

        return caps.Length == 0
            || combat.CounterState.CardsPlayedThisTurn
                < caps.Min();
    }

    private static bool IsCardAffordable(
        CombatState combat,
        CombatCardInstance card,
        PrototypeCardDefinition definition) =>
        definition.Cost.Kind switch
        {
            PrototypeCardCostKind.Fixed =>
                ResolveFixedCardEnergyCost(
                    combat,
                    card,
                    definition) <= combat.Energy,
            PrototypeCardCostKind.X => true,
            _ => throw new ArgumentOutOfRangeException()
        };

    private static int ResolveCardEnergySpent(
        CombatState combat,
        CombatCardInstance card,
        PrototypeCardDefinition definition) =>
        definition.Cost.Kind switch
        {
            PrototypeCardCostKind.Fixed =>
                ResolveFixedCardEnergyCost(
                    combat,
                    card,
                    definition),
            PrototypeCardCostKind.X => combat.Energy,
            _ => throw new ArgumentOutOfRangeException()
        };

    private static bool CardExhaustsOnUse(
        PrototypeCardDefinition definition,
        CombatCardInstance card) =>
        card.Enchantment?.Kind == PrototypeCardEnchantmentKind.Goopy
        || definition.ExhaustOnUse
        && !(card.UpgradeLevel > 0 && definition.LoseExhaustOnUpgrade)
        // SoulsPower.OnEnchant removes the LOCAL Exhaust keyword.
        // This must hold for both normal plays and auto-played cards.
        && card.Enchantment?.Kind !=
            PrototypeCardEnchantmentKind.SoulsPower;

    private static int ResolveFixedCardEnergyCost(
        CombatState combat,
        CombatCardInstance card,
        PrototypeCardDefinition definition)
    {
        if ((combat.Relics ?? Array.Empty<CombatRelicState>())
            .Where(relic => !relic.IsMelted)
            .Any(relic => PrototypeContent.Relic(relic.RelicId)
                .FreeCardAtManualCardPlayCount
                    == combat.CounterState.ManualCardsPlayedThisTurn))
        {
            return 0;
        }

        var baseCost = card.TemporaryEnergyCost is not null
            ? card.TemporaryEnergyCost.Cost
            : (card.Enchantment?.Kind == PrototypeCardEnchantmentKind.TezcatarasEmber
                ? 0
                : definition.Cost.AmountAt(
                    card.UpgradeLevel,
                    ResolveCardCostReductionCount(definition.Cost, combat)))
              + card.CombatEnergyCostDelta;

        var afflictionSurcharge = card.Affliction is not
                { } affliction
            ? 0
            : combat.PlayerPowers.Sum(power =>
            {
                var powerDefinition =
                    PrototypeContent.Power(power.PowerId);
                return power.Stacks > 0
                    && powerDefinition.AppliedCardAffliction
                        == affliction.Kind
                        ? powerDefinition
                            .AfflictedCardEnergyCostPerStack
                          * power.Stacks
                        : 0;
            });

        var curiousDiscount = definition.Type == PrototypeCardType.Power
            ? combat.PlayerPowers
                .Where(power =>
                    power.PowerId == PrototypeNativeGloryEvents.CuriousId)
                .Sum(power => Math.Max(0, power.Stacks))
            : 0;
        var relicSurcharge = definition.Type == PrototypeCardType.Power
            ? (combat.Relics ?? Array.Empty<CombatRelicState>())
                .Where(relic => !relic.IsMelted)
                .Sum(relic => PrototypeContent.Relic(relic.RelicId)
                    .PowerCardEnergyCostIncrease)
            : 0;

        return Math.Max(
            0,
            baseCost + afflictionSurcharge + relicSurcharge
                - curiousDiscount);
    }

    private static RunState ApplyBeforeCombatStartRelics(
        RunState state, RngBundle prototypeRng,
        ref RngBundle? nativeRunRng)
    {
        var relicTriggers = state.Player.ActiveRelics
            .Where(relic => PrototypeContent.Relic(relic.RelicId)
                .ProcureRandomPotionsBeforeCombat)
            .ToArray();
        if (relicTriggers.Length == 0)
            return state;

        var slots = (PotionInstance?[])state.Player.PotionSlots.Clone();
        foreach (var _ in relicTriggers)
        {
            var openSlot = Array.IndexOf(slots, null);
            while (openSlot >= 0)
            {
                var potionOwnerId =
                    $"combat:act:{state.World?.Act}:floor:{state.World?.Floor}"
                    + $":node:{state.World?.Map.CurrentNodeId}:potion_slot:{openSlot}";
                var potionId = nativeRunRng is null
                    ? PrototypeNativePotionShop.PickWeighted(
                        prototypeRng, "event")
                    : PrototypeNativePotionShop.PickWeightedDistinctNativeWithAudit(
                        nativeRunRng,
                        "run.combat_potion_generation", 1,
                        potionOwnerId,
                        new NativeRandomIndexProviderV111()).PotionIds[0];

                if (state.Player.ActiveRelics.Any(relic =>
                    PrototypeContent.Relic(relic.RelicId)
                        .PreventPotionAcquisition))
                {
                    break;
                }

                slots[openSlot] = new PotionInstance(
                    potionId, PrototypeJson.EmptyObject());
                openSlot = Array.IndexOf(slots, null);
            }
        }

        return state with
        {
            Player = state.Player with { PotionSlots = slots }
        };
    }

    private static CombatState ApplyDrawCardCost(
        CombatState combat, RngBundle legacyRng, long cardInstanceId,
        string cardModelId, string stage, int bound)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(legacyRng);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardModelId);
        if (stage is not ("relic" or "slither") || bound <= 0)
            throw new ArgumentOutOfRangeException(nameof(bound));
        if (cardInstanceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(cardInstanceId));
        var native = combat.Stochastic.NativeRng;
        if (ControlledChanceGuardV111.Active &&
            ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
            native is null)
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=combat.card_draw." + stage +
                "_energy_cost operation=missing_native_stream_owner");
        var streamId = native is null
            ? "combat"
            : SemanticOtherChanceEngineV111.CostStreamId;
        var streams = native?.RunStreams.Fork() ?? legacyRng;
        var stream = streams.GetStream(streamId);
        var callCount = StochasticProviderScopeV111.GetNextOrdinal(
            streams, streamId) is { } scopedOrdinal
                ? (ulong)scopedOrdinal
                : (stream.CallCount ?? 0);
        if (callCount > int.MaxValue)
            throw new NotSupportedException(
                "Random draw-cost provider ordinal exceeds the request range.");
        var request = new RandomIndexRequestV111(
            stage == "relic"
                ? StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId
                : StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId,
            streamId,
            "card_instance:" + cardInstanceId + ":model:" + cardModelId +
                ":stage:" + stage,
            (int)callCount, [], OperationKind: "bounded_integer",
            BoundedCount: bound);
        IRandomIndexProviderV111 provider = native is null
            ? new LegacyPrototypeRandomIndexProviderV111()
            : new NativeRandomIndexProviderV111();
        var decision = StochasticProviderScopeV111.ResolveIndex(
            provider, request, streams);
        if (decision.ProviderMode == "native-v111" &&
            RandomBoundaryJournalV111.HasActiveJournal &&
            StochasticSiteRegistryV111
                .IsCombatCardEnergyCostAuditSite(request.SiteId))
            RandomBoundaryJournalV111.RecordCardEnergyCostOutcome(
                SemanticCardEnergyCostAuditV111.Create(
                    decision, [cardInstanceId], cardInstanceId, cardModelId));
        var updated = native is null
            ? combat
            : combat with
            {
                Stochastic = CombatStochasticStateV111.WithNativeRng(
                    combat.Stochastic, native with { RunStreams = streams })
            };
        return SetCardTemporaryEnergyCost(
            updated, cardInstanceId,
            new PrototypeTemporaryCardCost(
                decision.SelectedIndex, PrototypeTemporaryCardCostExpiry.None));
    }

    private static CombatState SetCardTemporaryEnergyCost(
        CombatState combat,
        long cardInstanceId,
        PrototypeTemporaryCardCost temporaryCost) =>
        combat with
        {
            Cards = combat.Cards
                .Select(card =>
                    card.InstanceId == cardInstanceId
                        ? card with
                        {
                            TemporaryEnergyCost =
                                temporaryCost with { }
                        }
                        : card)
                .ToArray()
        };

    private static CombatState SetHandCardsTemporaryEnergyCost(
        CombatState combat,
        int cost,
        bool reduceOnly = false,
        PrototypeTemporaryCardCostExpiry expiry =
            PrototypeTemporaryCardCostExpiry.EndOfTurn
            | PrototypeTemporaryCardCostExpiry.WhenPlayed)
    {
        var hand = combat.Hand.ToHashSet();
        var cards = combat.Cards
            .Select(card =>
            {
                if (!hand.Contains(card.InstanceId))
                {
                    return card;
                }

                var definition = RuntimeCardDefinition(card);
                if (definition.Cost.Kind == PrototypeCardCostKind.X
                    || definition.Cost.Amount < 0)
                {
                    return card;
                }

                if (reduceOnly && ResolveFixedCardEnergyCost(
                        combat, card, definition) <= cost)
                {
                    return card;
                }

                return card with
                {
                    TemporaryEnergyCost = new PrototypeTemporaryCardCost(
                        Math.Max(0, cost), expiry)
                };
            })
            .ToArray();

        return combat with { Cards = cards };
    }

    private static CombatState ClearTemporaryCardCost(
        CombatState combat,
        long cardInstanceId,
        PrototypeTemporaryCardCostExpiry expiry)
    {
        var cards = combat.Cards
            .Select(card =>
                card.InstanceId == cardInstanceId
                    && card.TemporaryEnergyCost is not null
                    && card.TemporaryEnergyCost.Expiry.HasFlag(expiry)
                        ? card with { TemporaryEnergyCost = null }
                        : card)
            .ToArray();

        return combat with { Cards = cards };
    }

    private static CombatState ClearTemporaryCardCosts(
        CombatState combat,
        PrototypeTemporaryCardCostExpiry expiry) =>
        combat with
        {
            Cards = combat.Cards
                .Select(card =>
                    card.TemporaryEnergyCost is not null
                    && card.TemporaryEnergyCost.Expiry.HasFlag(expiry)
                        ? card with { TemporaryEnergyCost = null }
                        : card)
                .ToArray()
        };

    private static CombatState ClearTemporaryCardKeywordOverrides(
        CombatState combat,
        long cardInstanceId,
        PrototypeCardKeywordOverrideExpiry expiry)
    {
        var cards = combat.Cards
            .Select(card =>
            {
                if (card.InstanceId != cardInstanceId
                    || card.KeywordOverrides is null)
                {
                    return card;
                }

                var remaining = card.KeywordOverrides
                    .Where(item => !item.Expiry.HasFlag(expiry))
                    .ToArray();
                return card with
                {
                    KeywordOverrides = remaining.Length == 0
                        ? null
                        : remaining
                };
            })
            .ToArray();

        return combat with { Cards = cards };
    }

    private static CombatState ClearTemporaryCardKeywordOverrides(
        CombatState combat,
        PrototypeCardKeywordOverrideExpiry expiry)
    {
        var cards = combat.Cards
            .Select(card =>
            {
                if (card.KeywordOverrides is null)
                {
                    return card;
                }

                var remaining = card.KeywordOverrides
                    .Where(item => !item.Expiry.HasFlag(expiry))
                    .ToArray();
                return card with
                {
                    KeywordOverrides = remaining.Length == 0
                        ? null
                        : remaining
                };
            })
            .ToArray();

        return combat with { Cards = cards };
    }

    private static bool CardHasKeyword(
        CombatState combat,
        long cardInstanceId,
        PrototypeCardKeyword keyword)
    {
        var card = RequireCombatCard(combat, cardInstanceId);
        var baseValue = keyword switch
        {
            PrototypeCardKeyword.Retain =>
                PrototypeContent.Card(card.CardId).Retain
                || (card.UpgradeLevel > 0
                    && PrototypeContent.Card(card.CardId)
                        .RetainOnUpgrade)
                || card.Enchantment?.Kind
                    == PrototypeCardEnchantmentKind.Steady,
            PrototypeCardKeyword.Sly =>
                PrototypeContent.Card(card.CardId).Sly,
            PrototypeCardKeyword.Ethereal =>
                PrototypeContent.Card(card.CardId).Ethereal
                && !(card.UpgradeLevel > 0
                    && PrototypeContent.Card(card.CardId)
                        .LoseEtherealOnUpgrade),
            _ => throw new ArgumentOutOfRangeException(nameof(keyword))
        };

        var definition = PrototypeContent.Card(card.CardId);
        var tags = definition.Tags ?? Array.Empty<string>();
        var grantedByAffliction =
            keyword == PrototypeCardKeyword.Ethereal
            && card.Affliction?.Kind
                == PrototypeCardAfflictionKind.Hexed;
        var grantedByPower = combat.PlayerPowers.Any(power =>
        {
            if (power.Stacks <= 0)
            {
                return false;
            }

            var powerDefinition = PrototypeContent.Power(
                power.PowerId);
            return powerDefinition.GrantedCardKeyword == keyword
                && powerDefinition
                    .GrantedCardKeywordRequiredCardTag is
                    { } requiredTag
                && tags.Contains(
                    requiredTag,
                    StringComparer.Ordinal);
        });

        var matching = card.KeywordOverrides?
            .Where(item => item.Keyword == keyword)
            .ToArray();
        return matching is { Length: > 0 }
            ? matching[^1].Enabled
            : baseValue || grantedByAffliction || grantedByPower;
    }

    internal static bool IsLiveSlyAutoplaySource(
        CombatState combat, long cardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (!combat.DiscardPile.Contains(cardInstanceId))
            return false;
        var card = combat.Cards.FirstOrDefault(item =>
            item.InstanceId == cardInstanceId);
        return card is not null &&
            PrototypeContent.Card(card.CardId).Target ==
                PrototypeCardTarget.Enemy &&
            CardHasKeyword(
                combat, cardInstanceId, PrototypeCardKeyword.Sly);
    }

    private static CombatState ApplyCardKeywordOverride(
        CombatState combat,
        long cardInstanceId,
        PrototypeCardKeywordOverrideSpec action)
    {
        var cards = combat.Cards
            .Select(card =>
            {
                if (card.InstanceId != cardInstanceId)
                {
                    return card;
                }

                var overrides = card.KeywordOverrides
                    ?? Array.Empty<PrototypeCardKeywordOverride>();
                return card with
                {
                    KeywordOverrides = overrides.Append(
                        new PrototypeCardKeywordOverride(
                            action.Keyword,
                            action.Enabled,
                            action.Expiry)).ToArray()
                };
            })
            .ToArray();

        if (!cards.Any(card => card.InstanceId == cardInstanceId))
        {
            throw new InvalidOperationException(
                $"Combat card instance {cardInstanceId} is missing.");
        }

        return combat with { Cards = cards };
    }

    private static CombatState ApplyDrawnCardKeywordOverride(
        CombatState combat,
        PrototypeCardKeywordOverrideSpec? action,
        IEnumerable<long> drawnCardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(drawnCardInstanceIds);
        if (action is null)
            return combat;
        foreach (var cardInstanceId in drawnCardInstanceIds.Distinct())
            combat = ApplyCardKeywordOverride(
                combat, cardInstanceId, action);
        return combat;
    }

    private static CombatState ModifyCombatCardEnergyCost(
        CombatState combat,
        long cardInstanceId,
        int delta)
    {
        var cards = combat.Cards
            .Select(card =>
                card.InstanceId == cardInstanceId
                    ? card with
                    {
                        CombatEnergyCostDelta =
                            card.CombatEnergyCostDelta + delta
                    }
                    : card)
            .ToArray();

        if (!cards.Any(card => card.InstanceId == cardInstanceId))
        {
            throw new InvalidOperationException(
                $"Combat card instance {cardInstanceId} is missing.");
        }

        return combat with { Cards = cards };
    }

    private static int ResolveCardCostReductionCount(
        PrototypeCardCostSpec cost,
        CombatState combat) =>
        cost.ReductionCountKind is null
            ? 0
            : ResolveCombatCount(cost.ReductionCountKind.Value, combat);

    private static int ResolveCombatCount(
        PrototypeCombatCountKind kind,
        CombatState combat,
        int? targetEnemyId = null) =>
        kind switch
        {
            PrototypeCombatCountKind.SkillsInHand =>
                combat.Hand.Count(instanceId =>
                    PrototypeContent.Card(
                        RequireCombatCard(combat, instanceId).CardId).Type
                    == PrototypeCardType.Skill),
            PrototypeCombatCountKind.SkillsPlayedThisTurn =>
                combat.CounterState.SkillsPlayedThisTurn,
            PrototypeCombatCountKind.AttacksPlayedThisTurn =>
                combat.CounterState.AttacksPlayedThisTurn,
            PrototypeCombatCountKind.CardsDiscardedThisTurn =>
                combat.CounterState.CardsDiscardedThisTurn,
            PrototypeCombatCountKind.CardsDrawnThisCombat =>
                combat.CounterState.CardsDrawnThisCombat,
            PrototypeCombatCountKind.CardsPlayedThisCombat =>
                combat.CounterState.CardsPlayedThisCombat,
            PrototypeCombatCountKind.OtherCardsInHand =>
                combat.Hand.Length,
            PrototypeCombatCountKind.DrawPileCards =>
                combat.DrawPile.Length,
            PrototypeCombatCountKind.DiscardPileCards =>
                combat.DiscardPile.Length,
            PrototypeCombatCountKind.PlayerBlock =>
                combat.PlayerBlock,
            PrototypeCombatCountKind.TargetDebuffs =>
                combat.Enemies.Where(e => e.InstanceId == targetEnemyId
                    && e.Hp > 0).Sum(enemy =>
                    enemy.Statuses.Count(status => status.Value > 0)
                    + enemy.PowerStates.Count(power =>
                        power.Stacks > 0
                        && PrototypeContent.Power(power.PowerId).IsDebuff
                        && !PrototypeContent.Power(power.PowerId)
                            .RemoveAtEnemyTurnEnd)),

            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private static CombatState RecordCombatCounterEvent(
        CombatState combat,
        PrototypeCombatEvent combatEvent)
    {
        var counters = combat.CounterState;

        switch (combatEvent.Kind)
        {
            case PrototypeCombatEventKind.CardPlayed:
                if (combatEvent.CardId is null)
                {
                    return combat;
                }

                counters = counters with
                {
                    CardsPlayedThisTurn =
                        counters.CardsPlayedThisTurn + 1,
                    CardsPlayedThisCombat =
                        counters.CardsPlayedThisCombat + 1,
                    ManualCardsPlayedThisTurn = combatEvent.IsAutoPlay
                        ? counters.ManualCardsPlayedThisTurn
                        : counters.ManualCardsPlayedThisTurn + 1,
                    PlayedCardIdsThisTurn =
                        combatEvent.SourceCardInstanceId is { } playedId
                            ? (counters.PlayedCardIdsThisTurn
                                ?? Array.Empty<long>())
                                .Append(playedId).ToArray()
                            : counters.PlayedCardIdsThisTurn
                };

                var matchingCard = combatEvent.SourceCardInstanceId is { } sourceId
                    ? combat.Cards.FirstOrDefault(c => c.InstanceId == sourceId)
                    : null;
                var cardDefinition = matchingCard is null
                    ? PrototypeContent.Card(combatEvent.CardId)
                    : RuntimeCardDefinition(matchingCard);
                var type = cardDefinition.Type;
                // CardPlaysFinished is the native source for History
                // Course. Capture a completed non-dupe attack rather than
                // selecting an attack currently in hand/discard.
                if (type == PrototypeCardType.Attack
                    && combatEvent.SourceCardInstanceId is { } attackId
                    && combat.RelicStates.Any(relic => !relic.IsMelted && relic.RelicId ==
                        PrototypeNativeHiveEvents.HistoryCourseId))
                {
                    var attack = RequireCombatCard(combat, attackId);
                    if (!attack.IsHistoryCourseDupe)
                        counters = counters with
                        {
                            LastOriginalAttackThisTurn =
                                HistoryCourseSnapshot(attack)
                        };
                }
                counters = type switch
                {
                    PrototypeCardType.Attack => counters with
                    {
                        AttacksPlayedThisTurn =
                            counters.AttacksPlayedThisTurn + 1
                    },
                    PrototypeCardType.Skill => counters with
                    {
                        SkillsPlayedThisTurn =
                            counters.SkillsPlayedThisTurn + 1
                    },
                    _ => counters
                };

                var tags = cardDefinition.Tags
                    ?? Array.Empty<string>();
                if (tags.Length > 0)
                {
                    counters = counters with
                    {
                        PlayedCardTagsThisTurn =
                            counters.PlayedTags
                                .Concat(tags)
                                .ToArray()
                    };
                }

                break;

            case PrototypeCombatEventKind.CardDrawn:
                counters = counters with
                {
                    CardsDrawnThisCombat =
                        counters.CardsDrawnThisCombat + 1
                };
                break;

            case PrototypeCombatEventKind.CardDiscarded:
                counters = counters with
                {
                    CardsDiscardedThisTurn =
                        counters.CardsDiscardedThisTurn + 1
                };
                break;
        }

        var powers = combat.PlayerPowers;
        if (combatEvent.Kind == PrototypeCombatEventKind.CardPlayed
            && combatEvent.CardId is { } cardId
            && PrototypeContent.Card(cardId).Type == PrototypeCardType.Attack)
        {
            // Vigor lasts through every hit of the next attack, and is
            // consumed when that card's attack has completed.
            powers = powers.Where(power =>
                power.PowerId != "proto.power.vigor").ToArray();
        }

        return combat with { Counters = counters, PlayerPowers = powers };
    }

    private static bool EvaluateCombatPredicate(
        PrototypeCombatPredicateSpec? predicate,
        CombatState combat,
        int? targetEnemyId)
    {
        if (predicate is null)
        {
            return true;
        }

        return predicate.Kind switch
        {
            PrototypeCombatPredicateKind.DrawPileEmpty =>
                combat.DrawPile.Length == 0,
            PrototypeCombatPredicateKind.HandHasNoAttacks =>
                combat.Hand.All(instanceId =>
                    PrototypeContent.Card(
                        RequireCombatCard(combat, instanceId).CardId).Type
                    != PrototypeCardType.Attack),
            PrototypeCombatPredicateKind.HandHasOnlyAttacks =>
                combat.Hand.All(instanceId =>
                    PrototypeContent.Card(
                        RequireCombatCard(combat, instanceId).CardId).Type
                    == PrototypeCardType.Attack),

            PrototypeCombatPredicateKind.TargetHasStatus =>
                TargetHasStatus(
                    combat,
                    targetEnemyId,
                    predicate.StatusId),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static bool TargetHasStatus(
        CombatState combat,
        int? targetEnemyId,
        string? statusId)
    {
        if (targetEnemyId is null || statusId is null)
        {
            return false;
        }

        var enemy = combat.Enemies.FirstOrDefault(item =>
            item.InstanceId == targetEnemyId.Value);
        return enemy is not null
            && enemy.Statuses.GetValueOrDefault(statusId) > 0;
    }

    private static int ModifyPlayerAttackDamage(
        CombatState combat,
        long? sourceCardInstanceId,
        int targetEnemyId,
        int damage)
    {
        if (sourceCardInstanceId is null)
        {
            return damage;
        }

        var sourceCard = RequireCombatCard(
            combat,
            sourceCardInstanceId.Value);
        var sourceDefinition = PrototypeContent.Card(
            sourceCard.CardId);
        var tags = sourceDefinition.Tags
            ?? Array.Empty<string>();

        var additive = combat.PlayerPowers.Sum(power =>
        {
            var definition = PrototypeContent.Power(
                power.PowerId);
            var generic =
                definition.PlayerAttackDamageBonusPerStack
                * power.Stacks;
            var tagged = 0;
            if (definition.AttackDamageBonusPerStack != 0
                && definition.AttackDamageBonusRequiredCardTag is
                    { } requiredTag
                && tags.Contains(
                    requiredTag,
                    StringComparer.Ordinal))
            {
                tagged =
                    definition.AttackDamageBonusPerStack
                    * power.Stacks;
            }

            return generic + tagged;
        });

        var firstTaggedPlayBonus = combat.PlayerPowers.Sum(
            power =>
            {
                var definition = PrototypeContent.Power(
                    power.PowerId);
                if (power.Stacks <= 0
                    || definition
                        .FirstAttackDamageBonusPerStack == 0
                    || definition
                        .FirstAttackDamageBonusRequiredCardTag is
                        not { } requiredTag
                    || !tags.Contains(
                        requiredTag,
                        StringComparer.Ordinal)
                    || combat.CounterState.PlaysWithTag(
                        requiredTag) > 0)
                {
                    return 0;
                }

                return definition.FirstAttackDamageBonusPerStack
                    * power.Stacks;
            });

        var sharpBonus = sourceCard.Enchantment is
            { Kind: PrototypeCardEnchantmentKind.Sharp } sharp
                ? Math.Max(0, sharp.Amount)
                : 0;
        var vigorousBonus = sourceCard.Enchantment is
            {
                Kind: PrototypeCardEnchantmentKind.Vigorous,
                Disabled: false
            } vigorous
                ? Math.Max(0, vigorous.Amount)
                : 0;
        var modified =
            damage + sharpBonus + vigorousBonus + additive
            + firstTaggedPlayBonus;
        if (sourceCard.CardId == NativeGloryAncientsV111.MaulCardId)
        {
            modified += combat.CounterState.MaulDamageBonus;
        }
        var conditionalBonus = combat.PlayerPowers.Sum(power =>
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.AttackDamageBonusRequiredTargetStatus is null
                || definition.AttackDamageBonusTargetNumeratorPerStack == 0
                || definition.AttackDamageBonusTargetDenominator <= 0
                || !TargetHasStatus(
                    combat,
                    targetEnemyId,
                    definition.AttackDamageBonusRequiredTargetStatus))
            {
                return 0;
            }

            return (modified
                    * definition.AttackDamageBonusTargetNumeratorPerStack
                    * power.Stacks)
                / definition.AttackDamageBonusTargetDenominator;
        });

        modified += conditionalBonus;
        if (sourceDefinition.Type != PrototypeCardType.Attack)
        {
            return modified;
        }

        // Source: Instinct multiplies powered Attack damage by two.
        // This method is only used by powered-attack operations.
        if (sourceCard.Enchantment?.Kind
            == PrototypeCardEnchantmentKind.Instinct)
        {
            modified = checked(modified * 2);
        }

        return ModifyPlayerOutgoingAttackDamage(
            combat,
            modified);
    }

    private static int ModifyIncomingAttackDamage(
        CombatState combat,
        int enemyId,
        int damage)
    {
        var enemy = combat.Enemies.SingleOrDefault(item => item.InstanceId == enemyId)
            ?? throw new InvalidOperationException($"Enemy {enemyId} is missing.");

        var modified = damage;
        foreach (var status in enemy.Statuses)
        {
            var definition = PrototypeContent.Status(status.Key);
            modified = (modified * definition.IncomingAttackDamageNumerator)
                / definition.IncomingAttackDamageDenominator;
        }

        // Native SlowPower increments SlowAmount in AfterCardPlayed.
        // Damage during a card's resolution therefore observes only
        // previously completed plays, not the current card.
        var cardsPlayed = combat.CounterState.CardsPlayedThisTurn;
        var percentBonus = enemy.PowerStates.Sum(power =>
            PrototypeContent.Power(power.PowerId)
                .EnemyIncomingAttackDamagePercentPerCardPlayed
            * power.Stacks
            * cardsPlayed);
        if (percentBonus != 0)
        {
            modified =
                (modified * (100 + percentBonus))
                / 100;
        }

        return modified;
    }

    private static int PlayerAttackRetaliation(CombatState combat) =>
        combat.PlayerPowers.Sum(power =>
            PrototypeContent.Power(power.PowerId).AttackRetaliationPerStack * power.Stacks);

    private static int EnemyAttackRetaliation(
        CombatState combat,
        int enemyId)
    {
        var enemy = combat.Enemies.SingleOrDefault(item =>
            item.InstanceId == enemyId)
            ?? throw new InvalidOperationException($"Enemy {enemyId} is missing.");

        return enemy.PowerStates.Sum(power =>
            PrototypeContent.Power(power.PowerId).AttackRetaliationPerStack
            * power.Stacks);
    }

    private sealed record PrototypeDebuffBlockResult(
        CombatState Combat,
        bool Blocked);

    private static PrototypeDebuffBlockResult
        TryBlockIncomingPlayerDebuff(CombatState combat)
    {
        var candidate = combat.PlayerPowers
            .Where(power =>
                power.Stacks > 0
                && PrototypeContent.Power(power.PowerId)
                    .BlocksNextDebuff)
            .OrderBy(power => power.ApplicationOrder)
            .FirstOrDefault();
        if (candidate is null)
        {
            return new PrototypeDebuffBlockResult(
                combat,
                false);
        }

        var powers = combat.PlayerPowers.ToList();
        var index = powers.FindIndex(power =>
            power.ApplicationOrder
                == candidate.ApplicationOrder);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Debuff-blocking power disappeared before consumption.");
        }

        if (powers[index].Stacks <= 1)
        {
            powers.RemoveAt(index);
        }
        else
        {
            powers[index] = powers[index] with
            {
                Stacks = powers[index].Stacks - 1
            };
        }

        return new PrototypeDebuffBlockResult(
            combat with { PlayerPowers = powers.ToArray() },
            true);
    }

    private static (PlayerState Player, CombatState Combat)
        ApplyPlayerHpLoss(
            PlayerState player,
            CombatState combat,
            int amount)
    {
        amount = Math.Max(0, amount);
        if (amount == 0)
        {
            return (player, combat);
        }

        var buffer = combat.PlayerPowers
            .Where(power => power.Stacks > 0
                && PrototypeContent.Power(power.PowerId)
                    .PreventsNextHpLoss)
            .OrderBy(power => power.ApplicationOrder)
            .FirstOrDefault();
        if (buffer is not null)
        {
            var powers = combat.PlayerPowers
                .Select(power => power.Fork())
                .ToList();
            var index = powers.FindIndex(power =>
                power.ApplicationOrder == buffer.ApplicationOrder);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    "Buffer disappeared before consuming HP loss.");
            }

            if (powers[index].Stacks == 1)
            {
                powers.RemoveAt(index);
            }
            else
            {
                powers[index] = powers[index] with
                {
                    Stacks = powers[index].Stacks - 1
                };
            }

            return (player, combat with
            {
                PlayerPowers = powers.ToArray()
            });
        }

        return (player with
        {
            Hp = Math.Max(0, player.Hp - amount)
        }, combat);
    }

    private static CombatState ApplyPlayerPower(
        CombatState combat,
        string powerId,
        int stacks,
        int? sourceEnemyInstanceId = null,
        int storedValue = 0)
    {
        var definition = PrototypeContent.Power(powerId);
        if (definition.SourceBoundToEnemy
            && sourceEnemyInstanceId is null)
        {
            throw new InvalidOperationException(
                $"Source-bound power '{powerId}' requires an enemy source.");
        }

        sourceEnemyInstanceId =
            definition.SourceBoundToEnemy
                ? sourceEnemyInstanceId
                : null;

        var isDebuffApplication =
            (definition.IsDebuff && stacks > 0)
            || (definition.NegativeApplicationIsDebuff
                && stacks < 0);
        if (isDebuffApplication)
        {
            var blocked = TryBlockIncomingPlayerDebuff(combat);
            combat = blocked.Combat;
            if (blocked.Blocked)
            {
                return combat;
            }
        }

        if (definition.IsInstanced)
        {
            if (definition.RequiresCardPayload)
            {
                throw new InvalidOperationException(
                    $"Instanced power '{powerId}' requires a card payload.");
            }

            return AddPlayerPowerInstance(
                combat,
                powerId,
                stacks,
                null,
                sourceEnemyInstanceId,
                storedValue);
        }

        var powers = combat.PlayerPowers.ToList();
        var index = powers.FindIndex(power =>
            StringComparer.Ordinal.Equals(power.PowerId, powerId)
            && (!definition.SourceBoundToEnemy
                || power.SourceEnemyInstanceId
                    == sourceEnemyInstanceId));

        if (index >= 0)
        {
            var nextStacks = definition.DoesNotStack
                ? Math.Max(powers[index].Stacks, stacks)
                : powers[index].Stacks + stacks;
            PrototypePowerInstanceState? updatedPower = null;
            if (nextStacks == 0
                || (!definition.AllowNegative && nextStacks < 0))
            {
                powers.RemoveAt(index);
            }
            else
            {
                powers[index] = powers[index] with
                {
                    Stacks = nextStacks
                };
                updatedPower = powers[index];
            }

            combat = combat with
            {
                PlayerPowers = powers.ToArray()
            };
            return updatedPower is null
                ? combat
                : ApplySourceBoundCardEffects(
                    combat,
                    updatedPower);
        }

        if (stacks == 0 || (!definition.AllowNegative && stacks < 0))
        {
            return combat;
        }

        var instance = new PrototypePowerInstanceState(
            powerId,
            stacks,
            combat.NextPowerApplicationOrder,
            SourceEnemyInstanceId: sourceEnemyInstanceId);
        powers.Add(instance);

        combat = combat with
        {
            PlayerPowers = powers.ToArray(),
            NextPowerApplicationOrder =
                combat.NextPowerApplicationOrder + 1
        };
        return ApplySourceBoundCardEffects(
            combat,
            instance);
    }

    private static CombatState ApplyPlayerPowerWithSelectedCard(
        CombatState combat,
        PrototypeSelectedCardPowerAction action,
        CombatCardInstance selectedCard)
    {
        var definition = PrototypeContent.Power(action.PowerId);
        if (!definition.IsInstanced || !definition.RequiresCardPayload)
        {
            throw new InvalidOperationException(
                $"Selected-card power '{action.PowerId}' must be an instanced card-payload power.");
        }

        var snapshot = new PrototypeCombatCardSnapshot(
            selectedCard.CardId,
            action.RestoreSuppressedUpgradesInPayload
                ? selectedCard.UpgradeLevel
                    + selectedCard.SuppressedUpgradeLevels
                : selectedCard.UpgradeLevel,
            selectedCard.State.Clone(),
            selectedCard.CombatEnergyCostDelta,
            selectedCard.TemporaryEnergyCost is null
                ? null
                : selectedCard.TemporaryEnergyCost with { },
            selectedCard.KeywordOverrides is null
                ? null
                : selectedCard.KeywordOverrides
                    .Select(item => item with { })
                    .ToArray(),
            selectedCard.ReplayCount,
            selectedCard.Enchantment,
            selectedCard.EnchantmentTriggeredThisCombat,
            action.ClearAfflictionFromPayload
                ? null
                : selectedCard.Affliction,
            action.RestoreSuppressedUpgradesInPayload
                ? 0
                : selectedCard.SuppressedUpgradeLevels);

        return AddPlayerPowerInstance(
            combat,
            action.PowerId,
            action.Amount,
            snapshot);
    }

    private static CombatState AddPlayerPowerInstance(
        CombatState combat,
        string powerId,
        int stacks,
        PrototypeCombatCardSnapshot? cardPayload,
        int? sourceEnemyInstanceId = null,
        int storedValue = 0)
    {
        var definition = PrototypeContent.Power(powerId);
        if (stacks == 0 || (!definition.AllowNegative && stacks < 0))
        {
            return combat;
        }

        if (definition.RequiresCardPayload != (cardPayload is not null))
        {
            throw new InvalidOperationException(
                $"Power '{powerId}' card-payload requirement is not satisfied.");
        }

        if (definition.SourceBoundToEnemy
            && sourceEnemyInstanceId is null)
        {
            throw new InvalidOperationException(
                $"Source-bound power '{powerId}' requires an enemy source.");
        }

        combat = combat with
        {
            PlayerPowers = combat.PlayerPowers.Append(
                new PrototypePowerInstanceState(
                    powerId,
                    stacks,
                    combat.NextPowerApplicationOrder,
                    cardPayload?.Fork(),
                    sourceEnemyInstanceId,
                    StoredValue: storedValue,
                    TriggerCounts: new int[definition.Triggers.Length])).ToArray(),
            NextPowerApplicationOrder =
                combat.NextPowerApplicationOrder + 1
        };

        var added = combat.PlayerPowers[^1];
        return ApplySourceBoundCardEffects(
            combat,
            added);
    }

    private static CombatState ApplySourceBoundCardEffects(
        CombatState combat,
        PrototypePowerInstanceState power)
    {
        combat = ApplySourceBoundCardAffliction(
            combat,
            power);
        combat = ApplyPowerCardAffliction(
            combat,
            power);
        return ApplySourceBoundCardDowngrade(
            combat,
            power);
    }

    private static CombatState ApplySourceBoundCardDowngrade(
        CombatState combat,
        PrototypePowerInstanceState power)
    {
        var definition = PrototypeContent.Power(power.PowerId);
        if (!definition.DowngradeExistingCardsOnApply)
        {
            return combat;
        }

        if (power.SourceEnemyInstanceId is not { } sourceEnemyId)
        {
            throw new InvalidOperationException(
                $"Downgrade source power '{power.PowerId}' is missing its enemy source.");
        }

        var source = combat.Enemies.FirstOrDefault(enemy =>
            enemy.InstanceId == sourceEnemyId);
        if (source is null || source.Hp <= 0)
        {
            return combat;
        }

        return combat with
        {
            Cards = combat.Cards
                .Select(card =>
                {
                    if (card.UpgradeLevel <= 0)
                    {
                        return card;
                    }

                    return card with
                    {
                        SuppressedUpgradeLevels =
                            card.SuppressedUpgradeLevels
                            + card.UpgradeLevel,
                        UpgradeLevel = 0
                    };
                })
                .ToArray()
        };
    }

    private static CombatState ApplyPowerCardAffliction(
        CombatState combat,
        PrototypePowerInstanceState power)
    {
        var definition = PrototypeContent.Power(power.PowerId);
        if (definition.AppliedCardAffliction is not
                { } afflictionKind)
        {
            return combat;
        }

        return combat with
        {
            Cards = combat.Cards
                .Select(card =>
                {
                    if (definition
                            .AppliedCardAfflictionRequiredCardType
                            is { } requiredType
                        && PrototypeContent.Card(card.CardId).Type
                            != requiredType)
                    {
                        return card;
                    }

                    if (card.Affliction is not null
                        && definition.SkipCardsWithExistingAffliction)
                    {
                        return card;
                    }

                    return card with
                    {
                        Affliction =
                            new PrototypeCardAffliction(
                                afflictionKind)
                    };
                })
                .ToArray()
        };
    }

    private static CombatState ApplySourceBoundCardAffliction(
        CombatState combat,
        PrototypePowerInstanceState power)
    {
        var definition = PrototypeContent.Power(power.PowerId);
        if (definition.SourceBoundCardAffliction is not
                { } afflictionKind)
        {
            return combat;
        }

        if (power.SourceEnemyInstanceId is not { } sourceEnemyId)
        {
            throw new InvalidOperationException(
                $"Affliction source power '{power.PowerId}' is missing its enemy source.");
        }

        var source = combat.Enemies.FirstOrDefault(enemy =>
            enemy.InstanceId == sourceEnemyId);
        if (source is null || source.Hp <= 0)
        {
            return combat;
        }

        return combat with
        {
            Cards = combat.Cards
                .Select(card =>
                {
                    if (card.Affliction is not null
                        && definition.SkipCardsWithExistingAffliction)
                    {
                        return card;
                    }

                    return card with
                    {
                        Affliction = new PrototypeCardAffliction(
                            afflictionKind,
                            SourceEnemyInstanceId: sourceEnemyId)
                    };
                })
                .ToArray()
        };
    }

    private static CombatCardInstance
        ApplyActiveSourceBoundAfflictionToCard(
            CombatState combat,
            CombatCardInstance card)
    {
        if (card.Affliction is null
            && PrototypeContent.Card(card.CardId).Type
                == PrototypeCardType.Power)
        {
            var source = combat.Enemies
                .Where(enemy => enemy.Hp > 0)
                .SelectMany(enemy => enemy.PowerStates
                    .Where(power => power.Stacks > 0
                        && PrototypeContent.Power(power.PowerId)
                            .GalvanizePowerCardsPerStack > 0)
                    .Select(power => (Enemy: enemy, Power: power)))
                .FirstOrDefault();
            if (source.Enemy is not null)
            {
                return card with
                {
                    Affliction = new PrototypeCardAffliction(
                        PrototypeCardAfflictionKind.Galvanized,
                        source.Power.Stacks * PrototypeContent.Power(
                            source.Power.PowerId).GalvanizePowerCardsPerStack,
                        source.Enemy.InstanceId)
                };
            }
        }

        foreach (var power in combat.PlayerPowers
                     .OrderBy(item => item.ApplicationOrder))
        {
            var definition = PrototypeContent.Power(
                power.PowerId);
            if (definition.SourceBoundCardAffliction is
                    { } sourceAfflictionKind
                && power.SourceEnemyInstanceId is
                    { } sourceEnemyId)
            {
                var source = combat.Enemies.FirstOrDefault(enemy =>
                    enemy.InstanceId == sourceEnemyId);
                if (source is not null && source.Hp > 0)
                {
                    if (card.Affliction is not null
                        && definition.SkipCardsWithExistingAffliction)
                    {
                        continue;
                    }

                    return card with
                    {
                        Affliction = new PrototypeCardAffliction(
                            sourceAfflictionKind,
                            SourceEnemyInstanceId: sourceEnemyId)
                    };
                }
            }

            if (definition.SkillPlayAffliction is
                    { } skillAffliction
                && combat.CounterState.SkillsPlayedThisTurn > 0
                && PrototypeContent.Card(card.CardId).Type
                    == PrototypeCardType.Skill
                && card.Affliction is null)
            {
                return card with
                {
                    Affliction = new PrototypeCardAffliction(
                        skillAffliction)
                };
            }

            if (definition.AppliedCardAffliction is
                    { } appliedAfflictionKind)
            {
                if (definition
                        .AppliedCardAfflictionRequiredCardType
                        is { } requiredType
                    && PrototypeContent.Card(card.CardId).Type
                        != requiredType)
                {
                    continue;
                }

                if (card.Affliction is not null
                    && definition.SkipCardsWithExistingAffliction)
                {
                    continue;
                }

                return card with
                {
                    Affliction = new PrototypeCardAffliction(
                        appliedAfflictionKind)
                };
            }
        }

        return card;
    }

    private static CombatState
        CleanupSourceBoundPowersForDefeatedEnemies(
            CombatState combat)
    {
        var leaderHp = combat.Enemies
            .ToDictionary(
                enemy => enemy.InstanceId,
                enemy => enemy.Hp);
        var cleanedEnemies = combat.Enemies
            .Select(enemy =>
                enemy.LeaderEnemyInstanceId is
                        { } leaderId
                    && leaderHp.GetValueOrDefault(
                        leaderId) <= 0
                    ? enemy with { Hp = 0, Block = 0 }
                    : enemy)
            .ToArray();
        combat = combat with
        {
            Enemies = cleanedEnemies
        };

        var defeatedEnemyIds = combat.Enemies
            .Where(enemy => enemy.Hp <= 0)
            .Select(enemy => enemy.InstanceId)
            .ToHashSet();
        if (defeatedEnemyIds.Count == 0)
        {
            return combat;
        }

        var removed = combat.PlayerPowers
            .Where(power =>
                power.SourceEnemyInstanceId is { } sourceEnemyId
                && defeatedEnemyIds.Contains(sourceEnemyId)
                && PrototypeContent.Power(power.PowerId)
                    .SourceBoundToEnemy)
            .ToArray();
        if (removed.Length == 0)
        {
            return combat;
        }

        var clearAfflictions = removed
            .Select(power =>
            {
                var definition = PrototypeContent.Power(
                    power.PowerId);
                return (
                    SourceEnemyId:
                        power.SourceEnemyInstanceId!.Value,
                    Kind:
                        definition.SourceBoundCardAffliction,
                    Clear:
                        definition.ClearSourceAfflictionWhenRemoved);
            })
            .Where(item =>
                item.Clear && item.Kind is not null)
            .Select(item => (
                item.SourceEnemyId,
                Kind: item.Kind!.Value))
            .ToHashSet();

        var removedOrders = removed
            .Select(power => power.ApplicationOrder)
            .ToHashSet();
        var remainingPowers = combat.PlayerPowers
            .Where(power =>
                !removedOrders.Contains(
                    power.ApplicationOrder))
            .ToArray();

        var restoreDowngradePowerIds = removed
            .Select(power => power.PowerId)
            .Distinct(StringComparer.Ordinal)
            .Where(powerId =>
            {
                var definition = PrototypeContent.Power(powerId);
                return definition
                        .RestoreDowngradedCardsWhenLastSourceRemoved
                    && !remainingPowers.Any(power =>
                        StringComparer.Ordinal.Equals(
                            power.PowerId,
                            powerId));
            })
            .ToHashSet(StringComparer.Ordinal);

        var shouldRestoreSuppressedUpgrades =
            restoreDowngradePowerIds.Count > 0;

        return combat with
        {
            PlayerPowers = remainingPowers,
            Cards = combat.Cards
                .Select(card =>
                {
                    var next = card;
                    if (card.Affliction is
                            {
                                SourceEnemyInstanceId:
                                    { } sourceEnemyId
                            } affliction
                        && clearAfflictions.Contains((
                            sourceEnemyId,
                            affliction.Kind)))
                    {
                        next = next with
                        {
                            Affliction = null
                        };
                    }

                    if (shouldRestoreSuppressedUpgrades
                        && next.SuppressedUpgradeLevels > 0)
                    {
                        next = next with
                        {
                            UpgradeLevel =
                                next.UpgradeLevel
                                + next.SuppressedUpgradeLevels,
                            SuppressedUpgradeLevels = 0
                        };
                    }

                    return next;
                })
                .ToArray()
        };
    }

    private static (
        CombatState Combat,
        EnemyCombatState Enemy)
        ApplyEnemyPowerToState(
            CombatState combat,
            EnemyCombatState enemy,
            string powerId,
            int stacks,
            bool ignoreDebuffPrevention = false)
    {
        var definition = PrototypeContent.Power(powerId);
        if (definition.IsInstanced
            && definition.RequiresCardPayload)
        {
            throw new InvalidOperationException(
                $"Enemy power '{powerId}' requires a card payload.");
        }

        if (enemy.Hp <= 0)
        {
            return (combat, enemy);
        }

        var isDebuffApplication =
            (definition.IsDebuff && stacks > 0)
            || (definition.NegativeApplicationIsDebuff
                && stacks < 0);
        if (isDebuffApplication && !ignoreDebuffPrevention)
        {
            var blocked =
                TryBlockIncomingEnemyDebuff(enemy);
            enemy = blocked.Enemy;
            if (blocked.Blocked)
            {
                return (combat, enemy);
            }
        }

        var powers = enemy.PowerStates.ToList();
        var powerIndex = definition.IsInstanced
            ? -1
            : powers.FindIndex(power =>
                StringComparer.Ordinal.Equals(
                    power.PowerId,
                    powerId));

        if (powerIndex >= 0)
        {
            var nextStacks =
                powers[powerIndex].Stacks + stacks;
            if (nextStacks == 0
                || (!definition.AllowNegative
                    && nextStacks < 0))
            {
                powers.RemoveAt(powerIndex);
            }
            else
            {
                powers[powerIndex] =
                    powers[powerIndex] with
                    {
                        Stacks = nextStacks,
                        SkipNextEnemySideTurnEnd =
                            definition.SkipInitialEnemySideTurnEnd
                                && stacks > 0
                            || powers[powerIndex].SkipNextEnemySideTurnEnd
                    };
            }

            var changedEnemy = enemy with { Powers = powers.ToArray() };
            var strengthDelta =
                stacks * definition.EnemyStrengthOnApplyPerStack;
            return strengthDelta == 0
                ? (combat, changedEnemy)
                : ApplyEnemyPowerToState(
                    combat, changedEnemy, "proto.power.strength",
                    strengthDelta, ignoreDebuffPrevention: true);
        }

        if (stacks == 0
            || (!definition.AllowNegative && stacks < 0))
        {
            return (combat, enemy);
        }

        powers.Add(new PrototypePowerInstanceState(
            powerId,
            stacks,
            combat.NextPowerApplicationOrder,
            SkipNextEnemySideTurnEnd:
                definition.SkipInitialEnemySideTurnEnd));

        var nextCombat = combat with
        {
            NextPowerApplicationOrder =
                combat.NextPowerApplicationOrder + 1
        };
        var nextEnemy = enemy with { Powers = powers.ToArray() };
        var appliedStrengthDelta =
            stacks * definition.EnemyStrengthOnApplyPerStack;
        return appliedStrengthDelta == 0
            ? (nextCombat, nextEnemy)
            : ApplyEnemyPowerToState(
                nextCombat, nextEnemy, "proto.power.strength",
                appliedStrengthDelta, ignoreDebuffPrevention: true);
    }

    private static CombatState ApplyEnemyPower(
        CombatState combat,
        int enemyId,
        string powerId,
        int stacks)
    {
        var enemies = combat.Enemies
            .Select(enemy => enemy.Fork())
            .ToArray();
        var enemyIndex = Array.FindIndex(
            enemies,
            enemy => enemy.InstanceId == enemyId);
        if (enemyIndex < 0)
        {
            throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");
        }

        var result = ApplyEnemyPowerToState(
            combat,
            enemies[enemyIndex],
            powerId,
            stacks);
        enemies[enemyIndex] = result.Enemy;
        return result.Combat with { Enemies = enemies };
    }

    private static bool RelicTriggerMatchesPlayer(
        PrototypeRelicTriggerSpec trigger,
        PlayerState player,
        CombatState combat)
    {
        if (trigger.TurnEquals is { } turn
            && combat.Turn != turn)
        {
            return false;
        }

        if (trigger.MinTurn is { } minTurn
            && combat.Turn < minTurn)
        {
            return false;
        }

        if (trigger.MaxAttacksPlayedLastTurn is { } maxAttacks
            && combat.CounterState.AttacksPlayedLastTurn > maxAttacks)
        {
            return false;
        }

        if (trigger.RequiresZeroPlayerBlock
            && combat.PlayerBlock != 0)
        {
            return false;
        }

        if (player.MaxHp <= 0)
        {
            return trigger.MaxPlayerHpPercent is null
                && trigger.MinPlayerHpPercent is null;
        }

        var hpTimesHundred = player.Hp * 100;
        if (trigger.MaxPlayerHpPercent is { } maxPercent
            && hpTimesHundred > player.MaxHp * maxPercent)
        {
            return false;
        }

        if (trigger.MinPlayerHpPercent is { } minPercent
            && hpTimesHundred < player.MaxHp * minPercent)
        {
            return false;
        }

        return true;
    }

    private static CombatState ResetPerTurnRelicCounters(
        CombatState combat)
    {
        var relics = combat.RelicStates
            .Select(relic => relic.Fork())
            .ToArray();

        for (var relicIndex = 0;
             relicIndex < relics.Length;
             relicIndex++)
        {
            var definition =
                PrototypeContent.Relic(
                    relics[relicIndex].RelicId);
            var triggers =
                definition.Triggers
                ?? Array.Empty<PrototypeRelicTriggerSpec>();
            var counts =
                (int[])relics[relicIndex]
                    .TriggerCounts.Clone();

            for (var triggerIndex = 0;
                 triggerIndex < triggers.Length;
                 triggerIndex++)
            {
                if (triggers[triggerIndex]
                    .ResetCounterEachTurn)
                {
                    counts[triggerIndex] = 0;
                }
            }

            relics[relicIndex] =
                relics[relicIndex] with
                {
                    TriggerCounts = counts
                };
        }

        return combat with { Relics = relics };
    }

    private static (CombatState Combat, int Count) IncrementRelicTriggerCounter(
        CombatState combat,
        int relicStateIndex,
        int triggerIndex)
    {
        var relics = combat.RelicStates.Select(relic => relic.Fork()).ToArray();
        if (relicStateIndex < 0 || relicStateIndex >= relics.Length)
        {
            throw new InvalidOperationException("Relic trigger references a missing combat relic.");
        }

        var relic = relics[relicStateIndex];
        if (triggerIndex < 0 || triggerIndex >= relic.TriggerCounts.Length)
        {
            throw new InvalidOperationException("Relic trigger counter index is invalid.");
        }

        var counts = (int[])relic.TriggerCounts.Clone();
        counts[triggerIndex]++;
        relics[relicStateIndex] = relic with { TriggerCounts = counts };

        return (combat with { Relics = relics }, counts[triggerIndex]);
    }

    private static int[] CreateRelicTriggerCounts(
        RelicInstance relic,
        PrototypeRelicDefinition definition)
    {
        var counts = new int[(definition.Triggers
            ?? Array.Empty<PrototypeRelicTriggerSpec>()).Length];
        if (definition.PersistentTriggerCountProperty is not { } property
            || counts.Length == 0
            || relic.PersistentState.ValueKind !=
                System.Text.Json.JsonValueKind.Object
            || !relic.PersistentState.TryGetProperty(property, out var count)
            || count.ValueKind != System.Text.Json.JsonValueKind.Number)
        {
            return counts;
        }

        counts[0] = Math.Max(0, count.GetInt32());
        return counts;
    }

    private static PlayerState PersistRelicTriggerCount(
        PlayerState player,
        CombatRelicState relicState,
        int triggerIndex,
        int count)
    {
        var property = PrototypeContent.Relic(relicState.RelicId)
            .PersistentTriggerCountProperty;
        if (property is null || triggerIndex != 0
            || relicState.PersistentIndex < 0
            || relicState.PersistentIndex >= player.Relics.Length)
        {
            return player;
        }

        var relics = (RelicInstance[])player.Relics.Clone();
        var relic = relics[relicState.PersistentIndex];
        var persistent = relic.PersistentState.ValueKind ==
                System.Text.Json.JsonValueKind.Object
            ? System.Text.Json.Nodes.JsonNode.Parse(
                relic.PersistentState.GetRawText()) as
                    System.Text.Json.Nodes.JsonObject
                ?? new System.Text.Json.Nodes.JsonObject()
            : new System.Text.Json.Nodes.JsonObject();
        persistent[property] = count;
        relics[relicState.PersistentIndex] = relic with
        {
            PersistentState = System.Text.Json.JsonSerializer
                .SerializeToElement(persistent)
        };
        return player with { Relics = relics };
    }

    // This is keyed by power application order, so multiple Panaches or
    // separate countdowns remain independent and fork deterministically.
    private static (CombatState Combat, bool ShouldTrigger)
        CheckPowerSubscriberTrigger(
            CombatState combat,
            PrototypeEventSubscriberState subscriber)
    {
        if (subscriber.SourcePowerApplicationOrder is not { } order)
        {
            return (combat, false);
        }

        var index = Array.FindIndex(combat.PlayerPowers,
            power => power.ApplicationOrder == order);
        if (index < 0)
        {
            return (combat, false);
        }

        var current = combat.PlayerPowers[index];
        if (subscriber.CountdownBeforeTrigger)
        {
            if (current.Stacks > 1)
            {
                var powers = (PrototypePowerInstanceState[])combat.PlayerPowers.Clone();
                powers[index] = current with { Stacks = current.Stacks - 1 };
                return (combat with { PlayerPowers = powers }, false);
            }

            return (combat, true);
        }

        if (subscriber.EveryNth <= 1)
        {
            return (combat, true);
        }

        if (subscriber.PowerTriggerIndex is not { } triggerIndex)
        {
            throw new InvalidOperationException("Power trigger counter has no index.");
        }

        var counts = current.TriggerCounts is null
            ? new int[PrototypeContent.Power(current.PowerId).Triggers.Length]
            : (int[])current.TriggerCounts.Clone();
        var nextCount = checked(counts[triggerIndex] + 1);
        // Counter-style native powers restart at zero after their Nth
        // trigger; saving the residue also bounds the persisted state.
        counts[triggerIndex] = nextCount % subscriber.EveryNth;
        var updated = (PrototypePowerInstanceState[])combat.PlayerPowers.Clone();
        updated[index] = current with { TriggerCounts = counts };
        return (combat with { PlayerPowers = updated },
            counts[triggerIndex] == 0);
    }

    private static CombatState ResetPlayerPowerTriggerCounters(CombatState combat) =>
        combat with
        {
            PlayerPowers = combat.PlayerPowers.Select(power =>
                PrototypeContent.Power(power.PowerId)
                    .ResetTriggerCountersAtPlayerTurnEnd
                    && power.TriggerCounts is not null
                        ? power with
                        {
                            TriggerCounts = new int[power.TriggerCounts.Length]
                        }
                        : power).ToArray()
        };


    // Both hooks run at the native AfterCardPlayed boundary: after the
    // effects of the card have resolved, before the next card action.
    // Tender restores only Strength/Dexterity actually lost (Artifact can
    // block either application), with counters stored on the power instance.
    // Curl Up waits for the *same card instance* that first attacked it.
    private static CombatState RefreshNativeGloryGalvanic(
        CombatState combat)
    {
        var galvanicSource = combat.Enemies
            .Where(enemy => enemy.Hp > 0)
            .SelectMany(enemy => enemy.PowerStates
                .Where(power => power.Stacks > 0
                    && PrototypeContent.Power(power.PowerId)
                        .GalvanizePowerCardsPerStack > 0)
                .Select(power => (
                    Enemy: enemy,
                    Amount: power.Stacks * PrototypeContent.Power(
                        power.PowerId).GalvanizePowerCardsPerStack)))
            .FirstOrDefault();
        if (galvanicSource.Enemy is null)
            return combat;

        return combat with
        {
            Cards = combat.Cards.Select(card =>
                card.Affliction is null
                && PrototypeContent.Card(card.CardId).Type
                    == PrototypeCardType.Power
                    ? card with
                    {
                        Affliction = new PrototypeCardAffliction(
                            PrototypeCardAfflictionKind.Galvanized,
                            galvanicSource.Amount,
                            galvanicSource.Enemy.InstanceId)
                    }
                    : card).ToArray()
        };
    }

    private static CombatState RefreshNativeHiveVitalSpark(
        CombatState combat)
    {
        var sources = combat.Enemies
            .Where(enemy => enemy.Hp > 0)
            .Select(enemy => (enemy.InstanceId,
                Amount: enemy.PowerStates
                    .Where(power => power.PowerId ==
                        PrototypeNativeHiveElites.VitalSparkId)
                    .Sum(power => Math.Max(0, power.Stacks))))
            .Where(source => source.Amount > 0)
            .OrderBy(source => source.InstanceId)
            .ToArray();

        // One native Infested Prism per encounter. Retain deterministic
        // behavior for forced multi-Prism scenarios.
        var owner = sources.FirstOrDefault();
        return combat with
        {
            Cards = combat.Cards.Select(card =>
            {
                if (card.Affliction is
                    { Kind: PrototypeCardAfflictionKind.Tainted })
                {
                    return owner.Amount > 0
                        ? card with
                        {
                            Affliction = new PrototypeCardAffliction(
                                PrototypeCardAfflictionKind.Tainted,
                                owner.Amount, owner.InstanceId)
                        }
                        : card with { Affliction = null };
                }
                if (owner.Amount <= 0 || card.Affliction is not null
                    || PrototypeContent.Card(card.CardId).Type
                        != PrototypeCardType.Skill)
                    return card;
                return card with
                {
                    Affliction = new PrototypeCardAffliction(
                        PrototypeCardAfflictionKind.Tainted,
                        owner.Amount, owner.InstanceId)
                };
            }).ToArray()
        };
    }

    private static CombatState ResolveNativeCardCompletionPowers(
        CombatState combat, PrototypeCombatEvent combatEvent)
    {
        if (combatEvent.Kind == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            foreach (var tender in combat.PlayerPowers.Where(power =>
                PrototypeContent.Power(power.PowerId)
                    .PlayerStrengthDexterityLossPerCardPlayedPerStack > 0)
                .ToArray())
            {
                var reduced = tender.TriggerCounts ?? Array.Empty<int>();
                if (reduced.Length >= 2)
                {
                    if (reduced[0] > 0)
                        combat = ApplyPlayerPower(combat,
                            "proto.power.strength", reduced[0]);
                    if (reduced[1] > 0)
                        combat = ApplyPlayerPower(combat,
                            "proto.power.dexterity", reduced[1]);
                }
                combat = combat with
                {
                    PlayerPowers = combat.PlayerPowers.Select(power =>
                        power.ApplicationOrder == tender.ApplicationOrder
                            ? power with { TriggerCounts = null,
                                StoredValue = 0 }
                            : power).ToArray()
                };
            }
            return combat;
        }

        if (combatEvent.Kind != PrototypeCombatEventKind.CardPlayed
            || combatEvent.CardId is null)
            return combat;

        // Frantic Escape postpones the Sandpit countdown by one and
        // increases this card instance's cost for the rest of combat.
        if (combatEvent.CardId ==
                PrototypeNativeHiveBosses.FranticEscapeId
            && combatEvent.SourceCardInstanceId is { } escapeId)
        {
            var owner = combat.Enemies.FirstOrDefault(enemy =>
                enemy.Hp > 0 && enemy.PowerStates.Any(power =>
                    power.PowerId == PrototypeNativeHiveBosses.SandpitId));
            if (owner is not null)
                combat = ApplyEnemyPower(combat, owner.InstanceId,
                    PrototypeNativeHiveBosses.SandpitId, 1);
            combat = combat with
            {
                Cards = combat.Cards.Select(card =>
                    card.InstanceId == escapeId
                        ? card with
                        {
                            CombatEnergyCostDelta =
                                card.CombatEnergyCostDelta + 1
                        }
                        : card).ToArray()
            };
        }

        // Each completed afflicted Skill inflicts Tainted equal to
        // the number of Vital Spark stacks stamped on the card.
        if (combatEvent.SourceCardInstanceId is { } playedId)
        {
            var played = combat.Cards.FirstOrDefault(card =>
                card.InstanceId == playedId);
            if (played?.Affliction is
                { Kind: PrototypeCardAfflictionKind.Tainted } affliction)
            {
                combat = ApplyPlayerPower(combat,
                    PrototypeNativeHiveElites.TaintedPowerId,
                    affliction.Amount);
            }
        }

        foreach (var tender in combat.PlayerPowers.Where(power =>
            power.Stacks > 0
            && PrototypeContent.Power(power.PowerId)
                .PlayerStrengthDexterityLossPerCardPlayedPerStack > 0)
            .ToArray())
        {
            var amount = powerLoss(tender);
            var beforeStrength = combat.PlayerPowers
                .Where(power => power.PowerId == "proto.power.strength")
                .Sum(power => power.Stacks);
            combat = ApplyPlayerPower(combat, "proto.power.strength", -amount);
            var afterStrength = combat.PlayerPowers
                .Where(power => power.PowerId == "proto.power.strength")
                .Sum(power => power.Stacks);
            var beforeDexterity = combat.PlayerPowers
                .Where(power => power.PowerId == "proto.power.dexterity")
                .Sum(power => power.Stacks);
            combat = ApplyPlayerPower(combat, "proto.power.dexterity", -amount);
            var afterDexterity = combat.PlayerPowers
                .Where(power => power.PowerId == "proto.power.dexterity")
                .Sum(power => power.Stacks);
            var previous = tender.TriggerCounts ?? new int[2];
            combat = combat with
            {
                PlayerPowers = combat.PlayerPowers.Select(power =>
                    power.ApplicationOrder == tender.ApplicationOrder
                        ? power with
                        {
                            TriggerCounts = new[]
                            {
                                previous[0] + beforeStrength - afterStrength,
                                previous[1] + beforeDexterity - afterDexterity
                            },
                            StoredValue = power.StoredValue + 1
                        } : power).ToArray()
            };
        }

        if (combatEvent.SourceCardInstanceId is { } cardId)
        {
            combat = combat with
            {
                Enemies = combat.Enemies.Select(enemy =>
                {
                    if (enemy.Hp <= 0)
                        return enemy;
                    var completing = enemy.PowerStates.Where(power =>
                        power.PendingAttackingCardInstanceId == cardId
                        && PrototypeContent.Power(power.PowerId)
                            .EnemyBlockAfterAttackingCardPlayedPerStack > 0)
                        .ToArray();
                    if (completing.Length == 0)
                        return enemy;
                    var block = completing.Sum(power => power.Stacks
                        * PrototypeContent.Power(power.PowerId)
                            .EnemyBlockAfterAttackingCardPlayedPerStack);
                    var orders = completing.Select(power =>
                        power.ApplicationOrder).ToHashSet();
                    return enemy with
                    {
                        Block = enemy.Block + block,
                        Powers = enemy.PowerStates.Where(power =>
                            !orders.Contains(power.ApplicationOrder)).ToArray()
                    };
                }).ToArray()
            };
        }
        return combat;

        static int powerLoss(PrototypePowerInstanceState power) =>
            power.Stacks * PrototypeContent.Power(power.PowerId)
                .PlayerStrengthDexterityLossPerCardPlayedPerStack;
    }

    private static CombatState ResolveNativeGloryBossCardHooks(
        CombatState combat, PrototypeCombatEvent combatEvent)
    {
        if (combatEvent.Kind == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            return combat with
            {
                BoundCardPlayedThisTurn = false,
                Cards = combat.Cards.Select(card =>
                    card.Affliction?.Kind == PrototypeCardAfflictionKind.Bound
                        ? card with { Affliction = null }
                        : card).ToArray(),
                PlayerPowers = combat.PlayerPowers.Select(power =>
                    power.PowerId == PrototypeNativeGloryBosses.ChainsId
                        ? power with { StoredValue = 0 } : power).ToArray()
            };
        }

        if (combatEvent.Kind == PrototypeCombatEventKind.CardDrawn
            && combatEvent.SourceCardInstanceId is { } drawnId)
        {
            var card = combat.Cards.FirstOrDefault(candidate =>
                candidate.InstanceId == drawnId);
            if (card is not null && card.Affliction is null)
            {
                for (var powerIndex = 0; powerIndex < combat.PlayerPowers.Length;
                     powerIndex++)
                {
                    var power = combat.PlayerPowers[powerIndex];
                    if (power.PowerId != PrototypeNativeGloryBosses.ChainsId
                        || power.StoredValue >= power.Stacks
                        || power.SourceEnemyInstanceId is not { } sourceId
                        || !combat.Enemies.Any(enemy =>
                            enemy.InstanceId == sourceId && enemy.Hp > 0))
                        continue;
                    combat = combat with
                    {
                        Cards = combat.Cards.Select(candidate =>
                            candidate.InstanceId == drawnId
                                ? candidate with
                                {
                                    Affliction = new PrototypeCardAffliction(
                                        PrototypeCardAfflictionKind.Bound,
                                        SourceEnemyInstanceId: sourceId)
                                }
                                : candidate).ToArray(),
                        PlayerPowers = combat.PlayerPowers.Select((candidate, i) =>
                            i == powerIndex
                                ? candidate with { StoredValue = power.StoredValue + 1 }
                                : candidate).ToArray()
                    };
                    break;
                }
            }
        }

        if (combatEvent.Kind != PrototypeCombatEventKind.CardPlayed)
            return combat;

        if (combatEvent.SourceCardInstanceId is { } playedId
            && combat.Cards.FirstOrDefault(card =>
                card.InstanceId == playedId)?.Affliction?.Kind
                == PrototypeCardAfflictionKind.Bound)
            combat = combat with { BoundCardPlayedThisTurn = true };

        // Test Subject Enrage grows on each completed Skill play.
        if (combatEvent.CardId is { } id
            && PrototypeContent.Card(id).Type == PrototypeCardType.Skill)
        {
            var enemies = combat.Enemies.ToArray();
            for (var index = 0; index < enemies.Length; index++)
            {
                var enemy = enemies[index];
                if (enemy.Hp <= 0
                    || enemy.EnemyId != PrototypeNativeGloryBosses.TestSubjectId)
                    continue;
                var amount = enemy.PowerStates
                    .Where(power => power.PowerId
                        == PrototypeNativeGloryBosses.EnrageId)
                    .Sum(power => power.Stacks);
                if (amount <= 0)
                    continue;
                var applied = ApplyEnemyPowerToState(
                    combat, enemy, "proto.power.strength", amount);
                combat = applied.Combat;
                enemies[index] = applied.Enemy;
            }
            combat = combat with { Enemies = enemies };
        }

        // Aeonglass's Withering Presence has one six-card countdown
        // per owner, regardless of other enemies or card type.
        var updatedEnemies = combat.Enemies.ToArray();
        for (var index = 0; index < updatedEnemies.Length; index++)
        {
            var enemy = updatedEnemies[index];
            if (enemy.Hp <= 0
                || enemy.EnemyId != PrototypeNativeGloryBosses.AeonglassId)
                continue;
            var powers = enemy.PowerStates.ToArray();
            var powerIndex = Array.FindIndex(powers, power =>
                power.PowerId == PrototypeNativeGloryBosses.WitheringPresenceId);
            if (powerIndex < 0)
                continue;
            var next = powers[powerIndex].Stacks - 1;
            powers[powerIndex] = powers[powerIndex] with
            {
                Stacks = next > 0 ? next : 6
            };
            updatedEnemies[index] = enemy with { Powers = powers };
            combat = combat with { Enemies = updatedEnemies };
            if (next <= 0)
                combat = AddGeneratedEnemyCardsToHand(
                    combat, PrototypeNativeGloryBosses.WitherId, 1);
        }
        return combat;
    }

    private static (PlayerState Player, CombatState Combat) DispatchCombatEvent(
        PlayerState player,
        CombatState combat,
        PrototypeCombatEvent combatEvent,
        RngBundle rng,
        int eventDepth = 0,
        bool allowSuspension = false)
    {
        if (eventDepth > 64)
        {
            throw new InvalidOperationException(
                "Prototype combat event chain exceeded the safety depth limit.");
        }

        NativeEngineEventJournalV111.Record(
            "combat_event_dispatch", combatEvent.Kind.ToString(),
            sourceCardInstanceId: combatEvent.SourceCardInstanceId,
            targetEnemyInstanceId: combatEvent.TargetEnemyId,
            amount: combatEvent.Amount);

        if (combatEvent.Kind
            == PrototypeCombatEventKind.PlayerTurnStarted)
        {
            combat = ResetPerTurnRelicCounters(combat);
            combat = GrantEnemyAllyBlockOnPlayerTurnStart(combat);
        }

        if (combatEvent.Kind
            == PrototypeCombatEventKind.PlayerSideTurnStarted)
        {
            combat = combat with { IsPlayerSideActive = true };
        }

        combat = RecordCombatCounterEvent(combat, combatEvent);
        combat = ResolveNativeGloryBossCardHooks(combat, combatEvent);
        combat = RefreshNativeHiveVitalSpark(combat);
        combat = RefreshNativeGloryGalvanic(combat);
        combat = ResolveNativeCardCompletionPowers(combat, combatEvent);
        if (combatEvent.Kind == PrototypeCombatEventKind.CardPlayed
            && combatEvent.SourceCardInstanceId is { } goopyId
            && combat.Cards.FirstOrDefault(card => card.InstanceId == goopyId) is
                { Enchantment: { Kind: PrototypeCardEnchantmentKind.Goopy } goopy } goopyCard)
        {
            combat = combat with { Cards = combat.Cards.Select(card => card.InstanceId == goopyId
                ? card with { Enchantment = goopy with { Amount = checked(goopy.Amount + 1) } }
                : card).ToArray() };
            if (goopyCard.PersistentCardInstanceId is { } deckId)
                player = player with { Deck = player.Deck.Select(card =>
                    card.InstanceId == deckId && card.Enchantment is { Kind: PrototypeCardEnchantmentKind.Goopy } persistent
                        ? card with { Enchantment = persistent with { Amount = checked(persistent.Amount + 1) } }
                        : card).ToArray() };
        }
        // GalvanicPower.AfterCardPlayed is installed on the living
        // Globe Head, not on the card: the card remains afflicted if
        // the owner dies, but no further damage trigger fires.
        if (combatEvent.Kind == PrototypeCombatEventKind.CardPlayed
            && combatEvent.SourceCardInstanceId is { } galvanicCardId)
        {
            var card = combat.Cards.FirstOrDefault(item =>
                item.InstanceId == galvanicCardId);
            if (card?.Affliction?.Kind
                    == PrototypeCardAfflictionKind.Galvanized)
            {
                var damage = combat.Enemies
                    .Where(enemy => enemy.Hp > 0)
                    .SelectMany(enemy => enemy.PowerStates)
                    .Sum(power => Math.Max(0, power.Stacks)
                        * PrototypeContent.Power(power.PowerId)
                            .GalvanizePowerCardsPerStack);
                var absorbed = Math.Min(combat.PlayerBlock,
                    Math.Max(0, damage));
                combat = combat with
                {
                    PlayerBlock = combat.PlayerBlock - absorbed
                };
                var loss = ApplyPlayerHpLoss(
                    player,
                    combat,
                    damage - absorbed);
                player = loss.Player;
                combat = loss.Combat;
            }
        }
        if (combatEvent.Kind == PrototypeCombatEventKind.CardPlayed
            && combatEvent.IsPoweredAttack
            && combatEvent.CardId is { } poweredCardId
            && PrototypeContent.Card(poweredCardId).Type
                == PrototypeCardType.Attack)
        {
            combat = RemovePlayerPowers(
                combat,
                definition => definition.RemoveAfterFirstPoweredAttack);
        }
        if (combatEvent.Kind == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            var ritualStrength = combat.PlayerPowers.Sum(power =>
                Math.Max(0, power.Stacks)
                * PrototypeContent.Power(power.PowerId)
                    .PlayerStrengthAtTurnEndPerStack);
            if (ritualStrength > 0)
            {
                combat = ApplyPlayerPower(
                    combat,
                    "proto.power.strength",
                    ritualStrength);
            }

            var plating = combat.PlayerPowers.Sum(power =>
                Math.Max(0, power.Stacks)
                * PrototypeContent.Power(power.PowerId)
                    .PlayerBlockAtTurnEndPerStack);
            if (plating > 0)
            {
                combat = combat with
                {
                    PlayerBlock = combat.PlayerBlock
                        + Math.Max(0, ModifyPlayerBlockGain(
                            combat, plating, fromCard: false))
                };
            }
        }

        if (combatEvent.Kind == PrototypeCombatEventKind.CardPlayed
            && combatEvent.CardId is { } playedCardId
            && PrototypeContent.Card(playedCardId).Type
                == PrototypeCardType.Skill)
        {
            foreach (var power in combat.PlayerPowers)
            {
                var definition = PrototypeContent.Power(power.PowerId);
                if (power.Stacks > 0
                    && definition.SkillPlayAffliction is { } kind)
                {
                    combat = combat with
                    {
                        Cards = combat.Cards.Select(card =>
                            PrototypeContent.Card(card.CardId).Type
                                == PrototypeCardType.Skill
                            && card.Affliction is null
                                ? card with
                                {
                                    Affliction = new PrototypeCardAffliction(kind)
                                }
                                : card).ToArray()
                    };
                }
            }
        }

        if (combatEvent.Kind == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            var clearKinds = combat.PlayerPowers
                .Where(power => power.Stacks > 0
                    && PrototypeContent.Power(power.PowerId)
                        .ClearMatchingAfflictionAtPlayerTurnEnd)
                .Select(power => PrototypeContent.Power(power.PowerId)
                    .SkillPlayAffliction)
                .OfType<PrototypeCardAfflictionKind>()
                .ToHashSet();
            if (clearKinds.Count > 0)
            {
                combat = combat with
                {
                    Cards = combat.Cards.Select(card =>
                        card.Affliction is { SourceEnemyInstanceId: null }
                            affliction
                        && clearKinds.Contains(affliction.Kind)
                            ? card with { Affliction = null }
                            : card).ToArray()
                };
            }
        }

        var subscribers = new List<PrototypeEventSubscriberState>();

        foreach (var power in combat.PlayerPowers)
        {
            var definition = PrototypeContent.Power(power.PowerId);
            subscribers.AddRange(
                definition.Triggers
                    .Select((trigger, index) => new { Trigger = trigger, Index = index })
                    .Where(item =>
                        item.Trigger.EventKind == combatEvent.Kind
                        && (!item.Trigger.ExcludeHandDraw
                            || !combatEvent.FromHandDraw)
                        && (!item.Trigger.RequiresPlayerTurn
                            || combat.IsPlayerTurn)
                        && (item.Trigger.RequiredSourceCardType is null
                            || (combatEvent.CardId is not null
                                && PrototypeContent.Card(
                                    combatEvent.CardId).Type
                                    == item.Trigger.RequiredSourceCardType.Value))
                        && !item.Trigger.RequiresOwnerTarget
                        && (combatEvent.PowerApplicationOrderCeiling is null
                            || power.ApplicationOrder
                                <= combatEvent.PowerApplicationOrderCeiling.Value))
                    .Select(item => new PrototypeEventSubscriberState(
                        power.ApplicationOrder,
                        item.Trigger.Effects,
                        power.Stacks,
                        PrototypeEffectSourceKind.Power,
                        SourcePowerApplicationOrder: power.ApplicationOrder,
                        PowerCardPayload: power.CardPayload?.Fork(),
                        RemoveSourcePowerAfterTrigger:
                            item.Trigger.RemoveSourcePowerAfterTrigger,
                        EveryNth: item.Trigger.EveryNth,
                        PowerTriggerIndex: item.Index,
                        CountdownBeforeTrigger: item.Trigger.CountdownBeforeTrigger,
                        SourcePowerStoredValue: power.StoredValue)));
        }

        foreach (var enemy in combat.Enemies)
        {
            foreach (var power in enemy.PowerStates)
            {
                var definition = PrototypeContent.Power(power.PowerId);
                subscribers.AddRange(
                    definition.Triggers
                        .Where(trigger =>
                            trigger.EventKind == combatEvent.Kind
                            && (!trigger.ExcludeHandDraw
                                || !combatEvent.FromHandDraw)
                            && (!trigger.RequiresPlayerTurn
                                || combat.IsPlayerTurn)
                            && (trigger.RequiredSourceCardType is null
                                || (combatEvent.CardId is not null
                                    && PrototypeContent.Card(
                                        combatEvent.CardId).Type
                                        == trigger.RequiredSourceCardType.Value))
                            && (combatEvent.PowerApplicationOrderCeiling is null
                                || power.ApplicationOrder
                                    <= combatEvent.PowerApplicationOrderCeiling.Value)
                            && (!trigger.RequiresOwnerTarget
                                || combatEvent.TargetEnemyId == enemy.InstanceId)
                            && (enemy.Hp > 0
                                || (trigger.RequiresOwnerTarget
                                    && combatEvent.TargetEnemyId == enemy.InstanceId)))
                        .Select(trigger => new PrototypeEventSubscriberState(
                            power.ApplicationOrder,
                            trigger.Effects,
                            power.Stacks,
                            PrototypeEffectSourceKind.Power,
                            SourcePowerApplicationOrder: power.ApplicationOrder,
                            SourcePowerEnemyId: enemy.InstanceId,
                            PowerCardPayload: power.CardPayload?.Fork(),
                            RemoveSourcePowerAfterTrigger:
                                trigger.RemoveSourcePowerAfterTrigger)));
            }
        }

        for (var relicIndex = 0; relicIndex < combat.RelicStates.Length; relicIndex++)
        {
            var relic = combat.RelicStates[relicIndex];
            if (relic.IsMelted)
                continue;
            var definition = PrototypeContent.Relic(relic.RelicId);
            var triggers = definition.Triggers ?? Array.Empty<PrototypeRelicTriggerSpec>();
            if (relic.RelicId == NativePaelRewardsV111.TearsId && !relic.HadLeftoverEnergy)
                continue;
            for (var triggerIndex = 0; triggerIndex < triggers.Length; triggerIndex++)
            {
                var trigger = triggers[triggerIndex];
                if (trigger.EventKind != combatEvent.Kind
                    || (trigger.RequiresPlayerSide
                        && !combat.IsPlayerSideActive)
                    || (trigger.RequiredSourceCardType is not null
                        && (combatEvent.CardId is null
                            || PrototypeContent.Card(
                                combatEvent.CardId).Type
                                != trigger.RequiredSourceCardType.Value))
                    || (trigger.RequiredSourceCardTag is not null
                        && (combatEvent.CardId is null
                            || !(PrototypeContent.Card(combatEvent.CardId).Tags?
                                .Contains(trigger.RequiredSourceCardTag,
                                    StringComparer.Ordinal) ?? false)))
                    || (trigger.RequiresEmptyHand
                        && combat.Hand.Length != 0)
                    || !RelicTriggerMatchesPlayer(
                        trigger,
                        player,
                        combat))
                {
                    continue;
                }

                subscribers.Add(new PrototypeEventSubscriberState(
                    relic.ApplicationOrder,
                    trigger.Effects,
                    PowerStacks: 0,
                    SourceKind: PrototypeEffectSourceKind.Relic,
                    RelicStateIndex: relicIndex,
                    RelicTriggerIndex: triggerIndex,
                    EveryNth: trigger.EveryNth,
                    MaxTriggersPerCounterWindow:
                        trigger.MaxTriggersPerCounterWindow));
            }
        }

        var orderedSubscribers = subscribers
            .OrderBy(item => item.ApplicationOrder)
            .ToArray();

        for (var subscriberIndex = 0;
             subscriberIndex < orderedSubscribers.Length;
             subscriberIndex++)
        {
            var subscriber = orderedSubscribers[subscriberIndex];
            if (subscriber.PowerTriggerIndex is not null)
            {
                var checkedPower = CheckPowerSubscriberTrigger(combat, subscriber);
                combat = checkedPower.Combat;
                if (!checkedPower.ShouldTrigger)
                {
                    continue;
                }
            }

            if (subscriber.RelicStateIndex is not null)
            {
                if (subscriber.RelicTriggerIndex is null || subscriber.EveryNth <= 0)
                {
                    throw new InvalidOperationException("Relic trigger counter metadata is invalid.");
                }

                var incremented = IncrementRelicTriggerCounter(
                    combat,
                    subscriber.RelicStateIndex.Value,
                    subscriber.RelicTriggerIndex.Value);
                combat = incremented.Combat;
                player = PersistRelicTriggerCount(
                    player,
                    combat.RelicStates[subscriber.RelicStateIndex.Value],
                    subscriber.RelicTriggerIndex.Value,
                    incremented.Count);
                if (subscriber.MaxTriggersPerCounterWindow is
                        { } maxTriggers
                    && incremented.Count > maxTriggers)
                {
                    continue;
                }

                if (incremented.Count % subscriber.EveryNth != 0)
                {
                    continue;
                }
            }

            var operations = new Queue<PrototypeQueuedOperation>();
            foreach (var effect in subscriber.Effects)
            {
                EnqueueEffectOperations(
                    operations,
                    effect,
                    upgradeLevel: 0,
                    energySpent: 0,
                    actionTargetEnemyId: combatEvent.TargetEnemyId,
                    combat: combat,
                    powerStacks: subscriber.PowerStacks,
                    sourceKind: subscriber.SourceKind,
                    powerCardPayload: subscriber.PowerCardPayload,
                    sourcePowerEnemyId:
                        subscriber.SourcePowerEnemyId,
                    sourcePowerStoredValue:
                        subscriber.SourcePowerStoredValue,
                    sourcePowerApplicationOrder:
                        subscriber.SourcePowerApplicationOrder);
            }

            var continuation =
                new PrototypeEventDispatchContinuationState(
                    combatEvent,
                    subscriber.Fork(),
                    orderedSubscribers
                        .Skip(subscriberIndex + 1)
                        .Select(item => item.Fork())
                        .ToArray(),
                    eventDepth);
            var resolved = ResolveOperations(
                player,
                combat,
                operations,
                rng,
                eventDepth: eventDepth,
                eventSourceCardInstanceId:
                    combatEvent.SourceCardInstanceId,
                eventDispatchContinuation:
                    continuation);
            player = resolved.Player;
            combat = resolved.Combat;

            if (combat.PendingChoice is not null)
            {
                if (!allowSuspension)
                {
                    throw new NotSupportedException(
                        "Combat-event suspension outside a top-level automatic event step requires an outer continuation frame.");
                }

                return (player, combat);
            }

            combat = FinalizeEventSubscriber(
                combat,
                subscriber);
        }

        if (combatEvent.Kind == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            combat = combat with
            {
                PlayerPowers = combat.PlayerPowers
                    .Where(power =>
                        !PrototypeContent.Power(power.PowerId)
                            .RemoveAtPlayerTurnEnd)
                    .ToArray()
            };
            combat = DecrementPlayerPowers(
                combat,
                definition => definition.DecrementAtPlayerTurnEnd);
            combat = ResetPlayerPowerTriggerCounters(combat);
        }

        if (combatEvent.Kind == PrototypeCombatEventKind.PlayerTurnStarted)
        {
            combat = DecrementPlayerPowers(
                combat,
                definition => definition.DecrementAfterPlayerTurnStart);
        }

        return (player, combat);
    }

    private static (PlayerState Player, CombatState Combat)
        ResumeEventDispatchContinuation(
            PlayerState player,
            CombatState combat,
            PrototypeEventDispatchContinuationState continuation,
            RngBundle rng)
    {
        combat = FinalizeEventSubscriber(
            combat,
            continuation.CurrentSubscriber);

        var remaining = continuation.RemainingSubscribers;
        for (var index = 0; index < remaining.Length; index++)
        {
            var subscriber = remaining[index];
            if (subscriber.PowerTriggerIndex is not null)
            {
                var checkedPower = CheckPowerSubscriberTrigger(combat, subscriber);
                combat = checkedPower.Combat;
                if (!checkedPower.ShouldTrigger)
                {
                    continue;
                }
            }

            if (subscriber.RelicStateIndex is not null)
            {
                if (subscriber.RelicTriggerIndex is null
                    || subscriber.EveryNth <= 0)
                {
                    throw new InvalidOperationException(
                        "Relic trigger counter metadata is invalid.");
                }

                var incremented = IncrementRelicTriggerCounter(
                    combat,
                    subscriber.RelicStateIndex.Value,
                    subscriber.RelicTriggerIndex.Value);
                combat = incremented.Combat;
                player = PersistRelicTriggerCount(
                    player,
                    combat.RelicStates[subscriber.RelicStateIndex.Value],
                    subscriber.RelicTriggerIndex.Value,
                    incremented.Count);
                if (subscriber.MaxTriggersPerCounterWindow is
                        { } maxTriggers
                    && incremented.Count > maxTriggers)
                {
                    continue;
                }

                if (incremented.Count % subscriber.EveryNth != 0)
                {
                    continue;
                }
            }

            var operations = new Queue<PrototypeQueuedOperation>();
            foreach (var effect in subscriber.Effects)
            {
                EnqueueEffectOperations(
                    operations,
                    effect,
                    upgradeLevel: 0,
                    energySpent: 0,
                    actionTargetEnemyId:
                        continuation.CombatEvent.TargetEnemyId,
                    combat: combat,
                    powerStacks: subscriber.PowerStacks,
                    sourceKind: subscriber.SourceKind,
                    powerCardPayload: subscriber.PowerCardPayload,
                    sourcePowerEnemyId:
                        subscriber.SourcePowerEnemyId,
                    sourcePowerStoredValue:
                        subscriber.SourcePowerStoredValue,
                    sourcePowerApplicationOrder:
                        subscriber.SourcePowerApplicationOrder);
            }

            var nextContinuation =
                new PrototypeEventDispatchContinuationState(
                    continuation.CombatEvent,
                    subscriber.Fork(),
                    remaining
                        .Skip(index + 1)
                        .Select(item => item.Fork())
                        .ToArray(),
                    continuation.EventDepth);
            var resolved = ResolveOperations(
                player,
                combat,
                operations,
                rng,
                eventDepth: continuation.EventDepth,
                eventSourceCardInstanceId:
                    continuation.CombatEvent.SourceCardInstanceId,
                eventDispatchContinuation:
                    nextContinuation);
            player = resolved.Player;
            combat = resolved.Combat;

            if (combat.PendingChoice is not null)
            {
                return (player, combat);
            }

            combat = FinalizeEventSubscriber(
                combat,
                subscriber);
        }

        if (continuation.CombatEvent.Kind
            == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            combat = combat with
            {
                PlayerPowers = combat.PlayerPowers
                    .Where(power =>
                        !PrototypeContent.Power(power.PowerId)
                            .RemoveAtPlayerTurnEnd)
                    .ToArray()
            };
            combat = DecrementPlayerPowers(
                combat,
                definition => definition.DecrementAtPlayerTurnEnd);
            combat = ResetPlayerPowerTriggerCounters(combat);
        }

        if (continuation.CombatEvent.Kind
            == PrototypeCombatEventKind.PlayerTurnStarted)
        {
            combat = DecrementPlayerPowers(
                combat,
                definition => definition.DecrementAfterPlayerTurnStart);
        }

        return (player, combat);
    }

    private static CombatState FinalizeEventSubscriber(
        CombatState combat,
        PrototypeEventSubscriberState subscriber)
    {
        if (!subscriber.RemoveSourcePowerAfterTrigger)
        {
            return combat;
        }

        if (subscriber.SourcePowerApplicationOrder is null)
        {
            throw new InvalidOperationException(
                "Self-removing power trigger is missing source-power identity.");
        }

        return RemovePowerInstance(
            combat,
            subscriber.SourcePowerApplicationOrder.Value,
            subscriber.SourcePowerEnemyId);
    }

    private static CombatState RemovePowerInstance(
        CombatState combat,
        long applicationOrder,
        int? enemyId)
    {
        if (enemyId is null)
        {
            return combat with
            {
                PlayerPowers = combat.PlayerPowers
                    .Where(power => power.ApplicationOrder != applicationOrder)
                    .ToArray()
            };
        }

        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        var index = Array.FindIndex(
            enemies,
            enemy => enemy.InstanceId == enemyId.Value);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Enemy {enemyId.Value} is missing while removing a power.");
        }

        enemies[index] = enemies[index] with
        {
            Powers = enemies[index].PowerStates
                .Where(power => power.ApplicationOrder != applicationOrder)
                .ToArray()
        };
        return combat with { Enemies = enemies };
    }

    private static IReadOnlyList<GameAction> GetPendingChoiceActions(
        PendingCombatChoiceState pending)
    {
        if (!StringComparer.Ordinal.Equals(pending.ChoiceId, "select_cards"))
        {
            throw new InvalidOperationException($"Unknown pending combat choice '{pending.ChoiceId}'.");
        }

        var actions = new List<GameAction>();
        if (pending.Selection.SequentialOptional)
        {
            var selectedCount =
                pending.SequentialSelectedCardInstanceIds?.Length
                ?? 0;
            foreach (var candidate in
                     pending.CandidateCardInstanceIds)
            {
                if (selectedCount
                    >= pending.Selection.MaxSelections)
                {
                    break;
                }

                actions.Add(GameAction.Create(
                    "select_cards",
                    new SelectCardsPayload([candidate])));
            }

            if (selectedCount
                >= pending.Selection.MinSelections)
            {
                actions.Add(GameAction.Create(
                    "select_cards",
                    new SelectCardsPayload([])));
            }

            return actions;
        }

        for (var count = pending.Selection.MinSelections;
             count <= pending.Selection.MaxSelections;
             count++)
        {
            foreach (var selection in ChooseCombinations(
                         pending.CandidateCardInstanceIds,
                         count))
            {
                actions.Add(GameAction.Create(
                    "select_cards",
                    new SelectCardsPayload(selection)));
            }
        }

        return actions;
    }

    private static RunState ResolvePendingChoice(RunState state, GameAction action)
    {
        RequireKind(action, "select_cards");
        var payload = action.ReadPayload<SelectCardsPayload>();
        return ResolvePendingChoice(state, payload.CardInstanceIds, null);
    }

    private static RunState ResolvePendingChoice(
        RunState state,
        long[] selected,
        IReadOnlyDictionary<long, string>? transformReplacementCardIds,
        bool selectionAlreadyResolved = false)
        => ResolvePendingChoiceWithBoundary(
            state, selected, transformReplacementCardIds,
            selectionAlreadyResolved).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResolvePendingChoiceWithBoundary(
            RunState state,
            long[] selected,
            IReadOnlyDictionary<long, string>? transformReplacementCardIds,
            bool selectionAlreadyResolved = false)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");
        var pending = combat.PendingChoice
            ?? throw new InvalidOperationException("Combat has no pending choice.");
        bool automaticHandDrawHookCardSelection =
            IsAutomaticHandDrawHookCardSelectionContinuation(
                combat, pending);

        if (!StringComparer.Ordinal.Equals(pending.ChoiceId, "select_cards"))
        {
            throw new InvalidOperationException($"Unknown pending combat choice '{pending.ChoiceId}'.");
        }

        if (selected.Length != selected.Distinct().Count())
        {
            throw new InvalidOperationException(
                "Card selection contains duplicate instance IDs.");
        }

        if (pending.Selection.SequentialOptional && !selectionAlreadyResolved)
        {
            if (selected.Length > 1)
            {
                throw new InvalidOperationException(
                    "Sequential card selection accepts one card or the finish action.");
            }

            var accumulated =
                pending.SequentialSelectedCardInstanceIds
                ?? Array.Empty<long>();
            if (selected.Length == 1)
            {
                var selectedId = selected[0];
                if (!pending.CandidateCardInstanceIds
                        .Contains(selectedId))
                {
                    throw new InvalidOperationException(
                        "Sequential selection contains a card outside the pending candidate set.");
                }

                var nextAccumulated =
                    accumulated.Append(selectedId).ToArray();
                var remainingCandidates =
                    pending.CandidateCardInstanceIds
                        .Where(id => id != selectedId)
                        .ToArray();

                if (nextAccumulated.Length
                        < pending.Selection.MaxSelections
                    && remainingCandidates.Length > 0)
                {
                    combat = combat with
                    {
                        PendingChoice = pending with
                        {
                            CandidateCardInstanceIds =
                                remainingCandidates,
                            SequentialSelectedCardInstanceIds =
                                nextAccumulated
                        }
                    };
                    return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                        state with
                        {
                            World = world with
                            {
                                Combat = combat
                            }
                        });
                }

                selected = nextAccumulated;
            }
            else
            {
                if (accumulated.Length
                    < pending.Selection.MinSelections)
                {
                    throw new InvalidOperationException(
                        "Sequential selection cannot finish before its minimum selection count.");
                }

                selected = accumulated;
            }
        }

        if (selected.Length < pending.Selection.MinSelections
            || selected.Length > pending.Selection.MaxSelections)
        {
            throw new InvalidOperationException(
                $"Selection count {selected.Length} is outside " +
                $"{pending.Selection.MinSelections}..{pending.Selection.MaxSelections}.");
        }

        if (!pending.Selection.SequentialOptional)
        {
            var candidates =
                pending.CandidateCardInstanceIds.ToHashSet();
            if (selected.Any(cardId =>
                    !candidates.Contains(cardId)))
            {
                throw new InvalidOperationException(
                    "Selection contains a card outside the pending candidate set.");
            }
        }

        if (pending.Selection.Resolution ==
                PrototypeCardSelectionResolutionKind.TransformRandom &&
            selected.Length > 0 && transformReplacementCardIds is null &&
            ControlledChanceGuardV111.Active)
        {
            var sourceCards = GetZone(combat, pending.Selection.SourceZone);
            if (selected.Any(cardId => !sourceCards.Contains(cardId)))
                throw new InvalidOperationException(
                    "Selected card is no longer in the requested source zone.");
            combat = MakeControlledCardTransformChoice(
                combat, pending, selected);
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                state with { World = world with { Combat = combat } });
        }

        var prototypeRng = state.Stochastic.PrototypeRng;

        var slyCards = pending.Selection.SourceZone == PrototypeCardZone.Hand
            && pending.Selection.Resolution == PrototypeCardSelectionResolutionKind.MoveToDiscard
                ? selected
                    .Select(cardId => RequireCombatCard(combat, cardId))
                    .Where(card =>
                        CardHasKeyword(
                            combat,
                            card.InstanceId,
                            PrototypeCardKeyword.Sly))
                    .Select(card => card.InstanceId)
                    .ToArray()
                : Array.Empty<long>();

        combat = ApplyCardSelection(
            combat, pending.Selection, selected, prototypeRng,
            transformReplacementCardIds);
        combat = combat with { PendingChoice = null };

        if (pending.SelectedCardTemporaryCost is not null)
        {
            foreach (var cardInstanceId in selected)
            {
                combat = SetCardTemporaryEnergyCost(
                    combat,
                    cardInstanceId,
                    pending.SelectedCardTemporaryCost);
            }
        }

        if (pending.SelectedCardPower is not null)
        {
            if (selected.Length != 1)
            {
                throw new InvalidOperationException(
                    "Selected-card power actions require exactly one selected card.");
            }

            combat = ApplyPlayerPowerWithSelectedCard(
                combat,
                pending.SelectedCardPower,
                RequireCombatCard(combat, selected[0]));
        }

        if (pending.SelectedCardKeyword is not null)
        {
            foreach (var cardInstanceId in selected)
            {
                combat = ApplyCardKeywordOverride(
                    combat,
                    cardInstanceId,
                    pending.SelectedCardKeyword);
            }
        }

        // Resolve selection-induced exhaust and discard using the normal
        // ordered event-dispatch continuation (including nested choices).
        var discardEvents =
            pending.Selection.SourceZone == PrototypeCardZone.Hand
            && (pending.Selection.Resolution
                    == PrototypeCardSelectionResolutionKind.MoveToDiscard
                || pending.Selection.Resolution
                    == PrototypeCardSelectionResolutionKind.MoveToExhaust)
                ? selected.Select(cardId =>
                {
                    var movedCard = RequireCombatCard(combat, cardId);
                    return new PrototypeCombatEvent(
                        pending.Selection.Resolution
                            == PrototypeCardSelectionResolutionKind.MoveToExhaust
                                ? PrototypeCombatEventKind.CardExhausted
                                : PrototypeCombatEventKind.CardDiscarded,
                        SourceCardInstanceId: cardId,
                        CardId: movedCard.CardId);
                }).ToArray()
                : Array.Empty<PrototypeCombatEvent>();

        var continuationOperations =
            (PrototypeQueuedOperation[])
            pending.Continuation.Clone();
        if (pending.Selection
                .DrawEqualToSelectionsOnCompletion
            && selected.Length > 0)
        {
            continuationOperations =
                new[]
                {
                    new PrototypeQueuedOperation(
                        PrototypeCombatEffectKind.DrawCards,
                        selected.Length,
                        DrawEqualSelectionCount: selected.Length)
                }
                .Concat(continuationOperations)
                .ToArray();
        }

        var resolutionContinuation =
            new PrototypeChoiceResolutionContinuationState(
                SourceCardInstanceId:
                    pending.SourceCardInstanceId,
                SourceCardDestination:
                    pending.SourceCardDestination,
                Operations:
                    continuationOperations,
                PendingDiscardEvents: discardEvents,
                PendingSlyCardInstanceIds:
                    (long[])slyCards.Clone(),
                CompletionEvents:
                    (PrototypeCombatEvent[])
                    pending.CompletionEvents.Clone(),
                CardPlaySeries:
                    pending.CardPlaySeries,
                MoveSourceCardOnCompletion:
                    pending.MoveSourceCardOnCompletion,
                RemoveSourceCardOnCompletion:
                    pending.RemoveSourceCardOnCompletion,
                SourceCardAlreadyMoved:
                    pending.SourceCardAlreadyMoved,
                EventDispatchContinuation:
                    pending.EventDispatchContinuation?.Fork(),
                Parent:
                    pending.OuterChoiceContinuation?.Fork());

        var resolved = ResumeChoiceResolutionContinuation(
            state.Player,
            combat,
            prototypeRng,
            resolutionContinuation);

        state = state with
        {
            Player = resolved.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { Combat = resolved.Combat }
        };

        var automaticContinuation =
            resolved.Combat.AutomaticPipelineContinuation;
        if (automaticContinuation is not null &&
            automaticContinuation.Stochastic
                .TryGetActiveAutomaticHandDrawProgress(
                    automaticContinuation.Mechanics, out var handDrawProgress) &&
            handDrawProgress.Kind ==
                PrototypeAutomaticHandDrawProgressKindV111
                    .ControlledRecycleStratagem)
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                ResumeControlledRecycleStratagemSelection(state));

        // These choices are nested inside the active hand-draw stage. Their
        // owning draw continuation must finish before the ordinary pipeline
        // can settle a terminal reward boundary.
        if (pending.AutomaticSemanticStratagemDrawSelection ||
            pending.AutomaticSemanticDrawHookChoice ||
            pending.AutomaticSemanticDrawTargetChoice ||
            automaticHandDrawHookCardSelection)
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(state);

        return ResumeAutomaticPipelineContinuation(state);
    }

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeSemanticCardSelectionWithBoundary(
            RunState state, long[] selectedCardInstanceIds)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic card selection requires the controlled RNG guard.");
        ArgumentNullException.ThrowIfNull(selectedCardInstanceIds);
        return ResolvePendingChoiceWithBoundary(
            state, selectedCardInstanceIds, transformReplacementCardIds: null);
    }

    private static int ResolveLegacyRandomIndex(
        RngBundle legacyRng,
        string siteId,
        string ownerId,
        IEnumerable<int> orderedCandidates,
        string streamId = "combat",
        string[]? controlledCandidateLabels = null)
        => ResolveLegacyRandomIndex(
            legacyRng, siteId, ownerId, orderedCandidates, out _,
            streamId, controlledCandidateLabels);

    private static int ResolveLegacyRandomIndex(
        RngBundle legacyRng,
        string siteId,
        string ownerId,
        IEnumerable<int> orderedCandidates,
        out string? selectedCandidateLabel,
        string streamId = "combat",
        string[]? controlledCandidateLabels = null)
    {
        ArgumentNullException.ThrowIfNull(orderedCandidates);
        var candidates = orderedCandidates.ToArray();
        if (candidates.Length == 0)
            throw new ArgumentException(
                "Legacy random selection requires a nonempty ordered support.",
                nameof(orderedCandidates));
        if (controlledCandidateLabels is null &&
            PrototypeControlledEnemyActionContinuationV111
                .IsEnemyActionSite(siteId))
            controlledCandidateLabels = candidates.Select(candidate =>
                candidate.ToString(
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
        using var typedCandidateLabels =
            ControlledStochasticChanceReplayV111.DescribeCandidates(
                controlledCandidateLabels);
        using var candidateLabels =
            ControlledEventChoiceReplayV111.DescribeCandidates(
                controlledCandidateLabels);
        var selectedIndex = ResolveLegacyRandomOutcome(
            legacyRng, siteId, ownerId, candidates, null, streamId,
            out var request);
        selectedCandidateLabel = request is null ? null :
            ControlledStochasticChanceReplayV111
                .GetSelectedIndexCandidateLabel(request, selectedIndex);
        return selectedIndex;
    }

    private static string ResolveSelectedSummonIdentity(
        string label, string[] choices)
    {
        const string prefix = "enemy:";
        const string slotMarker = ":slot:";
        var slotSeparator = label.LastIndexOf(
            slotMarker, StringComparison.Ordinal);
        if (!label.StartsWith(prefix, StringComparison.Ordinal) ||
            slotSeparator <= prefix.Length ||
            slotSeparator + slotMarker.Length >= label.Length ||
            !int.TryParse(label.AsSpan(slotSeparator + slotMarker.Length),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var poolSlot) || poolSlot < 0 || poolSlot >= choices.Length)
            throw new InvalidDataException(
                "Saved summon-pool identity is malformed or outside its live support.");
        var enemyId = label[prefix.Length..slotSeparator];
        if (!StringComparer.Ordinal.Equals(choices[poolSlot], enemyId))
            throw new InvalidDataException(
                "Saved summon-pool identity differs from its live slot.");
        return enemyId;
    }

    private static int ResolveLegacyBoundedInteger(
        RngBundle legacyRng,
        string siteId,
        string ownerId,
        int boundedCount,
        string streamId = "combat")
    {
        if (boundedCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(boundedCount));
        return ResolveLegacyRandomOutcome(
            legacyRng, siteId, ownerId, [], boundedCount, streamId);
    }

    private static int ResolveLegacyRandomOutcome(
        RngBundle legacyRng,
        string siteId,
        string ownerId,
        int[] orderedCandidates,
        int? boundedCount,
        string streamId) => ResolveLegacyRandomOutcome(
            legacyRng, siteId, ownerId, orderedCandidates, boundedCount,
            streamId, out _);

    private static int ResolveLegacyRandomOutcome(
        RngBundle legacyRng,
        string siteId,
        string ownerId,
        int[] orderedCandidates,
        int? boundedCount,
        string streamId,
        out RandomIndexRequestV111? request)
    {
        var decision = boundedCount is { } bound
            ? LegacyRandomChoiceV111.ChooseBoundedInteger(
                legacyRng, siteId, streamId, ownerId, bound)
            : LegacyRandomChoiceV111.ChooseIndex(
                legacyRng, siteId, streamId, ownerId, orderedCandidates);
        request = decision.Request;
        return decision.SelectedValue;
    }

    private static (CombatState Combat, int SelectedIndex)
        ResolveRandomEnemyTarget(
            CombatState combat,
            RngBundle legacyRng,
            int[] eligibleEnemyInstanceIds,
            string siteId,
            string legacyStreamId,
            string ownerId,
            int ordinal)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(legacyRng);
        ArgumentNullException.ThrowIfNull(eligibleEnemyInstanceIds);
        if (!StochasticSiteRegistryV111.CombatRandomTargetSiteIds.Contains(
                siteId, StringComparer.Ordinal))
            throw new NotSupportedException(
                $"UNSUPPORTED_RANDOM_SITE site={siteId} " +
                "operation=random_enemy_target.");
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyStreamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (eligibleEnemyInstanceIds.Length == 0 ||
            eligibleEnemyInstanceIds.Distinct().Count() !=
                eligibleEnemyInstanceIds.Length)
            throw new ArgumentException(
                "Random enemy selection requires a nonempty unique support.",
                nameof(eligibleEnemyInstanceIds));

        var native = combat.Stochastic.NativeRng;
        var streamId = native is null
            ? legacyStreamId
            : "run.combat_targets";
        var streams = native?.RunStreams.Fork() ?? legacyRng;
        var stream = streams.GetStream(streamId);
        IRandomIndexProviderV111 provider = native is null
            ? new LegacyPrototypeRandomIndexProviderV111()
            : new NativeRandomIndexProviderV111();
        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal,
            (int[])eligibleEnemyInstanceIds.Clone());
        var decision = StochasticProviderScopeV111.ResolveIndex(
            provider, request, streams);
        if (decision.ProviderMode == "native-v111" &&
            RandomBoundaryJournalV111.HasActiveJournal)
            RandomBoundaryJournalV111.RecordRandomTargetOutcome(
                SemanticTargetNativeAuditV111.Create(decision));
        var updated = native is null
            ? combat
            : combat with
            {
                Stochastic = CombatStochasticStateV111.WithNativeRng(
                    combat.Stochastic, native with { RunStreams = streams })
            };
        return (updated, decision.SelectedIndex);
    }

    private static (CombatState Combat, int SelectedIndex)
        ResolveRandomHandCostChoice(
            CombatState combat,
            RngBundle legacyRng,
            PrototypeQueuedOperation operation,
            long? sourceCardInstanceId,
            long[] orderedHandCardIds,
            long? selectedCardInstanceId,
            int? boundedCount)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(legacyRng);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(orderedHandCardIds);
        if (orderedHandCardIds.Length == 0 ||
            orderedHandCardIds.Any(id => id <= 0) ||
            orderedHandCardIds.Distinct().Count() != orderedHandCardIds.Length)
            throw new ArgumentException(
                "Random hand-cost selection requires an ordered unique hand support.",
                nameof(orderedHandCardIds));

        var selectingCard = operation.Kind ==
            PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed;
        if (selectingCard != (selectedCardInstanceId is null) ||
            (selectingCard && boundedCount is not null) ||
            (!selectingCard && (boundedCount is null or <= 0 ||
                selectedCardInstanceId is null ||
                !orderedHandCardIds.Contains(selectedCardInstanceId.Value))) ||
            operation.Kind is not (
                PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed
                or PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed))
            throw new ArgumentException(
                "Random hand-cost request does not match its source operation.",
                nameof(operation));

        if (ControlledChanceGuardV111.Active &&
            !ControlledChanceGuardV111.EngineOwnedNativeProviderActive)
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=combat.random_hand_card " +
                "operation=provider_dispatch requires a saved hand-cost continuation.");

        var native = combat.Stochastic.NativeRng;
        var streamId = native is null
            ? "combat"
            : selectingCard
                ? "run.combat_card_selection"
                : "run.combat_energy_costs";
        var streams = native?.RunStreams.Fork() ?? legacyRng;
        var stream = streams.GetStream(streamId);
        var callOrdinal = StochasticProviderScopeV111.GetNextOrdinal(
            streams, streamId) is { } scopedOrdinal
                ? (ulong)scopedOrdinal
                : (stream.CallCount ?? 0);
        if (callOrdinal > int.MaxValue)
            throw new NotSupportedException(
                "Random hand-cost provider ordinal exceeds the request range.");
        IRandomIndexProviderV111 provider = native is null
            ? new LegacyPrototypeRandomIndexProviderV111()
            : new NativeRandomIndexProviderV111();
        var ownerId = "effect:" + operation.Kind + ":source:" +
            (sourceCardInstanceId?.ToString() ?? "none") + ":hand:" +
            CanonicalJson.Sha256(orderedHandCardIds);
        if (selectedCardInstanceId is { } cardId)
            ownerId += ":card_instance:" + cardId.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        var request = new RandomIndexRequestV111(
            selectingCard ? StochasticSiteRegistryV111.CombatRandomHandCardSelectSiteId
                          : StochasticSiteRegistryV111.CombatRandomHandCardEnergyCostSiteId,
            streamId, ownerId, (int)callOrdinal,
            selectingCard
                ? Enumerable.Range(0, orderedHandCardIds.Length).ToArray()
                : [],
            OperationKind: selectingCard ? "choose_index" : "bounded_integer",
            BoundedCount: selectingCard ? null : boundedCount);
        var decision = StochasticProviderScopeV111.ResolveIndex(
            provider, request, streams);
        if (selectingCard && decision.ProviderMode == "native-v111" &&
            RandomBoundaryJournalV111.HasActiveJournal &&
            StochasticSiteRegistryV111
                .IsCombatCardInstanceIndexAuditSite(request.SiteId))
            RandomBoundaryJournalV111.RecordCardInstanceIndexOutcome(
                SemanticCardInstanceIndexAuditV111.Create(
                    decision, orderedHandCardIds));
        else if (!selectingCard && selectedCardInstanceId is { } costCardId &&
            decision.ProviderMode == "native-v111" &&
            RandomBoundaryJournalV111.HasActiveJournal &&
            StochasticSiteRegistryV111
                .IsCombatCardEnergyCostAuditSite(request.SiteId))
            RandomBoundaryJournalV111.RecordCardEnergyCostOutcome(
                SemanticCardEnergyCostAuditV111.Create(
                    decision, orderedHandCardIds, costCardId,
                    RequireCombatCard(combat, costCardId).CardId));
        var updated = native is null
            ? combat
            : combat with
            {
                Stochastic = CombatStochasticStateV111.WithNativeRng(
                    combat.Stochastic, native with { RunStreams = streams })
            };
        return (updated, decision.SelectedIndex);
    }

    private static (CombatState Combat, int SelectedIndex,
        long? SelectedCardInstanceId)
        ResolveRandomCombatCardIndex(
            CombatState combat,
            RngBundle legacyRng,
            string siteId,
            string legacyStreamId,
            long[] orderedCardInstanceIds,
        string ownerId,
        string nativeStreamId = "run.combat_card_selection")
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(legacyRng);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyStreamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeStreamId);
        ArgumentNullException.ThrowIfNull(orderedCardInstanceIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (orderedCardInstanceIds.Length == 0 ||
            orderedCardInstanceIds.Any(id => id <= 0) ||
            orderedCardInstanceIds.Distinct().Count() !=
                orderedCardInstanceIds.Length)
            throw new ArgumentException(
                "Random combat-card selection requires ordered unique instances.",
                nameof(orderedCardInstanceIds));

        var native = combat.Stochastic.NativeRng;
        var streamId = native is null
            ? legacyStreamId
            : nativeStreamId;
        var streams = native?.RunStreams.Fork() ?? legacyRng;
        var stream = streams.GetStream(streamId);
        var callOrdinal = StochasticProviderScopeV111.GetNextOrdinal(
            streams, streamId) is { } scopedOrdinal
                ? (ulong)scopedOrdinal
                : (stream.CallCount ?? 0);
        if (callOrdinal > int.MaxValue)
            throw new NotSupportedException(
                "Random combat-card provider ordinal exceeds the request range.");
        if (siteId == StochasticSiteRegistryV111
                .CombatEnemyTheftCardSelectSiteId &&
            combat.Stochastic.SemanticDeck is
                { Mode: SemanticDeckStateV111.NatureMode } semanticDeck &&
            orderedCardInstanceIds.Any(id => combat.DrawPile.Contains(id) &&
                !SemanticDeckEngineV111.CanRemoveCardInstance(
                    semanticDeck, id)))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK " +
                "site=" + StochasticSiteRegistryV111
                    .CombatEnemyTheftCardSelectSiteId + " " +
                "operation=draw_pile_candidate_removal_breaks_constraints");
        IRandomIndexProviderV111 provider = native is null
            ? new LegacyPrototypeRandomIndexProviderV111()
            : new NativeRandomIndexProviderV111();
        var request = new RandomIndexRequestV111(
            siteId, streamId,
            ownerId + ":pool:" +
                CanonicalJson.Sha256(orderedCardInstanceIds),
            (int)callOrdinal,
            Enumerable.Range(0, orderedCardInstanceIds.Length).ToArray());
        var controlledCandidateLabels = siteId ==
                StochasticSiteRegistryV111.CombatEnemyTheftCardSelectSiteId
            ? orderedCardInstanceIds.Select(instanceId =>
                "card-instance:" + instanceId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToArray()
            : null;
        using var typedCandidateLabels =
            ControlledStochasticChanceReplayV111.DescribeCandidates(
                controlledCandidateLabels);
        using var eventCandidateLabels =
            ControlledEventChoiceReplayV111.DescribeCandidates(
                controlledCandidateLabels);
        if (ControlledChanceGuardV111.Active &&
            !ControlledChanceGuardV111.EngineOwnedNativeProviderActive)
        {
            if (PrototypeControlledEnemyActionContinuationV111
                    .IsEnemyActionSite(siteId))
            {
                var selectedIndex = StochasticProviderScopeV111
                    .ResolveIndex(provider, request, streams).SelectedIndex;
                return (combat, selectedIndex,
                    ResolveSelectedTheftCardIdentity(
                        request, selectedIndex, orderedCardInstanceIds));
            }
            throw new NotSupportedException(
                $"UNSUPPORTED_CONTROLLED_CHANCE site={siteId} " +
                "operation=choose_index requires a saved continuation.");
        }

        var decision = StochasticProviderScopeV111.ResolveIndex(
            provider, request, streams);
        if (decision.ProviderMode == "native-v111" &&
            RandomBoundaryJournalV111.HasActiveJournal &&
            StochasticSiteRegistryV111
                .IsCombatCardInstanceIndexAuditSite(siteId))
            RandomBoundaryJournalV111.RecordCardInstanceIndexOutcome(
                SemanticCardInstanceIndexAuditV111.Create(
                    decision, orderedCardInstanceIds));
        var updated = native is null
            ? combat
            : combat with
            {
                Stochastic = CombatStochasticStateV111.WithNativeRng(
                    combat.Stochastic, native with { RunStreams = streams })
            };
        return (updated, decision.SelectedIndex,
            ResolveSelectedTheftCardIdentity(
                request, decision.SelectedIndex, orderedCardInstanceIds));
    }

    private static long? ResolveSelectedTheftCardIdentity(
        RandomIndexRequestV111 request, int selectedIndex,
        long[] orderedCardInstanceIds)
    {
        if (request.SiteId != StochasticSiteRegistryV111
                .CombatEnemyTheftCardSelectSiteId)
            return null;
        var label = ControlledStochasticChanceReplayV111
            .GetSelectedIndexCandidateLabel(request, selectedIndex);
        if (label is null)
            return null;
        const string prefix = "card-instance:";
        if (!label.StartsWith(prefix, StringComparison.Ordinal) ||
            !long.TryParse(label.AsSpan(prefix.Length),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var instanceId) || instanceId <= 0 ||
            !orderedCardInstanceIds.Contains(instanceId))
            throw new InvalidDataException(
                "Saved enemy theft identity is malformed or outside its live support.");
        return instanceId;
    }

    private static CombatState ShuffleCombatCardPool<T>(
        CombatState combat,
        RngBundle legacyRng,
        T[] orderedPool,
        string siteId,
        string legacyStreamId,
        string nativeStreamId,
        string ownerPrefix,
        PrototypeCombatEffectKind sourceOperationKind,
        int selectedCount,
        long? sourceCardInstanceId = null,
        bool forceLegacyProfile = false)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(legacyRng);
        ArgumentNullException.ThrowIfNull(orderedPool);
        if (!StochasticSiteRegistryV111.CombatCardPoolShuffleSiteIds.Contains(
                siteId, StringComparer.Ordinal))
            throw new NotSupportedException(
                $"UNSUPPORTED_RANDOM_SITE site={siteId} " +
                "operation=combat_card_pool_shuffle.");
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyStreamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeStreamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerPrefix);
        if (orderedPool.Length < 2)
            return combat;
        if (ControlledChanceGuardV111.Active &&
            !ControlledChanceGuardV111.EngineOwnedNativeProviderActive)
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE " +
                "site=" + siteId + " " +
                "operation=shuffle requires a saved shortlist continuation.");

        var native = forceLegacyProfile ? null : combat.Stochastic.NativeRng;
        var streamId = native is null
            ? legacyStreamId
            : nativeStreamId;
        var streams = native?.RunStreams.Fork() ?? legacyRng;
        var stream = streams.GetStream(streamId);
        var callOrdinal = StochasticProviderScopeV111.GetNextOrdinal(
            streams, streamId) is { } scopedOrdinal
                ? (ulong)scopedOrdinal
                : (stream.CallCount ?? 0);
        if (callOrdinal > int.MaxValue)
            throw new NotSupportedException(
                "Random combat-card pool ordinal exceeds the request range.");
        IRandomIndexProviderV111 provider = native is null
            ? new LegacyPrototypeRandomIndexProviderV111()
            : new NativeRandomIndexProviderV111();
        var ownerId = ownerPrefix + ":pool:" +
            CanonicalJson.Sha256(orderedPool);
        var originalLabels = native is not null &&
            RandomBoundaryJournalV111.HasActiveJournal
                ? NativePoolShuffleLabels(combat, orderedPool)
                : null;
        var decisions = NativeFisherYatesV111.ShuffleInPlace(
            orderedPool, streams, provider, streamId,
            ownerId, siteId,
            (int)callOrdinal);
        if (originalLabels is not null)
            SemanticPoolShuffleChoiceV111.RecordNativeAudit(
                originalLabels,
                NativePoolShuffleLabels(combat, orderedPool), decisions,
                selectedCount, sourceOperationKind.ToString(),
                sourceCardInstanceId);
        return native is null
            ? combat
            : combat with
            {
                Stochastic = CombatStochasticStateV111.WithNativeRng(
                    combat.Stochastic, native with { RunStreams = streams })
            };
    }

    private static string[] NativePoolShuffleLabels<T>(
        CombatState combat, IReadOnlyList<T> pool)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pool);
        return pool.Select(value => value switch
        {
            long instanceId => "card-instance:" + instanceId.ToString(
                System.Globalization.CultureInfo.InvariantCulture) + ":" +
                RequireCombatCard(combat, instanceId).CardId,
            string cardId => cardId,
            _ => throw new NotSupportedException(
                "UNSUPPORTED_NATIVE_POOL_SHUFFLE_IDENTITY type=" +
                typeof(T).FullName)
        }).ToArray();
    }

    private static string[] NativePoolShuffleLabels(
        IReadOnlyDictionary<long, string> cardModels,
        IReadOnlyList<long> pool)
    {
        ArgumentNullException.ThrowIfNull(cardModels);
        ArgumentNullException.ThrowIfNull(pool);
        return pool.Select(instanceId =>
        {
            if (!cardModels.TryGetValue(instanceId, out var cardId))
                throw new InvalidDataException(
                    "Native entry shuffle support contains an unknown card instance.");
            return "card-instance:" + instanceId.ToString(
                System.Globalization.CultureInfo.InvariantCulture) + ":" +
                cardId;
        }).ToArray();
    }

    private static (CombatState Combat, int SelectedIndex)
        ResolveGeneratedCardModelIndex(
            CombatState combat,
            RngBundle legacyRng,
            string[] candidateCardIds,
            PrototypeCombatEffectKind operationKind,
            long? sourceCardInstanceId,
            int ordinal,
            string siteId = StochasticSiteRegistryV111.CombatGeneratedCardSelectSiteId)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(legacyRng);
        ArgumentNullException.ThrowIfNull(candidateCardIds);
        if (!StochasticSiteRegistryV111.CombatGeneratedCardModelSiteIds.Contains(
                siteId, StringComparer.Ordinal))
            throw new NotSupportedException(
                $"UNSUPPORTED_RANDOM_SITE site={siteId} " +
                "operation=generated_card_selection.");
        if (candidateCardIds.Length == 0 ||
            candidateCardIds.Any(string.IsNullOrWhiteSpace) ||
            candidateCardIds.Distinct(StringComparer.Ordinal).Count() !=
                candidateCardIds.Length)
            throw new ArgumentException(
                "Generated-card selection requires a nonempty unique support.",
                nameof(candidateCardIds));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var native = combat.Stochastic.NativeRng;
        var streamId = native is null
            ? "combat"
            : "run.combat_card_generation";
        var streams = native?.RunStreams.Fork() ?? legacyRng;
        IRandomIndexProviderV111 provider = native is null
            ? new LegacyPrototypeRandomIndexProviderV111()
            : new NativeRandomIndexProviderV111();
        var ownerId = sourceCardInstanceId is { } sourceCardId
            ? "card_instance:" + sourceCardId
            : "effect:" + operationKind;
        ownerId += ":pool:" + CanonicalJson.Sha256(candidateCardIds);
        var decision = StochasticProviderScopeV111.ResolveIndex(
            provider, new RandomIndexRequestV111(
                siteId, streamId, ownerId, ordinal,
                Enumerable.Range(0, candidateCardIds.Length).ToArray()),
            streams);
        if (decision.ProviderMode == "native-v111" &&
            RandomBoundaryJournalV111.HasActiveJournal)
        {
            if (SemanticGeneratedCardNativeAuditV111.IsSupportedAuditSite(
                    decision.Request.SiteId))
                RandomBoundaryJournalV111.RecordGeneratedCardModelOutcome(
                    SemanticGeneratedCardNativeAuditV111.Create(
                        decision, candidateCardIds, operationKind,
                        sourceCardInstanceId));
            else if (decision.Request.SiteId ==
                     SemanticCardTransformChoiceV111.SiteId)
            {
                var transformedCardInstanceId = sourceCardInstanceId ??
                    throw new InvalidDataException(
                        "Native transform selection has no source card identity.");
                var originalCardId = RequireCombatCard(
                    combat, transformedCardInstanceId).CardId;
                RandomBoundaryJournalV111.RecordCardTransformOutcome(
                    SemanticCardTransformNativeAuditV111.Create(
                        decision, candidateCardIds, operationKind,
                        transformedCardInstanceId, originalCardId));
            }
        }
        var updated = native is null
            ? combat
            : combat with
            {
                Stochastic = CombatStochasticStateV111.WithNativeRng(
                    combat.Stochastic, native with { RunStreams = streams })
            };
        return (updated, decision.SelectedIndex);
    }

    private static (CombatState Combat, int SelectedIndex,
        string? SelectedGapIdentity)
        ResolveRandomPilePosition(
            CombatState combat,
            RngBundle legacyRng,
            long[] orderedDrawPile,
            string cardModelId,
            PrototypeCombatEffectKind? operationKind,
            long? sourceCardInstanceId,
            int ordinal,
            string siteId = StochasticSiteRegistryV111.DrawPileRandomInsertSiteId,
            string? requestOwnerPrefix = null,
            string pileName = "draw")
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(legacyRng);
        ArgumentNullException.ThrowIfNull(orderedDrawPile);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardModelId);
        if (!StochasticSiteRegistryV111.CombatRandomPilePositionSiteIds.Contains(
                siteId, StringComparer.Ordinal))
            throw new NotSupportedException(
                $"UNSUPPORTED_RANDOM_SITE site={siteId} " +
                "operation=random_pile_insertion.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (requestOwnerPrefix is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(requestOwnerPrefix);
        if (pileName is not ("draw" or "discard"))
            throw new ArgumentOutOfRangeException(nameof(pileName));
        if (orderedDrawPile.Distinct().Count() != orderedDrawPile.Length)
            throw new ArgumentException(
                "Random insertion support requires unique card instances.",
                nameof(orderedDrawPile));

        var native = combat.Stochastic.NativeRng;
        var streamId = native is null
            ? "combat"
            : "run.shuffle";
        var streams = native?.RunStreams.Fork() ?? legacyRng;
        IRandomIndexProviderV111 provider = native is null
            ? new LegacyPrototypeRandomIndexProviderV111()
            : new NativeRandomIndexProviderV111();
        var ownerId = requestOwnerPrefix ??
            (sourceCardInstanceId is { } sourceCardId
                ? "card_instance:" + sourceCardId
                : operationKind is { } kind
                    ? "effect:" + kind
                    : throw new ArgumentException(
                        "Random pile insertion requires a stable owner.",
                        nameof(requestOwnerPrefix)));
        ownerId += ":card:" + cardModelId + ":" + pileName + "_pile:" +
            CanonicalJson.Sha256(orderedDrawPile);
        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId,
            ordinal, [], OperationKind: "bounded_integer",
            BoundedCount: checked(orderedDrawPile.Length + 1));
        var controlledCandidateLabels =
            PrototypeControlledEnemyActionContinuationV111
                    .IsEnemyActionSite(siteId) &&
                (ControlledStochasticChanceReplayV111.IsActive ||
                 ControlledEventChoiceReplayV111.IsActive)
                ? DescribeEnemyInsertionCandidateLabels(
                    cardModelId, pileName, orderedDrawPile)
                : null;
        using var typedCandidateLabels =
            ControlledStochasticChanceReplayV111.DescribeCandidates(
                controlledCandidateLabels);
        using var eventCandidateLabels =
            ControlledEventChoiceReplayV111.DescribeCandidates(
                controlledCandidateLabels);
        if (ControlledChanceGuardV111.Active &&
            !ControlledChanceGuardV111.EngineOwnedNativeProviderActive)
        {
            if (PrototypeControlledEnemyActionContinuationV111
                    .IsEnemyActionSite(siteId))
            {
                var selectedIndex = StochasticProviderScopeV111
                    .ResolveIndex(provider, request, streams).SelectedIndex;
                return (combat, selectedIndex,
                    ResolveSelectedEnemyInsertionGapIdentity(
                        request, selectedIndex));
            }
            throw new NotSupportedException(
                $"UNSUPPORTED_CONTROLLED_CHANCE site={siteId} " +
                "operation=bounded_integer requires a saved pile-insertion continuation.");
        }

        var decision = StochasticProviderScopeV111.ResolveIndex(
            provider, request, streams);
        if (decision.ProviderMode == "native-v111" &&
            RandomBoundaryJournalV111.HasActiveJournal &&
            StochasticSiteRegistryV111.ControlledDrawInsertionSiteIds
                .Contains(siteId, StringComparer.Ordinal))
            RandomBoundaryJournalV111.RecordDrawInsertionOutcome(
                SemanticDrawInsertionNativeAuditV111.Create(
                    decision, orderedDrawPile, cardModelId));
        if (decision.ProviderMode == "native-v111" &&
            RandomBoundaryJournalV111.HasActiveJournal &&
            StochasticSiteRegistryV111.CombatEnemyPileInsertionSiteIds
                .Contains(siteId, StringComparer.Ordinal))
            RandomBoundaryJournalV111.RecordEnemyPileInsertionOutcome(
                SemanticEnemyPileInsertionNativeAuditV111.Create(
                    decision, orderedDrawPile, cardModelId, pileName));
        var updated = native is null
            ? combat
            : combat with
            {
                Stochastic = CombatStochasticStateV111.WithNativeRng(
                    combat.Stochastic, native with { RunStreams = streams })
            };
        return (updated, decision.SelectedIndex,
            ResolveSelectedEnemyInsertionGapIdentity(
                request, decision.SelectedIndex));
    }

    private static string? ResolveSelectedEnemyInsertionGapIdentity(
        RandomIndexRequestV111 request, int selectedIndex) =>
        PrototypeControlledEnemyActionContinuationV111.IsEnemyActionSite(
                request.SiteId)
            ? ControlledStochasticChanceReplayV111
                .GetSelectedIndexCandidateLabel(request, selectedIndex)
            : null;

    private static string[] DescribeEnemyInsertionCandidateLabels(
        string cardModelId, string pileName, long[] orderedPile) =>
        SemanticDrawPileAdapterV111
            .DescribePhysicalPileInsertionGapLabels(
                cardModelId, pileName, orderedPile);

    private static CombatState MakeControlledRandomTargetChoice(
        CombatState combat, string kind, int[] eligible,
        PrototypeChoiceResolutionContinuationState continuation,
        long? slyCardInstanceId = null,
        long? autoPlayCardInstanceId = null)
    {
        if (!ControlledChanceGuardV111.Active ||
            eligible.Length == 0 || eligible.Distinct().Count() != eligible.Length ||
            combat.PendingChoice is not null)
            throw new InvalidOperationException(
                "Controlled random target suspension requires a unique live support.");
        return combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "controlled_random_target",
                SourceCardInstanceId: continuation.SourceCardInstanceId,
                SourceCardDestination: continuation.SourceCardDestination,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand, 0, 0,
                    PrototypeCardSelectionResolutionKind.MoveToDiscard),
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                OuterChoiceContinuation: continuation.Fork(),
                ControlledTarget: new PrototypeControlledTargetChoiceV111(
                    kind, (int[])eligible.Clone(), slyCardInstanceId,
                    autoPlayCardInstanceId))
        };
    }

    private static CombatState MakeControlledSemanticDrawChoice(
        CombatState combat, PrototypeControlledSemanticDrawV111 draw,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        var deck = combat.Stochastic.SemanticDeck;
        if (!ControlledChanceGuardV111.Active ||
            combat.PendingChoice is not null ||
            draw.RemainingCount <= 0 || draw.EventDepth < 0 ||
            deck is not { Mode: SemanticDeckStateV111.NatureMode,
                Pending: not null })
            throw new InvalidOperationException(
                "Semantic card-effect draw suspension requires a pending Nature card.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Semantic card-effect draw suspension requires a pending Nature card.",
            "Semantic card-effect draw suspension requires a pending Nature card.",
            allowPending: true);
        return combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "controlled_semantic_draw",
                SourceCardInstanceId: continuation.SourceCardInstanceId,
                SourceCardDestination: continuation.SourceCardDestination,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand, 0, 0,
                    PrototypeCardSelectionResolutionKind.Preserve),
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                OuterChoiceContinuation: continuation.Fork(),
                ControlledSemanticDraw: draw.Fork())
        };
    }

    private static CombatState AttachControlledSemanticDrawCost(
        CombatState combat, PrototypeControlledSemanticDrawV111 draw,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        var pending = combat.PendingChoice;
        var cost = pending?.ControlledDrawCost;
        var deck = combat.Stochastic.SemanticDeck;
        if (!ControlledChanceGuardV111.Active ||
            pending is not { ChoiceId: "controlled_draw_cost",
                OuterChoiceContinuation: null,
                ControlledSemanticDraw: null } ||
            cost is null || draw.RemainingCount < 0 || draw.EventDepth < 0 ||
            cost.RemainingCount != draw.RemainingCount ||
            cost.FromHandDraw != draw.FromHandDraw ||
            cost.EventDepth != draw.EventDepth ||
            deck is not { Mode: SemanticDeckStateV111.NatureMode,
                Pending: null })
            throw new InvalidOperationException(
                "Semantic effect draw cost requires a matching saved draw frame.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Semantic effect draw cost requires a matching saved draw frame.",
            "Semantic effect draw cost requires a matching saved draw frame.");
        return combat with
        {
            PendingChoice = pending with
            {
                OuterChoiceContinuation = continuation.Fork(),
                ControlledSemanticDraw = draw.Fork()
            }
        };
    }

    private static CombatState MakeControlledRandomPileCardChoice(
        CombatState combat, long[] eligible,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        if (!ControlledChanceGuardV111.Active || eligible.Length == 0 ||
            eligible.Distinct().Count() != eligible.Length ||
            combat.PendingChoice is not null ||
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } deck)
            throw new InvalidOperationException(
                "Controlled random pile-card suspension requires a unique semantic support.");
        var models = eligible.Select(id => RequireCombatCard(combat, id).CardId)
            .ToArray();
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            candidateCardInstanceIds = eligible,
            candidateCardModelIds = models
        });
        return combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "controlled_random_pile_card",
                SourceCardInstanceId: continuation.SourceCardInstanceId,
                SourceCardDestination: continuation.SourceCardDestination,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.DrawPile, 0, 0,
                    PrototypeCardSelectionResolutionKind.Preserve),
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                OuterChoiceContinuation: continuation.Fork(),
                ControlledRandomPileCard:
                    new PrototypeControlledRandomPileCardV111(
                        StochasticSiteRegistryV111
                            .CombatJeweledMaskRandomPowerDrawCardSiteId,
                        combat.Turn - 1,
                        (long[])eligible.Clone(), supportFingerprint))
        };
    }

    /// <summary>
    /// Resume from an effect/Sly target choice within the live combat engine.
    /// The already-applied card/discard/event effects are preserved. The
    /// selected enemy identity is injected into the saved first operation or
    /// Sly autoplay; then the original queued continuation is resumed.
    /// </summary>
    internal static RunState ResumeControlledRandomTarget(
        RunState state, int selectedEnemyInstanceId)
        => ResumeControlledRandomTargetWithBoundary(
            state, selectedEnemyInstanceId).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledRandomTargetWithBoundary(
            RunState state, int selectedEnemyInstanceId)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled targeting requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("No combat for target choice.");
        var prototypeRng = state.Stochastic.PrototypeRng;
        var pending = combat.PendingChoice ??
            throw new InvalidOperationException("Missing target choice.");
        if (pending.ChoiceId != "controlled_random_target" ||
            pending.ControlledTarget is not { } target ||
            pending.OuterChoiceContinuation is not { } continuation)
            throw new InvalidOperationException(
                "The pending combat choice belongs to the player.");
        if (!SemanticTargetChoiceV111.HasValidLiveSupport(combat, pending))
            throw new InvalidDataException(
                "Saved target support or source disagrees with the live continuation.");
        var eligible = target.EligibleEnemyInstanceIds;
        var selectedIndex = Array.IndexOf(eligible,
            selectedEnemyInstanceId);
        if (selectedIndex < 0)
            throw new InvalidOperationException(
                "Selected enemy identity is outside the saved target support.");

        combat = combat with { PendingChoice = null };
        (PlayerState Player, CombatState Combat) resolved;
        if (target.Kind == "effect_random_enemy")
        {
            if (continuation.Operations.Length == 0)
                throw new InvalidDataException("Missing suspended target operation.");
            var fixedOperation = continuation.Operations[0] with
            {
                TargetEnemyId = selectedEnemyInstanceId,
                TargetMode = PrototypeEffectTarget.ActionTargetEnemy
            };
            resolved = ResumeChoiceResolutionContinuation(
                state.Player, combat, prototypeRng, continuation with
                {
                    Operations = (new[] { fixedOperation })
                        .Concat(continuation.Operations.Skip(1)).ToArray()
                });
        }
        else if (target.Kind == "sly_auto_play")
        {
            if (target.SlyCardInstanceId is not { } cardId)
                throw new InvalidDataException("Sly choice lost its source card.");
            resolved = AutoPlaySlyCard(
                state.Player, combat, cardId, prototypeRng,
                selectedEnemyInstanceId);
            if (resolved.Combat.PendingChoice is not null)
                resolved = (resolved.Player, AttachOuterChoiceContinuation(
                    resolved.Combat, continuation));
            else
                resolved = ResumeChoiceResolutionContinuation(
                    resolved.Player, resolved.Combat, prototypeRng, continuation);
        }
        else if (target.Kind == "autoplay_random_enemy")
        {
            if (target.AutoPlayCardInstanceId is not { } autoPlayCardId ||
                continuation.Operations.Length == 0)
                throw new InvalidDataException(
                    "Auto-play target choice lost its suspended source operation.");
            var suspendedOperation = continuation.Operations[0];
            if (suspendedOperation.Kind !=
                    PrototypeCombatEffectKind.AutoPlayCombatCard ||
                suspendedOperation.CardInstanceId != autoPlayCardId)
                throw new InvalidDataException(
                    "Auto-play target choice lost its suspended source operation.");
            var fixedOperation = suspendedOperation with
            {
                TargetEnemyId = selectedEnemyInstanceId
            };
            resolved = ResumeChoiceResolutionContinuation(
                state.Player, combat, prototypeRng, continuation with
                {
                    Operations = (new[] { fixedOperation })
                        .Concat(continuation.Operations.Skip(1)).ToArray()
                });
        }
        else
            throw new InvalidDataException("Unknown controlled target kind.");

        var result = state with
        {
            Player = resolved.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { Combat = resolved.Combat }
        };
        return ResumeAutomaticPipelineContinuation(result);
    }

    /// <summary>
    /// Resolve one deferred card identity inside a card-effect draw, continue
    /// the same DrawCards invocation, then resume the saved effect queue.
    /// </summary>
    internal static RunState ResumeControlledSemanticEffectDraw(
        RunState state, long selectedCardInstanceId)
        => ResumeControlledSemanticEffectDrawWithBoundary(
            state, selectedCardInstanceId).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledSemanticEffectDrawWithBoundary(
            RunState state, long selectedCardInstanceId)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic card-effect draw requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ?? throw new InvalidOperationException(
            "No combat for semantic card-effect draw.");
        var prototypeRng = state.Stochastic.PrototypeRng;
        var pending = combat.PendingChoice;
        var draw = pending?.ControlledSemanticDraw;
        var continuation = pending?.OuterChoiceContinuation;
        var deck = combat.Stochastic.SemanticDeck;
        bool supportedPipelineDraw = pending is not null &&
            IsSupportedAutomaticPipelineSemanticDrawContinuation(
                combat, pending);
        if (state.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is not null &&
             !supportedPipelineDraw) ||
            pending is not { ChoiceId: "controlled_semantic_draw" } ||
            draw is null || continuation is null ||
            deck is not { Mode: SemanticDeckStateV111.NatureMode,
                Pending: not null })
            throw new InvalidDataException(
                "Semantic card-effect draw checkpoint is inconsistent.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Semantic card-effect draw checkpoint is inconsistent.",
            "Semantic card-effect draw checkpoint is inconsistent.",
            allowPending: true);
        var request = SemanticDeckEngineV111.Inspect(deck) ??
            throw new InvalidDataException(
                "Semantic card-effect draw lost its pending event.");
        var decision = SemanticDrawChoiceV111.Select(
            request, request.EventId, selectedCardInstanceId);
        combat = SemanticDrawChoiceV111.Commit(
            combat with { PendingChoice = null }, decision);
        if (continuation.Operations.FirstOrDefault()?.Kind ==
            PrototypeCombatEffectKind.AutoPlayTopDrawCards)
        {
            var resumed = ResumeChoiceResolutionContinuation(
                state.Player, combat, prototypeRng, continuation);
            var resumedState = state with
            {
                Player = resumed.Player,
                Stochastic = RunStochasticStateV111.WithPrototypeRng(
                    state.Stochastic, prototypeRng),
                World = world with { Combat = resumed.Combat }
            };
            return ResumeAutomaticPipelineContinuation(resumedState);
        }
        var drawn = DrawCards(
            state.Player, combat, draw.RemainingCount, prototypeRng,
            draw.FromHandDraw, eventDepth: draw.EventDepth,
            suspensionPolicy:
                PrototypeDrawCardsSuspensionPolicyV111
                    .SemanticDrawCards(effectDraw: true));
        var continued = ContinueControlledSemanticEffectDraw(
            state with
            {
                Stochastic = RunStochasticStateV111.WithPrototypeRng(
                    state.Stochastic, prototypeRng)
            }, drawn, draw, continuation);
        return ResumeAutomaticPipelineContinuation(continued);
    }

    private static RunState ContinueControlledSemanticEffectDraw(
        RunState state, PrototypeDrawCardsResult drawn,
        PrototypeControlledSemanticDrawV111 draw,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        var result = ResolveControlledSemanticDrawResult(
            state, drawn, draw, continuation,
            automaticHandDrawRequested: null,
            recycleSite: "combat.card_effect.draw",
            recycleOperation: "recycle_requires_composed_continuation");
        return result.Run;
    }

    private static (PlayerState Player, CombatState Combat) AutoPlaySlyCard(
        PlayerState player,
        CombatState combat,
        long cardInstanceId,
        RngBundle rng,
        int? controlledSelectedEnemyInstanceId = null)
    {
        if (!combat.DiscardPile.Contains(cardInstanceId))
        {
            throw new InvalidOperationException(
                $"Sly card {cardInstanceId} must be in the discard pile before auto-play.");
        }

        var card = RequireCombatCard(combat, cardInstanceId);
        var definition = PrototypeContent.Card(card.CardId);
        if (!CardHasKeyword(
                combat,
                cardInstanceId,
                PrototypeCardKeyword.Sly))
        {
            throw new InvalidOperationException(
                $"Card {cardInstanceId} is not Sly.");
        }

        // Source CardCmd.AutoPlay(choiceContext, card, target: null,
        // AutoPlayType.SlyDiscard) chooses from the ordered hittable set
        // using RunState.Rng.CombatTargets. Native combat routes this through
        // the typed run.combat_targets provider; the retained prototype
        // profile keeps its legacy combat stream. Source cursor parity is
        // still unverified.
        int? autoPlayTarget = null;
        if (definition.Target == PrototypeCardTarget.Enemy)
        {
            var eligible = combat.Enemies
                .Where(enemy => enemy.Hp > 0 && !enemy.Escaped)
                .ToArray();
            if (eligible.Length == 0)
            {
                // Source moves an un-targetable auto-play into its result
                // pile without activating effects. This is normally
                // intercepted earlier by the end-of-combat guard.
                return (player, combat);
            }
            if (ControlledChanceGuardV111.Active &&
                controlledSelectedEnemyInstanceId is null)
                throw new NotSupportedException(
                    "UNSUPPORTED_CONTROLLED_CHANCE site=combat.sly_autoplay.target " +
                    "operation=choose_index requires a saved target continuation.");
            int selectedIndex;
            if (controlledSelectedEnemyInstanceId is { } selectedEnemyId)
            {
                selectedIndex = Array.FindIndex(eligible,
                    enemy => enemy.InstanceId == selectedEnemyId);
                if (selectedIndex < 0)
                    throw new InvalidOperationException(
                        "Selected Sly target identity is outside current support.");
            }
            else
            {
                var eligibleIds = eligible.Select(enemy =>
                    enemy.InstanceId).ToArray();
                var target = ResolveRandomEnemyTarget(
                    combat, rng, eligibleIds,
                    StochasticSiteRegistryV111.CombatSlyAutoplayRandomTargetSiteId,
                    "combat",
                    "card:" + cardInstanceId,
                    combat.AutoPlayTargetRolls?.Count(roll =>
                        roll.CardInstanceId == cardInstanceId &&
                        roll.Turn == combat.Turn) ?? 0);
                combat = target.Combat;
                selectedIndex = target.SelectedIndex;
            }
            if (selectedIndex < 0 || selectedIndex >= eligible.Length)
                throw new InvalidOperationException(
                    "Selected Sly enemy index is outside current support.");
            autoPlayTarget = eligible[selectedIndex].InstanceId;
            combat = combat with
            {
                AutoPlayTargetRolls =
                    (combat.AutoPlayTargetRolls
                     ?? Array.Empty<PrototypeAutoPlayTargetRoll>())
                    .Append(new PrototypeAutoPlayTargetRoll(
                        cardInstanceId, combat.Turn, eligible.Length,
                        selectedIndex, autoPlayTarget.Value))
                    .ToArray()
            };
        }

        combat = SetZone(
            combat, PrototypeCardZone.DiscardPile,
            combat.DiscardPile.Where(id => id != cardInstanceId).ToArray());

        if (IsCardFreeByPower(combat, definition))
        {
            combat = ConsumeMatchingFreeCardPower(combat, definition.Type);
        }

        var playCountResult =
            ResolveCardPlayCountAndConsumeModifiers(
                combat,
                card,
                definition.Type);
        combat = playCountResult.Combat;

        var sourceDestination = CardExhaustsOnUse(
                definition, card)
            ? PrototypeCardZone.ExhaustPile
            : PrototypeCardZone.DiscardPile;

        return ResolveCardPlaySeries(
            player,
            combat,
            rng,
            new PrototypeCardPlaySeriesState(
                SourceCardInstanceId: cardInstanceId,
                SourceCardDestination: sourceDestination,
                TargetEnemyId: autoPlayTarget,
                EnergySpent: 0,
                PlayCount: playCountResult.PlayCount,
                NextPlayIndex: 0,
                RemoveSourceCardOnCompletion:
                    definition.Type == PrototypeCardType.Power,
                IsAutoPlay: true));
    }

    private static CombatState ApplyCardSelection(
        CombatState combat,
        PrototypeCardSelectionSpec selection,
        long[] selected,
        RngBundle rng,
        IReadOnlyDictionary<long, string>? transformReplacementCardIds = null)
    {
        var source = GetZone(combat, selection.SourceZone);
        var selectedSet = selected.ToHashSet();

        if (selected.Any(cardId => !source.Contains(cardId)))
        {
            throw new InvalidOperationException(
                "Selected card is no longer in the requested source zone.");
        }

        if (selection.Resolution == PrototypeCardSelectionResolutionKind.CopyToHand)
        {
            foreach (var id in selected)
            {
                var card = RequireCombatCard(combat, id);
                combat = AddGeneratedCardCopies(combat, new PrototypeCombatCardSnapshot(
                    card.CardId, card.UpgradeLevel, card.State.Clone(), card.CombatEnergyCostDelta,
                    card.TemporaryEnergyCost, card.KeywordOverrides, card.ReplayCount,
                    card.Enchantment, card.EnchantmentTriggeredThisCombat, card.Affliction,
                    card.SuppressedUpgradeLevels), selection.CopiesPerSelection);
            }
            return combat;
        }

        if (selection.Resolution
                == PrototypeCardSelectionResolutionKind.TransformRandom)
        {
            var transformOrdinal = 0;
            foreach (var instanceId in selected)
            {
                var original = RequireCombatCard(combat, instanceId);
                if (PrototypeCardRules.IsEternal(original))
                {
                    throw new InvalidOperationException(
                        "Eternal combat cards cannot be transformed.");
                }

                // Native combat transforms into a different eligible
                // card from the ORIGINAL card's own generation pool.
                var eligible = EligibleCombatTransformPool(
                    original.CardId, combat);
                if (eligible.Length == 0)
                {
                    throw new NotSupportedException(
                        "No eligible native-pool solo transform targets.");
                }

                string replacement;
                if (transformReplacementCardIds is not null)
                {
                    if (!transformReplacementCardIds.TryGetValue(
                            instanceId, out var selectedReplacement) ||
                        !eligible.Contains(selectedReplacement,
                            StringComparer.Ordinal))
                        throw new InvalidDataException(
                            "Controlled transform replacement is outside its source pool.");
                    replacement = selectedReplacement;
                }
                else
                {
                    if (ControlledChanceGuardV111.Active)
                        throw new NotSupportedException(
                            "UNSUPPORTED_CONTROLLED_CHANCE " +
                            "site=combat.card_transform.replacement.index_draw " +
                            "operation=choose_card_model requires a saved transform continuation.");
                    var replacementChoice = ResolveGeneratedCardModelIndex(
                        combat, rng, eligible,
                        PrototypeCombatEffectKind.ChooseCards,
                        instanceId, transformOrdinal,
                        StochasticSiteRegistryV111.CombatCardTransformReplacementSiteId);
                    combat = replacementChoice.Combat;
                    replacement = eligible[replacementChoice.SelectedIndex];
                }
                transformOrdinal++;
                var replacementId = combat.NextCardInstanceId;
                var oldPersistentId = original.PersistentCardInstanceId;
                var transformedDrawPile = combat.DrawPile.Select(id =>
                    id == instanceId ? replacementId : id).ToArray();
                var drawPileIndex = Array.IndexOf(
                    combat.DrawPile, instanceId);

                var nextCardZones = combat.Mechanics.CardZones with
                {
                    DrawPile = transformedDrawPile,
                    Hand = combat.Hand.Select(id =>
                        id == instanceId ? replacementId : id).ToArray(),
                    DiscardPile = combat.DiscardPile.Select(id =>
                        id == instanceId ? replacementId : id).ToArray(),
                    ExhaustPile = combat.ExhaustPile.Select(id =>
                        id == instanceId ? replacementId : id).ToArray(),
                    PlayPile = combat.PlayCardIds.Select(id =>
                        id == instanceId ? replacementId : id).ToArray(),
                    ChoicePool = combat.ChoiceCardIds.Select(id =>
                        id == instanceId ? replacementId : id).ToArray()
                };
                var replacementCard = new CombatCardInstance(
                    replacementId,
                    PersistentCardInstanceId: null,
                    CardId: replacement,
                    UpgradeLevel: 0,
                    IsTemporary: true,
                    State: PrototypeJson.EmptyObject());

                // Transform creates a new physical card; it does not
                // mutate the original card's persistent deck identity.
                // Track that absence explicitly for run invariants.
                combat = drawPileIndex >= 0
                    ? SemanticDrawPileAdapterV111.ApplyDrawPileReplacement(
                        combat, drawPileIndex, instanceId, replacementId,
                        nextCardZones, replacementCard,
                        transformedPersistentCardInstanceId:
                            oldPersistentId,
                        recordPersistentCardTransform: true)
                    : SemanticDrawPileAdapterV111
                        .ApplyZoneIdentityReplacement(
                            combat, nextCardZones, instanceId, replacementId,
                            replacementCard,
                            transformedPersistentCardInstanceId:
                                oldPersistentId,
                            recordPersistentCardTransform: true);
            }
            return combat;
        }

        if (selection.Resolution
                == PrototypeCardSelectionResolutionKind.Preserve
            && !selection.RemoveUnselectedFromSource)
        {
            return combat;
        }

        var destinationZone = selection.Resolution switch
        {
            PrototypeCardSelectionResolutionKind.Preserve =>
                (PrototypeCardZone?)null,
            PrototypeCardSelectionResolutionKind.MoveToHand =>
                PrototypeCardZone.Hand,
            PrototypeCardSelectionResolutionKind.MoveToDiscard =>
                PrototypeCardZone.DiscardPile,
            PrototypeCardSelectionResolutionKind.MoveToExhaust =>
                PrototypeCardZone.ExhaustPile,
            // DrawCards pops the last draw-pile entry: append means top.
            PrototypeCardSelectionResolutionKind.MoveToDrawTop =>
                PrototypeCardZone.DrawPile,
            _ => throw new ArgumentOutOfRangeException()
        };

        if (destinationZone == selection.SourceZone)
        {
            throw new InvalidOperationException(
                "Card-selection source and destination zones coincide.");
        }

        // Source card-selection movement can alter the hidden draw pile
        // without an RNG call. The adapter commits provider constraints and
        // physical zones together. MoveToDrawTop appends to the engine's
        // bottom-first array and preserves the selected identities as known
        // semantic slots.
        if (combat.Stochastic.SemanticDeck is not null &&
            (selection.SourceZone == PrototypeCardZone.DrawPile ||
             destinationZone == PrototypeCardZone.DrawPile))
        {
            SemanticDrawPileAdapterV111.ValidateSynchronized(
                combat, requireIdle: true);
            if (selection.RemoveUnselectedFromSource &&
                selection.SourceZone == PrototypeCardZone.DrawPile)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.card_selection " +
                    "operation=remove_unselected_hidden_draw_cards");
        }

        var unselected = source
            .Where(cardId => !selectedSet.Contains(cardId))
            .ToArray();
        if (selection.RemoveUnselectedFromSource)
        {
            var unselectedSet = unselected.ToHashSet();
            if (combat.Cards.Any(card =>
                    unselectedSet.Contains(card.InstanceId)
                    && !card.IsTemporary))
            {
                throw new InvalidOperationException(
                    "Only temporary choice cards may be removed when a selection resolves.");
            }

        }

        var zoneChanges = new Dictionary<PrototypeCardZone, long[]>
        {
            [selection.SourceZone] = selection.RemoveUnselectedFromSource
                ? Array.Empty<long>()
                : unselected
        };

        if (destinationZone is { } destination)
        {
            var destinationCards = combat.Mechanics.CardZones
                .CardsIn(destination);
            zoneChanges[destination] =
                destinationCards.Concat(selected).ToArray();
        }

        var selectionZones = combat.Mechanics.CardZones
            .WithZoneChanges(zoneChanges);
        if (selection.SourceZone == PrototypeCardZone.DrawPile)
        {
            var removed = combat.DrawPile
                .Where(cardId => !selectionZones.DrawPile.Contains(cardId))
                .ToArray();
            combat = SemanticDrawPileAdapterV111.ApplyDrawPileRemoval(
                combat, removed, selectionZones,
                removedFromCombatInstanceIds:
                    selection.RemoveUnselectedFromSource
                        ? unselected : []);
        }
        else if (destinationZone == PrototypeCardZone.DrawPile &&
                 selected.Length > 0)
        {
            combat = SemanticDrawPileAdapterV111
                .ApplyDrawPileInsertionSequence(
                    combat, combat.DrawPile.Length, selected,
                    selectionZones, publiclyKnown: true,
                    removedCardInstanceIds:
                        selection.RemoveUnselectedFromSource
                            ? unselected : []);
        }
        else if (selection.RemoveUnselectedFromSource)
        {
            combat = SemanticDrawPileAdapterV111.ApplyZoneIdentityDelta(
                combat, selectionZones, [], unselected,
                removedCombatCardInstanceIds: unselected);
        }
        else
        {
            combat = SemanticDrawPileAdapterV111.ApplyZoneTransition(
                combat, selectionZones);
        }
        return combat;
    }

    private static long[] GetZone(CombatState combat, PrototypeCardZone zone) =>
        combat.Mechanics.CardZones.CardsIn(zone);

    private static CombatState MoveCards(
        CombatState combat,
        PrototypeCardZone sourceZone,
        PrototypeCardZone destinationZone,
        IReadOnlyCollection<long> cardInstanceIds)
        => SemanticDrawPileAdapterV111.ApplyCardZoneMove(
            combat, sourceZone, destinationZone, cardInstanceIds);

    private static void RequireSemanticNativeStreamOwner(
        CombatState combat, string siteId)
    {
        if (combat.Stochastic.SemanticDeck is
                { Mode: SemanticDeckStateV111.NativeMode } &&
            combat.Stochastic.NativeRng is null)
            throw new NotSupportedException(
                $"UNSUPPORTED_SEMANTIC_NATIVE site={siteId} " +
                "operation=missing_native_stream_owner");
    }

    private static CombatState SetZone(
        CombatState combat,
        PrototypeCardZone zone,
        long[] cards)
    {
        if (zone == PrototypeCardZone.DrawPile)
        {
            if (combat.DrawPile.SequenceEqual(cards))
                return combat;

            if (cards.Length <= combat.DrawPile.Length ||
                !cards.Take(combat.DrawPile.Length)
                    .SequenceEqual(combat.DrawPile))
                throw new InvalidOperationException(
                    "Draw-pile writes through SetZone must be known top insertions.");

            var inserted = cards.Skip(combat.DrawPile.Length).ToArray();
            var insertedSet = inserted.ToHashSet();
            var zoneChanges = new Dictionary<PrototypeCardZone, long[]>
            {
                [PrototypeCardZone.DrawPile] = (long[])cards.Clone()
            };
            foreach (var sourceZone in Enum.GetValues<PrototypeCardZone>()
                .Where(sourceZone => sourceZone !=
                    PrototypeCardZone.DrawPile))
            {
                var sourceCards = combat.Mechanics.CardZones
                    .CardsIn(sourceZone);
                if (sourceCards.Any(insertedSet.Contains))
                    zoneChanges[sourceZone] = sourceCards
                        .Where(id => !insertedSet.Contains(id)).ToArray();
            }

            return SemanticDrawPileAdapterV111
                .ApplyDrawPileInsertionSequence(
                    combat,
                    combat.DrawPile.Length,
                    inserted,
                    combat.Mechanics.CardZones.WithZoneChanges(zoneChanges),
                    publiclyKnown: true);
        }

        var nextZones = combat.Mechanics.CardZones.WithCardsIn(zone, cards);
        var currentIds = Enum.GetValues<PrototypeCardZone>()
            .SelectMany(currentZone =>
                combat.Mechanics.CardZones.CardsIn(currentZone)).ToHashSet();
        var nextIds = Enum.GetValues<PrototypeCardZone>()
            .SelectMany(nextZone => nextZones.CardsIn(nextZone)).ToHashSet();
        return SemanticDrawPileAdapterV111.ApplyZoneIdentityDelta(
            combat, nextZones,
            nextIds.Except(currentIds).ToArray(),
            currentIds.Except(nextIds).ToArray());
    }

    private static IEnumerable<long[]> ChooseCombinations(long[] source, int count)
    {
        if (count < 0 || count > source.Length)
        {
            yield break;
        }

        if (count == 0)
        {
            yield return Array.Empty<long>();
            yield break;
        }

        var buffer = new long[count];
        foreach (var result in ChooseCombinationsCore(source, 0, 0, buffer))
        {
            yield return result;
        }
    }

    private static IEnumerable<long[]> ChooseCombinationsCore(
        long[] source,
        int start,
        int depth,
        long[] buffer)
    {
        if (depth == buffer.Length)
        {
            yield return (long[])buffer.Clone();
            yield break;
        }

        var remaining = buffer.Length - depth;
        for (var index = start; index <= source.Length - remaining; index++)
        {
            buffer[depth] = source[index];
            foreach (var result in ChooseCombinationsCore(
                         source,
                         index + 1,
                         depth + 1,
                         buffer))
            {
                yield return result;
            }
        }
    }

    private static bool IsInnate(CombatCardInstance card)
    {
        var definition = PrototypeContent.Card(card.CardId);
        return definition.Innate
            || (card.UpgradeLevel > 0 && definition.InnateOnUpgrade);
    }

    private static CombatCardInstance RequireCombatCard(
        CombatState combat,
        long instanceId) =>
        combat.Cards.FirstOrDefault(card => card.InstanceId == instanceId)
        ?? throw new InvalidOperationException($"Combat card instance {instanceId} is missing.");

    private sealed record PrototypeDamageResult(
        CombatState Combat,
        int DamageDealt,
        bool Defeated);

    private static CombatState SetEnemyBlock(
        CombatState combat,
        int enemyId,
        int block)
    {
        var enemies = combat.Enemies
            .Select(enemy => enemy.Fork())
            .ToArray();
        var index = Array.FindIndex(
            enemies,
            enemy => enemy.InstanceId == enemyId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");
        }

        enemies[index] = enemies[index] with
        {
            Block = Math.Max(0, block)
        };
        return combat with { Enemies = enemies };
    }

    private static CombatState RemoveEnemyPower(
        CombatState combat,
        int enemyId,
        string powerId)
    {
        var enemies = combat.Enemies
            .Select(enemy => enemy.Fork())
            .ToArray();
        var index = Array.FindIndex(
            enemies,
            enemy => enemy.InstanceId == enemyId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");
        }

        enemies[index] = enemies[index] with
        {
            Powers = enemies[index].PowerStates
                .Where(power =>
                    !StringComparer.Ordinal.Equals(
                        power.PowerId,
                        powerId))
                .ToArray()
        };
        return combat with { Enemies = enemies };
    }

    private static int ResolveEnemyStatusTriggerCount(
        CombatState combat,
        string statusId,
        int stacks)
    {
        var extraTriggers =
            combat.PlayerPowers.Sum(power =>
            {
                var definition =
                    PrototypeContent.Power(
                        power.PowerId);
                return power.Stacks > 0
                    && StringComparer.Ordinal.Equals(
                        definition
                            .ExtraEnemyStatusTriggerStatusId,
                        statusId)
                        ? definition
                            .ExtraEnemyStatusTriggersPerStack
                          * power.Stacks
                        : 0;
            });

        return Math.Min(
            Math.Max(0, stacks),
            1 + Math.Max(0, extraTriggers));
    }

    private static CombatState TriggerEnemyStatus(
        CombatState combat,
        int enemyId,
        string statusId,
        RngBundle rng)
    {
        var enemy = combat.Enemies.SingleOrDefault(
            item => item.InstanceId == enemyId)
            ?? throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");
        var stacks =
            enemy.Statuses.GetValueOrDefault(statusId);
        var triggerCount =
            ResolveEnemyStatusTriggerCount(
                combat,
                statusId,
                stacks);

        for (var trigger = 0;
             trigger < triggerCount;
             trigger++)
        {
            enemy = combat.Enemies.Single(
                item =>
                    item.InstanceId == enemyId);
            if (enemy.Hp <= 0
                || enemy.Statuses.GetValueOrDefault(
                    statusId) <= 0)
            {
                break;
            }

            combat = TriggerEnemyStatusOnce(
                combat,
                enemyId,
                statusId,
                rng);
        }

        return combat;
    }

    private static CombatState TriggerEnemyStatusOnce(
        CombatState combat,
        int enemyId,
        string statusId,
        RngBundle rng)
    {
        var definition = PrototypeContent.Status(statusId);
        var enemy = combat.Enemies.SingleOrDefault(item =>
            item.InstanceId == enemyId)
            ?? throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");

        var stacks = enemy.Statuses.GetValueOrDefault(
            statusId);
        if (enemy.Hp <= 0 || stacks <= 0)
        {
            return combat;
        }

        if (definition.TriggerKind
            != PrototypeStatusTriggerKind.DamageSelfByStacks)
        {
            throw new InvalidOperationException(
                $"Status '{statusId}' has no immediate trigger semantics.");
        }

        var hpLoss = LoseEnemyHp(
            combat,
            enemyId,
            stacks);
        combat = hpLoss.Combat;

        if (!hpLoss.Defeated
            && definition.DecayOnTrigger != 0)
        {
            var enemies = combat.Enemies
                .Select(item => item.Fork())
                .ToArray();
            var index = Array.FindIndex(
                enemies,
                item => item.InstanceId == enemyId);
            var statuses = new Dictionary<string, int>(
                enemies[index].Statuses,
                StringComparer.Ordinal);
            var remaining =
                stacks - definition.DecayOnTrigger;
            if (remaining > 0)
            {
                statuses[statusId] = remaining;
            }
            else
            {
                statuses.Remove(statusId);
            }

            enemies[index] = enemies[index] with
            {
                Statuses = statuses
            };
            combat = combat with { Enemies = enemies };
        }

        if (hpLoss.Defeated)
        {
            combat =
                CleanupSourceBoundPowersForDefeatedEnemies(
                    combat);
            combat = ResolveEnemyDeathSummons(
                combat,
                rng);
        }

        return combat;
    }

    // Narrow internal combat-state test seams: don't expose the
    // implementation-private PrototypeDamageResult result type.
    internal static CombatState ApplyRawHpLossForSourceRegression(
        CombatState combat, int enemyId, int amount) =>
        LoseEnemyHp(combat, enemyId, amount).Combat;

    internal static CombatState ApplyHitForSourceRegression(
        CombatState combat, int enemyId, int amount) =>
        DamageEnemy(combat, enemyId, amount).Combat;

    private static PrototypeDamageResult LoseEnemyHp(
        CombatState combat,
        int enemyId,
        int hpLoss)
    {
        var enemies = combat.Enemies
            .Select(enemy => enemy.Fork())
            .ToArray();
        var index = Array.FindIndex(
            enemies,
            enemy => enemy.InstanceId == enemyId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");
        }

        var enemy = enemies[index];
        if (enemy.Hp <= 0 || enemy.BossPendingRevival)
        {
            return new PrototypeDamageResult(
                combat,
                0,
                false);
        }

        // Native PlowPower/ShriekPower trigger only from
        // PowerModel.AfterDamageReceived when result.UnblockedDamage > 0.
        // Poison, direct LoseHp effects and health costs are not damage
        // callbacks. Preserve the default prototype's historical
        // generalized HP-threshold behavior unless native combat owns
        // one of these source-audited bosses.
        var nativeDamageOnlyThreshold =
            (combat.Stochastic.NativeRng?.Formation.NativeEncounterEntry,
                enemy.EnemyId) is
            ("CeremonialBeastBoss", "proto.enemy.ceremonial_beast")
            or ("TerrorEelElite", "proto.enemy.terror_eel");
        var modified = ModifyEnemyHpLoss(
            enemy,
            hpLoss,
            allowDamageTriggeredThreshold: !nativeDamageOnlyThreshold);
        enemy = modified.Enemy;
        var nextHp = Math.Max(
            0,
            enemy.Hp - modified.HpLoss);
        var damageDealt = enemy.Hp - nextHp;

        enemies[index] = InterceptEnemyLethalDeath(
            enemy with { Hp = nextHp });

        var defeated = enemy.Hp > 0 && enemies[index].Hp == 0;
        var nextCombat = combat with { Enemies = enemies };
        if (defeated)
        {
            nextCombat = ResolveAllyDeathPowers(nextCombat, enemyId);
        }
        return new PrototypeDamageResult(
            nextCombat, damageDealt, Defeated: defeated);
    }

    private static CombatState ApplyPlayerEnemyBlockBrokenRelics(
        PlayerState player, CombatState combat, int enemyId)
    {
        foreach (var relic in player.ActiveRelics)
        {
            var amount = PrototypeContent.Relic(relic.RelicId).EnemyVulnerableOnBlockBroken;
            if (amount > 0) combat = ApplyEnemyPower(combat, enemyId, "proto.power.vulnerable", amount);
        }
        return combat;
    }

    private static PrototypeDamageResult DamageEnemy(
        CombatState combat,
        int enemyId,
        int damage,
        int minPoweredAttackHpLoss = 0) =>
        DamageEnemyInternal(combat, enemyId, damage,
            minPoweredAttackHpLoss, false);

    private static PrototypeDamageResult DamageEnemyInternal(
        CombatState combat,
        int enemyId,
        int damage,
        int minPoweredAttackHpLoss,
        bool isPoweredAttack)
    {
        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        var index = Array.FindIndex(enemies, enemy => enemy.InstanceId == enemyId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Enemy {enemyId} is missing.");
        }

        var enemy = enemies[index];
        if (enemy.Hp <= 0 || enemy.BossPendingRevival)
        {
            return new PrototypeDamageResult(combat, 0, false);
        }

        // Flutter modifies only powered attack damage. Ordinary HP loss,
        // poison and non-powered hit effects bypass the 50% modifier.
        var flutter = enemy.PowerStates.FirstOrDefault(power =>
            power.Stacks > 0
            && power.PowerId == "proto.native.hive.flutter");
        if (isPoweredAttack && flutter is not null)
        {
            damage = (int)Math.Floor(damage * 0.5m);
        }

        if (isPoweredAttack)
        {
            foreach (var power in enemy.PowerStates.Where(power =>
                power.Stacks > 0))
            {
                var definition = PrototypeContent.Power(power.PowerId);
                if (definition.EnemyIncomingPoweredAttackDenominator <= 0)
                    throw new InvalidOperationException(
                        "Enemy powered-attack modifier has nonpositive denominator.");
                damage = (int)Math.Floor(
                    (decimal)damage
                    * definition.EnemyIncomingPoweredAttackNumerator
                    / definition.EnemyIncomingPoweredAttackDenominator);
            }
        }

        var absorbed = Math.Min(enemy.Block, Math.Max(0, damage));
        // BurrowedPower.AfterBlockBroken fires when a HIT actually
        // consumes the last point of the shield. Ordinary enemy Block
        // resetting, HP-loss effects and hits on zero Block do not stun.
        var brokeBurrowedBlock = enemy.Block > 0
            && absorbed == enemy.Block
            && enemy.PowerStates.Any(power =>
                power.Stacks > 0
                && power.PowerId == "proto.native.hive.burrowed");
        var unblocked = Math.Max(0, damage - absorbed);
        // The Boot modifies attack HP loss AFTER Block absorption.
        // A fully blocked hit or non-attack HP loss does not trigger.
        // Native enemy HP-loss caps are applied afterwards.
        var hpDamage = ModifyEnemyHpLoss(
            enemy,
            unblocked > 0
                ? Math.Max(unblocked, minPoweredAttackHpLoss)
                : 0);
        enemy = hpDamage.Enemy;
        var nextHp = Math.Max(0, enemy.Hp - hpDamage.HpLoss);
        var damageDealt = enemy.Hp - nextHp;

        if (isPoweredAttack && hpDamage.HpLoss > 0
            && flutter is not null && nextHp > 0)
        {
            // Flutter.AfterDamageReceived decrements once per unblocked
            // powered hit. On the fifth, CreatureCmd.Stun overrides the
            // previously committed move for exactly one enemy turn;
            // resume the interrupted move next turn.
            var remaining = flutter.Stacks - 1;
            enemy = enemy with
            {
                Powers = enemy.PowerStates
                    .Where(power => power.ApplicationOrder
                        != flutter.ApplicationOrder)
                    .Concat(remaining > 0
                        ? new[] { flutter with { Stacks = remaining } }
                        : Array.Empty<PrototypePowerInstanceState>())
                    .OrderBy(power => power.ApplicationOrder)
                    .ToArray(),
                PlannedMoveIndex = remaining <= 0
                    ? 5 : enemy.PlannedMoveIndex,
                PlannedNextAiStateId = remaining <= 0
                    ? enemy.AiStateId : enemy.PlannedNextAiStateId
            };
        }

        // SlumberPower.AfterDamageReceived removes one stack for each
        // damage callback with unblocked damage. It is distinct from raw
        // HP loss (poison, LoseHp, costs) and from the Underdocks Asleep
        // power, which wakes on the first qualifying attack hit.
        PrototypePowerDefinition? wake = null;
        if (unblocked > 0 && nextHp > 0)
        {
            foreach (var powerState in enemy.PowerStates)
            {
                var definition = PrototypeContent.Power(
                    powerState.PowerId);
                if (definition.EnemyStacksDecayOnUnblockedDamage <= 0
                    || powerState.Stacks <= 0)
                {
                    continue;
                }

                var remaining = powerState.Stacks
                    - definition.EnemyStacksDecayOnUnblockedDamage;
                enemy = enemy with
                {
                    Powers = enemy.PowerStates
                        .Where(power => power.ApplicationOrder
                            != powerState.ApplicationOrder)
                        .Concat(remaining > 0
                            ? [powerState with { Stacks = remaining }]
                            : Array.Empty<PrototypePowerInstanceState>())
                        .OrderBy(power => power.ApplicationOrder)
                        .ToArray()
                };
                if (remaining <= 0)
                {
                    wake ??= definition;
                }
            }
        }

        // Some buffs (e.g. native Asleep) wake on unblocked attack HP
        // damage, not on blocked attacks or poison HP loss. Stun the
        // pending enemy action and select its waking AI state.
        wake ??= hpDamage.HpLoss > 0 && nextHp > 0
            ? enemy.PowerStates
                .Select(power => PrototypeContent.Power(power.PowerId))
                .FirstOrDefault(power =>
                    power.WakeOwnerOnUnblockedAttackDamage)
            : null;
        if (wake is not null)
        {
            var remove = (wake.RemoveOwnerPowersOnWake
                    ?? Array.Empty<string>())
                .ToHashSet(StringComparer.Ordinal);
            enemy = enemy with
            {
                Powers = enemy.PowerStates
                    .Where(power => !remove.Contains(power.PowerId))
                    .ToArray(),
                AiStateId = wake.OwnerAiStateOnWake ?? enemy.AiStateId,
                EnemyActionSkipsRemaining = wake.StunOwnerOnWake
                    ? Math.Max(1, enemy.EnemyActionSkipsRemaining)
                    : enemy.EnemyActionSkipsRemaining
            };
        }

        enemies[index] = InterceptEnemyLethalDeath(
            enemy with
            {
                Block = enemy.Block - absorbed,
                Hp = nextHp
            });

        if (brokeBurrowedBlock && enemies[index].Hp > 0)
        {
            // Tunneler.GetStunned -> CreatureCmd.Stun(StillDizzy,
            // "BITE_MOVE") overrides the previously announced BELOW.
            // Persist the forced action now, not during observation.
            var interrupted = enemies[index];
            enemies[index] = interrupted with
            {
                Powers = interrupted.PowerStates.Where(power =>
                    power.PowerId != "proto.native.hive.burrowed")
                    .ToArray(),
                Block = 0,
                AiStateId = "dizzy",
                PlannedMoveIndex = 3,
                PlannedNextAiStateId = "bite"
            };
        }

        var defeated = enemy.Hp > 0 && enemies[index].Hp == 0;
        var nextCombat = combat with { Enemies = enemies };
        if (defeated)
        {
            nextCombat = ResolveAllyDeathPowers(nextCombat, enemyId);
        }
        return new PrototypeDamageResult(
            nextCombat, damageDealt, Defeated: defeated);
    }

    /// <summary>
    /// A last-stand power can turn lethal enemy HP loss into a separate
    /// scripted combat phase. The intercept is one-shot so the eventual
    /// scripted self-death is not intercepted again.
    /// </summary>
    private static EnemyCombatState InterceptEnemyLethalDeath(
        EnemyCombatState enemy)
    {
        if (enemy.BossPendingRevival || enemy.Hp > 0
            || enemy.LastStandTriggered)
        {
            return enemy;
        }

        // Adaptable: the first two lethal hits interrupt the announced
        // action with a forced resurrection on the same enemy instance.
        // The final (third-form) lethal hit ends combat normally.
        if (enemy.EnemyId == PrototypeNativeGloryBosses.TestSubjectId
            && enemy.BossPhase < 2
            && enemy.PowerStates.Any(power =>
                power.PowerId == PrototypeNativeGloryBosses.AdaptableId))
        {
            return enemy with
            {
                Hp = 1,
                Block = 0,
                BossPendingRevival = true,
                AiStateId = "respawn",
                PlannedMoveIndex = 3,
                PlannedNextAiStateId = enemy.BossPhase == 0
                    ? "claw" : "lacerate"
            };
        }

        foreach (var power in enemy.PowerStates)
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (power.Stacks <= 0
                || definition.LastStandHp <= 0
                || definition.LastStandAiStateId is not { } nextState)
            {
                continue;
            }

            // A lethal-intercept script overrides the previously
            // announced action immediately. If its destination is a
            // deterministic move state, commit that move and its
            // continuation without consuming a new RNG draw.
            var moveState = PrototypeContent.Enemy(enemy.EnemyId)
                .Ai?.States.FirstOrDefault(state =>
                    StringComparer.Ordinal.Equals(state.Id, nextState)
                    && state.Kind == PrototypeEnemyAiStateKind.Move);
            return enemy with
            {
                Hp = definition.LastStandHp,
                Block = 0,
                LastStandTriggered = true,
                AiStateId = nextState,
                PlannedMoveIndex = moveState?.MoveIndex,
                PlannedNextAiStateId = moveState?.NextStateId
            };
        }

        return enemy;
    }

    /// <summary>
    /// Dispatch source-backed powers that react to a different ally's
    /// death. This runs for both powered attacks and raw HP loss (e.g.,
    /// poison), before removal of defeated creatures. Only a living
    /// owner may react, and the owner never reacts to its own death.
    /// </summary>
    private static CombatState ResolveAllyDeathPowers(
        CombatState combat, int defeatedEnemyId)
    {
        // PossessStrength/PossessSpeed store the actual successful
        // player stat loss, not the attempted debuff: Artifact can
        // prevent theft. Death returns precisely those stolen stacks,
        // once, before living allies process their death reactions.
        var defeated = combat.Enemies.FirstOrDefault(enemy =>
            enemy.InstanceId == defeatedEnemyId);
        if (defeated is not null
            && defeated.EnemyId == PrototypeNativeGloryBosses.AmalgamId)
        {
            combat = combat with
            {
                Enemies = combat.Enemies.Select(enemy =>
                    enemy.EnemyId == PrototypeNativeGloryBosses.QueenId
                    && enemy.Hp > 0
                    && enemy.PlannedMoveIndex == 2
                        ? enemy with
                        {
                            AiStateId = "enrage",
                            PlannedMoveIndex = 5,
                            PlannedNextAiStateId = "heads"
                        }
                        : enemy).ToArray()
            };
        }
        if (defeated is not null)
        {
            foreach (var stolenPower in defeated.PowerStates.Where(power =>
                power.StoredValue > 0
                && power.PowerId is
                    (PrototypeNativeGloryNewNormals.PossessStrengthId
                     or PrototypeNativeGloryNewNormals.PossessSpeedId)))
            {
                var restoredStat = stolenPower.PowerId ==
                    PrototypeNativeGloryNewNormals.PossessStrengthId
                    ? "proto.power.strength"
                    : "proto.power.dexterity";
                combat = ApplyPlayerPower(
                    combat, restoredStat, stolenPower.StoredValue);
            }
            combat = combat with
            {
                Enemies = combat.Enemies.Select(enemy =>
                    enemy.InstanceId == defeatedEnemyId
                        ? enemy with
                        {
                            Powers = enemy.PowerStates.Select(power =>
                                power.StoredValue > 0
                                && power.PowerId is
                                    (PrototypeNativeGloryNewNormals.PossessStrengthId
                                     or PrototypeNativeGloryNewNormals.PossessSpeedId)
                                    ? power with { StoredValue = 0 }
                                    : power).ToArray()
                        }
                        : enemy).ToArray()
            };
        }

        foreach (var owner in combat.Enemies
            .Where(item => item.Hp > 0
                && item.InstanceId != defeatedEnemyId)
            .OrderBy(item => item.InstanceId))
        {
            var reactions = owner.PowerStates
                .OrderBy(power => power.ApplicationOrder)
                .Where(power => power.Stacks > 0)
                .Select(power => (
                    Definition: PrototypeContent.Power(power.PowerId),
                    power.Stacks))
                .Where(pair =>
                    pair.Definition.AllyDeathStrengthPerStack > 0
                    || pair.Definition.AllyDeathBlockPerStack > 0
                    || pair.Definition.StunOnAllyDeath)
                .ToArray();
            foreach (var reaction in reactions)
            {
                if (reaction.Definition.AllyDeathStrengthPerStack > 0)
                {
                    combat = ApplyEnemyPower(
                        combat, owner.InstanceId, "proto.power.strength",
                        checked(reaction.Stacks
                            * reaction.Definition.AllyDeathStrengthPerStack));
                }
                if (reaction.Definition.AllyDeathBlockPerStack > 0
                    || reaction.Definition.ConsumeOnAllyDeath)
                {
                    var enemies = combat.Enemies
                        .Select(item => item.Fork()).ToArray();
                    var idx = Array.FindIndex(enemies, item =>
                        item.InstanceId == owner.InstanceId);
                    if (idx >= 0 && enemies[idx].Hp > 0)
                    {
                        enemies[idx] = enemies[idx] with
                        {
                            Block = enemies[idx].Block + checked(
                                reaction.Stacks *
                                reaction.Definition.AllyDeathBlockPerStack),
                            Powers = reaction.Definition.ConsumeOnAllyDeath
                                ? enemies[idx].PowerStates.Where(power =>
                                    power.PowerId != reaction.Definition.Id)
                                    .ToArray()
                                : enemies[idx].PowerStates
                        };
                        combat = combat with { Enemies = enemies };
                    }
                }
                if (reaction.Definition.StunOnAllyDeath)
                {
                    var enemies = combat.Enemies
                        .Select(item => item.Fork())
                        .ToArray();
                    var index = Array.FindIndex(enemies,
                        item => item.InstanceId == owner.InstanceId);
                    if (index < 0 || enemies[index].Hp <= 0)
                    {
                        continue;
                    }
                    // Native CreatureCmd.Stun replaces the pending move
                    // for one turn; repeated applications before that turn
                    // must not stack multiple skipped turns.
                    enemies[index] = enemies[index] with
                    {
                        EnemyActionSkipsRemaining =
                            Math.Max(1,
                                enemies[index].EnemyActionSkipsRemaining)
                    };
                    combat = combat with { Enemies = enemies };
                }
            }
        }
        return combat;
    }

    private sealed record PrototypeEnemyHpLossResult(
        EnemyCombatState Enemy,
        int HpLoss);

    private static PrototypeEnemyHpLossResult ModifyEnemyHpLoss(
        EnemyCombatState enemy,
        int requestedHpLoss,
        bool allowDamageTriggeredThreshold = true)
    {
        var hpLoss = Math.Max(0, requestedHpLoss);
        if (hpLoss == 0 || enemy.Hp <= 0 || enemy.BossPendingRevival)
        {
            return new PrototypeEnemyHpLossResult(
                enemy,
                enemy.BossPendingRevival ? 0 : hpLoss);
        }

        var powers = enemy.PowerStates.ToList();
        var capCandidate = powers
            .Where(power =>
                power.Stacks > 0
                && PrototypeContent.Power(power.PowerId)
                    .EnemyHpLossCapPerTrigger > 0)
            .OrderBy(power => power.ApplicationOrder)
            .FirstOrDefault();

        if (capCandidate is not null)
        {
            var definition = PrototypeContent.Power(
                capCandidate.PowerId);
            hpLoss = Math.Min(
                hpLoss,
                definition.EnemyHpLossCapPerTrigger);

            if (definition.ConsumeOnEnemyHpLoss)
            {
                var index = powers.FindIndex(power =>
                    power.ApplicationOrder
                        == capCandidate.ApplicationOrder);
                if (index < 0)
                {
                    throw new InvalidOperationException(
                        "Enemy HP-loss modifier disappeared before consumption.");
                }

                if (powers[index].Stacks <= 1)
                {
                    powers.RemoveAt(index);
                }
                else
                {
                    powers[index] = powers[index] with
                    {
                        Stacks = powers[index].Stacks - 1
                    };
                }
            }
        }

        var hpLossBudget = powers
            .Where(power => power.Stacks > 0
                && PrototypeContent.Power(power.PowerId)
                    .EnemyHpLossLimitedPerSideTurnByStacks)
            .Select(power => power.Stacks)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        if (hpLossBudget != int.MaxValue)
        {
            hpLoss = Math.Min(hpLoss,
                Math.Max(0, hpLossBudget - enemy.HpLossBudgetUsed));
            enemy = enemy with
            {
                HpLossBudgetUsed = enemy.HpLossBudgetUsed + hpLoss
            };
        }

        var nextHp = Math.Max(0, enemy.Hp - hpLoss);
        var thresholdCandidate = powers
            .Where(power =>
            {
                if (power.Stacks <= 0)
                {
                    return false;
                }

                var definition = PrototypeContent.Power(
                    power.PowerId);
                return allowDamageTriggeredThreshold
                    && definition.TriggerOwnerAtHpAtOrBelowStacks
                    && nextHp <= power.Stacks;
            })
            .OrderBy(power => power.ApplicationOrder)
            .FirstOrDefault();

        if (thresholdCandidate is not null)
        {
            var thresholdDefinition =
                PrototypeContent.Power(
                    thresholdCandidate.PowerId);
            powers.RemoveAll(power =>
                power.ApplicationOrder
                    == thresholdCandidate.ApplicationOrder);

            if (thresholdDefinition
                    .ClearOwnerStrengthOnHpThresholdTrigger)
            {
                powers.RemoveAll(power =>
                    StringComparer.Ordinal.Equals(
                        power.PowerId,
                        "proto.power.strength"));
            }

            var forcedStateId =
                thresholdDefinition.OwnerAiStateOnHpThresholdTrigger;
            var forcedAiState = forcedStateId is null
                ? null
                : PrototypeContent.Enemy(enemy.EnemyId)
                    .Ai?.States.FirstOrDefault(state =>
                        StringComparer.Ordinal.Equals(
                            state.Id, forcedStateId));
            // A player-triggered threshold may OVERRIDE an announced move,
            // e.g. the Ceremonial Beast is visibly stunned. This is a
            // deterministic interrupt, never a new hidden random roll.
            enemy = enemy with
            {
                AiStateId = forcedStateId ?? enemy.AiStateId,
                PlannedMoveIndex = forcedAiState is
                    { Kind: PrototypeEnemyAiStateKind.Move }
                    ? forcedAiState.MoveIndex : enemy.PlannedMoveIndex,
                PlannedNextAiStateId = forcedAiState is
                    { Kind: PrototypeEnemyAiStateKind.Move }
                    ? forcedAiState.NextStateId : enemy.PlannedNextAiStateId
            };
        }

        enemy = enemy with
        {
            Powers = powers.ToArray()
        };

        return new PrototypeEnemyHpLossResult(
            enemy,
            hpLoss);
    }

    private static CombatState MultiplyEnemyStatus(
        CombatState combat,
        int enemyId,
        string statusId,
        int multiplier)
    {
        _ = PrototypeContent.Status(statusId);
        if (multiplier < 0)
        {
            throw new InvalidOperationException("Status multiplier cannot be negative.");
        }

        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        var index = Array.FindIndex(enemies, enemy => enemy.InstanceId == enemyId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Enemy {enemyId} is missing.");
        }

        var enemy = enemies[index];
        if (enemy.Hp <= 0)
        {
            return combat;
        }

        var statuses = new Dictionary<string, int>(enemy.Statuses, StringComparer.Ordinal);
        var current = statuses.GetValueOrDefault(statusId);
        var next = current * multiplier;
        if (next > 0)
        {
            statuses[statusId] = next;
        }
        else
        {
            statuses.Remove(statusId);
        }

        enemies[index] = enemy with { Statuses = statuses };
        return combat with { Enemies = enemies };
    }

    private static CombatState ApplyEnemyStatus(
        CombatState combat,
        int enemyId,
        string statusId,
        int amount)
    {
        var statusDefinition =
            PrototypeContent.Status(statusId);
        var enemies = combat.Enemies
            .Select(enemy => enemy.Fork())
            .ToArray();
        var index = Array.FindIndex(
            enemies,
            enemy => enemy.InstanceId == enemyId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");
        }

        var enemy = enemies[index];
        if (enemy.Hp <= 0)
        {
            return combat;
        }

        if (amount > 0 && statusDefinition.IsDebuff)
        {
            var blocked =
                TryBlockIncomingEnemyDebuff(enemy);
            enemy = blocked.Enemy;
            if (blocked.Blocked)
            {
                enemies[index] = enemy;
                return combat with { Enemies = enemies };
            }
        }

        var statuses = new Dictionary<string, int>(
            enemy.Statuses,
            StringComparer.Ordinal);
        statuses[statusId] =
            statuses.GetValueOrDefault(statusId)
            + amount;
        enemies[index] = enemy with
        {
            Statuses = statuses
        };
        return combat with { Enemies = enemies };
    }

    private sealed record PrototypeEnemyDebuffBlockResult(
        EnemyCombatState Enemy,
        bool Blocked);

    private static PrototypeEnemyDebuffBlockResult
        TryBlockIncomingEnemyDebuff(
            EnemyCombatState enemy)
    {
        var candidate = enemy.PowerStates
            .Where(power =>
                power.Stacks > 0
                && PrototypeContent.Power(
                    power.PowerId)
                    .BlocksNextDebuff)
            .OrderBy(power => power.ApplicationOrder)
            .FirstOrDefault();
        if (candidate is null)
        {
            return new PrototypeEnemyDebuffBlockResult(
                enemy,
                false);
        }

        var powers = enemy.PowerStates
            .Select(power => power.Fork())
            .ToList();
        var index = powers.FindIndex(power =>
            power.ApplicationOrder
                == candidate.ApplicationOrder);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Enemy debuff-blocking power disappeared before consumption.");
        }

        if (powers[index].Stacks <= 1)
        {
            powers.RemoveAt(index);
        }
        else
        {
            powers[index] = powers[index] with
            {
                Stacks = powers[index].Stacks - 1
            };
        }

        return new PrototypeEnemyDebuffBlockResult(
            enemy with { Powers = powers.ToArray() },
            true);
    }

    private static CombatState CommitTopAutoplayDrawCheckpoint(
        CombatState combat,
        IReadOnlyList<long> drawPile,
        IReadOnlyList<long> discardPile,
        IReadOnlyCollection<long> newlyStagedCardInstanceIds,
        IReadOnlyList<DrawPileTopCardResolutionV111> topCardResolutions)
    {
        var nextZones = combat.Mechanics.CardZones.WithZoneChanges(
            new Dictionary<PrototypeCardZone, long[]>
            {
                [PrototypeCardZone.DrawPile] = drawPile.ToArray(),
                [PrototypeCardZone.DiscardPile] = discardPile.ToArray(),
                [PrototypeCardZone.PlayPile] = combat.PlayCardIds
                    .Concat(newlyStagedCardInstanceIds).ToArray()
            });
        return SemanticDrawPileAdapterV111.CommitTopCardResolutions(
            combat, topCardResolutions, nextZones);
    }

    private static PrototypeDrawCardsResult DrawCards(
        PlayerState player,
        CombatState combat,
        int count,
        RngBundle rng,
        bool fromHandDraw,
        int eventDepth = 0,
        PrototypeDrawCardsSuspensionPolicyV111? suspensionPolicy = null,
        bool automaticHandDrawStage = false)
    {
        var suspension = suspensionPolicy ??
            PrototypeDrawCardsSuspensionPolicyV111.None;
        var drawnCardInstanceIds = new List<long>();

        for (var drawNumber = 0; drawNumber < count; drawNumber++)
        {
            if (!fromHandDraw && combat.IsPlayerTurn
                && player.ActiveRelics.Any(relic =>
                    relic.RelicId == NativeGloryAncientsV111.FiddleId))
                break;

            if (!fromHandDraw
                && combat.PlayerPowers.Any(power =>
                    PrototypeContent.Power(power.PowerId)
                        .PreventsAdditionalDraw))
            {
                break;
            }

            if (!PrototypeContent.Rules.HasHandCapacity(
                    combat.Hand.Length))
            {
                break;
            }

            var draw = combat.DrawPile.ToList();
            var discard = combat.DiscardPile.ToList();

            if (draw.Count == 0)
            {
                if (discard.Count == 0)
                {
                    break;
                }

                if (combat.Stochastic.SemanticDeck is { } deferred)
                {
                    if (deferred.Mode == SemanticDeckStateV111.NatureMode)
                    {
                        if (!ControlledChanceGuardV111.Active ||
                            !suspension.SemanticDraw)
                            throw new NotSupportedException(
                                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.recycle " +
                                "operation=missing_semantic_continuation");
                    }
                    else if (deferred.Mode == SemanticDeckStateV111.NativeMode)
                    {
                        // Native mode commits one exact source-time
                        // Fisher-Yates permutation per recycle epoch. Later
                        // card draws reveal this order without a second
                        // per-card random choice.
                        combat = CommitNativeSemanticRecycle(
                            combat, "combat.draw.recycle");
                        deferred = combat.Stochastic.SemanticDeck!;
                        draw = combat.DrawPile.ToList();
                        discard = combat.DiscardPile.ToList();
                        combat = ApplyRelicAfterShuffleCards(combat, rng);
                        draw = combat.DrawPile.ToList();
                        discard = combat.DiscardPile.ToList();
                    }
                    else
                        throw new InvalidDataException(
                            "Unsupported provider-owned semantic deck mode.");

                    if (deferred.Mode == SemanticDeckStateV111.NatureMode)
                    {
                        // Nature preserves the shuffled-set constraints now
                        // and chooses each unknown physical identity only when
                        // DrawCards asks. Commit the zone transfer and epoch
                        // through the shared adapter boundary.
                        var recycled = discard.ToArray();
                        var nextPhysicalDrawPile = draw.Concat(recycled).ToArray();
                        combat = CommitNatureDrawPileRecycle(
                            combat, nextPhysicalDrawPile);
                        deferred = combat.Stochastic.SemanticDeck!;
                        draw = combat.DrawPile.ToList();
                        discard = combat.DiscardPile.ToList();
                        var afterShuffleInsertions =
                            ControlledAfterShuffleRelicInsertions(combat);
                        if (afterShuffleInsertions.Length > 0)
                        {
                            if (!ControlledChanceGuardV111.Active ||
                                !suspension.SemanticDraw ||
                                !suspension.AfterShuffleInsertion)
                                throw new NotSupportedException(
                                    "UNSUPPORTED_SEMANTIC_DECK " +
                                    "site=combat.draw.recycle " +
                                    "operation=missing_after_shuffle_continuation");
                            return CreateDrawCardsResult(
                                player, combat,
                                drawnCardInstanceIds.ToArray(),
                                PrototypeDrawCardsBoundaryV111
                                    .AfterShuffleInsertionPending(
                                        count - drawNumber,
                                        afterShuffleInsertions),
                                automaticHandDrawStage);
                        }
                    }
                    if (ExpectedStratagemSelectionCount(combat) > 0)
                    {
                        combat = SemanticDrawPileAdapterV111.ApplyZoneTransition(
                            combat,
                            combat.Mechanics.CardZones.WithZoneChanges(
                                new Dictionary<PrototypeCardZone, long[]>
                                {
                                    [PrototypeCardZone.DrawPile] = draw.ToArray(),
                                    [PrototypeCardZone.DiscardPile] = discard.ToArray()
                                }));
                        return CreateDrawCardsResult(
                            player, combat,
                            drawnCardInstanceIds.ToArray(),
                            PrototypeDrawCardsBoundaryV111
                                .ShuffleSelection(count - drawNumber),
                            automaticHandDrawStage);
                    }
                }
                else
                {
                    if (ControlledChanceGuardV111.Active &&
                        !suspension.ControlledRecycle)
                        throw new NotSupportedException(
                            "UNSUPPORTED_CONTROLLED_CHANCE site=combat.draw.recycle " +
                            "operation=shuffle_recycle outside resumable hand-draw stage");
                    if (ControlledChanceGuardV111.Active)
                        return CreateDrawCardsResult(
                            player, combat, drawnCardInstanceIds.ToArray(),
                            PrototypeDrawCardsBoundaryV111
                                .ControlledRecycle(count - drawNumber),
                            automaticHandDrawStage);

                    combat = CommitLegacyDrawPileRecycle(
                        combat, rng);
                    draw = combat.DrawPile.ToList();
                    discard = combat.DiscardPile.ToList();
                    combat = ApplyRelicAfterShuffleCards(combat, rng);
                    draw = combat.DrawPile.ToList();
                    discard = combat.DiscardPile.ToList();
                    if (ExpectedStratagemSelectionCount(combat) > 0)
                    {
                        // Persist the recycled draw pile before prompting.
                        // The suspended draw resumes after this choice.
                        combat = SemanticDrawPileAdapterV111.ApplyZoneTransition(
                            combat,
                            combat.Mechanics.CardZones.WithZoneChanges(
                                new Dictionary<PrototypeCardZone, long[]>
                                {
                                    [PrototypeCardZone.DrawPile] = draw.ToArray(),
                                    [PrototypeCardZone.DiscardPile] = discard.ToArray()
                                }));
                        return CreateDrawCardsResult(
                            player, combat, drawnCardInstanceIds.ToArray(),
                            PrototypeDrawCardsBoundaryV111
                                .ShuffleSelection(count - drawNumber),
                            automaticHandDrawStage);
                    }
                }
            }

            var semanticDeck = combat.Stochastic.SemanticDeck;
            if (semanticDeck is
                    { Mode: SemanticDeckStateV111.NatureMode } &&
                (!ControlledChanceGuardV111.Active ||
                 !suspension.SemanticDraw))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.draw " +
                    "operation=missing_semantic_continuation");
            if (semanticDeck is
                    { Mode: SemanticDeckStateV111.NativeMode } &&
                combat.Stochastic.NativeRng is null)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_NATIVE site=combat.draw " +
                    "operation=missing_native_stream_owner");

            var resolvedTop = SemanticDrawPileAdapterV111
                .ResolveTopCard(semanticDeck, draw);
            if (resolvedTop.IsPending)
                return CreateDrawCardsResult(
                    player,
                    SemanticDrawPileAdapterV111.CommitTopCardResolutions(
                        combat, [resolvedTop], combat.Mechanics.CardZones),
                    drawnCardInstanceIds.ToArray(),
                    PrototypeDrawCardsBoundaryV111
                        .SemanticDraw(count - drawNumber),
                    automaticHandDrawStage);

            var cardInstanceId = resolvedTop.CardInstanceId!.Value;
            draw = resolvedTop.RemainingPhysicalDrawPile.ToList();
            var nextZones = combat.Mechanics.CardZones.WithZoneChanges(
                new Dictionary<PrototypeCardZone, long[]>
                {
                    [PrototypeCardZone.DrawPile] = draw.ToArray(),
                    [PrototypeCardZone.Hand] = combat.Hand
                        .Append(cardInstanceId).ToArray(),
                    [PrototypeCardZone.DiscardPile] = discard.ToArray()
                });
            combat = SemanticDrawPileAdapterV111.CommitTopCardResolutions(
                combat, [resolvedTop], nextZones);
            drawnCardInstanceIds.Add(cardInstanceId);

            var card = RequireCombatCard(combat, cardInstanceId);
            var randomizedCostUpperBound =
                player.ActiveRelics
                    .Select(relic =>
                        PrototypeContent.Relic(relic.RelicId)
                            .RandomizeDrawnCardCostMaxExclusive)
                    .DefaultIfEmpty(0)
                    .Max();
            var cardDefinition =
                PrototypeContent.Card(card.CardId);
            var needsRelicCost = randomizedCostUpperBound > 0
                && cardDefinition.Cost.Kind == PrototypeCardCostKind.Fixed
                && cardDefinition.Cost.Amount >= 0;
            var hasSlither = card.Enchantment is
                { Kind: PrototypeCardEnchantmentKind.Slither };
            if (combat.Stochastic.SemanticDeck is
                    { Mode: SemanticDeckStateV111.NatureMode } &&
                     (needsRelicCost || hasSlither))
            {
                if (!suspension.SemanticDraw)
                    throw new NotSupportedException(
                        "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.cost " +
                        "operation=missing_semantic_cost_continuation");
                combat = MakeControlledDrawCostChoice(
                    combat, cardInstanceId, count - drawNumber - 1,
                    fromHandDraw, eventDepth, needsRelicCost
                        ? randomizedCostUpperBound : 0, hasSlither);
                return CreateDrawCardsResult(
                    player, combat, drawnCardInstanceIds.ToArray(),
                    PrototypeDrawCardsBoundaryV111
                        .ControlledDrawCost(count - drawNumber - 1),
                    automaticHandDrawStage);
            }
            if (ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
                (needsRelicCost || hasSlither))
            {
                if (!suspension.ControlledDrawCost)
                    throw new NotSupportedException(
                        "UNSUPPORTED_CONTROLLED_CHANCE site=combat.draw_cost." +
                        "effect_draw operation=draw_card_cost");
                combat = MakeControlledDrawCostChoice(
                    combat, cardInstanceId, count - drawNumber - 1,
                    fromHandDraw, eventDepth, needsRelicCost
                        ? randomizedCostUpperBound : 0, hasSlither);
                return CreateDrawCardsResult(
                    player, combat, drawnCardInstanceIds.ToArray(),
                    PrototypeDrawCardsBoundaryV111
                        .ControlledDrawCost(count - drawNumber - 1),
                    automaticHandDrawStage);
            }
            if (needsRelicCost)
            {
                combat = ApplyDrawCardCost(
                    combat, rng, cardInstanceId, card.CardId, "relic",
                    randomizedCostUpperBound);
                card = RequireCombatCard(
                    combat,
                    cardInstanceId);
            }

            if (card.Enchantment is
                    { Kind: PrototypeCardEnchantmentKind.Slither })
            {
                // Slither rerolls this combat's cost every time its
                // card is drawn, including after pile recycling.
                combat = ApplyDrawCardCost(
                    combat, rng, cardInstanceId, card.CardId, "slither", 4);
                card = RequireCombatCard(combat, cardInstanceId);
            }

            var dispatched = DispatchCardDrawnAfterCost(
                player, combat, cardInstanceId, fromHandDraw, rng,
                eventDepth, allowSuspension: suspension.CardDrawnChoice);
            player = dispatched.Player;
            combat = dispatched.Combat;
            if (suspension.CardDrawnChoice &&
                combat.PendingChoice is not null)
                return CreateDrawCardsResult(
                    player, combat, drawnCardInstanceIds.ToArray(),
                    PrototypeDrawCardsBoundaryV111
                        .NestedCardDrawnChoice(count - drawNumber - 1),
                    automaticHandDrawStage);
            if (combat.Stochastic.SemanticDeck is not null &&
                (combat.PendingChoice is not null ||
                 combat.PendingKnowledgeDemonChoice is not null))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                    "operation=nested_choice_continuation");

            // CardDrawn mechanics may update either physical pile. Continue
            // from the committed zone owner rather than retaining a second
            // pre-hook copy in these local working lists.
            SemanticDrawPileAdapterV111.ValidateSynchronized(
                combat, requireIdle: true);
            draw = combat.DrawPile.ToList();
            discard = combat.DiscardPile.ToList();
        }

        return CreateDrawCardsResult(
            player,
            combat,
            drawnCardInstanceIds.ToArray(),
            PrototypeDrawCardsBoundaryV111.Complete(),
            automaticHandDrawStage);
    }

    private static void ValidateTarget(
        PrototypeCardTarget targetKind,
        int? targetEnemyId,
        CombatState combat)
    {
        if (targetKind == PrototypeCardTarget.None)
        {
            if (targetEnemyId is not null)
            {
                throw new InvalidOperationException("This action does not accept an enemy target.");
            }

            return;
        }

        if (targetEnemyId is null)
        {
            throw new InvalidOperationException("This action requires an enemy target.");
        }

        if (!combat.Enemies.Any(
            enemy => enemy.InstanceId == targetEnemyId.Value && enemy.Hp > 0))
        {
            throw new InvalidOperationException($"Enemy {targetEnemyId.Value} is not a live target.");
        }
    }

    // MinionPower identifies secondary enemies. A combat with primary
    // enemies is won when they fall even if a summoned egg or illusion
    // still survives; standalone minion fixtures require all-dead.
    private static CombatState ResolveKaiserCrabFacing(
        CombatState combat, int? targetEnemyId)
    {
        if (targetEnemyId is null)
            return combat;
        var target = combat.Enemies.FirstOrDefault(enemy =>
            enemy.InstanceId == targetEnemyId.Value && enemy.Hp > 0);
        if (target is null)
            return combat;

        // Facing defaults Right. Targeting the left arm turns Left,
        // targeting the right arm turns Right, including non-attacks.
        var direction = target.PowerStates.Any(power =>
            power.PowerId == PrototypeNativeHiveBosses.BackLeftId) ? 1
            : target.PowerStates.Any(power =>
                power.PowerId == PrototypeNativeHiveBosses.BackRightId) ? 0
            : -1;
        if (direction < 0)
            return combat;
        return combat with
        {
            PlayerPowers = combat.PlayerPowers.Select(power =>
                power.PowerId == PrototypeNativeHiveBosses.SurroundedId
                    ? power with { StoredValue = direction } : power)
                .ToArray()
        };
    }

    private static bool AllEnemiesDefeated(CombatState combat)
    {
        var primary = combat.Enemies.Where(enemy =>
            !PrototypeContent.Enemy(enemy.EnemyId).IsMinion).ToArray();
        return primary.Length > 0
            ? primary.All(enemy => enemy.Hp <= 0)
            : combat.Enemies.All(enemy => enemy.Hp <= 0);
    }
}

using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledInitialHpV111Tests
{
    private const string Encounter = "MytesNormal";
    private const string EnemyId = PrototypeNativeHiveNormals.MytesNormalId;

    private static (CombatState Bare, NativeEncounterFormationV111 Formation)
        Input(string seed)
    {
        var rolled = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
            seed, 2, EnemyId, Encounter, 21,
            mapColumn: 3, mapRow: 5, commitInitialIntents: false);
        var formation = rolled.NativeRng!.Formation.Fork();
        var bare = rolled with
        {
            NativeRng = null,
            Enemies = rolled.Enemies.Select(enemy =>
            {
                var range = PrototypeContent.Enemy(enemy.EnemyId)
                    .HpRangeAt(rolled.Act, rolled.Ascension);
                return enemy with { Hp = range.Max, MaxHp = range.Max };
            }).ToArray()
        };
        return (bare, formation);
    }

    [Fact]
    public void NatureChoicesResumeExactMechanicsWithoutReplayingEarlierMonsters()
    {
        for (var iteration = 0; iteration < 36; iteration++)
        {
            var seed = $"nature-hp-continuation-{iteration}";
            var (initial, formation) = Input(seed);
            var native = NativeCombatRngEntryV111.AttachAndRollInitialHpWithAudit(
                initial, formation, NativeRngV111.CreateBundle(seed));
            var originalCombatHash = CanonicalJson.Sha256(initial);
            var originalFormationHash = CanonicalJson.Sha256(formation);
            var nativeWithoutCursor = native.Combat with { NativeRng = null };
            var state = ControlledInitialHpV111.Begin(initial, formation);
            Assert.Equal(0, state.NextEnemyIndex);
            Assert.NotNull(state.Pending);
            Assert.Equal("controlled-v111", state.ProviderMode);

            for (int index = 0; index < native.Choices.Length; index++)
            {
                var expected = native.Choices[index];
                var pending = Assert.IsType<PendingInitialHpChoiceV111>(state.Pending);
                Assert.Equal(expected.Request.SiteId, pending.Request.SiteId);
                Assert.Equal(expected.Request.StreamId, pending.Request.StreamId);
                Assert.Equal(expected.Request.OwnerId, pending.Request.OwnerId);
                Assert.Equal(expected.Request.OrderedCandidates,
                    pending.Request.OrderedCandidates);

                var beforeHash = CanonicalJson.Sha256(state);
                var snapshot = ControlledInitialHpV111.Save(state);
                var loaded = ControlledInitialHpV111.Restore(snapshot);
                Assert.Equal(beforeHash, CanonicalJson.Sha256(loaded));
                Assert.Equal(pending.BoundaryId, loaded.Pending!.BoundaryId);

                var previous = state;
                state = ControlledInitialHpV111.Resolve(
                    loaded, pending.BoundaryId, expected.SelectedIndex);
                Assert.Equal(beforeHash, CanonicalJson.Sha256(previous));
                Assert.Equal(index + 1, state.NextEnemyIndex);
                Assert.Equal(index + 1, state.Transcript.Length);
                Assert.Equal(expected.SelectedValue, state.Combat.Enemies[index].Hp);
                Assert.Equal(expected.SelectedValue, state.Combat.Enemies[index].MaxHp);
                Assert.Null(state.Combat.NativeRng);
                Assert.Null(state.Transcript[index].NativeCursorBefore);
                Assert.Null(state.Transcript[index].NativeCursorAfter);
                Assert.Equal("controlled-v111", state.Transcript[index].ProviderMode);
            }

            Assert.True(state.Complete);
            Assert.Null(state.Pending);
            Assert.Equal(CanonicalJson.Sha256(nativeWithoutCursor),
                CanonicalJson.Sha256(state.Combat));
            Assert.Equal(originalCombatHash, CanonicalJson.Sha256(initial));
            Assert.Equal(originalFormationHash, CanonicalJson.Sha256(formation));
            Assert.Throws<InvalidOperationException>(() =>
                ControlledInitialHpV111.Resolve(state, "already-used", 0));
        }
    }

    [Fact]
    public void ForkedNatureChoicesAreIsolatedAndChangeLaterEligibility()
    {
        var (initial, formation) = Input("nature-forked-hp-choice");
        var root = ControlledInitialHpV111.Begin(initial, formation);
        var first = Assert.IsType<PendingInitialHpChoiceV111>(root.Pending);
        Assert.True(first.Request.OrderedCandidates.Length > 1);
        var rootHash = CanonicalJson.Sha256(root);
        var left = ControlledInitialHpV111.Resolve(root, first.BoundaryId, 0);
        var right = ControlledInitialHpV111.Resolve(root.Fork(), first.BoundaryId, 1);

        Assert.Equal(rootHash, CanonicalJson.Sha256(root));
        Assert.NotEqual(CanonicalJson.Sha256(left), CanonicalJson.Sha256(right));
        Assert.NotEqual(left.Combat.Enemies[0].Hp, right.Combat.Enemies[0].Hp);
        Assert.NotEqual(left.Pending!.BoundaryId, right.Pending!.BoundaryId);
        // The next candidate pool is computed from the already applied HP,
        // not from a fixed pre-enumerated random draw sequence.
        Assert.DoesNotContain(left.Combat.Enemies[0].Hp,
            left.Pending.Request.OrderedCandidates);
        Assert.DoesNotContain(right.Combat.Enemies[0].Hp,
            right.Pending.Request.OrderedCandidates);

        var leftRestored = ControlledInitialHpV111.Restore(
            ControlledInitialHpV111.Save(left));
        var rightRestored = ControlledInitialHpV111.Restore(
            ControlledInitialHpV111.Save(right));
        var leftLast = ControlledInitialHpV111.Resolve(
            leftRestored, leftRestored.Pending!.BoundaryId, 0);
        var rightLast = ControlledInitialHpV111.Resolve(
            rightRestored, rightRestored.Pending!.BoundaryId, 0);
        Assert.True(leftLast.Complete);
        Assert.True(rightLast.Complete);
        Assert.NotEqual(CanonicalJson.Sha256(leftLast.Combat),
            CanonicalJson.Sha256(rightLast.Combat));
    }

    [Fact]
    public void InvalidStaleOrTamperedNatureChoicesFailWithoutPartialMutation()
    {
        var (initial, formation) = Input("nature-invalid-resume");
        var state = ControlledInitialHpV111.Begin(initial, formation);
        var pending = state.Pending!;
        var unchanged = CanonicalJson.Sha256(state);

        Assert.Throws<ArgumentException>(() =>
            ControlledInitialHpV111.Resolve(state, pending.BoundaryId, -1));
        Assert.Throws<ArgumentException>(() =>
            ControlledInitialHpV111.Resolve(
                state, pending.BoundaryId, pending.Request.SupportCount));
        Assert.Throws<InvalidOperationException>(() =>
            ControlledInitialHpV111.Resolve(state, "wrong-boundary", 0));
        Assert.Equal(unchanged, CanonicalJson.Sha256(state));

        var next = ControlledInitialHpV111.Resolve(
            state, pending.BoundaryId, 0);
        Assert.Throws<InvalidOperationException>(() =>
            ControlledInitialHpV111.Resolve(next, pending.BoundaryId, 0));
        var tampered = next with
        {
            Pending = next.Pending! with
            {
                Request = next.Pending.Request with
                {
                    OrderedCandidates = [999]
                }
            }
        };
        Assert.Throws<InvalidDataException>(() =>
            ControlledInitialHpV111.Validate(tampered));
        Assert.Throws<InvalidDataException>(() =>
            ControlledInitialHpV111.Save(tampered));
        Assert.Equal(unchanged, CanonicalJson.Sha256(state));

        var wrongOwner = next with { Pending = next.Pending! with
        {
            Request = next.Pending.Request with { OwnerId = "creature:wrong" }
        } };
        Assert.Throws<InvalidDataException>(() =>
            ControlledInitialHpV111.Validate(wrongOwner));
    }

    [Fact]
    public void SourceOwnerSwapAndTranscriptTamperingAreRejected()
    {
        var (initial, formation) = Input("nature-ownership-check");
        var invalid = formation.Fork();
        invalid.MonsterContexts[1] = invalid.MonsterContexts[1] with
        {
            CombatId = invalid.MonsterContexts[0].CombatId
        };
        Assert.Throws<InvalidDataException>(() =>
            ControlledInitialHpV111.Begin(initial, invalid));

        var start = ControlledInitialHpV111.Begin(initial, formation);
        var next = ControlledInitialHpV111.Resolve(
            start, start.Pending!.BoundaryId, 0);
        var altered = next.Fork();
        altered.Transcript[0] = altered.Transcript[0] with
        {
            SelectedIndex = (altered.Transcript[0].SelectedIndex + 1)
                % altered.Transcript[0].Request.SupportCount
        };
        Assert.Throws<InvalidDataException>(() =>
            ControlledInitialHpV111.Validate(altered));
        Assert.Equal(0, start.NextEnemyIndex);
        ControlledInitialHpV111.Validate(start);
        ControlledInitialHpV111.Validate(next);
    }

    [Fact]
    public void ControlledContinuationDoesNotAcceptAlreadyNativeAttachedCombat()
    {
        var (initial, formation) = Input("native-owner-cannot-be-nature");
        var native = NativeCombatRngEntryV111.AttachAndRollInitialHp(
            initial, formation, NativeRngV111.CreateBundle(
                "native-owner-cannot-be-nature"));
        Assert.Throws<InvalidOperationException>(() =>
            ControlledInitialHpV111.Begin(native, formation));
    }
}

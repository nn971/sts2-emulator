using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class CanonicalJsonTests
{
    [Fact]
    public void ObjectPropertyOrderDoesNotChangeCanonicalHash()
    {
        using var leftDoc = JsonDocument.Parse("{\"b\":2,\"a\":1}");
        using var rightDoc = JsonDocument.Parse("{\"a\":1,\"b\":2}");

        Assert.Equal(
            CanonicalJson.Sha256(leftDoc.RootElement),
            CanonicalJson.Sha256(rightDoc.RootElement));
    }

    [Fact]
    public void ArrayOrderChangesCanonicalHash()
    {
        using var leftDoc = JsonDocument.Parse("[1,2]");
        using var rightDoc = JsonDocument.Parse("[2,1]");

        Assert.NotEqual(
            CanonicalJson.Sha256(leftDoc.RootElement),
            CanonicalJson.Sha256(rightDoc.RootElement));
    }
    [Fact]
    public void ReadOnlyRunStateHashReuseIsScopedAndReferenceBased()
    {
        var pilot = SemanticPilotCombatEngineV111.BeginSeeded(
            new SemanticSeededEntryV111(
                "h7-readonly-sha-memo-test",
                1, "proto.encounter.mawler_normal", "MawlerNormal",
                2, 10, 1, 3, 1, ReplayOpeningDrawHooks: true));
        var run = pilot.Run;
        var expected = CanonicalJson.Sha256(run);
        var hits = 0;
        using (SemanticDrawStageProfilerV111.Begin(
            (name, _, _) =>
            {
                if (name == "sub.semantic_draw_hash_RunState_cache_hit")
                    hits++;
            }))
        {
            using (CanonicalJson.BeginReadOnlyRunStateHashReuse())
            {
                Assert.Equal(expected, CanonicalJson.Sha256(run));
                using (CanonicalJson.BeginReadOnlyRunStateHashReuse())
                {
                    Assert.Equal(expected, CanonicalJson.Sha256(run));
                }
                Assert.Equal(expected, CanonicalJson.Sha256(run));
                var next = run with { DecisionIndex = run.DecisionIndex + 1 };
                Assert.NotEqual(expected, CanonicalJson.Sha256(next));
            }
            // Disposing the scope recomputes the digest from the run.
            Assert.Equal(expected, CanonicalJson.Sha256(run));
        }
        Assert.Equal(2, hits);
    }

    [Fact]
    public void ReadOnlyRunStateHashReuseIsReleasedAfterExceptions()
    {
        var pilot = SemanticPilotCombatEngineV111.BeginSeeded(
            new SemanticSeededEntryV111(
                "h7-readonly-sha-throw-test",
                1, "proto.encounter.mawler_normal", "MawlerNormal",
                2, 10, 1, 3, 1, ReplayOpeningDrawHooks: true));
        var source = pilot.Run;
        var original = CanonicalJson.Sha256(source);
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using var scope = CanonicalJson.BeginReadOnlyRunStateHashReuse();
            Assert.Equal(original, CanonicalJson.Sha256(source));
            throw new InvalidOperationException("test");
        }));
        Assert.Equal(original, CanonicalJson.Sha256(source));
        pilot.Validate();
    }


    [Theory]
    [InlineData("{\"z\":[1,{\"c\":null,\"a\":true}],\"a\":\"中文 🌟\"}")]
    [InlineData("[3.14,1e-7,0,-2,false,null,\"slash\\\\quote\\\\\"]")]
    [InlineData("{\"nested\":{\"b\":2,\"a\":[{\"y\":1,\"x\":2}]}}")]
    public void DirectUtf8HashMatchesPreviousStringRoundTrip(string source)
    {
        using var json = JsonDocument.Parse(source);
        var canonical = CanonicalJson.Serialize(json.RootElement);
        var priorHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
        Assert.Equal(priorHash, CanonicalJson.Sha256(json.RootElement));
    }

    [Fact]
    public void DirectUtf8HashMatchesFullRunAndPilotSession()
    {
        var pilot = SemanticPilotCombatEngineV111.BeginSeeded(
            new SemanticSeededEntryV111(
                "h7-direct-utf8-hash-test",
                1, "proto.encounter.mawler_normal", "MawlerNormal",
                2, 10, 1, 3, 1, ReplayOpeningDrawHooks: true));
        AssertLegacyHashMatches(pilot.Run);
        AssertLegacyHashMatches(pilot);
    }

    [Fact]
    public void PilotFingerprintReuseIsScopedAndReferenceBased()
    {
        var pilot = SemanticPilotCombatEngineV111.BeginSeeded(
            new SemanticSeededEntryV111(
                "h7-pilot-sha-memo-test",
                1, "proto.encounter.mawler_normal", "MawlerNormal",
                2, 10, 1, 3, 1, ReplayOpeningDrawHooks: true));
        var expected = CanonicalJson.Sha256(pilot);
        var hits = 0;
        using (SemanticDrawStageProfilerV111.Begin(
            (name, _, _) =>
            {
                if (name == "sub.semantic_draw_hash_SemanticPilotCombatV111_cache_hit")
                    hits++;
            }))
        {
            using (CanonicalJson.BeginReadOnlyRunStateHashReuse())
            {
                Assert.Equal(expected, CanonicalJson.Sha256(pilot));
                Assert.Equal(expected, CanonicalJson.Sha256(pilot));
                // Record 'with' creates a distinct snapshot reference.
                var other = pilot with { };
                Assert.Equal(expected, CanonicalJson.Sha256(other));
                Assert.Equal(expected, CanonicalJson.Sha256(pilot));
            }
            Assert.Equal(expected, CanonicalJson.Sha256(pilot));
        }
        Assert.Equal(2, hits);
    }

    private static void AssertLegacyHashMatches<T>(T value)
    {
        var canonical = CanonicalJson.Serialize(value);
        var original = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
        Assert.Equal(original, CanonicalJson.Sha256(value));
    }
}

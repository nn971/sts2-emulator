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
    public void Sha256MatchesHashOfCanonicalUtf8Json()
    {
        using var doc = JsonDocument.Parse(
            "{\"z\":[{\"ñ\":\"汉字😀\",\"b\":-12.75},null,true],\"a\":\"line\\n\\u003c\"}");
        var canonical = CanonicalJson.Serialize(doc.RootElement);
        var expected = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        Assert.Equal(expected, CanonicalJson.Sha256(doc.RootElement));
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

}

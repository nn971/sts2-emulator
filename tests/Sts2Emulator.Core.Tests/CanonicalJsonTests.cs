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
}

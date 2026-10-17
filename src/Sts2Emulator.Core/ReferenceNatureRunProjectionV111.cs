namespace Sts2Emulator.Core;

/// <summary>Run projection shared by ReferenceNature boundaries that must not
/// retain the Native order of persistent relic queues.</summary>
internal static class ReferenceNatureRunProjectionV111
{
    public static RunState CreateNatureReplayOrigin(
        RunState source, bool createRelicBagWhenMissing = false,
        bool allowMissingRelicBag = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        var world = source.World ?? throw new InvalidOperationException(
            "ReferenceNature run projection requires an active world.");
        var bag = world.RelicBag;
        if (bag is null)
        {
            if (allowMissingRelicBag)
                return source;
            if (!createRelicBagWhenMissing)
                throw new NotSupportedException(
                    "ReferenceNature replay requires a committed relic bag.");
            bag = PrototypeNativeRelicGrabBag.CreateNatureUnordered(
                source.Player);
        }

        static string[] Canonical(string[] ids) => ids
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();

        var projectedBag = new PrototypeRelicBagState(
            Canonical(bag.Common), Canonical(bag.Uncommon),
            Canonical(bag.Rare), Canonical(bag.Shop))
        {
            OrderMode = PrototypeRelicBagOrderMode.NatureUnordered
        };
        return source with
        {
            World = world with { RelicBag = projectedBag }
        };
    }
}

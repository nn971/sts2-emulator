namespace Sts2Emulator.Core;

/// <summary>Run projection shared by ReferenceNature boundaries that must not
/// retain the Native order of persistent relic queues.</summary>
internal static class ReferenceNatureRunProjectionV111
{
    public static void ValidateNatureRelicBagProjection(
        RunState run, string boundary, bool allowMissing = false)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundary);
        if (run.World is not { } world)
            throw new InvalidDataException(
                $"{boundary} Nature relic projection requires an active world.");
        if (world.Stochastic.RelicBag is not { } bag)
        {
            if (allowMissing)
                return;
            throw new InvalidDataException(
                $"{boundary} Nature relic projection requires a relic bag.");
        }
        if (bag.OrderMode != PrototypeRelicBagOrderMode.NatureUnordered)
            throw new InvalidDataException(
                $"{boundary} Nature relic projection must omit Native queue order.");
        try
        {
            PrototypeNativeRelicGrabBag.Validate(bag, run.Player);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException(
                $"{boundary} Nature relic projection is not canonical.",
                exception);
        }
    }

    public static RunState CreateNatureReplayOrigin(
        RunState source, bool createRelicBagWhenMissing = false,
        bool allowMissingRelicBag = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        var world = source.World ?? throw new InvalidOperationException(
            "ReferenceNature run projection requires an active world.");
        var bag = world.Stochastic.RelicBag;
        if (bag is null)
        {
            if (allowMissingRelicBag)
                return source;
            if (!createRelicBagWhenMissing)
                throw new NotSupportedException(
                    "ReferenceNature replay requires a committed relic bag.");
        }
        var projectedBag = PrototypeNativeRelicGrabBag
            .CreateNatureUnordered(source.Player, bag);
        return source with
        {
            World = world with
            {
                Stochastic = world.Stochastic with
                {
                    RelicBag = projectedBag
                }
            }
        };
    }
}

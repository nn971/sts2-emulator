namespace Sts2ReferenceMod.Capture;

/// <summary>
/// Stable scenario-local identity for semantic source keys. Call Retire when a native
/// instance is destroyed, before its host key can be reused for a different object.
/// </summary>
public sealed class EntityIdentityRegistry
{
    private readonly Dictionary<(string Kind, string HostKey), long> _alive = new();
    private readonly Dictionary<string, long> _nextByKind = new(StringComparer.Ordinal);

    public string Resolve(string kind, string hostKey)
    {
        if (!_alive.TryGetValue((kind, hostKey), out var id))
        {
            _nextByKind.TryGetValue(kind, out var last);
            id = last + 1;
            _nextByKind[kind] = id;
            _alive.Add((kind, hostKey), id);
        }
        return kind + ":" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Retire(string kind, string hostKey) => _alive.Remove((kind, hostKey));

    public void NewScenario()
    {
        _alive.Clear();
        _nextByKind.Clear();
    }
}

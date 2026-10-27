namespace Core;

// A pretend copy of the drawing that the checks change as they go,
// so each step is checked against the drawing as it will be after the steps before it.
public class DrawingState
{
    public HashSet<string> Layers { get; }
    public HashSet<string> Frozen { get; }
    public HashSet<string> Linetypes { get; }
    public string? Current { get; set; }

    public DrawingState(Scan scan)
    {
        Layers = new HashSet<string>(scan.Layers, StringComparer.OrdinalIgnoreCase);
        Frozen = new HashSet<string>(scan.Frozen, StringComparer.OrdinalIgnoreCase);
        Linetypes = new HashSet<string>(scan.Linetypes, StringComparer.OrdinalIgnoreCase);
        Current = scan.Current;
    }

    public bool HasLayer(string name) => Layers.Contains(name);

    public bool IsCurrent(string name) =>
        Current is not null && string.Equals(Current, name, StringComparison.OrdinalIgnoreCase);

    public void RenameLayer(string from, string to)
    {
        Layers.Remove(from);
        Layers.Add(to);
        if (Frozen.Remove(from))
        {
            Frozen.Add(to);
        }
        if (IsCurrent(from))
        {
            Current = to;
        }
    }

    public void RemoveLayer(string name)
    {
        Layers.Remove(name);
        Frozen.Remove(name);
    }
}

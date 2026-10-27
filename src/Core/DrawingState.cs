namespace Core;

// A pretend copy of the drawing that the checks change as they go,
// so each step is checked against the drawing as it will be after the steps before it.
public class DrawingState
{
    public HashSet<string> Layers { get; }
    public HashSet<string> Frozen { get; }
    public HashSet<string> Linetypes { get; }
    public HashSet<string> Off { get; }
    public HashSet<string> Locked { get; }
    public HashSet<string> NoPlot { get; }
    public string? Current { get; set; }

    // Older scans don't record everything, so some notes can't be trusted
    public bool KnowsFrozen { get; }
    public bool KnowsOnOffLockPlot { get; }

    public DrawingState(Scan scan)
    {
        Layers = Copy(scan.Layers);
        Frozen = Copy(scan.Frozen);
        Linetypes = Copy(scan.Linetypes);
        Off = Copy(scan.Off);
        Locked = Copy(scan.Locked);
        NoPlot = Copy(scan.NoPlot);
        Current = scan.Current;
        KnowsFrozen = scan.Version >= 2;
        KnowsOnOffLockPlot = scan.Version >= 3;
    }

    // A copy of another pretend drawing (used to try a step before adding it)
    public DrawingState(DrawingState other)
    {
        Layers = Copy(other.Layers);
        Frozen = Copy(other.Frozen);
        Linetypes = Copy(other.Linetypes);
        Off = Copy(other.Off);
        Locked = Copy(other.Locked);
        NoPlot = Copy(other.NoPlot);
        Current = other.Current;
        KnowsFrozen = other.KnowsFrozen;
        KnowsOnOffLockPlot = other.KnowsOnOffLockPlot;
    }

    private static HashSet<string> Copy(IEnumerable<string> items) =>
        new HashSet<string>(items, StringComparer.OrdinalIgnoreCase);

    public bool HasLayer(string name) => Layers.Contains(name);

    public bool IsCurrent(string name) =>
        Current is not null && string.Equals(Current, name, StringComparison.OrdinalIgnoreCase);

    public void RenameLayer(string from, string to)
    {
        Layers.Remove(from);
        Layers.Add(to);
        foreach (var set in new[] { Frozen, Off, Locked, NoPlot })
        {
            if (set.Remove(from))
            {
                set.Add(to);
            }
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
        Off.Remove(name);
        Locked.Remove(name);
        NoPlot.Remove(name);
    }
}

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

    // From version 4 scans: objects per layer, linetype scale settings, text styles, text
    public Dictionary<string, int> Counts { get; }
    public LinetypeSettings Linetype { get; set; }
    public Dictionary<string, TextStyleInfo> TextStyles { get; }
    public List<TextItem> Texts { get; }

    // Older scans don't record everything, so some notes can't be trusted
    public int Version { get; }
    public bool KnowsFrozen => Version >= 2;
    public bool KnowsOnOffLockPlot => Version >= 3;
    public bool KnowsContents => Version >= 4;

    public DrawingState(Scan scan)
    {
        Layers = Copy(scan.Layers);
        Frozen = Copy(scan.Frozen);
        Linetypes = Copy(scan.Linetypes);
        Off = Copy(scan.Off);
        Locked = Copy(scan.Locked);
        NoPlot = Copy(scan.NoPlot);
        Current = scan.Current;
        Counts = new(scan.Counts, StringComparer.OrdinalIgnoreCase);
        Linetype = scan.Linetype;
        TextStyles = new(scan.TextStyles, StringComparer.OrdinalIgnoreCase);
        Texts = new(scan.Texts);
        Version = scan.Version;
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
        Counts = new(other.Counts, StringComparer.OrdinalIgnoreCase);
        Linetype = other.Linetype;
        TextStyles = new(other.TextStyles, StringComparer.OrdinalIgnoreCase);
        Texts = new(other.Texts);
        Version = other.Version;
    }

    private static HashSet<string> Copy(IEnumerable<string> items) =>
        new HashSet<string>(items, StringComparer.OrdinalIgnoreCase);

    public bool HasLayer(string name) => Layers.Contains(name);

    public bool IsCurrent(string name) =>
        Current is not null && string.Equals(Current, name, StringComparison.OrdinalIgnoreCase);

    // How many objects are on a layer (null if the scan didn't count them)
    public int? CountOf(string layer) =>
        KnowsContents ? Counts.GetValueOrDefault(layer) : null;

    public void AddLayer(string name)
    {
        Layers.Add(name);
        Counts[name] = 0;
    }

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
        if (Counts.Remove(from, out var count))
        {
            Counts[to] = count;
        }
        for (int i = 0; i < Texts.Count; i++)
        {
            if (Texts[i].Layer.Equals(from, StringComparison.OrdinalIgnoreCase))
            {
                Texts[i] = Texts[i] with { Layer = to };
            }
        }
    }

    public void RemoveLayer(string name)
    {
        Layers.Remove(name);
        Frozen.Remove(name);
        Off.Remove(name);
        Locked.Remove(name);
        NoPlot.Remove(name);
        Counts.Remove(name);
    }
}

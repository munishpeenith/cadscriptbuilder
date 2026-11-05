namespace Core;

// What layers.txt holds: the drawing's name and its layers, plus extra details from newer scans.
// Version 1 = layer names only, 2 = adds current/frozen/linetypes,
// 3 = adds which layers are off, locked and not plotting,
// 4 = adds object counts, linetype scale settings, text styles and the drawing's text.
// FromRun is true when the scan was written by a generated script, right after it ran.
public class Scan
{
    public string Drawing { get; init; } = "";
    public HashSet<string> Layers { get; init; } = NewSet();
    public string? Current { get; init; }
    public HashSet<string> Frozen { get; init; } = NewSet();
    public HashSet<string> Linetypes { get; init; } = NewSet();
    public HashSet<string> Off { get; init; } = NewSet();
    public HashSet<string> Locked { get; init; } = NewSet();
    public HashSet<string> NoPlot { get; init; } = NewSet();
    public int Version { get; init; } = 1;
    public bool FromRun { get; init; }

    // Version 4
    public Dictionary<string, int> Counts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public LinetypeSettings Linetype { get; init; } = new();
    public Dictionary<string, TextStyleInfo> TextStyles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<TextItem> Texts { get; init; } = new();

    public static HashSet<string> NewSet() => new(StringComparer.OrdinalIgnoreCase);
}

// The drawing-wide linetype scale settings (null when the scan didn't record them)
public record LinetypeSettings(double? LtScale = null, double? CeLtScale = null, int? PsLtScale = null, int? MsLtScale = null);

// A text style: its font file (e.g. arial.ttf or romans.shx) and fixed height (0 = set per text)
public record TextStyleInfo(string Font, double Height);

// One piece of text in the drawing: TEXT, MTEXT or MULTILEADER, found by its handle
public record TextItem(string Type, string Handle, string Layer, string Content);

namespace Core;

// What layers.txt holds: the drawing's name and its layers, plus extra details
// from newer scan files. Version 1 = layer names only, 2 = adds current/frozen/linetypes,
// 3 = also adds which layers are off, locked and not plotting.
public record Scan(
    string Drawing,
    HashSet<string> Layers,
    string? Current,
    HashSet<string> Frozen,
    HashSet<string> Linetypes,
    HashSet<string> Off,
    HashSet<string> Locked,
    HashSet<string> NoPlot,
    int Version);

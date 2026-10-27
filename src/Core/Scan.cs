namespace Core;

// What layers.txt holds: the drawing's name, its layers, and (from scan v2)
// the current layer, which layers are frozen, and which linetypes are loaded
public record Scan(
    string Drawing,
    HashSet<string> Layers,
    string? Current,
    HashSet<string> Frozen,
    HashSet<string> Linetypes);

namespace Core;

   // What layers.txt holds: the drawing's name, and its layers
public record Scan(string Drawing, HashSet<string> Layers);
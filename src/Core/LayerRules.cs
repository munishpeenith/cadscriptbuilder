namespace Core;

// AutoCAD's own rules about layer names
public static class LayerRules
{
    // Characters AutoCAD doesn't allow in layer names
    private const string Forbidden = "<>/\\\":;?*|,=`";

    // Layers AutoCAD won't let you rename or purge
    public static bool IsProtected(string name) =>
        name.Equals("0", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase);

    // What's wrong with a new layer name, or null if it's fine
    public static string? NameProblem(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "a layer name can't be empty";
        }
        if (name.IndexOfAny(Forbidden.ToCharArray()) >= 0)
        {
            return $"{name} uses a character layer names can't have ( < > / \\ \" : ; ? * | , = ` )";
        }
        if (name.Length > 255)
        {
            return $"{name} is longer than 255 characters";
        }
        return null;
    }
}

namespace Generator;

public class RenameLayer : Operation
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    public override string Describe() => $"Would rename {From} -> {To}";

    public override string ToLisp() => $"""(command "-RENAME" "LAyer" "{From}" "{To}")""";

    public override List<string> Check(HashSet<string> layers)
    {
    var problems = new List<string>();

    if (!layers.Contains(From))
        {
        problems.Add($"Can't rename {From}: there's no layer called {From}");
        }
    else if (layers.Contains(To))
        {
        problems.Add($"Can't rename {From} to {To}: {To} already exists");
        }
    else
        {
        layers.Remove(From);
        layers.Add(To);
        }

    return problems;
    }
}
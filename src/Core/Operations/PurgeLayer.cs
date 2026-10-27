namespace Generator;

public class PurgeLayer : Operation
{
    public string Layer { get; set; } = "";

    public override string Describe() => $"Would purge layer {Layer} (only if empty)";

    public override string ToLisp() => $"""(command "-PURGE" "LA" "{Layer}" "N")""";

    public override List<string> Check(HashSet<string> layers)
    {
    var problems = new List<string>();

    if (!layers.Contains(Layer))
        {
        problems.Add($"Can't purge {Layer}: there's no layer called {Layer}");
        }
    else
        {
        layers.Remove(Layer);
        }

    return problems;
    }
}
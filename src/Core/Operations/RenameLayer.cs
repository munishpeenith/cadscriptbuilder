namespace Core;

public class RenameLayer : Operation
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    public override string Describe() => $"Would rename {From} -> {To}";

    public override string ToLisp() => $"""(command "-RENAME" "LAyer" "{From}" "{To}")""";

    public override List<string> Check(DrawingState drawing)
    {
        var problems = new List<string>();
        var nameProblem = LayerRules.NameProblem(To);

        if (!drawing.HasLayer(From))
        {
            problems.Add($"Can't rename {From}: there's no layer called {From}");
        }
        else if (LayerRules.IsProtected(From))
        {
            problems.Add($"Can't rename {From}: AutoCAD doesn't allow it");
        }
        else if (nameProblem is not null)
        {
            problems.Add($"Can't rename {From}: {nameProblem}");
        }
        else if (drawing.HasLayer(To))
        {
            problems.Add($"Can't rename {From} to {To}: {To} already exists");
        }
        else
        {
            drawing.RenameLayer(From, To);
        }

        return problems;
    }
}

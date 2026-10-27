namespace Core;

public class PurgeLayer : Operation
{
    public string Layer { get; set; } = "";

    public override string Describe() => $"Would purge layer {Layer} (only if empty)";

    public override string ToLisp() => $"""(command "-PURGE" "LA" "{Layer}" "N")""";

    public override List<string> Check(DrawingState drawing)
    {
        var problems = new List<string>();

        if (!drawing.HasLayer(Layer))
        {
            problems.Add($"Can't purge {Layer}: there's no layer called {Layer}");
        }
        else if (LayerRules.IsProtected(Layer))
        {
            problems.Add($"Can't purge {Layer}: AutoCAD doesn't allow it");
        }
        else if (drawing.IsCurrent(Layer))
        {
            problems.Add($"Can't purge {Layer}: it's the current layer");
        }
        else
        {
            drawing.RemoveLayer(Layer);
        }

        return problems;
    }
}

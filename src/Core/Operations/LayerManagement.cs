namespace Core;

// Making layers and choosing which one is current

public class NewLayer : Operation
{
    public string Layer { get; set; } = "";

    public override string Describe() => $"Would create layer {Layer}";

    public override string ToLisp() => $"""(command "_.-LAYER" "_New" {Lisp.Quote(Layer)} "")""";

    public override List<string> Check(DrawingState drawing)
    {
        var problems = new List<string>();
        var nameProblem = LayerRules.NameProblem(Layer);

        if (nameProblem is not null)
        {
            problems.Add($"Can't create layer: {nameProblem}");
        }
        else if (drawing.HasLayer(Layer))
        {
            problems.Add($"Can't create {Layer}: it already exists");
        }
        else
        {
            drawing.Layers.Add(Layer);
        }

        return problems;
    }
}

public class SetCurrentLayer : LayerSetting
{
    protected override string Verb => "make current";
    protected override string[] Options => ["_Set"];

    public override string Describe() => $"Would make {Layer} the current layer";

    protected override void CheckMore(DrawingState drawing, List<string> problems)
    {
        if (drawing.Frozen.Contains(Layer))
        {
            problems.Add($"Can't make {Layer} current: it's frozen");
        }
        else
        {
            drawing.Current = Layer;
        }
    }
}

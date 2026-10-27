namespace Core;

// Making layers and choosing which one is current

public class NewLayer : Operation
{
    public string Layer { get; set; } = "";

    public override string Describe() => $"Would create layer {Layer}";

    public override string ToLisp() => $"""(command "_.-LAYER" "_New" {Lisp.Quote(Layer)} "")""";

    public override CheckResult Check(DrawingState drawing)
    {
        var result = new CheckResult();
        var nameProblem = LayerRules.NameProblem(Layer);

        if (nameProblem is not null)
        {
            result.Problem($"Can't create layer: {nameProblem}");
        }
        else if (drawing.HasLayer(Layer))
        {
            result.Problem($"Can't create {Layer}: it already exists");
        }
        else
        {
            drawing.Layers.Add(Layer);
        }

        return result;
    }
}

public class SetCurrentLayer : LayerSetting
{
    protected override string Verb => "switch to";
    protected override string[] Options => ["_Set"];

    public override string Describe() => $"Would make {Layer} the current layer";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (drawing.Frozen.Contains(Layer))
        {
            result.Problem($"Can't make {Layer} current: it's frozen");
            return;
        }
        if (drawing.IsCurrent(Layer))
        {
            result.Note($"{Layer} is already the current layer, so this step changes nothing");
        }
        else if (drawing.KnowsOnOffLockPlot && drawing.Off.Contains(Layer))
        {
            result.Note($"{Layer} is off, so anything drawn on it won't be visible");
        }
        drawing.Current = Layer;
    }
}

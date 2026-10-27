namespace Core;

// The base for blocks that run one -LAYER option on one existing layer, e.g.
//   (command "_.-LAYER" "_Freeze" "C-ROAD" "")
// Each block only fills in: its verb, the option words, and any extra checks.
public abstract class LayerSetting : Operation
{
    public string Layer { get; set; } = "";

    // Used in messages, e.g. "freeze"
    protected abstract string Verb { get; }

    // The words typed after -LAYER and before the layer name, e.g. ["_Color", "1"]
    protected abstract string[] Options { get; }

    public override string ToLisp()
    {
        var words = Options.Append(Layer).Select(Lisp.Quote);
        return $"""(command "_.-LAYER" {string.Join(" ", words)} "")""";
    }

    public override CheckResult Check(DrawingState drawing)
    {
        var result = new CheckResult();

        if (!drawing.HasLayer(Layer))
        {
            result.Problem($"Can't {Verb} {Layer}: there's no layer called {Layer}");
        }
        else
        {
            CheckMore(drawing, result);
        }

        return result;
    }

    // Extra rules for one block, and where it updates the pretend drawing
    protected virtual void CheckMore(DrawingState drawing, CheckResult result)
    {
    }
}

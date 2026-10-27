namespace Core;

public class PurgeLayer : Operation
{
    public string Layer { get; set; } = "";

    public override string Describe() => $"Would purge layer {Layer} (only if empty)";

    public override string ToLisp() => $"""(command "-PURGE" "LA" "{Layer}" "N")""";

    // Purge only works on empty layers, so the summary checks whether the layer actually went.
    // (Skipped if a later step makes a layer with the same name, as the check would be fooled.)
    public override string SummaryLisp(IEnumerable<Operation> later)
    {
        bool remade = later.Any(op =>
            (op is NewLayer n && n.Layer.Equals(Layer, StringComparison.OrdinalIgnoreCase)) ||
            (op is RenameLayer r && r.To.Equals(Layer, StringComparison.OrdinalIgnoreCase)));
        if (remade)
        {
            return base.SummaryLisp(later);
        }

        var name = Lisp.Escape(Layer);
        return $"""(princ (if (tblsearch "LAYER" "{name}") "\n  - {name} was NOT purged: it still has objects on it" "\n  - Purged {name}"))""";
    }

    public override CheckResult Check(DrawingState drawing)
    {
        var result = new CheckResult();

        if (!drawing.HasLayer(Layer))
        {
            result.Problem($"Can't purge {Layer}: there's no layer called {Layer}");
        }
        else if (LayerRules.IsProtected(Layer))
        {
            result.Problem($"Can't purge {Layer}: AutoCAD doesn't allow it");
        }
        else if (drawing.IsCurrent(Layer))
        {
            result.Problem($"Can't purge {Layer}: it's the current layer");
        }
        else
        {
            drawing.RemoveLayer(Layer);
        }

        return result;
    }
}

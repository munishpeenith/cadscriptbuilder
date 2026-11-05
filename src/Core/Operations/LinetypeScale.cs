namespace Core;

// Linetype scale: per layer (every object on it), and the drawing-wide settings

// AutoCAD layers don't have a linetype scale of their own: each object does.
// So this sets the linetype scale of every object on the layer.
public class SetLayerLinetypeScale : LayerSetting
{
    public string Scale { get; set; } = "";

    protected override string Verb => "set the linetype scale on";
    protected override string[] Options => [];

    public override string Describe() => $"Would set the linetype scale of every object on {Layer} to {Scale}";

    public override string Done() => $"Linetype scale {Scale} on {Layer}";

    public override bool NeedsRegen() => true;

    public override string ToLisp()
    {
        var scale = Number.ToLisp(Number.Parse(Scale) ?? 1);
        return $"(setq {ResultName()} (csb-apply (csb-objs {Lisp.Quote(Layer)} nil) " +
               $"(function (lambda (o) (vla-put-linetypescale o {scale})))))";
    }

    public override string SummaryLisp(IEnumerable<Operation> later) =>
        $"""(princ (strcat "\n  - Linetype scale {Lisp.Escape(Scale)} on {Lisp.Escape(Layer)}: " (csb-count {ResultName()} "objects")))""";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        var value = Number.Parse(Scale);
        if (value is null || value <= 0)
        {
            result.Problem($"Can't set the linetype scale on {Layer} to {Scale}: use a number above 0, e.g. 0.5 or 2");
            return;
        }
        if (drawing.Locked.Contains(Layer))
        {
            result.Problem($"Can't change objects on {Layer}: it's locked. Add an Unlock step before this one");
            return;
        }
        if (drawing.CountOf(Layer) == 0)
        {
            result.Note($"{Layer} has no objects on it, so this step changes nothing");
        }
    }
}

// One of the drawing's linetype scale settings (system variables)
public class SetLinetypeSetting : Operation
{
    // LTSCALE, CELTSCALE, PSLTSCALE or MSLTSCALE
    public string Setting { get; set; } = "";
    public string Value { get; set; } = "";

    public static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LTSCALE"] = "the drawing's linetype scale (LTSCALE)",
        ["CELTSCALE"] = "the linetype scale for new objects (CELTSCALE)",
        ["PSLTSCALE"] = "viewport linetype scaling (PSLTSCALE)",
        ["MSLTSCALE"] = "model space linetype scaling (MSLTSCALE)",
    };

    private bool IsOnOff => Setting.Equals("PSLTSCALE", StringComparison.OrdinalIgnoreCase) ||
                            Setting.Equals("MSLTSCALE", StringComparison.OrdinalIgnoreCase);

    private string Name => Names.GetValueOrDefault(Setting, Setting);

    public override string Kind() => $"{nameof(SetLinetypeSetting)}:{Setting.ToUpperInvariant()}";

    public override bool NeedsRegen() => true;

    // On/Off (or 1/0) for the switches, as a number
    private int? Switch() => Value.Trim().ToLowerInvariant() switch
    {
        "on" or "1" or "yes" => 1,
        "off" or "0" or "no" => 0,
        _ => null
    };

    private string Shown => IsOnOff ? (Switch() == 1 ? "on" : "off") : Value;

    public override string Describe() => $"Would set {Name} to {Shown}";

    public override string ToLisp()
    {
        var value = IsOnOff ? (Switch() ?? 0).ToString() : Number.ToLisp(Number.Parse(Value) ?? 1);
        return $"""(setvar "{Setting.ToUpperInvariant()}" {value})""";
    }

    public override CheckResult Check(DrawingState drawing)
    {
        var result = new CheckResult();

        if (!Names.ContainsKey(Setting))
        {
            result.Problem($"Can't set {Setting}: use LTSCALE, CELTSCALE, PSLTSCALE or MSLTSCALE");
            return result;
        }

        var lt = drawing.Linetype;
        if (IsOnOff)
        {
            var value = Switch();
            if (value is null)
            {
                result.Problem($"Can't set {Name} to {Value}: use On or Off");
                return result;
            }
            var now = Setting.Equals("PSLTSCALE", StringComparison.OrdinalIgnoreCase) ? lt.PsLtScale : lt.MsLtScale;
            if (now == value)
            {
                result.Note($"{Name} is already {Shown}, so this step changes nothing");
            }
            drawing.Linetype = Setting.Equals("PSLTSCALE", StringComparison.OrdinalIgnoreCase)
                ? lt with { PsLtScale = value }
                : lt with { MsLtScale = value };
        }
        else
        {
            var value = Number.Parse(Value);
            if (value is null || value <= 0)
            {
                result.Problem($"Can't set {Name} to {Value}: use a number above 0, e.g. 1 or 0.5");
                return result;
            }
            var now = Setting.Equals("LTSCALE", StringComparison.OrdinalIgnoreCase) ? lt.LtScale : lt.CeLtScale;
            if (now is not null && Math.Abs(now.Value - value.Value) < 1e-9)
            {
                result.Note($"{Name} is already {Number.Show(value.Value)}, so this step changes nothing");
            }
            drawing.Linetype = Setting.Equals("LTSCALE", StringComparison.OrdinalIgnoreCase)
                ? lt with { LtScale = value }
                : lt with { CeLtScale = value };
        }

        return result;
    }
}

using System.Globalization;

namespace Core;

// Colour, linetype, lineweight, transparency, description, plot

public class SetLayerColour : LayerSetting
{
    public string Colour { get; set; } = "";

    protected override string Verb => "set the colour of";
    protected override string[] Options => ["_Color", Colour];

    public override string Describe() => $"Would set {Layer} colour to {Colour}";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (!int.TryParse(Colour, out var number) || number < 1 || number > 255)
        {
            result.Problem($"Can't set {Layer} colour to {Colour}: use a number from 1 to 255");
        }
    }
}

public class SetLayerLinetype : LayerSetting
{
    public string Linetype { get; set; } = "";

    protected override string Verb => "set the linetype of";
    protected override string[] Options => ["_Ltype", Linetype];

    public override string Describe() => $"Would set {Layer} linetype to {Linetype}";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (drawing.Linetypes.Count == 0)
        {
            result.Problem("Can't check linetypes: rescan the drawing with the latest scan file");
        }
        else if (!drawing.Linetypes.Contains(Linetype))
        {
            result.Problem($"Can't set {Layer} to {Linetype}: that linetype isn't loaded in this drawing");
        }
    }
}

public class SetLayerLineweight : LayerSetting
{
    // The lineweights AutoCAD accepts, in mm
    public static readonly string[] Allowed =
    [
        "0", "0.05", "0.09", "0.13", "0.15", "0.18", "0.2", "0.25", "0.3", "0.35", "0.4", "0.5",
        "0.53", "0.6", "0.7", "0.8", "0.9", "1", "1.06", "1.2", "1.4", "1.58", "2", "2.11"
    ];

    public string Lineweight { get; set; } = "";

    protected override string Verb => "set the lineweight of";
    protected override string[] Options => ["_LWeight", Lineweight];

    public override string Describe() => $"Would set {Layer} lineweight to {Lineweight} mm";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        var ok = decimal.TryParse(Lineweight, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            && Allowed.Any(a => decimal.Parse(a, CultureInfo.InvariantCulture) == value);

        if (!ok)
        {
            result.Problem($"Can't set {Layer} lineweight to {Lineweight}: use a standard mm value like 0.25 or 0.35");
        }
    }
}

public class SetLayerTransparency : LayerSetting
{
    public string Transparency { get; set; } = "";

    protected override string Verb => "set the transparency of";
    protected override string[] Options => ["_TRansparency", Transparency];

    public override string Describe() => $"Would set {Layer} transparency to {Transparency}";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (!int.TryParse(Transparency, out var number) || number < 0 || number > 90)
        {
            result.Problem($"Can't set {Layer} transparency to {Transparency}: use a number from 0 to 90");
        }
    }
}

public class SetLayerDescription : LayerSetting
{
    public string Description { get; set; } = "";

    protected override string Verb => "describe";
    protected override string[] Options => ["_Description", Description];

    public override string Describe() => $"Would set {Layer} description to \"{Description}\"";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (string.IsNullOrWhiteSpace(Description))
        {
            result.Problem($"Can't set {Layer} description: it's empty");
        }
    }
}

public class PlotLayer : LayerSetting
{
    protected override string Verb => "set plotting on";
    protected override string[] Options => ["_Plot", "_Plot"];

    public override string Describe() => $"Would set {Layer} to plot";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (drawing.KnowsOnOffLockPlot && !drawing.NoPlot.Contains(Layer))
        {
            result.Note($"{Layer} already plots, so this step changes nothing");
        }
        drawing.NoPlot.Remove(Layer);
    }
}

public class NoPlotLayer : LayerSetting
{
    protected override string Verb => "set plotting off";
    protected override string[] Options => ["_Plot", "_No"];

    public override string Describe() => $"Would set {Layer} to not plot";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (drawing.KnowsOnOffLockPlot && drawing.NoPlot.Contains(Layer))
        {
            result.Note($"{Layer} is already set to not plot, so this step changes nothing");
        }
        drawing.NoPlot.Add(Layer);
    }
}

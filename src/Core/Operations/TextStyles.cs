namespace Core;

// Fonts and text heights. Changing a text style changes every piece of text that uses it,
// and Civil 3D label styles point at text styles too, so labels follow the same font.

public class SetTextStyleFont : Operation
{
    public string Style { get; set; } = "";

    // A font name (Arial) or a font file (romans.shx, arial.ttf)
    public string Font { get; set; } = "";

    public override string Describe() => $"Would change the font of text style {Style} to {Font} (all text and labels using it)";

    public override string Done() => $"Text style {Style} now uses {Font}";

    public override bool NeedsRegen() => true;

    public override string ToLisp() => $"(setq {ResultName()} (csb-style-font {Lisp.Quote(Style)} {Lisp.Quote(Font)}))";

    public override string SummaryText(IEnumerable<Operation> later) =>
        $"""(if {ResultName()} "\n  - Text style {Lisp.Escape(Style)} now uses {Lisp.Escape(Font)}" "\n  - Text style {Lisp.Escape(Style)} NOT changed: AutoCAD couldn't use the font {Lisp.Escape(Font)}")""";

    public override CheckResult Check(DrawingState drawing)
    {
        var result = new CheckResult();
        if (!TextStyleRules.CheckStyle(drawing, Style, "change the font of", result))
        {
            return result;
        }
        if (string.IsNullOrWhiteSpace(Font))
        {
            result.Problem($"Can't change the font of {Style}: no font given");
            return result;
        }

        var now = drawing.TextStyles[Style];
        if (TextStyleRules.SameFont(now.Font, Font))
        {
            result.Note($"{Style} already uses {Font}, so this step changes nothing");
        }
        drawing.TextStyles[Style] = now with { Font = Font };
        return result;
    }
}

// Every TEXT, MTEXT and multileader on a layer gets the same height
public class SetTextHeight : LayerSetting
{
    public string Height { get; set; } = "";

    protected override string Verb => "set the text height on";
    protected override string[] Options => [];

    public override string Describe() => $"Would set the height of all text on {Layer} to {Height}";

    public override bool NeedsRegen() => true;

    public override string ToLisp()
    {
        var h = Number.ToLisp(Number.Parse(Height) ?? 1);
        return $"(setq {ResultName()} (csb-apply (csb-objs {Lisp.Quote(Layer)} \"TEXT,MTEXT,MULTILEADER\") " +
               $"(function (lambda (o) (if (= (vla-get-objectname o) \"AcDbMLeader\") (vla-put-textheight o {h}) (vla-put-height o {h}))))))";
    }

    public override string SummaryText(IEnumerable<Operation> later) =>
        $"""(strcat "\n  - Text height {Lisp.Escape(Height)} on {Lisp.Escape(Layer)}: " (csb-count {ResultName()} "texts"))""";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        var value = Number.Parse(Height);
        if (value is null || value <= 0)
        {
            result.Problem($"Can't set the text height on {Layer} to {Height}: use a number above 0, e.g. 2.5");
            return;
        }
        TextStyleRules.CheckTextLayer(drawing, Layer, result);
    }
}

// Every TEXT, MTEXT and multileader on a layer switches to one text style
public class SetTextStyleOnLayer : LayerSetting
{
    public string Style { get; set; } = "";

    protected override string Verb => "set the text style on";
    protected override string[] Options => [];

    public override string Describe() => $"Would switch all text on {Layer} to text style {Style}";

    public override bool NeedsRegen() => true;

    public override string ToLisp()
    {
        var style = Lisp.Quote(Style);
        return $"(setq {ResultName()} (csb-apply (csb-objs {Lisp.Quote(Layer)} \"TEXT,MTEXT,MULTILEADER\") " +
               $"(function (lambda (o) (if (= (vla-get-objectname o) \"AcDbMLeader\") (vla-put-textstylename o {style}) (vla-put-stylename o {style}))))))";
    }

    public override string SummaryText(IEnumerable<Operation> later) =>
        $"""(strcat "\n  - Text style {Lisp.Escape(Style)} on {Lisp.Escape(Layer)}: " (csb-count {ResultName()} "texts"))""";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (!TextStyleRules.CheckStyle(drawing, Style, $"switch text on {Layer} to", result))
        {
            return;
        }
        TextStyleRules.CheckTextLayer(drawing, Layer, result);
    }
}

public static class TextStyleRules
{
    // Fonts people usually reach for, offered in the dropdown alongside the drawing's own
    public static readonly string[] CommonFonts =
    [
        "Arial", "Arial Narrow", "Calibri", "Segoe UI", "Tahoma", "Verdana", "Times New Roman",
        "romans.shx", "simplex.shx", "isocp.shx", "isocpeur.ttf", "txt.shx", "monotxt.shx"
    ];

    // Does the style exist? (needs a version 4 scan to know)
    public static bool CheckStyle(DrawingState drawing, string style, string verb, CheckResult result)
    {
        if (!drawing.KnowsContents)
        {
            result.Problem($"Can't check text styles: rescan the drawing with the latest SCAN tile");
            return false;
        }
        if (!drawing.TextStyles.ContainsKey(style))
        {
            result.Problem($"Can't {verb} {style}: there's no text style called {style}");
            return false;
        }
        return true;
    }

    // Locked layers can't change; a layer with no text is worth a heads-up
    public static void CheckTextLayer(DrawingState drawing, string layer, CheckResult result)
    {
        if (drawing.Locked.Contains(layer))
        {
            result.Problem($"Can't change text on {layer}: it's locked. Add an Unlock step before this one");
        }
        else if (drawing.KnowsContents && !drawing.TextsOn(layer).Any())
        {
            result.Note($"{layer} has no text on it, so this step changes nothing");
        }
    }

    // "arial.ttf" and "Arial" count as the same font
    public static bool SameFont(string a, string b) =>
        string.Equals(Path.GetFileNameWithoutExtension(a.Trim()), Path.GetFileNameWithoutExtension(b.Trim()),
            StringComparison.OrdinalIgnoreCase);
}

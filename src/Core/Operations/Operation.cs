using System.Text.Json.Serialization;

namespace Core;

// The registry: the "type" word in a plan's JSON, and the block it means
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RenameLayer), "renameLayer")]
[JsonDerivedType(typeof(PurgeLayer), "purgeLayer")]
[JsonDerivedType(typeof(NewLayer), "newLayer")]
[JsonDerivedType(typeof(SetCurrentLayer), "setCurrentLayer")]
[JsonDerivedType(typeof(FreezeLayer), "freezeLayer")]
[JsonDerivedType(typeof(ThawLayer), "thawLayer")]
[JsonDerivedType(typeof(TurnOffLayer), "turnOffLayer")]
[JsonDerivedType(typeof(TurnOnLayer), "turnOnLayer")]
[JsonDerivedType(typeof(LockLayer), "lockLayer")]
[JsonDerivedType(typeof(UnlockLayer), "unlockLayer")]
[JsonDerivedType(typeof(PlotLayer), "plotLayer")]
[JsonDerivedType(typeof(NoPlotLayer), "noPlotLayer")]
[JsonDerivedType(typeof(SetLayerColour), "setLayerColour")]
[JsonDerivedType(typeof(SetLayerLinetype), "setLayerLinetype")]
[JsonDerivedType(typeof(SetLayerLineweight), "setLayerLineweight")]
[JsonDerivedType(typeof(SetLayerTransparency), "setLayerTransparency")]
[JsonDerivedType(typeof(SetLayerDescription), "setLayerDescription")]
[JsonDerivedType(typeof(SetLayerLinetypeScale), "setLayerLinetypeScale")]
[JsonDerivedType(typeof(SetLinetypeSetting), "setLinetypeSetting")]
[JsonDerivedType(typeof(SetTextStyleFont), "setTextStyleFont")]
[JsonDerivedType(typeof(SetTextHeight), "setTextHeight")]
[JsonDerivedType(typeof(SetTextStyleOnLayer), "setTextStyleOnLayer")]
public abstract class Operation
{
    // Which step this is in the script (set while building it). Steps that count what they changed
    // keep the result in a LISP variable named after it, e.g. csb-r3, for the summary.
    [JsonIgnore]
    public int Step { get; set; }

    public string ResultName() => $"csb-r{Step}";

    // True for steps whose change only shows after a regen (linetype scales, text styles)
    public virtual bool NeedsRegen() => false;

    // Which block this step came from. Usually its type, but one type can serve several blocks.
    public virtual string Kind() => GetType().Name;

    public abstract CheckResult Check(DrawingState drawing);
    public abstract string Describe();
    public abstract string ToLisp();

    // What this step did, for the summary printed after a run: "Would freeze X" becomes "Freeze X"
    public virtual string Done()
    {
        var text = Describe();
        return text.StartsWith("Would ") ? char.ToUpper(text[6]) + text[7..] : text;
    }

    // The LISP that prints this step's line in the after-run summary.
    // "later" is the steps that come after this one (some summaries need to know).
    public virtual string SummaryLisp(IEnumerable<Operation> later) => $"""(princ "\n  - {Lisp.Escape(Done())}")""";
}

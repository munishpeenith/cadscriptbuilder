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
public abstract class Operation
{
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

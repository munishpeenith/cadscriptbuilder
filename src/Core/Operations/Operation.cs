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
    public abstract List<string> Check(DrawingState drawing);
    public abstract string Describe();
    public abstract string ToLisp();
}

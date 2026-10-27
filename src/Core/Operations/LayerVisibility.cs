namespace Core;

// Freeze/thaw, off/on, lock/unlock

public class FreezeLayer : LayerSetting
{
    protected override string Verb => "freeze";
    protected override string[] Options => ["_Freeze"];

    public override string Describe() => $"Would freeze {Layer}";

    protected override void CheckMore(DrawingState drawing, List<string> problems)
    {
        if (drawing.IsCurrent(Layer))
        {
            problems.Add($"Can't freeze {Layer}: it's the current layer");
        }
        else
        {
            drawing.Frozen.Add(Layer);
        }
    }
}

public class ThawLayer : LayerSetting
{
    protected override string Verb => "thaw";
    protected override string[] Options => ["_Thaw"];

    public override string Describe() => $"Would thaw {Layer}";

    protected override void CheckMore(DrawingState drawing, List<string> problems)
    {
        drawing.Frozen.Remove(Layer);
    }
}

public class TurnOffLayer : LayerSetting
{
    protected override string Verb => "turn off";
    protected override string[] Options => ["_OFF"];

    public override string Describe() => $"Would turn off {Layer}";

    protected override void CheckMore(DrawingState drawing, List<string> problems)
    {
        // AutoCAD asks an extra question for the current layer, which would break the script
        if (drawing.IsCurrent(Layer))
        {
            problems.Add($"Can't turn off {Layer}: it's the current layer");
        }
    }
}

public class TurnOnLayer : LayerSetting
{
    protected override string Verb => "turn on";
    protected override string[] Options => ["_ON"];

    public override string Describe() => $"Would turn on {Layer}";
}

public class LockLayer : LayerSetting
{
    protected override string Verb => "lock";
    protected override string[] Options => ["_LOck"];

    public override string Describe() => $"Would lock {Layer}";
}

public class UnlockLayer : LayerSetting
{
    protected override string Verb => "unlock";
    protected override string[] Options => ["_Unlock"];

    public override string Describe() => $"Would unlock {Layer}";
}

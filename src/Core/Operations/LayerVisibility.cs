namespace Core;

// Freeze/thaw, off/on, lock/unlock

public class FreezeLayer : LayerSetting
{
    protected override string Verb => "freeze";
    protected override string[] Options => ["_Freeze"];

    public override string Describe() => $"Would freeze {Layer}";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (drawing.IsCurrent(Layer))
        {
            result.Problem($"Can't freeze {Layer}: it's the current layer");
            return;
        }
        if (drawing.KnowsFrozen && drawing.Frozen.Contains(Layer))
        {
            result.Note($"{Layer} is already frozen, so this step changes nothing");
        }
        drawing.Frozen.Add(Layer);
    }
}

public class ThawLayer : LayerSetting
{
    protected override string Verb => "thaw";
    protected override string[] Options => ["_Thaw"];

    public override string Describe() => $"Would thaw {Layer}";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (drawing.KnowsFrozen && !drawing.Frozen.Contains(Layer))
        {
            result.Note($"{Layer} is already thawed, so this step changes nothing");
        }
        drawing.Frozen.Remove(Layer);
    }
}

public class TurnOffLayer : LayerSetting
{
    protected override string Verb => "turn off";
    protected override string[] Options => ["_OFF"];

    public override string Describe() => $"Would turn off {Layer}";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        // AutoCAD asks an extra question for the current layer, which would break the script
        if (drawing.IsCurrent(Layer))
        {
            result.Problem($"Can't turn off {Layer}: it's the current layer");
            return;
        }
        if (drawing.KnowsOnOffLockPlot && drawing.Off.Contains(Layer))
        {
            result.Note($"{Layer} is already off, so this step changes nothing");
        }
        drawing.Off.Add(Layer);
    }
}

public class TurnOnLayer : LayerSetting
{
    protected override string Verb => "turn on";
    protected override string[] Options => ["_ON"];

    public override string Describe() => $"Would turn on {Layer}";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (drawing.KnowsOnOffLockPlot && !drawing.Off.Contains(Layer))
        {
            result.Note($"{Layer} is already on, so this step changes nothing");
        }
        drawing.Off.Remove(Layer);
    }
}

public class LockLayer : LayerSetting
{
    protected override string Verb => "lock";
    protected override string[] Options => ["_LOck"];

    public override string Describe() => $"Would lock {Layer}";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (drawing.KnowsOnOffLockPlot && drawing.Locked.Contains(Layer))
        {
            result.Note($"{Layer} is already locked, so this step changes nothing");
        }
        drawing.Locked.Add(Layer);
    }
}

public class UnlockLayer : LayerSetting
{
    protected override string Verb => "unlock";
    protected override string[] Options => ["_Unlock"];

    public override string Describe() => $"Would unlock {Layer}";

    protected override void CheckMore(DrawingState drawing, CheckResult result)
    {
        if (drawing.KnowsOnOffLockPlot && !drawing.Locked.Contains(Layer))
        {
            result.Note($"{Layer} is already unlocked, so this step changes nothing");
        }
        drawing.Locked.Remove(Layer);
    }
}

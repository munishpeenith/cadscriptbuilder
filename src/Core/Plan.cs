namespace Core;

public class Plan
{
    public bool AskBeforeRunning { get; set; }
    public List<Operation> Operations { get; set; } = new();
}
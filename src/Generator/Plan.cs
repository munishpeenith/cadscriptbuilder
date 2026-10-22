namespace Generator;

public class Plan
{
    public bool AskBeforeRunning { get; set; }
    public List<Operation> Operations { get; set; } = new();
}

public class Operation
{
    public string Type { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}
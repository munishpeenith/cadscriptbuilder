namespace Generator;

public class RenameLayer : Operation
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    public override string Describe() => $"Would rename {From} -> {To}";

    public override string ToLisp() => $"""(command "-RENAME" "LAyer" "{From}" "{To}")""";
}
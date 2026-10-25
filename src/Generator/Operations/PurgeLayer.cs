namespace Generator;

public class PurgeLayer : Operation
{
    public string Layer { get; set; } = "";

    public override string Describe() => $"Would purge layer {Layer} (only if empty)";

    public override string ToLisp() => $"""(command "-PURGE" "LA" "{Layer}" "N")""";
}
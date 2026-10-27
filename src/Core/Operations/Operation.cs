using System.Text.Json.Serialization;

namespace Core;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RenameLayer), "renameLayer")]
[JsonDerivedType(typeof(PurgeLayer), "purgeLayer")]
public abstract class Operation
{
    public abstract List<string> Check(HashSet<string> layers);
    public abstract string Describe();
    public abstract string ToLisp();
}
using System.Text.Json.Serialization;

namespace Generator;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RenameLayer), "renameLayer")]
public abstract class Operation
{
    public abstract string Describe();
    public abstract string ToLisp();
}
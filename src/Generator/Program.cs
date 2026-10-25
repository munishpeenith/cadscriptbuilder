using System.Text;
using System.Text.Json;
using Generator;

if (args.Length < 2)
{
    Console.WriteLine("Usage: Generator <plan file> <output .lsp>");
    return;
}

var json = File.ReadAllText(args[0]);
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    AllowOutOfOrderMetadataProperties = true
};

Plan? plan;
try
{
    plan = JsonSerializer.Deserialize<Plan>(json, options);
}
catch (Exception ex) when (ex is JsonException or NotSupportedException)
{
    Console.WriteLine($"Could not read the plan: {ex.Message}");
    Console.WriteLine("Nothing generated.");
    return;
}

if (plan is null)
{
    Console.WriteLine("Could not read the plan.");
    return;
}

var lisp = new StringBuilder();
lisp.AppendLine("(defun c:CSB_RUN ()");

foreach (var op in plan.Operations)
{
    lisp.AppendLine($"""  (princ "\n{op.Describe()}")""");
}

lisp.AppendLine("""  (initget "Yes No")""");
lisp.AppendLine("""  (if (= (getkword "\nApply changes? [Yes/No] <No>: ") "Yes")""");
lisp.AppendLine("""    (progn""");
lisp.AppendLine("""      (command "_.UNDO" "_BEGIN")""");

foreach (var op in plan.Operations)
{
    lisp.AppendLine($"      {op.ToLisp()}");
}

lisp.AppendLine("""      (command "_.UNDO" "_END")""");
lisp.AppendLine("""      (princ "\nCADScriptBuilder: done. Type U once to undo."))""");
lisp.AppendLine("""    (princ "\nCancelled. Nothing changed."))""");
lisp.AppendLine("""  (princ))""");
lisp.AppendLine("(c:CSB_RUN)");
lisp.AppendLine("(princ)");

File.WriteAllText(args[1], lisp.ToString());
Console.WriteLine($"Wrote {args[1]}");
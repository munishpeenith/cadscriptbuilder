using System.Text;
using System.Text.Json;
using Generator;

if (args.Length < 2)
{
    Console.WriteLine("Usage: Generator <plan file> <output .lsp> [scan file]");
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

// Check the plan against the drawing's layers, if a scan file was given
if (args.Length >= 3)
{
    if (!File.Exists(args[2]))
    {
        Console.WriteLine($"Can't find the scan file {args[2]}. Nothing generated.");
        return;
    }

    var scan = File.ReadAllLines(args[2]);
    if (scan.Length == 0)
    {
        Console.WriteLine("The scan file is empty. Nothing generated.");
        return;
    }

    var drawing = scan[0];
    var layers = new HashSet<string>(scan.Skip(1), StringComparer.OrdinalIgnoreCase);

    var problems = new List<string>();
    foreach (var op in plan.Operations)
    {
        problems.AddRange(op.Check(layers));
    }

    if (problems.Count > 0)
    {
        Console.WriteLine($"The plan doesn't match {drawing}:");
        foreach (var problem in problems)
        {
            Console.WriteLine($"  - {problem}");
        }
        Console.WriteLine("Nothing generated.");
        return;
    }

    Console.WriteLine($"Checked against {drawing}: OK");
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

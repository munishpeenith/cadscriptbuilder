using System.Text;
using System.Text.Json;
using Generator;

if (args.Length < 2)
{
    Console.WriteLine("Usage: Generator <plan file> <output .lsp>");
    return;
}

var json = File.ReadAllText(args[0]);
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var plan = JsonSerializer.Deserialize<Plan>(json, options);


if (plan is null)
{
    Console.WriteLine("Could not read the plan.");
    return;
}

// 1. Refuse anything we don't recognise, before writing a single line
foreach (var op in plan.Operations)
{
    if (op.Type != "renameLayer")
    {
        Console.WriteLine($"Unknown operation: {op.Type}. Nothing generated.");
        return;
    }
}

// 2. Build the script
var lisp = new StringBuilder();
lisp.AppendLine("(defun c:CSB_RUN ()");

foreach (var op in plan.Operations)
{
    lisp.AppendLine($"""  (princ "\nWould rename {op.From} -> {op.To}")""");
}

lisp.AppendLine("""  (initget "Yes No")""");
lisp.AppendLine("""  (if (= (getkword "\nApply changes? [Yes/No] <No>: ") "Yes")""");
lisp.AppendLine("""    (progn""");
lisp.AppendLine("""      (command "_.UNDO" "_BEGIN")""");

foreach (var op in plan.Operations)
{
    lisp.AppendLine($"""      (command "-RENAME" "LAyer" "{op.From}" "{op.To}")""");
}

lisp.AppendLine("""      (command "_.UNDO" "_END")""");
lisp.AppendLine("""      (princ "\nCADScriptBuilder: done. Type U once to undo."))""");
lisp.AppendLine("""    (princ "\nCancelled. Nothing changed."))""");
lisp.AppendLine("""  (princ))""");
lisp.AppendLine("(c:CSB_RUN)");
lisp.AppendLine("(princ)");

File.WriteAllText(args[1], lisp.ToString());
Console.WriteLine($"Wrote {args[1]}");


using System.Text;
using System.Text.Json;

namespace Core;

// The brain: reads plans and scans, checks, builds LISP.
// It never prints and never quits. It returns answers, or throws
// an InvalidDataException with a friendly message when something is wrong.
public static class Engine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true
    };

    public static Plan ReadPlan(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Can't find the plan file {path}.");
        }

        Plan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidDataException($"Could not read the plan: {ex.Message}");
        }

        if (plan is null)
        {
            throw new InvalidDataException("Could not read the plan.");
        }

        return plan;
    }

    public static Scan ReadScan(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Can't find the scan file {path}.");
        }

        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
        {
            throw new InvalidDataException("The scan file is empty.");
        }

        var layers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var frozen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var linetypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? current = null;

        // Lines starting with * are extra info (layer names can't contain *)
        foreach (var line in lines.Skip(1))
        {
            if (line.StartsWith("*current="))
            {
                current = line["*current=".Length..];
            }
            else if (line.StartsWith("*frozen="))
            {
                frozen.Add(line["*frozen=".Length..]);
            }
            else if (line.StartsWith("*linetype="))
            {
                linetypes.Add(line["*linetype=".Length..]);
            }
            else if (line != "")
            {
                layers.Add(line);
            }
        }

        return new Scan(lines[0], layers, current, frozen, linetypes);
    }

    public static List<string> Check(Plan plan, Scan scan)
    {
        // Work on a pretend copy, so checking never changes the scan itself
        var drawing = new DrawingState(scan);

        var problems = new List<string>();
        foreach (var op in plan.Operations)
        {
            problems.AddRange(op.Check(drawing));
        }
        return problems;
    }

    public static string BuildLisp(Plan plan)
    {
        var lisp = new StringBuilder();
        lisp.AppendLine("(defun c:CSB_RUN ()");

        foreach (var op in plan.Operations)
        {
            lisp.AppendLine($"""  (princ "\n{Lisp.Escape(op.Describe())}")""");
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

        return lisp.ToString();
    }
}
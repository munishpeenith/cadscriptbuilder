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

    // How saved plans are written: neat, indented, camelCase like the hand-written ones
    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void SavePlan(Plan plan, string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(plan, SaveOptions));
    }

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
        var off = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var locked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var noPlot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? current = null;
        int version = 1;

        // Lines starting with * are extra info (layer names can't contain *)
        foreach (var line in lines.Skip(1))
        {
            if (line.StartsWith("*scan="))
            {
                int.TryParse(line["*scan=".Length..], out version);
            }
            else if (line.StartsWith("*current="))
            {
                current = line["*current=".Length..];
                version = Math.Max(version, 2);
            }
            else if (line.StartsWith("*frozen="))
            {
                frozen.Add(line["*frozen=".Length..]);
            }
            else if (line.StartsWith("*linetype="))
            {
                linetypes.Add(line["*linetype=".Length..]);
            }
            else if (line.StartsWith("*off="))
            {
                off.Add(line["*off=".Length..]);
            }
            else if (line.StartsWith("*locked="))
            {
                locked.Add(line["*locked=".Length..]);
            }
            else if (line.StartsWith("*noplot="))
            {
                noPlot.Add(line["*noplot=".Length..]);
            }
            else if (line != "")
            {
                layers.Add(line);
            }
        }

        return new Scan(lines[0], layers, current, frozen, linetypes, off, locked, noPlot, version);
    }

    public static CheckResult Check(Plan plan, Scan scan)
    {
        // Work on a pretend copy, so checking never changes the scan itself
        var drawing = new DrawingState(scan);

        var result = new CheckResult();
        foreach (var op in plan.Operations)
        {
            result.Add(op.Check(drawing));
        }
        return result;
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

        // After running: a summary of what was done
        var changes = plan.Operations.Count == 1 ? "1 change" : $"{plan.Operations.Count} changes";
        lisp.AppendLine($"""      (princ "\nCADScriptBuilder: done, {changes}:")""");
        for (int i = 0; i < plan.Operations.Count; i++)
        {
            lisp.AppendLine($"      {plan.Operations[i].SummaryLisp(plan.Operations.Skip(i + 1))}");
        }
        lisp.AppendLine("""      (princ "\nType U once to undo all of it."))""");
        lisp.AppendLine("""    (princ "\nCancelled. Nothing changed."))""");
        lisp.AppendLine("""  (princ))""");
        lisp.AppendLine("(c:CSB_RUN)");
        lisp.AppendLine("(princ)");

        return lisp.ToString();
    }
}
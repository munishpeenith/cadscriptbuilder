using System.Globalization;
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
        File.WriteAllText(path, PlanToJson(plan));
    }

    public static string PlanToJson(Plan plan) => JsonSerializer.Serialize(plan, SaveOptions);

    public static Plan ReadPlan(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Can't find the plan file {path}.");
        }
        return ParsePlan(File.ReadAllText(path));
    }

    // A plan from JSON text (a saved file, or steps proposed by AI mode)
    public static Plan ParsePlan(string json)
    {
        Plan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<Plan>(json, JsonOptions);
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

        var lines = Decode(File.ReadAllBytes(path))
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
        if (lines.Length == 0 || lines[0].Trim() == "")
        {
            throw new InvalidDataException("The scan file is empty.");
        }

        var layers = Scan.NewSet();
        var frozen = Scan.NewSet();
        var linetypes = Scan.NewSet();
        var off = Scan.NewSet();
        var locked = Scan.NewSet();
        var noPlot = Scan.NewSet();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var styles = new Dictionary<string, TextStyleInfo>(StringComparer.OrdinalIgnoreCase);
        var texts = new List<TextItem>();
        double? ltScale = null, ceLtScale = null;
        int? psLtScale = null, msLtScale = null;
        string? current = null;
        int version = 1;
        bool fromRun = false;

        // Lines starting with * are extra info (layer names can't contain *)
        foreach (var line in lines.Skip(1))
        {
            if (line == "")
            {
                continue;
            }
            if (!line.StartsWith('*'))
            {
                layers.Add(line);
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }
            var key = line[1..eq];
            var value = line[(eq + 1)..];

            switch (key)
            {
                case "scan": int.TryParse(value, out version); break;
                case "source": fromRun = value == "run"; break;
                case "current": current = value; version = Math.Max(version, 2); break;
                case "frozen": frozen.Add(value); break;
                case "linetype": linetypes.Add(value); break;
                case "off": off.Add(value); break;
                case "locked": locked.Add(value); break;
                case "noplot": noPlot.Add(value); break;
                case "ltscale": ltScale = Number.Parse(value); break;
                case "celtscale": ceLtScale = Number.Parse(value); break;
                case "psltscale": psLtScale = int.TryParse(value, out var ps) ? ps : null; break;
                case "msltscale": msLtScale = int.TryParse(value, out var ms) ? ms : null; break;
                case "count":
                {
                    // count|layer name
                    var parts = value.Split('|', 2);
                    if (parts.Length == 2 && int.TryParse(parts[0], out var n))
                    {
                        counts[parts[1]] = n;
                    }
                    break;
                }
                case "textstyle":
                {
                    // height|font|style name
                    var parts = value.Split('|', 3);
                    if (parts.Length == 3)
                    {
                        styles[parts[2]] = new TextStyleInfo(parts[1], Number.Parse(parts[0]) ?? 0);
                    }
                    break;
                }
                case "text":
                {
                    // type|handle|length of layer name|layer name, then the text itself
                    var parts = value.Split('|', 4);
                    if (parts.Length == 4 && int.TryParse(parts[2], out var length) && length <= parts[3].Length)
                    {
                        texts.Add(new TextItem(parts[0], parts[1], parts[3][..length], parts[3][length..]));
                    }
                    break;
                }
                // Anything else is extra info from a newer version of the app: skip it
            }
        }

        return new Scan
        {
            Drawing = lines[0].Trim(),
            Layers = layers,
            Current = current,
            Frozen = frozen,
            Linetypes = linetypes,
            Off = off,
            Locked = locked,
            NoPlot = noPlot,
            Version = version,
            FromRun = fromRun,
            Counts = counts,
            Linetype = new LinetypeSettings(ltScale, ceLtScale, psLtScale, msLtScale),
            TextStyles = styles,
            Texts = texts
        };
    }

    // Scans are written as UTF-8 by newer AutoCAD, or the Windows code page by older ones
    private static string Decode(byte[] bytes)
    {
        try
        {
            var text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            return text.TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
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

    // Steps that change text work out their exact edits from the scan, so the scan is needed
    // whenever the plan has one. Without a scan, every other block still builds.
    public static string BuildLisp(Plan plan, Scan? scan = null)
    {
        if (scan is not null)
        {
            Check(plan, scan);   // lets each step prepare its edits against the drawing as it'll be
        }

        for (int i = 0; i < plan.Operations.Count; i++)
        {
            plan.Operations[i].Step = i + 1;
        }

        var results = string.Join(" ", plan.Operations.Select(op => op.ResultName()));
        var lisp = new StringBuilder();
        lisp.AppendLine(";; Generated by CADScriptBuilder");
        lisp.AppendLine(ScanLisp.Helpers);
        lisp.AppendLine(LispHelpers.Code);
        lisp.AppendLine(ScanLisp.Function);
        lisp.AppendLine($"(defun c:CSB_RUN ( / *error* csb-undo {results})");

        // If anything goes wrong part way, close the undo group so one U still undoes it all
        lisp.AppendLine("""
              (defun *error* (msg)
                (if csb-undo (command-s "_.UNDO" "_END"))
                (if (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*,*EXIT*"))
                  (princ (strcat "\nCADScriptBuilder stopped: " msg)))
                (if csb-undo (princ "\nType U once to undo the part that ran."))
                (princ))
            """);

        foreach (var op in plan.Operations)
        {
            lisp.AppendLine($"""  (princ "\n{Lisp.Escape(op.Describe())}")""");
        }

        lisp.AppendLine("""  (initget "Yes No")""");
        lisp.AppendLine("""  (if (= (getkword "\nApply changes? [Yes/No] <No>: ") "Yes")""");
        lisp.AppendLine("""    (progn""");
        lisp.AppendLine("""      (command "_.UNDO" "_BEGIN")""");
        lisp.AppendLine("""      (setq csb-undo T)""");

        foreach (var op in plan.Operations)
        {
            lisp.AppendLine($"      {op.ToLisp()}");
        }

        // Linetype scales and text styles only show once the drawing regenerates
        if (plan.Operations.Any(op => op.NeedsRegen()))
        {
            lisp.AppendLine("""      (command "_.REGENALL")""");
        }

        lisp.AppendLine("""      (command "_.UNDO" "_END")""");
        lisp.AppendLine("""      (setq csb-undo nil)""");

        // After running: a summary of what was done
        var changes = plan.Operations.Count == 1 ? "1 change" : $"{plan.Operations.Count} changes";
        lisp.AppendLine($"""      (princ "\nCADScriptBuilder: done, {changes}:")""");
        for (int i = 0; i < plan.Operations.Count; i++)
        {
            lisp.AppendLine($"      {plan.Operations[i].SummaryLisp(plan.Operations.Skip(i + 1))}");
        }
        lisp.AppendLine("""      (csb-scan "run")""");
        lisp.AppendLine("""      (princ "\nType U once to undo all of it."))""");
        lisp.AppendLine("""    (princ "\nCancelled. Nothing changed."))""");
        lisp.AppendLine("""  (princ))""");
        lisp.AppendLine("(c:CSB_RUN)");
        lisp.AppendLine("(princ)");

        return lisp.ToString();
    }
}

using Core;

if (args.Length < 2)
{
    Console.WriteLine("Usage: Generator <plan file> <output .lsp> [scan file]");
    return;
}

var planPath = args[0];
var outputPath = args[1];

// Remove any old output first, so a failed run can't leave a stale script behind
if (File.Exists(outputPath))
{
    File.Delete(outputPath);
}

try
{
    var plan = Engine.ReadPlan(planPath);

    // Check the plan against the drawing's layers, if a scan file was given
    if (args.Length >= 3)
    {
        var scan = Engine.ReadScan(args[2]);
        var result = Engine.Check(plan, scan);

        if (result.HasProblems)
        {
            Console.WriteLine($"The plan doesn't match {scan.Drawing}:");
            foreach (var problem in result.Problems)
            {
                Console.WriteLine($"  - {problem}");
            }
            Console.WriteLine("Nothing generated.");
            return;
        }

        // Notes don't stop anything, they're just worth knowing
        foreach (var note in result.Notes)
        {
            Console.WriteLine($"Note: {note}");
        }

        Console.WriteLine($"Checked against {scan.Drawing}: OK");
    }

    File.WriteAllText(outputPath, Engine.BuildLisp(plan));
    Console.WriteLine($"Wrote {outputPath}");
}
catch (InvalidDataException ex)
{
    Console.WriteLine(ex.Message);
    Console.WriteLine("Nothing generated.");
}
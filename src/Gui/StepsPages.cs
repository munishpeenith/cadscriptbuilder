using Core;

namespace Gui;

// Page 2: build the steps, see a live tick on each one, generate the .lsp.
public class StepsPage : UserControl
{
    // The drawing we synced with on page 1, and the plan being built
    private readonly Scan scan;
    private readonly Plan plan = new();

    private readonly ComboBox blockBox = new();
    private readonly Label firstLabel = new();
    private readonly TextBox firstBox = new();
    private readonly Label secondLabel = new();
    private readonly TextBox secondBox = new();
    private readonly Button addButton = new();
    private readonly ListBox stepsList = new();
    private readonly Button removeButton = new();
    private readonly TextBox statusBox = new();
    private readonly Button generateButton = new();

    public StepsPage(Scan scan)
    {
        this.scan = scan;

        // Which drawing we're working on
        Place(new Label
        {
            Text = $"Drawing: {scan.Drawing}  ({scan.Layers.Count} layers)",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            AutoSize = true
        }, 12, 12);

        // Pick a block and fill in its boxes
        Place(new Label { Text = "Block:", AutoSize = true }, 12, 48);

        blockBox.DropDownStyle = ComboBoxStyle.DropDownList;
        blockBox.Items.Add("Rename layer");
        blockBox.Items.Add("Purge layer");
        blockBox.SelectedIndex = 0;
        Place(blockBox, 60, 45, 120);

        firstLabel.AutoSize = true;
        Place(firstLabel, 195, 48);
        Place(firstBox, 245, 45, 110);

        secondLabel.Text = "To:";
        secondLabel.AutoSize = true;
        Place(secondLabel, 365, 48);
        Place(secondBox, 395, 45, 110);

        addButton.Text = "Add step";
        Place(addButton, 430, 78, 118);

        // The list of steps, each with a tick or a cross
        Place(new Label { Text = "Steps:", AutoSize = true }, 12, 115);
        stepsList.Height = 150;
        Place(stepsList, 12, 135, 410);

        removeButton.Text = "Remove step";
        Place(removeButton, 430, 135, 118);

        // Messages, then the Generate button
        statusBox.Multiline = true;
        statusBox.ReadOnly = true;
        statusBox.ScrollBars = ScrollBars.Vertical;
        statusBox.BackColor = SystemColors.Control;
        statusBox.Height = 70;
        Place(statusBox, 12, 295, 536);

        generateButton.Text = "Generate .lsp...";
        generateButton.Height = 36;
        Place(generateButton, 12, 375, 536);

        // What happens when things are clicked
        blockBox.SelectedIndexChanged += (sender, e) => UpdateBoxes();
        addButton.Click += (sender, e) => AddStep();
        removeButton.Click += (sender, e) => RemoveStep();
        generateButton.Click += (sender, e) => Generate();

        UpdateBoxes();
        RefreshSteps();
    }

    // Put a control on the page at (x, y), optionally setting its width
    private void Place(Control control, int x, int y, int width = 0)
    {
        control.Location = new Point(x, y);
        if (width > 0)
        {
            control.Width = width;
        }
        Controls.Add(control);
    }

    // Rename needs two boxes (From, To). Purge needs one (Layer).
    private void UpdateBoxes()
    {
        bool isRename = blockBox.SelectedIndex == 0;
        firstLabel.Text = isRename ? "From:" : "Layer:";
        secondLabel.Visible = isRename;
        secondBox.Visible = isRename;
    }

    private void AddStep()
    {
        var first = firstBox.Text.Trim();
        var second = secondBox.Text.Trim();

        if (blockBox.SelectedIndex == 0)
        {
            if (first == "" || second == "")
            {
                ShowError("Fill in both From and To.");
                return;
            }
            plan.Operations.Add(new RenameLayer { From = first, To = second });
        }
        else
        {
            if (first == "")
            {
                ShowError("Fill in the layer name.");
                return;
            }
            plan.Operations.Add(new PurgeLayer { Layer = first });
        }

        firstBox.Clear();
        secondBox.Clear();
        firstBox.Focus();
        RefreshSteps();
    }

    private void RemoveStep()
    {
        int i = stepsList.SelectedIndex;
        if (i < 0)
        {
            ShowError("Click a step in the list first.");
            return;
        }

        plan.Operations.RemoveAt(i);
        RefreshSteps();
    }

    // Redraw the list, checking every step against the drawing as we go.
    // Runs after every add or remove, so the ticks are always up to date.
    private void RefreshSteps()
    {
        stepsList.Items.Clear();

        // A copy of the drawing's layers that the steps can "pretend" to change
        var layers = new HashSet<string>(scan.Layers, StringComparer.OrdinalIgnoreCase);
        var allProblems = new List<string>();

        foreach (var op in plan.Operations)
        {
            var problems = op.Check(layers);
            var mark = problems.Count == 0 ? "✔" : "✖";
            stepsList.Items.Add($"{mark}  {op.Describe()}");
            allProblems.AddRange(problems);
        }

        if (plan.Operations.Count == 0)
        {
            ShowInfo("Add a step to get started.");
            generateButton.Enabled = false;
        }
        else if (allProblems.Count > 0)
        {
            ShowError(string.Join(Environment.NewLine, allProblems));
            generateButton.Enabled = false;
        }
        else
        {
            ShowSuccess($"✔ All steps match {scan.Drawing}");
            generateButton.Enabled = true;
        }
    }

    private void Generate()
    {
        // Final check with the same Engine the command line uses
        if (Engine.Check(plan, scan).Count > 0)
        {
            RefreshSteps();
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save the script",
            Filter = "AutoLISP files (*.lsp)|*.lsp",
            FileName = "csb_run.lsp"
        };
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, Engine.BuildLisp(plan));
        ShowSuccess($"Wrote {dialog.FileName}" + Environment.NewLine + "Drag it into Civil 3D to run it.");
    }

    // The message box, in red, green or grey
    private void ShowError(string message) => SetStatus(message, Color.Firebrick);
    private void ShowSuccess(string message) => SetStatus(message, Color.ForestGreen);
    private void ShowInfo(string message) => SetStatus(message, Color.Gray);

    private void SetStatus(string message, Color color)
    {
        statusBox.ForeColor = color;
        statusBox.Text = message;
    }
}
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Core;
using InfoBarSeverity = Wpf.Ui.Controls.InfoBarSeverity;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Gui;

// One line in the steps list: a tick or cross, the description, and its colour
public record StepRow(SymbolRegular Icon, string Text, Brush Color);

// Page 2: build the steps, see a live tick on each one, generate the script as a draggable tile.
public partial class StepsPage : UserControl
{
    // The drawing we synced with on page 1, and the plan being built
    private readonly Scan scan;
    private readonly Plan plan = new();

    // Where the generated script is saved (overwritten each time)
    private readonly string runScript = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CADScriptBuilder", "csb_run.lsp");

    public StepsPage(Scan scan)
    {
        InitializeComponent();
        this.scan = scan;

        DrawingText.Text = $"{scan.Drawing}  ·  {scan.Layers.Count} layers";

        BlockBox.SelectedIndex = 0;
        BlockBox.SelectionChanged += (sender, e) => UpdateBoxes();

        UpdateBoxes();
        RefreshSteps();
    }

    // Rename needs two boxes (From, To). Purge needs one (Layer).
    private void UpdateBoxes()
    {
        bool isRename = BlockBox.SelectedIndex == 0;
        FirstBox.PlaceholderText = isRename ? "From layer" : "Layer to purge";
        SecondBox.Visibility = isRename ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(FirstBox, isRename ? 1 : 2);
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var first = (FirstBox.Text ?? "").Trim();
        var second = SecondBox.Text.Trim();

        if (BlockBox.SelectedIndex == 0)
        {
            if (first == "" || second == "")
            {
                SetStatus(InfoBarSeverity.Warning, "Missing a layer name", "Fill in both From and To.");
                return;
            }
            plan.Operations.Add(new RenameLayer { From = first, To = second });
        }
        else
        {
            if (first == "")
            {
                SetStatus(InfoBarSeverity.Warning, "Missing a layer name", "Fill in the layer to purge.");
                return;
            }
            plan.Operations.Add(new PurgeLayer { Layer = first });
        }

        FirstBox.Text = "";
        SecondBox.Clear();
        FirstBox.Focus();
        RefreshSteps();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        int i = StepsList.SelectedIndex;
        if (i < 0)
        {
            SetStatus(InfoBarSeverity.Informational, "Nothing selected", "Click a step in the list first.");
            return;
        }

        plan.Operations.RemoveAt(i);
        RefreshSteps();
    }

    // Redraw the list, checking every step against the drawing as we go.
    // Runs after every add or remove, so the ticks are always up to date.
    private void RefreshSteps()
    {
        // The steps changed, so any script made earlier is out of date: remove it
        RunTile.Visibility = Visibility.Collapsed;
        if (File.Exists(runScript))
        {
            File.Delete(runScript);
        }

        // A copy of the drawing's layers that the steps can "pretend" to change
        var layers = new HashSet<string>(scan.Layers, StringComparer.OrdinalIgnoreCase);
        var rows = new List<StepRow>();
        var allProblems = new List<string>();

        var good = GetBrush("SystemFillColorSuccessBrush", Color.FromRgb(0x6C, 0xCB, 0x5F));
        var bad = GetBrush("SystemFillColorCriticalBrush", Color.FromRgb(0xFF, 0x99, 0xA4));

        foreach (var op in plan.Operations)
        {
            var problems = op.Check(layers);
            rows.Add(problems.Count == 0
                ? new StepRow(SymbolRegular.CheckmarkCircle24, op.Describe(), good)
                : new StepRow(SymbolRegular.DismissCircle24, op.Describe(), bad));
            allProblems.AddRange(problems);
        }

        StepsList.ItemsSource = rows;

                // Suggest the layers as they'll be after the steps so far, A to Z
        FirstBox.OriginalItemsSource = layers
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToList();

        if (plan.Operations.Count == 0)
        {
            SetStatus(InfoBarSeverity.Informational, "No steps yet", "Pick a block above and press Add.");
            GenerateButton.IsEnabled = false;
        }
        else if (allProblems.Count > 0)
        {
            SetStatus(InfoBarSeverity.Error, "Doesn't match the drawing", string.Join(Environment.NewLine, allProblems));
            GenerateButton.IsEnabled = false;
        }
        else
        {
            SetStatus(InfoBarSeverity.Success, "All steps match", $"Every layer checks out against {scan.Drawing}.");
            GenerateButton.IsEnabled = true;
        }
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        // Final check with the same Engine the command line uses
        if (Engine.Check(plan, scan).Count > 0)
        {
            RefreshSteps();
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(runScript)!);
        File.WriteAllText(runScript, Engine.BuildLisp(plan));

        RunTile.Visibility = Visibility.Visible;
        SetStatus(InfoBarSeverity.Success, "Script ready",
            "Drag the RUN tile into Civil 3D. Answer Yes to apply, then U once to undo.");
    }

    // Let the user drag the generated script out of the app
    private void RunTile_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !File.Exists(runScript))
        {
            return;
        }

        var data = new DataObject(DataFormats.FileDrop, new[] { runScript });
        DragDrop.DoDragDrop(RunTile, data, DragDropEffects.Copy);
    }

    private void SetStatus(InfoBarSeverity severity, string title, string message)
    {
        Status.Severity = severity;
        Status.Title = title;
        Status.Message = message;
    }

    // A colour from the current theme, or a fallback if the theme doesn't have it
    private Brush GetBrush(string key, Color fallback)
    {
        return TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
    }
}

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

// What a box suggests as you type
public enum Suggest { Nothing, Layers, Linetypes, Lineweights }

// One choice in the block dropdown: its name, the hints and suggestions for its boxes,
// and how to build the block from what's typed. SecondHint is null for one-box blocks.
public record BlockChoice(
    string Name,
    string FirstHint, Suggest FirstSuggest,
    string? SecondHint, Suggest SecondSuggest,
    Func<string, string, Operation> Make);

// Page 2: build the steps, see a live tick on each one, generate the script as a draggable tile.
public partial class StepsPage : UserControl
{
    // Tells the window "go back to the scan page" (the steps are kept)
    public event Action? ChangeDrawingClicked;

    // Every block the user can pick, in dropdown order
    private static readonly BlockChoice[] Blocks =
    [
        new("Rename layer", "From layer", Suggest.Layers, "To layer", Suggest.Nothing,
            (a, b) => new RenameLayer { From = a, To = b }),
        new("New layer", "New layer name", Suggest.Nothing, null, Suggest.Nothing,
            (a, _) => new NewLayer { Layer = a }),
        new("Purge layer", "Layer to purge", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new PurgeLayer { Layer = a }),
        new("Set current", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new SetCurrentLayer { Layer = a }),
        new("Freeze", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new FreezeLayer { Layer = a }),
        new("Thaw", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new ThawLayer { Layer = a }),
        new("Turn off", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new TurnOffLayer { Layer = a }),
        new("Turn on", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new TurnOnLayer { Layer = a }),
        new("Lock", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new LockLayer { Layer = a }),
        new("Unlock", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new UnlockLayer { Layer = a }),
        new("Set colour", "Layer", Suggest.Layers, "Colour 1-255", Suggest.Nothing,
            (a, b) => new SetLayerColour { Layer = a, Colour = b }),
        new("Set linetype", "Layer", Suggest.Layers, "Linetype", Suggest.Linetypes,
            (a, b) => new SetLayerLinetype { Layer = a, Linetype = b }),
        new("Set lineweight", "Layer", Suggest.Layers, "Lineweight mm", Suggest.Lineweights,
            (a, b) => new SetLayerLineweight { Layer = a, Lineweight = b }),
        new("Set transparency", "Layer", Suggest.Layers, "Transparency 0-90", Suggest.Nothing,
            (a, b) => new SetLayerTransparency { Layer = a, Transparency = b }),
        new("Set description", "Layer", Suggest.Layers, "Description", Suggest.Nothing,
            (a, b) => new SetLayerDescription { Layer = a, Description = b }),
        new("Plot on", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new PlotLayer { Layer = a }),
        new("Plot off", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new NoPlotLayer { Layer = a }),
    ];

    // The drawing we synced with on page 1, and the plan being built
    private Scan scan;
    private readonly Plan plan = new();

    // The pretend drawing after all the steps so far (feeds the suggestions)
    private DrawingState after;

    // Where the generated script is saved (overwritten each time)
    private readonly string runScript = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CADScriptBuilder", "csb_run.lsp");

    public StepsPage(Scan scan)
    {
        InitializeComponent();
        this.scan = scan;
        after = new DrawingState(scan);

        ShowDrawing();

        BlockBox.ItemsSource = Blocks.Select(block => block.Name).ToList();
        BlockBox.SelectedIndex = 0;
        BlockBox.SelectionChanged += (sender, e) => UpdateBoxes();

        UpdateBoxes();
        RefreshSteps();
    }

    // Back from the scan page with a new (or fresh) scan: keep the steps, re-check them
    public void UseScan(Scan newScan)
    {
        scan = newScan;
        ShowDrawing();
        RefreshSteps();
    }

    private void ShowDrawing()
    {
        ConnectedText.Text = $"Connected: {scan.Drawing}";
        DrawingText.Text = $"{scan.Layers.Count} layers in this drawing";
    }

    private void ChangeDrawing_Click(object sender, RoutedEventArgs e)
    {
        ChangeDrawingClicked?.Invoke();
    }

    private BlockChoice SelectedBlock => Blocks[BlockBox.SelectedIndex];

    // Show one or two boxes, with the right hints, for the chosen block
    private void UpdateBoxes()
    {
        var block = SelectedBlock;
        bool twoBoxes = block.SecondHint is not null;

        FirstBox.PlaceholderText = block.FirstHint;
        SecondBox.PlaceholderText = block.SecondHint ?? "";
        SecondBox.Visibility = twoBoxes ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(FirstBox, twoBoxes ? 1 : 2);

        UpdateSuggestions();
    }

    // Fill each box's suggestions from the drawing as it'll be after the steps so far
    private void UpdateSuggestions()
    {
        FirstBox.OriginalItemsSource = SuggestionsFor(SelectedBlock.FirstSuggest);
        SecondBox.OriginalItemsSource = SuggestionsFor(SelectedBlock.SecondSuggest);
    }

    private List<object> SuggestionsFor(Suggest kind)
    {
        IEnumerable<string> items = kind switch
        {
            Suggest.Layers => after.Layers.OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
            Suggest.Linetypes => after.Linetypes.OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
            Suggest.Lineweights => SetLayerLineweight.Allowed,
            _ => [],
        };
        return items.Cast<object>().ToList();
    }

    // Clicking a box shows its whole list, like a dropdown. Typing still filters it.
    private void SuggestBox_Click(object sender, MouseButtonEventArgs e)
    {
        var box = (Wpf.Ui.Controls.AutoSuggestBox)sender;

        // Ignore clicks on the list itself (picking an item), only react to clicks on the box
        if (e.OriginalSource is not Visual clicked || !box.IsAncestorOf(clicked))
        {
            return;
        }

        if (box.OriginalItemsSource.Count == 0)
        {
            return;
        }

        box.ItemsSource = box.OriginalItemsSource;
        box.IsSuggestionListOpen = true;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var block = SelectedBlock;
        var first = (FirstBox.Text ?? "").Trim();
        var second = (SecondBox.Text ?? "").Trim();

        if (first == "" || (block.SecondHint is not null && second == ""))
        {
            var what = block.SecondHint is null ? "the box" : "both boxes";
            SetStatus(InfoBarSeverity.Warning, "Something's missing", $"Fill in {what} for {block.Name}.");
            return;
        }

        plan.Operations.Add(block.Make(first, second));

        FirstBox.Text = "";
        SecondBox.Text = "";
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

    // Start again: remove every step (and the old script with them)
    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (plan.Operations.Count == 0)
        {
            return;
        }

        plan.Operations.Clear();
        RefreshSteps();
        SetStatus(InfoBarSeverity.Informational, "Cleared", "All steps removed. Ready for a new script.");
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

        // A pretend copy of the drawing that the steps change as they're checked
        var drawing = new DrawingState(scan);
        var rows = new List<StepRow>();
        var allProblems = new List<string>();

        var good = GetBrush("SystemFillColorSuccessBrush", Color.FromRgb(0x6C, 0xCB, 0x5F));
        var bad = GetBrush("SystemFillColorCriticalBrush", Color.FromRgb(0xFF, 0x99, 0xA4));

        foreach (var op in plan.Operations)
        {
            var problems = op.Check(drawing);
            rows.Add(problems.Count == 0
                ? new StepRow(SymbolRegular.CheckmarkCircle24, op.Describe(), good)
                : new StepRow(SymbolRegular.DismissCircle24, op.Describe(), bad));
            allProblems.AddRange(problems);
        }

        StepsList.ItemsSource = rows;

        after = drawing;
        UpdateSuggestions();

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
            SetStatus(InfoBarSeverity.Success, "All steps match", $"Every step checks out against {scan.Drawing}.");
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

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
// Show (optional) narrows the layer suggestions to the ones that make sense for this block.
// Pattern blocks turn into several steps when added (one per matching layer).
public record BlockChoice(
    string Name,
    string FirstHint, Suggest FirstSuggest,
    string? SecondHint, Suggest SecondSuggest,
    Func<string, string, Operation> Make,
    Func<DrawingState, string, bool>? Show = null,
    bool Pattern = false);

// Page 2: build the steps, see a live tick on each one, generate the script as a draggable tile.
public partial class StepsPage : UserControl
{
    // Tells the window "go back to the scan page" (the steps are kept)
    public event Action? ChangeDrawingClicked;

    // Every block the user can pick, in dropdown order
    private static readonly BlockChoice[] Blocks =
    [
        new("Rename layer", "From layer", Suggest.Layers, "To layer", Suggest.Nothing,
            (a, b) => new RenameLayer { From = a, To = b },
            Show: (d, l) => !LayerRules.IsProtected(l)),
        new("Rename by pattern", "Find text, e.g. C-", Suggest.Nothing, "Replace with, e.g. CIV-", Suggest.Nothing,
            (a, b) => new RenameLayer { From = a, To = b },
            Pattern: true),
        new("New layer", "New layer name", Suggest.Nothing, null, Suggest.Nothing,
            (a, _) => new NewLayer { Layer = a }),
        new("Purge layer", "Layer to purge", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new PurgeLayer { Layer = a },
            Show: (d, l) => !LayerRules.IsProtected(l) && !d.IsCurrent(l)),
        new("Set current", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new SetCurrentLayer { Layer = a },
            Show: (d, l) => !d.Frozen.Contains(l) && !d.IsCurrent(l)),
        new("Freeze", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new FreezeLayer { Layer = a },
            Show: (d, l) => !d.IsCurrent(l) && (!d.KnowsFrozen || !d.Frozen.Contains(l))),
        new("Thaw", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new ThawLayer { Layer = a },
            Show: (d, l) => !d.KnowsFrozen || d.Frozen.Contains(l)),
        new("Turn off", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new TurnOffLayer { Layer = a },
            Show: (d, l) => !d.IsCurrent(l) && (!d.KnowsOnOffLockPlot || !d.Off.Contains(l))),
        new("Turn on", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new TurnOnLayer { Layer = a },
            Show: (d, l) => !d.KnowsOnOffLockPlot || d.Off.Contains(l)),
        new("Lock", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new LockLayer { Layer = a },
            Show: (d, l) => !d.KnowsOnOffLockPlot || !d.Locked.Contains(l)),
        new("Unlock", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new UnlockLayer { Layer = a },
            Show: (d, l) => !d.KnowsOnOffLockPlot || d.Locked.Contains(l)),
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
            (a, _) => new PlotLayer { Layer = a },
            Show: (d, l) => !d.KnowsOnOffLockPlot || d.NoPlot.Contains(l)),
        new("Plot off", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new NoPlotLayer { Layer = a },
            Show: (d, l) => !d.KnowsOnOffLockPlot || !d.NoPlot.Contains(l)),
    ];

    // The drawing we synced with on page 1, and the plan being built
    private Scan scan;
    private readonly Plan plan = new();

    // The pretend drawing after all the steps so far (feeds the suggestions)
    private DrawingState after;

    // Steps that failed their check, waiting in case the user clicks "Add anyway"
    private readonly List<Operation> pendingSteps = new();

    // Layers ticked in the picker, in the order they were ticked, and their number badges
    private readonly List<string> picked = new();
    private readonly Dictionary<string, TextBlock> pickedBadges = new(StringComparer.OrdinalIgnoreCase);

    // Which step is being edited (double-click), or -1 when not editing
    private int editingIndex = -1;

    // Where a drag to reorder started
    private Point dragStart;
    private int dragFrom = -1;

    // Where saved plans go by default
    private static readonly string PlansFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CADScriptBuilder", "Plans");

    // Where scans land (written by the SCAN tile, and by every script after it runs)
    private static readonly string LayersFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CADScriptBuilder", "layers.txt");

    // The scan script, next to the .exe (copied there by Gui.csproj)
    private static readonly string ScanScript = Path.Combine(AppContext.BaseDirectory, "csb_scan.lsp");

    // Watches for new scans while this page is showing, so it re-syncs by itself
    private FileSystemWatcher? refreshWatcher;
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

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
        FirstBox.TextChanged += (sender, e) => CancelPending();
        PickerSearch.TextChanged += (sender, e) => BuildPickerList();
        SecondBox.TextChanged += (sender, e) => CancelPending();

        UpdateBoxes();
        RefreshSteps();

        refreshTimer.Tick += (sender, e) => RefreshFromScan();

        // Only watch for new scans while this page is on screen
        Loaded += (sender, e) => StartWatching();
        Unloaded += (sender, e) => StopWatching();

        // Switching to another window (e.g. Civil 3D) closes any open dropdown too
        Loaded += (sender, e) =>
        {
            var window = Window.GetWindow(this);
            if (window is not null && !watchingWindow)
            {
                window.Deactivated += (s, a) => CloseDropdowns();
                watchingWindow = true;
            }
        };
    }

    private bool watchingWindow;

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

    // ---- Refresh: re-sync with the drawing after it changes ----

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshPopup.IsOpen = !RefreshPopup.IsOpen;
    }

    // The mini SCAN tile: drag it into the drawing, like on the first page
    private void RefreshTile_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !File.Exists(ScanScript))
        {
            return;
        }

        var data = new DataObject(DataFormats.FileDrop, new[] { ScanScript });
        DragDrop.DoDragDrop(RefreshTile, data, DragDropEffects.Copy);
    }

    private void StartWatching()
    {
        if (refreshWatcher is not null)
        {
            return;
        }

        var folder = Path.GetDirectoryName(LayersFile)!;
        Directory.CreateDirectory(folder);
        refreshWatcher = new FileSystemWatcher(folder, "layers.txt");
        refreshWatcher.Created += (sender, e) => Dispatcher.Invoke(() => RestartRefreshTimer());
        refreshWatcher.Changed += (sender, e) => Dispatcher.Invoke(() => RestartRefreshTimer());
        refreshWatcher.EnableRaisingEvents = true;
    }

    private void StopWatching()
    {
        refreshTimer.Stop();
        if (refreshWatcher is not null)
        {
            refreshWatcher.EnableRaisingEvents = false;
            refreshWatcher.Dispose();
            refreshWatcher = null;
        }
    }

    // Wait half a second for Civil 3D to finish writing, then read the new scan
    private void RestartRefreshTimer()
    {
        refreshTimer.Stop();
        refreshTimer.Start();
    }

    private void RefreshFromScan()
    {
        refreshTimer.Stop();

        Scan fresh;
        try
        {
            fresh = Engine.ReadScan(LayersFile);
        }
        catch (IOException)
        {
            refreshTimer.Start();   // still being written, try again shortly
            return;
        }
        catch (InvalidDataException)
        {
            return;
        }

        RefreshPopup.IsOpen = false;
        UseScan(fresh);

        if (plan.Operations.Count == 0 || Status.Severity == InfoBarSeverity.Success)
        {
            SetStatus(InfoBarSeverity.Informational, "Drawing refreshed",
                $"Re-synced with {scan.Drawing}." + (plan.Operations.Count > 0 ? " All steps still check out." : ""));
        }
        else
        {
            SetStatus(InfoBarSeverity.Warning, "Drawing refreshed",
                "Your steps were re-checked against the updated drawing. If your script just ran, " +
                "the steps that already happened now show ✖. Press Clear all to start the next one.");
        }
    }

    private BlockChoice SelectedBlock => Blocks[BlockBox.SelectedIndex];

    // Show one or two boxes, with the right hints, for the chosen block
    private void UpdateBoxes()
    {
        CancelPending();
        var block = SelectedBlock;
        bool twoBoxes = block.SecondHint is not null;

        FirstBox.PlaceholderText = block.FirstHint;
        SecondBox.PlaceholderText = block.SecondHint ?? "";
        SecondBox.Visibility = twoBoxes ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(FirstArea, twoBoxes ? 1 : 2);

        // Picking several only makes sense for blocks that act on existing layers one at a time
        SelectButton.IsEnabled = block.FirstSuggest == Suggest.Layers && block.Name != "Rename layer" && !block.Pattern;

        UpdateSuggestions();
    }

    // Fill each box's suggestions from the drawing as it'll be after the steps so far
    private void UpdateSuggestions()
    {
        FirstBox.OriginalItemsSource = SuggestionsFor(SelectedBlock.FirstSuggest, SelectedBlock.Show);
        SecondBox.OriginalItemsSource = SuggestionsFor(SelectedBlock.SecondSuggest, null);
    }

    private List<object> SuggestionsFor(Suggest kind, Func<DrawingState, string, bool>? show)
    {
        IEnumerable<string> items = kind switch
        {
            Suggest.Layers => after.Layers
                .Where(name => show is null || show(after, name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
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

        // Clicking the box again (e.g. the arrow) while it's open closes it
        if (box.IsSuggestionListOpen)
        {
            box.IsSuggestionListOpen = false;
            return;
        }

        if (box.OriginalItemsSource.Count == 0)
        {
            return;
        }

        box.ItemsSource = box.OriginalItemsSource;
        box.IsSuggestionListOpen = true;
    }

    // A click on empty space anywhere on the page closes any open dropdown
    private void Page_MouseDown(object sender, MouseButtonEventArgs e)
    {
        CloseDropdowns();
        Keyboard.ClearFocus();
    }

    private void CloseDropdowns()
    {
        FirstBox.IsSuggestionListOpen = false;
        SecondBox.IsSuggestionListOpen = false;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var block = SelectedBlock;
        var first = (FirstBox.Text ?? "").Trim();
        var second = (SecondBox.Text ?? "").Trim();

        if (first == "" || (block.SecondHint is not null && second == "" && !block.Pattern))
        {
            var what = block.SecondHint is null || block.Pattern ? "the first box" : "both boxes";
            SetStatus(InfoBarSeverity.Warning, "Something's missing", $"Fill in {what} for {block.Name}.");
            return;
        }

        // Editing an existing step: replace it where it is
        if (editingIndex >= 0)
        {
            if (block.Pattern)
            {
                SetStatus(InfoBarSeverity.Warning, "Can't edit with a pattern",
                    "Pick a single-step block to edit this step, or press Esc to cancel.");
                return;
            }

            int index = editingIndex;
            plan.Operations[index] = block.Make(first, second);
            FirstBox.Text = "";
            SecondBox.Text = "";
            RefreshSteps();
            StepsList.SelectedIndex = index;
            return;
        }

        if (block.Pattern)
        {
            AddPatternRenames(first, second);
            return;
        }

        TryAddSteps([block.Make(first, second)]);
    }

    // "Rename by pattern": one rename step for every layer containing the text
    private void AddPatternRenames(string find, string replace)
    {
        var steps = after.Layers
            .Where(name => !LayerRules.IsProtected(name) && name.Contains(find, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new RenameLayer { From = name, To = name.Replace(find, replace, StringComparison.OrdinalIgnoreCase) })
            .Where(rename => rename.From != rename.To)
            .ToList();

        if (steps.Count == 0)
        {
            SetStatus(InfoBarSeverity.Warning, "Nothing to rename", $"No layers contain \"{find}\".");
            return;
        }

        var (added, held) = TryAddSteps(steps.Cast<Operation>().ToList());
        if (held == 0)
        {
            var preview = string.Join(", ", steps.Take(4).Select(r => $"{r.From} → {r.To}"));
            var more = steps.Count > 4 ? $" and {steps.Count - 4} more" : "";
            SetStatus(InfoBarSeverity.Success, $"Added {added} renames", preview + more + ".");
        }
    }

    // Check each step against the drawing as it'll be (including the ones just before it).
    // Steps that work, or only have notes, are added. Ones that can't work are held back.
    private (int Added, int Held) TryAddSteps(List<Operation> steps)
    {
        var trial = new DrawingState(after);
        var good = new List<Operation>();
        var held = new List<Operation>();
        var reasons = new List<string>();

        foreach (var step in steps)
        {
            var result = step.Check(trial);
            if (result.HasProblems)
            {
                held.Add(step);
                reasons.AddRange(result.Problems);
            }
            else
            {
                good.Add(step);
            }
        }

        if (good.Count > 0)
        {
            AddSteps(good);
        }

        if (held.Count > 0)
        {
            pendingSteps.Clear();
            pendingSteps.AddRange(held);
            AddAnywayButton.Visibility = Visibility.Visible;
            var title = held.Count == 1 ? "1 step not added" : $"{held.Count} steps not added";
            SetStatus(InfoBarSeverity.Error, title, string.Join(Environment.NewLine, reasons));
        }

        return (good.Count, held.Count);
    }

    // Add them even though they can't work yet (e.g. they're meant for a different drawing)
    private void AddAnyway_Click(object sender, RoutedEventArgs e)
    {
        if (pendingSteps.Count > 0)
        {
            AddSteps(pendingSteps.ToList());
        }
    }

    private void AddSteps(List<Operation> steps)
    {
        plan.Operations.AddRange(steps);

        FirstBox.Text = "";
        SecondBox.Text = "";
        FirstBox.Focus();
        RefreshSteps();
    }

    // Steps waiting for "Add anyway" are forgotten as soon as anything changes
    private void CancelPending()
    {
        pendingSteps.Clear();
        AddAnywayButton.Visibility = Visibility.Collapsed;
    }

    // ---- The multi-select picker ----

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        picked.Clear();
        PickerSearch.Text = "";
        BuildPickerList();
        PickerPopup.IsOpen = true;
    }

    // Fill the picker with this block's layers (filtered by the search box), keeping what's ticked
    private void BuildPickerList()
    {
        PickerList.Children.Clear();
        pickedBadges.Clear();

        var search = (PickerSearch.Text ?? "").Trim();
        var layers = SuggestionsFor(SelectedBlock.FirstSuggest, SelectedBlock.Show)
            .Cast<string>()
            .Where(name => search == "" || name.Contains(search, StringComparison.OrdinalIgnoreCase));

        foreach (var name in layers)
        {
            var badge = new TextBlock
            {
                Width = 24,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center
            };
            badge.SetResourceReference(TextBlock.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
            pickedBadges[name] = badge;

            var box = new CheckBox { Content = name, IsChecked = picked.Contains(name), Margin = new Thickness(0, 2, 0, 2) };
            box.Checked += (sender, e) => { if (!picked.Contains(name)) picked.Add(name); UpdatePicker(); };
            box.Unchecked += (sender, e) => { picked.Remove(name); UpdatePicker(); };

            var row = new DockPanel();
            DockPanel.SetDock(badge, Dock.Left);
            row.Children.Add(badge);
            row.Children.Add(box);
            PickerList.Children.Add(row);
        }

        UpdatePicker();
    }

    // Show 1, 2, 3... beside the ticked layers, and the count on the Add button
    private void UpdatePicker()
    {
        foreach (var (name, badge) in pickedBadges)
        {
            int position = picked.FindIndex(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
            badge.Text = position >= 0 ? (position + 1).ToString() : "";
        }

        PickerAddButton.IsEnabled = picked.Count > 0;
        PickerAddButton.Content = picked.Count switch
        {
            0 => "Add",
            1 => "Add 1 step",
            _ => $"Add {picked.Count} steps"
        };
        PickerCount.Text = picked.Count == 0 ? "Tick layers in the order you want them" : $"{picked.Count} selected";
    }

    private void PickerAdd_Click(object sender, RoutedEventArgs e)
    {
        var block = SelectedBlock;
        var second = (SecondBox.Text ?? "").Trim();

        if (block.SecondHint is not null && second == "")
        {
            PickerPopup.IsOpen = false;
            SetStatus(InfoBarSeverity.Warning, "Something's missing",
                $"Fill in {block.SecondHint} first. It's used for every layer you picked.");
            return;
        }

        var steps = picked.Select(name => block.Make(name, second)).ToList();
        picked.Clear();
        PickerPopup.IsOpen = false;
        TryAddSteps(steps);
    }

    private void PickerCancel_Click(object sender, RoutedEventArgs e)
    {
        picked.Clear();
        PickerPopup.IsOpen = false;
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e) => RemoveSelected();

    private void RemoveSelected()
    {
        int i = StepsList.SelectedIndex;
        if (i < 0)
        {
            SetStatus(InfoBarSeverity.Informational, "Nothing selected", "Click a step in the list first.");
            return;
        }

        plan.Operations.RemoveAt(i);
        RefreshSteps();
        StepsList.SelectedIndex = Math.Min(i, plan.Operations.Count - 1);
    }

    // ---- The steps list: Delete key, double-click to edit, drag to reorder ----

    private void StepsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            RemoveSelected();
            e.Handled = true;
        }
    }

    private void StepsList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        int i = StepsList.SelectedIndex;
        if (i < 0)
        {
            return;
        }

        // Load the step into the boxes above; Add becomes Update
        var step = plan.Operations[i];
        int blockIndex = Array.FindIndex(Blocks, b => !b.Pattern && b.Make("x", "y").GetType() == step.GetType());
        if (blockIndex < 0)
        {
            return;
        }

        BlockBox.SelectedIndex = blockIndex;
        var (first, second) = ValuesOf(step);
        FirstBox.Text = first;
        SecondBox.Text = second;
        CloseDropdowns();

        editingIndex = i;
        AddButton.Content = "Update";
        SetStatus(InfoBarSeverity.Informational, $"Editing step {i + 1}",
            "Change it above and press Update. Esc cancels.");
    }

    // The values a step was made from, to put back in the boxes when editing
    private static (string First, string Second) ValuesOf(Operation step) => step switch
    {
        RenameLayer r => (r.From, r.To),
        NewLayer n => (n.Layer, ""),
        PurgeLayer p => (p.Layer, ""),
        SetLayerColour c => (c.Layer, c.Colour),
        SetLayerLinetype t => (t.Layer, t.Linetype),
        SetLayerLineweight w => (w.Layer, w.Lineweight),
        SetLayerTransparency t => (t.Layer, t.Transparency),
        SetLayerDescription d => (d.Layer, d.Description),
        LayerSetting other => (other.Layer, ""),
        _ => ("", "")
    };

    private void EndEdit()
    {
        editingIndex = -1;
        AddButton.Content = "Add";
    }

    // Esc: stop editing, and close any dropdowns
    private void Page_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CloseDropdowns();
        if (editingIndex >= 0)
        {
            FirstBox.Text = "";
            SecondBox.Text = "";
            RefreshSteps();
            e.Handled = true;
        }
    }

    private void StepsList_MouseDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(null);
        dragFrom = IndexOfItemAt(e.OriginalSource as DependencyObject);
    }

    private void StepsList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || dragFrom < 0)
        {
            return;
        }

        // Only start a drag once the mouse has actually moved a little
        var now = e.GetPosition(null);
        if (Math.Abs(now.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(now.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        int from = dragFrom;
        dragFrom = -1;
        DragDrop.DoDragDrop(StepsList, new DataObject("csb-step", from), DragDropEffects.Move);
    }

    private void StepsList_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("csb-step"))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Highlight the step it'll land beside
        e.Effects = DragDropEffects.Move;
        StepsList.SelectedIndex = Math.Min(DropIndex(e), plan.Operations.Count - 1);
        e.Handled = true;
    }

    private void StepsList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("csb-step"))
        {
            return;
        }

        int from = (int)e.Data.GetData("csb-step");
        int to = DropIndex(e);
        if (to > from)
        {
            to--;   // removing it first shifts everything after it up by one
        }
        if (to == from)
        {
            return;
        }

        var step = plan.Operations[from];
        plan.Operations.RemoveAt(from);
        plan.Operations.Insert(to, step);
        RefreshSteps();
        StepsList.SelectedIndex = to;
    }

    // Which step (if any) a point in the list belongs to
    private int IndexOfItemAt(DependencyObject? element)
    {
        if (element is not Visual)
        {
            return -1;
        }
        return ItemsControl.ContainerFromElement(StepsList, element) is ListViewItem item
            ? StepsList.ItemContainerGenerator.IndexFromContainer(item)
            : -1;
    }

    // Where a dropped step goes: before the step under the mouse, or after it if in its lower half
    private int DropIndex(DragEventArgs e)
    {
        var element = StepsList.InputHitTest(e.GetPosition(StepsList)) as DependencyObject;
        if (element is Visual && ItemsControl.ContainerFromElement(StepsList, element) is ListViewItem item)
        {
            int index = StepsList.ItemContainerGenerator.IndexFromContainer(item);
            return e.GetPosition(item).Y > item.ActualHeight / 2 ? index + 1 : index;
        }
        return plan.Operations.Count;
    }

    // ---- Save and open plans ----

    private void SavePlan_Click(object sender, RoutedEventArgs e)
    {
        if (plan.Operations.Count == 0)
        {
            SetStatus(InfoBarSeverity.Informational, "Nothing to save", "Add some steps first.");
            return;
        }

        Directory.CreateDirectory(PlansFolder);
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save plan",
            Filter = "CADScriptBuilder plans (*.json)|*.json",
            InitialDirectory = PlansFolder,
            FileName = "my-plan.json"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        Engine.SavePlan(plan, dialog.FileName);
        SetStatus(InfoBarSeverity.Success, "Plan saved",
            $"Saved {plan.Operations.Count} steps as {Path.GetFileName(dialog.FileName)}. Open it on any drawing.");
    }

    private void OpenPlan_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(PlansFolder);
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open plan",
            Filter = "CADScriptBuilder plans (*.json)|*.json",
            InitialDirectory = PlansFolder
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var loaded = Engine.ReadPlan(dialog.FileName);
            plan.AskBeforeRunning = loaded.AskBeforeRunning;
            plan.Operations.Clear();
            plan.Operations.AddRange(loaded.Operations);
            RefreshSteps();
        }
        catch (InvalidDataException ex)
        {
            SetStatus(InfoBarSeverity.Error, "Couldn't open that plan", ex.Message);
        }
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

        CancelPending();
        EndEdit();

        // A pretend copy of the drawing that the steps change as they're checked
        var drawing = new DrawingState(scan);
        var rows = new List<StepRow>();
        var problems = new List<string>();
        var notes = new List<string>();

        var good = GetBrush("SystemFillColorSuccessBrush", Color.FromRgb(0x6C, 0xCB, 0x5F));
        var careful = GetBrush("SystemFillColorCautionBrush", Color.FromRgb(0xFC, 0xE1, 0x00));
        var bad = GetBrush("SystemFillColorCriticalBrush", Color.FromRgb(0xFF, 0x99, 0xA4));

        int number = 0;
        foreach (var op in plan.Operations)
        {
            number++;
            var result = op.Check(drawing);

            if (result.HasProblems)
            {
                rows.Add(new StepRow(SymbolRegular.DismissCircle24, op.Describe(), bad));
            }
            else if (result.HasNotes)
            {
                rows.Add(new StepRow(SymbolRegular.Warning24, op.Describe(), careful));
            }
            else
            {
                rows.Add(new StepRow(SymbolRegular.CheckmarkCircle24, op.Describe(), good));
            }

            problems.AddRange(result.Problems.Select(p => $"Step {number}: {p}"));
            notes.AddRange(result.Notes.Select(n => $"Step {number}: {n}"));
        }

        StepsList.ItemsSource = rows;

        after = drawing;
        UpdateSuggestions();

        var nl = Environment.NewLine;
        if (plan.Operations.Count == 0)
        {
            SetStatus(InfoBarSeverity.Informational, "No steps yet", "Pick a block above and press Add.");
            GenerateButton.IsEnabled = false;
        }
        else if (problems.Count > 0)
        {
            // Problems first, then any notes underneath
            SetStatus(InfoBarSeverity.Error, "Doesn't match the drawing", string.Join(nl, problems.Concat(notes)));
            GenerateButton.IsEnabled = false;
        }
        else if (notes.Count > 0)
        {
            var title = notes.Count == 1 ? "1 thing to check" : $"{notes.Count} things to check";
            SetStatus(InfoBarSeverity.Warning, title, string.Join(nl, notes));
            GenerateButton.IsEnabled = true;
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
        if (Engine.Check(plan, scan).HasProblems)
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
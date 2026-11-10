using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Core;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;
using InfoBarSeverity = Wpf.Ui.Controls.InfoBarSeverity;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Gui;

// One line in the steps list: a tick or cross, the description, and its colour
public record StepRow(SymbolRegular Icon, string Text, Brush Color);

// What a box suggests as you type
public enum Suggest { Nothing, Layers, Linetypes, Lineweights, OnOff, TextStyles, Fonts }

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

        // Linetype scale
        new("Set linetype scale", "Layer", Suggest.Layers, "Scale, e.g. 0.5", Suggest.Nothing,
            (a, b) => new SetLayerLinetypeScale { Layer = a, Scale = b },
            Show: (d, l) => !d.Locked.Contains(l) && d.CountOf(l) != 0 && d.IsPatterned(l) != false),
        new("Drawing linetype scale (LTSCALE)", "Scale, e.g. 1", Suggest.Nothing, null, Suggest.Nothing,
            (a, _) => new SetLinetypeSetting { Setting = "LTSCALE", Value = a }),
        new("New object linetype scale (CELTSCALE)", "Scale, e.g. 1", Suggest.Nothing, null, Suggest.Nothing,
            (a, _) => new SetLinetypeSetting { Setting = "CELTSCALE", Value = a }),
        new("Viewport linetype scaling (PSLTSCALE)", "On or Off", Suggest.OnOff, null, Suggest.Nothing,
            (a, _) => new SetLinetypeSetting { Setting = "PSLTSCALE", Value = a }),
        new("Model space linetype scaling (MSLTSCALE)", "On or Off", Suggest.OnOff, null, Suggest.Nothing,
            (a, _) => new SetLinetypeSetting { Setting = "MSLTSCALE", Value = a }),

        // Fonts and text (Civil 3D labels use text styles, so their fonts follow too)
        new("Change text style font", "Text style", Suggest.TextStyles, "Font, e.g. Arial or romans.shx", Suggest.Fonts,
            (a, b) => new SetTextStyleFont { Style = a, Font = b }),
        new("Set text height", "Layer", Suggest.Layers, "Height, e.g. 2.5", Suggest.Nothing,
            (a, b) => new SetTextHeight { Layer = a, Height = b },
            Show: (d, l) => !d.Locked.Contains(l) && (!d.KnowsContents || d.TextsOn(l).Any())),
        new("Set text style on layer", "Layer", Suggest.Layers, "Text style", Suggest.TextStyles,
            (a, b) => new SetTextStyleOnLayer { Layer = a, Style = b },
            Show: (d, l) => !d.Locked.Contains(l) && (!d.KnowsContents || d.TextsOn(l).Any())),

        // Changing the words in the drawing's text
        new("Replace text", "Find (exact capitals), e.g. EXISTNG", Suggest.Nothing, "Replace with, e.g. EXISTING", Suggest.Nothing,
            (a, b) => new ReplaceText { Find = a, Replace = b }),
        new("Make text capitals", "Layer", Suggest.Layers, null, Suggest.Nothing,
            (a, _) => new UpperCaseText { Layer = a },
            Show: (d, l) => !d.Locked.Contains(l) && (!d.KnowsContents || d.TextsOn(l).Any())),
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

    // The scan script, written out to Documents by the app when it starts
    private static readonly string ScanScript = ScanFile.Path;

    // The ⚙ choices, and the blocks they leave showing in the dropdown
    private readonly Settings settings = Settings.Load();
    private List<BlockChoice> shownBlocks = [];
    private BlockChoice? tempShown;   // a hidden block shown just while editing one of its steps

    // What a script saves after running (its summary), read back for the history panel
    private static readonly string LastRunFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CADScriptBuilder", "last-run.txt");

    // The script made most recently: its tag, and its steps (in case the run's summary can't be read)
    private string? lastRunId;
    private List<string> lastRunSteps = [];
    private string? handledRunId;
    private readonly DispatcherTimer runLogTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

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

        ShowBlocks();
        UpdateAiButton();
        BlockBox.SelectionChanged += (sender, e) => UpdateBoxes();
        FirstBox.TextChanged += (sender, e) => CancelPending();
        PickerSearch.TextChanged += (sender, e) => BuildPickerList();
        SecondBox.TextChanged += (sender, e) => CancelPending();

        UpdateBoxes();
        RefreshSteps();

        refreshTimer.Tick += (sender, e) => RefreshFromScan();
        runLogTimer.Tick += (sender, e) => CheckRunLog();

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

    // ---- Settings (the ⚙, bottom right) ----

    // Fill the block dropdown with the blocks that aren't hidden, keeping the current pick if we can
    private void ShowBlocks()
    {
        var picked = BlockBox.SelectedIndex >= 0 && BlockBox.SelectedIndex < shownBlocks.Count
            ? shownBlocks[BlockBox.SelectedIndex]
            : null;

        shownBlocks = Blocks.Where(b => !settings.HiddenBlocks.Contains(b.Name) || b == tempShown).ToList();
        if (shownBlocks.Count == 0)
        {
            shownBlocks = Blocks.ToList();
        }

        BlockBox.ItemsSource = shownBlocks.Select(b => b.Name).ToList();
        BlockBox.SelectedIndex = picked is null ? 0 : Math.Max(shownBlocks.IndexOf(picked), 0);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        PopupToggle.Toggle(SettingsPopup, beforeOpen: () =>
        {
            SettingsPanel.Children.Clear();
            BuildSettings();
        });
    }

    // Built in code, like the help: one checkbox per block, then the advanced options.
    // Every change is saved straight away.
    private void BuildSettings()
    {
        AddSettingsText("Settings", 16, bold: true, top: 0);

        AddSettingsText("Blocks", 13, bold: true, top: 12);
        AddSettingsText("Untick the blocks you don't use to hide them from the list.", 12, bold: false, top: 0);

        var boxes = new List<CheckBox>();
        foreach (var block in Blocks)
        {
            var box = new CheckBox
            {
                Content = block.Name,
                IsChecked = !settings.HiddenBlocks.Contains(block.Name),
                Margin = new Thickness(0, 2, 0, 0)
            };
            box.Click += (sender, e) =>
            {
                // Always keep at least one block
                if (box.IsChecked != true && boxes.Count(b => b.IsChecked == true) == 0)
                {
                    box.IsChecked = true;
                    return;
                }

                settings.HiddenBlocks.Remove(block.Name);
                if (box.IsChecked != true)
                {
                    settings.HiddenBlocks.Add(block.Name);
                }
                settings.Save();
                ShowBlocks();
            };
            boxes.Add(box);
            SettingsPanel.Children.Add(box);
        }

        AddSettingsText("Advanced", 13, bold: true, top: 16);
        AddSettingsOption("Clear steps after a run",
            "Once a script runs in Civil 3D, start a fresh list by itself. Off: Clear all lights up instead.",
            settings.ClearAfterRun, on => settings.ClearAfterRun = on);
        AddSettingsOption("Start pinned on top",
            "Open the window on top of Civil 3D. Takes effect next time the app opens.",
            settings.StartPinned, on => settings.StartPinned = on);

        AddSettingsText("Standards", 13, bold: true, top: 16);
        AddSettingsText("Layer naming standard for the standards check:", 12, bold: false, top: 0);

        var names = NamingStandard.Presets.Select(p => $"{p.Name} (e.g. {p.Example})").Append("Custom pattern").ToList();
        var standards = new ComboBox { ItemsSource = names, Margin = new Thickness(0, 2, 0, 4) };
        int chosen = Array.FindIndex(NamingStandard.Presets, p => p.Name == settings.NamingStandard);
        standards.SelectedIndex = settings.NamingStandard == "Custom pattern" ? names.Count - 1 : Math.Max(chosen, 0);
        SettingsPanel.Children.Add(standards);

        var pattern = new TextBox
        {
            Text = settings.CustomPattern,
            Margin = new Thickness(0, 2, 0, 2),
            ToolTip = "A regular expression every layer name must match, e.g. ^[A-Z]-[A-Z0-9-]+$"
        };
        var patternNote = AddSettingsText("", 12, bold: false, top: 0);
        SettingsPanel.Children.Remove(patternNote);
        SettingsPanel.Children.Add(pattern);
        SettingsPanel.Children.Add(patternNote);

        void ShowPattern()
        {
            bool custom = standards.SelectedIndex == names.Count - 1;
            pattern.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
            patternNote.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
            patternNote.Text = NamingStandard.IsValidPattern(pattern.Text)
                ? "A regular expression every layer name must match."
                : "That pattern isn't valid yet, so names aren't checked against it.";
        }

        standards.SelectionChanged += (sender, e) =>
        {
            settings.NamingStandard = standards.SelectedIndex == names.Count - 1
                ? "Custom pattern"
                : NamingStandard.Presets[Math.Max(standards.SelectedIndex, 0)].Name;
            settings.Save();
            ShowPattern();
        };
        pattern.TextChanged += (sender, e) =>
        {
            settings.CustomPattern = pattern.Text;
            settings.Save();
            ShowPattern();
        };
        ShowPattern();

        AddSettingsText("AI mode", 13, bold: true, top: 16);
        AddSettingsOption("Allow AI mode",
            "Shows the ✨ Ask AI button. It only connects when you press Connect. Your request and this drawing's " +
            "layer names, linetypes and text style names are sent to Anthropic; never the drawing, its geometry or its text.",
            settings.AiEnabled, on => { settings.AiEnabled = on; UpdateAiButton(); });

        if (Settings.KeyFromEnvironment)
        {
            AddSettingsText("API key: using ANTHROPIC_API_KEY from your environment.", 12, bold: false, top: 4);
        }
        else
        {
            AddSettingsText("Anthropic API key (saved encrypted for your Windows account):", 12, bold: false, top: 4);
            var key = new PasswordBox { Margin = new Thickness(0, 2, 0, 2) };
            if (settings.ApiKeyProtected != "")
            {
                key.Password = "saved-key-hidden";
            }
            key.PasswordChanged += (sender, e) =>
            {
                if (key.Password != "saved-key-hidden")
                {
                    settings.SetApiKey(key.Password);
                    settings.Save();
                }
            };
            SettingsPanel.Children.Add(key);
        }

        AddSettingsText("Model:", 12, bold: false, top: 4);
        var model = new TextBox { Text = settings.AiModel, Margin = new Thickness(0, 2, 0, 2) };
        model.TextChanged += (sender, e) =>
        {
            settings.AiModel = model.Text.Trim() == "" ? AiPlanner.DefaultModel : model.Text.Trim();
            settings.Save();
        };
        SettingsPanel.Children.Add(model);

        if (settings.IgnoredWords.Count > 0)
        {
            var ignored = AddSettingsText($"{settings.IgnoredWords.Count} words ignored by the spelling check.", 12, bold: false, top: 6);
            var clear = new Wpf.Ui.Controls.Button { Content = "Clear ignored words", Margin = new Thickness(0, 2, 0, 0) };
            clear.Click += (sender, e) =>
            {
                settings.IgnoredWords.Clear();
                settings.Save();
                ignored.Text = "No words ignored.";
                clear.IsEnabled = false;
            };
            SettingsPanel.Children.Add(clear);
        }
    }

    private void AddSettingsOption(string name, string detail, bool isOn, Action<bool> set)
    {
        var box = new CheckBox { Content = name, IsChecked = isOn, Margin = new Thickness(0, 6, 0, 0) };
        box.Click += (sender, e) =>
        {
            set(box.IsChecked == true);
            settings.Save();
        };
        SettingsPanel.Children.Add(box);

        var text = AddSettingsText(detail, 12, bold: false, top: 0);
        text.Margin = new Thickness(28, 0, 0, 0);
    }

    private TextBlock AddSettingsText(string text, double size, bool bold, double top)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, top, 0, 4)
        };
        block.SetResourceReference(TextBlock.ForegroundProperty,
            bold ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        SettingsPanel.Children.Add(block);
        return block;
    }

    // ---- AI mode: describe it, the AI proposes steps, the engine checks them ----

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };

    // Connected only for this session, and only after the user says Yes
    private bool aiConnected;
    private bool aiBusy;
    private TextBox? aiRequest;
    private TextBlock? aiStatus;

    private static readonly string AiHistoryFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CADScriptBuilder", "ai-history.jsonl");

    private void UpdateAiButton()
    {
        AiButton.Visibility = settings.AiEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (!settings.AiEnabled)
        {
            aiConnected = false;
            AiPopup.IsOpen = false;
        }
    }

    private void AiButton_Click(object sender, RoutedEventArgs e)
    {
        PopupToggle.Toggle(AiPopup, beforeOpen: BuildAiPanel);
    }

    private void BuildAiPanel()
    {
        AiPanel.Children.Clear();
        AddAiText("Ask AI", 16, bold: true);

        if (!aiConnected)
        {
            AddAiText("Describe what you want, e.g. \"freeze all the C-TEMP layers and make the road text 2.5 high\". " +
                      "The AI proposes steps from the same blocks you use. It never writes the script.", 12, bold: false);
            AddAiText("Connecting sends your request and this drawing's layer names, linetypes and text style names to " +
                      "Anthropic. The drawing file, its geometry and its text are never sent.", 12, bold: false);

            var connect = new Wpf.Ui.Controls.Button
            {
                Content = "Connect",
                Appearance = ControlAppearance.Primary,
                Margin = new Thickness(0, 8, 0, 0)
            };
            connect.Click += (sender, e) => ConnectAi();
            AiPanel.Children.Add(connect);
            return;
        }

        aiRequest ??= new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80,
            Margin = new Thickness(0, 4, 0, 6)
        };
        AiPanel.Children.Add(aiRequest);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var propose = new Wpf.Ui.Controls.Button
        {
            Content = "Propose steps",
            Appearance = ControlAppearance.Primary,
            IsEnabled = !aiBusy
        };
        propose.Click += async (sender, e) => await ProposeSteps(propose);
        var history = new Wpf.Ui.Controls.Button
        {
            Content = "History",
            Appearance = ControlAppearance.Transparent,
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = "Every request and what the AI proposed, kept in Documents\\CADScriptBuilder\\ai-history.jsonl"
        };
        history.Click += (sender, e) => OpenAiHistory();
        var disconnect = new Wpf.Ui.Controls.Button
        {
            Content = "Disconnect",
            Appearance = ControlAppearance.Transparent,
            Margin = new Thickness(8, 0, 0, 0)
        };
        disconnect.Click += (sender, e) =>
        {
            aiConnected = false;
            BuildAiPanel();
        };
        buttons.Children.Add(propose);
        buttons.Children.Add(history);
        buttons.Children.Add(disconnect);
        AiPanel.Children.Add(buttons);

        aiStatus = AddAiText(aiStatus?.Text ?? "Connected. Steps it proposes are checked like any other, and held back if they can't work.",
            12, bold: false);
        aiStatus.Margin = new Thickness(0, 8, 0, 0);
        aiRequest.Focus();
    }

    // The yes/no gate: nothing is sent until the user agrees, once per session
    private void ConnectAi()
    {
        if (settings.GetApiKey() is null)
        {
            AiPanel.Children.Clear();
            AddAiText("Ask AI", 16, bold: true);
            AddAiText("Add your Anthropic API key in ⚙ Settings (AI mode) first, then press ✨ again.", 12, bold: false);
            return;
        }

        var answer = MessageBox.Show(
            "Connect AI mode for this session?\n\n" +
            "Each request sends what you type, plus this drawing's layer names and states, linetypes, " +
            "text style names and linetype settings, to Anthropic's API.\n\n" +
            "Never sent: the drawing file, its geometry, or the words in its text.\n\n" +
            "The AI only proposes steps. They're checked here and previewed in Civil 3D before anything changes.",
            "CADScriptBuilder: AI mode", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        aiConnected = true;
        BuildAiPanel();
        AiPopup.IsOpen = true;
    }

    private async Task ProposeSteps(Wpf.Ui.Controls.Button propose)
    {
        var request = (aiRequest?.Text ?? "").Trim();
        var key = settings.GetApiKey();
        if (request == "" || aiBusy || key is null || aiStatus is null)
        {
            return;
        }

        aiBusy = true;
        propose.IsEnabled = false;
        aiStatus.Text = "Thinking…";

        string explanation = "";
        string reply = "";
        var steps = new List<Operation>();
        int added = 0, held = 0;
        try
        {
            var body = AiPlanner.BuildRequest(request, after, settings.AiModel);
            using var message = new HttpRequestMessage(HttpMethod.Post, AiPlanner.Endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            message.Headers.Add("x-api-key", key);
            message.Headers.Add("anthropic-version", AiPlanner.ApiVersion);

            using var response = await Http.SendAsync(message);
            reply = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new InvalidDataException("The API key wasn't accepted. Check it in ⚙ Settings.");
            }

            (steps, explanation, var skipped) = AiPlanner.ParseResponse(reply);
            if (steps.Count > 0)
            {
                (added, held) = TryAddSteps(steps);
            }

            var parts = new List<string> { explanation };
            parts.Add(steps.Count == 0 ? "No steps proposed." :
                $"Added {added} {(added == 1 ? "step" : "steps")}" + (held > 0 ? $", {held} held back (see the message below the list)." : "."));
            if (skipped.Count > 0)
            {
                parts.Add($"Ignored {skipped.Count} unknown {(skipped.Count == 1 ? "step" : "steps")}.");
            }
            aiStatus.Text = string.Join(" ", parts.Where(p => p != ""));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            aiStatus.Text = "Couldn't reach the AI service. Check your connection (or your firewall), then try again.";
        }
        catch (InvalidDataException ex)
        {
            aiStatus.Text = ex.Message;
        }
        finally
        {
            aiBusy = false;
            propose.IsEnabled = true;
        }

        LogAi(request, explanation, steps, added, held);
    }

    // The history tracker: one line per request, so there's a record of what the AI suggested
    private void LogAi(string request, string explanation, List<Operation> steps, int added, int held)
    {
        History.Add("AI", $"AI proposed {steps.Count} {(steps.Count == 1 ? "step" : "steps")} ({added} added{(held > 0 ? $", {held} held back" : "")})",
            $"Asked: {request}", explanation);
        try
        {
            var proposed = new Plan();
            proposed.Operations.AddRange(steps);
            var line = new JsonObject
            {
                ["time"] = DateTime.Now.ToString("s"),
                ["drawing"] = scan.Drawing,
                ["model"] = settings.AiModel,
                ["request"] = request,
                ["explanation"] = explanation,
                ["added"] = added,
                ["heldBack"] = held,
                ["proposed"] = JsonNode.Parse(Engine.PlanToJson(proposed))
            };
            Directory.CreateDirectory(Path.GetDirectoryName(AiHistoryFile)!);
            File.AppendAllText(AiHistoryFile, line.ToJsonString() + Environment.NewLine);
        }
        catch (IOException)
        {
            // The log is a convenience: never let it stop the work
        }
    }

    private void OpenAiHistory()
    {
        if (!File.Exists(AiHistoryFile))
        {
            if (aiStatus is not null)
            {
                aiStatus.Text = "No history yet.";
            }
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AiHistoryFile) { UseShellExecute = true });
    }

    private TextBlock AddAiText(string text, double size, bool bold)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };
        block.SetResourceReference(TextBlock.ForegroundProperty,
            bold ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        AiPanel.Children.Add(block);
        return block;
    }

    // ---- Standards check: layer naming and spelling ----

    private void StandardsButton_Click(object sender, RoutedEventArgs e)
    {
        PopupToggle.Toggle(StandardsPopup, beforeOpen: BuildStandards);
    }

    // Checks the drawing as it'll be after the steps so far, so fixed things drop off the list
    private void BuildStandards()
    {
        StandardsPanel.Children.Clear();
        var naming = NamingStandard.Find(settings.NamingStandard, settings.CustomPattern);
        var findings = StandardsCheck.Run(after, naming, settings.IgnoredWords);

        AddStandardsText("Standards check", 16, bold: true);
        AddStandardsText($"Layer naming: {naming.Name}. Change it in ⚙ Settings. Spelling: British English, offline.", 12, bold: false);
        if (!after.KnowsContents)
        {
            AddStandardsText("Rescan the drawing (↻) to check the spelling of its text too.", 12, bold: false);
        }

        if (findings.Count == 0)
        {
            AddStandardsText("✔  Everything checks out.", 13, bold: true).Margin = new Thickness(0, 12, 0, 0);
            return;
        }

        var fixable = findings.Where(f => f.Fix is not null).ToList();
        var summary = AddStandardsText(
            $"{findings.Count(f => f.Kind == "Naming")} naming, {findings.Count(f => f.Kind == "Spelling")} spelling. " +
            "Fixes are added as steps, so you still see them all before anything runs.", 12, bold: false);
        summary.Margin = new Thickness(0, 8, 0, 4);

        if (fixable.Count > 1)
        {
            var all = new Wpf.Ui.Controls.Button
            {
                Content = $"Add all {fixable.Count} fixes",
                Appearance = ControlAppearance.Primary,
                Margin = new Thickness(0, 4, 0, 8)
            };
            all.Click += (sender, e) =>
            {
                TryAddSteps(fixable.Select(f => f.Fix!).ToList());
                BuildStandards();
            };
            StandardsPanel.Children.Add(all);
        }

        foreach (var finding in findings)
        {
            var row = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            DockPanel.SetDock(buttons, Dock.Right);

            if (finding.Fix is not null)
            {
                var fix = new Wpf.Ui.Controls.Button { Content = "Fix", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 4, 10, 4) };
                fix.Click += (sender, e) =>
                {
                    TryAddSteps([finding.Fix]);
                    BuildStandards();
                };
                buttons.Children.Add(fix);
            }
            if (finding.IgnoreWord is not null)
            {
                var ignore = new Wpf.Ui.Controls.Button
                {
                    Content = "Ignore",
                    Appearance = ControlAppearance.Transparent,
                    Margin = new Thickness(4, 0, 0, 0),
                    Padding = new Thickness(10, 4, 10, 4),
                    ToolTip = "Accept this word from now on (e.g. a project or place name)"
                };
                ignore.Click += (sender, e) =>
                {
                    settings.IgnoredWords.Add(finding.IgnoreWord);
                    settings.Save();
                    BuildStandards();
                };
                buttons.Children.Add(ignore);
            }

            var text = new TextBlock
            {
                Text = (finding.Kind == "Naming" ? "Layer  ·  " : "Spelling  ·  ") + finding.Message,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");

            row.Children.Add(buttons);
            row.Children.Add(text);
            StandardsPanel.Children.Add(row);
        }
    }

    private TextBlock AddStandardsText(string text, double size, bool bold)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };
        block.SetResourceReference(TextBlock.ForegroundProperty,
            bold ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        StandardsPanel.Children.Add(block);
        return block;
    }

    // ---- Refresh: re-sync with the drawing after it changes ----

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        PopupToggle.Toggle(RefreshPopup);
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
        refreshWatcher = new FileSystemWatcher(folder, "*.txt");
        refreshWatcher.Created += (sender, e) => Dispatcher.Invoke(() => FileChanged(e.Name));
        refreshWatcher.Changed += (sender, e) => Dispatcher.Invoke(() => FileChanged(e.Name));
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

    // layers.txt means a new scan; last-run.txt means a script finished (or was cancelled)
    private void FileChanged(string? name)
    {
        if (string.Equals(name, "layers.txt", StringComparison.OrdinalIgnoreCase))
        {
            RestartRefreshTimer();
        }
        else if (string.Equals(name, "last-run.txt", StringComparison.OrdinalIgnoreCase))
        {
            runLogTimer.Stop();
            runLogTimer.Start();
        }
    }

    // Typing No in Civil 3D leaves the drawing alone, so there's no rescan: the run log says so instead
    private void CheckRunLog()
    {
        runLogTimer.Stop();
        var log = RunLog.Read(LastRunFile);
        if (log is null || !log.Cancelled || log.RunId != lastRunId || handledRunId == log.RunId)
        {
            return;
        }
        handledRunId = log.RunId;
        History.Add("Cancelled", "Cancelled in Civil 3D", "Nothing changed.");
        History.SetSync(SyncState.WaitingForRun, "Cancelled in Civil 3D. Drag RUN in again to apply it.");
    }

    // Wait half a second for Civil 3D to finish writing, then read the new scan
    private void RestartRefreshTimer()
    {
        refreshTimer.Stop();
        refreshTimer.Start();
    }

    // What the history panel shows for a rescan: a run (with what Civil 3D actually did) or a hand refresh
    private void RecordRefresh(Scan fresh)
    {
        if (fresh.FromRun)
        {
            var log = RunLog.Read(LastRunFile);
            bool ours = log is not null && log.RunId == lastRunId;
            var lines = ours ? log!.Lines : lastRunSteps;
            if (log is not null && log.RunId == handledRunId)
            {
                return;   // already recorded this run
            }
            handledRunId = log?.RunId ?? lastRunId;
            History.Add("Run", $"Ran in Civil 3D on {fresh.Drawing}: {lines.Count} {(lines.Count == 1 ? "change" : "changes")}", lines);
            History.SetSync(SyncState.Synced, $"Synced with {fresh.Drawing} straight after the run.");
        }
        else
        {
            History.Add("Refresh", $"Re-synced with {fresh.Drawing}", "Rescanned by hand (SCAN tile).");
            History.SetSync(SyncState.Synced, $"Synced with {fresh.Drawing}.");
        }
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
        RecordRefresh(fresh);

        // Straight after a run, with "Clear steps after a run" on in Settings: start fresh
        if (fresh.FromRun && settings.ClearAfterRun && plan.Operations.Count > 0)
        {
            int ran = plan.Operations.Count;
            plan.Operations.Clear();
            UseScan(fresh);
            SetStatus(InfoBarSeverity.Success, "Script ran",
                $"{ran} {(ran == 1 ? "step" : "steps")} applied to {scan.Drawing} and cleared. Ready for the next one.");
            return;
        }

        UseScan(fresh);

        if (plan.Operations.Count == 0 || Status.Severity == InfoBarSeverity.Success)
        {
            SetStatus(InfoBarSeverity.Informational, "Drawing refreshed",
                $"Re-synced with {scan.Drawing}." + (plan.Operations.Count > 0 ? " All steps still check out." : ""));
        }
        else
        {
            // Steps that can't work any more (usually because they just ran): light up Clear all
            if (Status.Severity == InfoBarSeverity.Error)
            {
                ClearButton.Appearance = ControlAppearance.Primary;
            }

            if (fresh.FromRun)
            {
                SetStatus(InfoBarSeverity.Warning, "Script ran",
                    "The steps that just happened now show ✖, because they're already done. " +
                    "Press Clear all to start the next one.");
            }
            else
            {
                SetStatus(InfoBarSeverity.Warning, "Drawing refreshed",
                    "Your steps were re-checked against the updated drawing. If your script just ran, " +
                    "the steps that already happened now show ✖. Press Clear all to start the next one.");
            }
        }
    }

    private BlockChoice SelectedBlock => shownBlocks[Math.Max(BlockBox.SelectedIndex, 0)];

    // Show one or two boxes, with the right hints, for the chosen block
    private void UpdateBoxes()
    {
        if (BlockBox.SelectedIndex < 0)
        {
            return;   // only happens for a moment while the list is being refilled
        }

        CancelPending();
        var block = SelectedBlock;
        bool twoBoxes = block.SecondHint is not null;

        FirstBox.PlaceholderText = block.FirstHint;
        SecondBox.PlaceholderText = block.SecondHint ?? "";
        SecondBox.Visibility = twoBoxes ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(FirstArea, twoBoxes ? 1 : 2);

        // Picking several only makes sense for blocks that act on existing layers one at a time
        SelectButton.IsEnabled = (block.FirstSuggest == Suggest.Layers || block.FirstSuggest == Suggest.TextStyles)
            && block.Name != "Rename layer" && !block.Pattern;

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
            Suggest.OnOff => ["On", "Off"],
            Suggest.TextStyles => after.TextStyles.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
            Suggest.Fonts => after.TextStyles.Values.Select(s => s.Font)
                .Concat(TextStyleRules.CommonFonts)
                .Where(font => font != "")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(font => font, StringComparer.OrdinalIgnoreCase),
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
            var edited = block.Make(first, second);

            // Keep the settings the boxes don't show (e.g. whole words only, on one layer)
            if (edited is ReplaceText newer && plan.Operations[index] is ReplaceText older)
            {
                newer.MatchCase = older.MatchCase;
                newer.WholeWord = older.WholeWord;
                newer.Layer = older.Layer;
            }
            plan.Operations[index] = edited;
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
        var block = Blocks.FirstOrDefault(b => !b.Pattern && b.Make("x", "y").Kind() == step.Kind());
        if (block is null)
        {
            return;
        }

        // Its block may be hidden in Settings: show it just while editing
        if (!shownBlocks.Contains(block))
        {
            tempShown = block;
            ShowBlocks();
        }

        BlockBox.SelectedIndex = shownBlocks.IndexOf(block);
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
        SetLayerLinetypeScale s => (s.Layer, s.Scale),
        SetLinetypeSetting s => (s.Value, ""),
        SetTextStyleFont f => (f.Style, f.Font),
        SetTextHeight h => (h.Layer, h.Height),
        SetTextStyleOnLayer s => (s.Layer, s.Style),
        ReplaceText r => (r.Find, r.Replace),
        UpperCaseText u => (u.Layer, ""),
        LayerSetting other => (other.Layer, ""),
        _ => ("", "")
    };

    private void EndEdit()
    {
        editingIndex = -1;
        AddButton.Content = "Add";

        if (tempShown is not null)
        {
            tempShown = null;
            ShowBlocks();
        }
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
        History.Add("Plan", $"Saved plan {Path.GetFileName(dialog.FileName)}", $"{plan.Operations.Count} steps.");
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
            History.Add("Plan", $"Opened plan {Path.GetFileName(dialog.FileName)}", $"{loaded.Operations.Count} steps.");
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

        int removed = plan.Operations.Count;
        plan.Operations.Clear();
        RefreshSteps();
        History.Add("Steps", "Cleared all steps", $"{removed} {(removed == 1 ? "step" : "steps")} removed.");
        SetStatus(InfoBarSeverity.Informational, "Cleared", "All steps removed. Ready for a new script.");
    }

    // Redraw the list, checking every step against the drawing as we go.
    // Runs after every add or remove, so the ticks are always up to date.
    private void RefreshSteps()
    {
        // The steps changed, so Clear all goes back to normal
        ClearButton.Appearance = ControlAppearance.Secondary;

        // The steps changed, so any script made earlier is out of date: remove it
        RunTile.Visibility = Visibility.Collapsed;
        if (File.Exists(runScript))
        {
            File.Delete(runScript);
        }
        if (History.Sync == SyncState.WaitingForRun)
        {
            History.SetSync(SyncState.Synced, "Steps changed, so the unrun script was discarded.");
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
        var runId = Guid.NewGuid().ToString("N")[..8];
        try
        {
            File.WriteAllText(runScript, Engine.BuildLisp(plan, scan, runId));
        }
        catch (InvalidDataException ex)
        {
            SetStatus(InfoBarSeverity.Error, "Couldn't make the script", ex.Message);
            return;
        }

        RunTile.Visibility = Visibility.Visible;
        lastRunId = runId;
        lastRunSteps = plan.Operations.Select(op => op.Done()).ToList();
        History.Add("Script", $"Script made: {plan.Operations.Count} {(plan.Operations.Count == 1 ? "step" : "steps")}", lastRunSteps);
        History.SetSync(SyncState.WaitingForRun, "Script ready: drag RUN into Civil 3D and type Yes.");
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
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using PlainTextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using DockPanel = System.Windows.Controls.DockPanel;
using Dock = System.Windows.Controls.Dock;
using Orientation = System.Windows.Controls.Orientation;

namespace Gui;

// The window. It shows one page at a time: the scan page, then the steps page.
// The steps page is kept, so going back to rescan doesn't lose your steps.
public partial class MainWindow : FluentWindow
{
    private StepsPage? stepsPage;

    public MainWindow()
    {
        InitializeComponent();

        // Always dark, with our own blue for buttons and highlights
        ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica, false);
        ApplicationAccentColorManager.Apply(Color.FromRgb(0x00, 0x78, 0xD4), ApplicationTheme.Dark);

        // Write out a fresh copy of the scan script for the SCAN tiles to drag
        ScanFile.Write();

        // Start pinned or not, as chosen in Settings
        SetPinned(Settings.Load().StartPinned);

        BuildHelp();
        History.Changed += BuildHistory;
        ShowScanPage();
    }

    // ---- History panel ----

    private const double HistoryWidth = 312;

    // Open or close the panel. The window grows to make room, so the steps keep their space.
    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        bool opening = HistoryPane.Visibility != Visibility.Visible;
        HistoryPane.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
        HistoryIcon.Filled = opening;
        if (WindowState == WindowState.Normal)
        {
            Width = Math.Max(MinWidth, Width + (opening ? HistoryWidth : -HistoryWidth));
        }
        if (opening)
        {
            BuildHistory();
        }
    }

    private void BuildHistory()
    {
        if (HistoryPane.Visibility != Visibility.Visible)
        {
            return;
        }

        // Sync status: a coloured dot and a line of text
        SyncPanel.Children.Clear();
        var (colour, label) = History.Sync switch
        {
            SyncState.Synced => ("SystemFillColorSuccessBrush", "Synced"),
            SyncState.WaitingForRun => ("SystemFillColorCautionBrush", "Script waiting to run"),
            _ => ("SystemFillColorCriticalBrush", "Not connected")
        };
        var status = new StackPanel { Orientation = Orientation.Horizontal };
        var dot = new Ellipse { Width = 10, Height = 10, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        dot.SetResourceReference(Shape.FillProperty, colour);
        status.Children.Add(dot);
        status.Children.Add(HistoryText(label, 14, bold: true));
        SyncPanel.Children.Add(status);
        SyncPanel.Children.Add(HistoryText(History.SyncText, 12, bold: false));
        if (History.LastSynced is { } when)
        {
            SyncPanel.Children.Add(HistoryText($"Last synced at {when:HH:mm}", 12, bold: false));
        }

        var header = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var clear = new Wpf.Ui.Controls.Button
        {
            Content = "Clear",
            Appearance = ControlAppearance.Transparent,
            Padding = new Thickness(8, 2, 8, 2),
            ToolTip = "Clear the history (the drawing isn't touched)"
        };
        clear.Click += (sender, e) => History.Clear();
        DockPanel.SetDock(clear, Dock.Right);
        header.Children.Add(clear);
        header.Children.Add(HistoryText("History", 14, bold: true));
        SyncPanel.Children.Add(header);

        // The entries, newest first, with a date line whenever the day changes
        HistoryList.Children.Clear();
        if (History.Entries.Count == 0)
        {
            HistoryList.Children.Add(HistoryText("Nothing yet. Scans, scripts and runs will show here.", 12, bold: false));
            return;
        }

        DateTime? day = null;
        foreach (var entry in History.Entries)
        {
            if (day != entry.Time.Date)
            {
                day = entry.Time.Date;
                var date = HistoryText(day == DateTime.Today ? "Today" : $"{day:ddd d MMM}", 12, bold: true);
                date.Margin = new Thickness(0, 10, 0, 2);
                HistoryList.Children.Add(date);
            }

            var title = HistoryText($"{entry.Time:HH:mm}  {Icon(entry.Kind)}  {entry.Title}", 13, bold: false);
            title.Margin = new Thickness(0, 6, 0, 0);
            title.SetResourceReference(PlainTextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            HistoryList.Children.Add(title);

            foreach (var detail in entry.Details.Take(12))
            {
                var line = HistoryText(detail, 12, bold: false);
                line.Margin = new Thickness(46, 1, 0, 0);
                HistoryList.Children.Add(line);
            }
            if (entry.Details.Count > 12)
            {
                var more = HistoryText($"…and {entry.Details.Count - 12} more", 12, bold: false);
                more.Margin = new Thickness(46, 1, 0, 0);
                HistoryList.Children.Add(more);
            }
        }
    }

    private static string Icon(string kind) => kind switch
    {
        "Scan" => "🔗",
        "Refresh" => "↻",
        "Script" => "📄",
        "Run" => "✔",
        "Cancelled" => "✖",
        "Plan" => "💾",
        "AI" => "✨",
        _ => "•"
    };

    private static PlainTextBlock HistoryText(string text, double size, bool bold)
    {
        var block = new PlainTextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap
        };
        block.SetResourceReference(PlainTextBlock.ForegroundProperty,
            bold ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        return block;
    }

    // The pin in the title bar: keep this window floating above Civil 3D, or not
    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        SetPinned(!Topmost);
    }

    private void SetPinned(bool pinned)
    {
        Topmost = pinned;
        PinIcon.Filled = Topmost;
        PinButton.ToolTip = Topmost
            ? "Unpin: stop keeping this window on top"
            : "Pin: keep this window on top of Civil 3D";
    }

    // The ? in the title bar: open or close the instructions
    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        PopupToggle.Toggle(HelpPopup);
    }

    private void ShowScanPage()
    {
        var scanPage = new ScanPage(canGoBack: stepsPage is not null);

        scanPage.NextClicked += scan =>
        {
            History.Add("Scan", $"Connected to {scan.Drawing}",
                $"{scan.Layers.Count} layers" + (scan.Version >= 4 ? $", {scan.Texts.Count} texts, {scan.TextStyles.Count} text styles" : ""));
            History.SetSync(SyncState.Synced, $"Connected to {scan.Drawing}.");
            if (stepsPage is null)
            {
                stepsPage = new StepsPage(scan);
                stepsPage.ChangeDrawingClicked += ShowScanPage;
            }
            else
            {
                stepsPage.UseScan(scan);
            }
            PageHost.Content = stepsPage;
        };

        scanPage.BackClicked += () => PageHost.Content = stepsPage;

        PageHost.Content = scanPage;
    }

    // ---- The instructions. To change them, just edit the text here. ----

    private void BuildHelp()
    {
        AddHelpTitle("How to use CADScriptBuilder");

        AddHelpSection("1. Connect to your drawing",
            "Open the drawing in Civil 3D (a copy or sample drawing while testing).",
            "Drag the SCAN tile into the drawing.",
            "When it ticks green, press Next.");

        AddHelpSection("2. Build your steps",
            "Pick a block, fill in the box (or boxes), and press Add or Enter.",
            "The dropdowns only show layers that make sense for the block.",
            "☑ Select picks several layers at once. The numbers show the order they'll be added in.",
            "Rename by pattern finds text in layer names and replaces it, one step per layer.");

        AddHelpSection("3. Read the ticks",
            "✔  The step is fine.",
            "⚠  It will run, but it won't change anything. Worth a look.",
            "✖  It can't work. It isn't added unless you press Add anyway, and you can't generate until it's fixed.");

        AddHelpSection("4. Run it",
            "Press Generate script, then drag the RUN tile into Civil 3D.",
            "Civil 3D lists every change. Type Yes to apply, or just press Enter to cancel.",
            "Afterwards it prints a summary of what was done, including anything that didn't happen.",
            "The app then re-syncs with the drawing by itself.",
            "Type U once to undo the whole script.");

        AddHelpSection("Linetype scale",
            "Set linetype scale changes every object on a layer (layers don't hold a scale themselves). Pick several layers with ☑ Select.",
            "Its dropdown lists layers with objects and a dashed or dotted linetype. You can still type any layer name.",
            "LTSCALE, CELTSCALE, PSLTSCALE and MSLTSCALE have their own blocks, for the drawing-wide settings.",
            "Save a set of these as a plan to reuse as a preset, e.g. one plan per drawing scale.",
            "Objects on locked layers can't change, so unlock the layer first.");

        AddHelpSection("Fonts and text",
            "Change text style font swaps the font for every text, and every Civil 3D label, that uses the style.",
            "Use a font name like Arial, or a font file like romans.shx.",
            "Set text height changes all text on a layer. Civil 3D label heights live in the label style, so they don't change.",
            "Set text style on layer moves all text on a layer onto one style.",
            "Text with a font typed into it by hand (an MText override) keeps that font.");

        AddHelpSection("Changing text",
            "Replace text finds and replaces words in every TEXT, MTEXT and multileader. Capitals must match exactly.",
            "Make text capitals puts all text on a layer into capitals.",
            "Formatting (fonts, colours, new lines) is left exactly as it was.",
            "If someone edits a text after the scan, the script leaves that text alone and reports it as skipped.");

        AddHelpSection("Standards check",
            "The shield button (next to Open and Save) checks layer names and spells every text in the drawing.",
            "Pick the layer naming standard in ⚙ Settings, or type your own pattern.",
            "Fix adds the fix as a step, so you still preview it before anything runs. Ignore teaches it a word.",
            "The spelling check is British English and works offline: nothing leaves your PC.");

        AddHelpSection("AI mode (optional)",
            "Off by default. Turn it on in ⚙ Settings and add an Anthropic API key (saved encrypted).",
            "✨ Ask AI, then Connect: it asks Yes/No before anything is sent, once per session.",
            "Describe what you want. The AI proposes steps from the same blocks you use; it never writes the script.",
            "Its steps are checked like yours, held back if they can't work, and previewed in Civil 3D before anything runs.",
            "Sent: your request, layer names and states, linetypes, text style names. Never the drawing, its geometry or its text.",
            "History keeps a log of every request and what was proposed.");

        AddHelpSection("History",
            "The clock button (top right) opens a side panel: whether you're synced with the drawing, and every change.",
            "Each run lists what actually happened in Civil 3D, including anything skipped or refused.",
            "Kept in Documents\\CADScriptBuilder\\history.jsonl, so it's still there next time.");

        AddHelpSection("Editing steps",
            "Drag a step up or down to reorder it.",
            "Delete removes the selected step. Clear all removes every step.",
            "Double-click a step to edit it, then press Update. Esc cancels.");

        AddHelpSection("Plans and drawings",
            "Save keeps your steps as a plan. Open loads a plan onto any drawing.",
            "Changed the drawing by hand? Press ↻ (top right) and drag the mini SCAN tile in to refresh.",
            "Change (top right) switches to a different drawing. Your steps are kept and re-checked.");

        AddHelpSection("Settings",
            "⚙ (bottom right of the steps page) hides blocks you don't use.",
            "Advanced: clear the steps by itself after a run, and whether the window starts pinned.");

        AddHelpSection("The window",
            "The pin keeps this window on top of Civil 3D. Click it to unpin.",
            "Drag the window to the edge of the screen (or Win + ← / →) to snap it beside Civil 3D.");
    }

    private void AddHelpTitle(string text)
    {
        var title = new PlainTextBlock
        {
            Text = text,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        title.SetResourceReference(PlainTextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        HelpPanel.Children.Add(title);
    }

    private void AddHelpSection(string heading, params string[] lines)
    {
        var title = new PlainTextBlock
        {
            Text = heading,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 4)
        };
        title.SetResourceReference(PlainTextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        HelpPanel.Children.Add(title);

        foreach (var line in lines)
        {
            var text = new PlainTextBlock
            {
                Text = "•  " + line,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2)
            };
            text.SetResourceReference(PlainTextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            HelpPanel.Children.Add(text);
        }
    }
}

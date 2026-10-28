using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Core;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Gui;

// Page 1: hand the user the scan script, wait for layers.txt, tick green.
public partial class ScanPage : UserControl
{
    // Tells the window "Next was clicked, and here's the scan"
    public event Action<Scan>? NextClicked;

    // Tells the window "go back to the steps without rescanning"
    public event Action? BackClicked;

    // The scan script, copied next to the .exe by Gui.csproj
    private readonly string scanScript = ScanFile.Path;

    // Where the scan script writes its results (the same path as inside the .lsp)
    private readonly string layersFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CADScriptBuilder", "layers.txt");

    private readonly FileSystemWatcher watcher;
    private readonly DispatcherTimer settleTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private Scan? scan;

    public ScanPage(bool canGoBack = false)
    {
        InitializeComponent();
        BackButton.Visibility = canGoBack ? Visibility.Visible : Visibility.Collapsed;

        // Watch the folder for layers.txt being written
        var folder = Path.GetDirectoryName(layersFile)!;
        Directory.CreateDirectory(folder);
        watcher = new FileSystemWatcher(folder, "layers.txt");
        watcher.Created += (sender, e) => Dispatcher.Invoke(() => FileChanged());
        watcher.Changed += (sender, e) => Dispatcher.Invoke(() => FileChanged());
        watcher.EnableRaisingEvents = true;

        settleTimer.Tick += (sender, e) => ReadScan();

        BuildSupportList();
    }

    // Let the user drag the scan script out of the app, like a file from Explorer
    private void ScanTile_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (!File.Exists(scanScript))
        {
            ShowError($"Can't find {scanScript}. Try restarting the app.");
            return;
        }

        var data = new DataObject(DataFormats.FileDrop, new[] { scanScript });
        DragDrop.DoDragDrop(ScanTile, data, DragDropEffects.Copy);
    }

    // layers.txt changed. Wait half a second for Civil 3D to finish writing, then read it.
    private void FileChanged()
    {
        settleTimer.Stop();
        settleTimer.Start();
    }

    private void ReadScan()
    {
        settleTimer.Stop();

        try
        {
            scan = Engine.ReadScan(layersFile);
        }
        catch (IOException)
        {
            // Civil 3D is still writing it. Try again in a moment.
            settleTimer.Start();
            return;
        }
        catch (InvalidDataException ex)
        {
            scan = null;
            ShowError(ex.Message);
            return;
        }

        WaitingRing.Visibility = Visibility.Collapsed;
        StatusIcon.Visibility = Visibility.Visible;
        StatusIcon.Symbol = SymbolRegular.CheckmarkCircle24;
        StatusIcon.Foreground = GetBrush("SystemFillColorSuccessBrush", Color.FromRgb(0x6C, 0xCB, 0x5F));
        StatusText.Text = $"Synced with {scan.Drawing}  ·  {scan.Layers.Count} layers";
        NextButton.IsEnabled = true;
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (scan is null)
        {
            return;
        }

        StopWatching();
        NextClicked?.Invoke(scan);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        StopWatching();
        BackClicked?.Invoke();
    }

    // This page's job is done: stop watching for layers.txt
    private void StopWatching()
    {
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        settleTimer.Stop();
    }

    private void ShowError(string message)
    {
        WaitingRing.Visibility = Visibility.Collapsed;
        StatusIcon.Visibility = Visibility.Visible;
        StatusIcon.Symbol = SymbolRegular.DismissCircle24;
        StatusIcon.Foreground = GetBrush("SystemFillColorCriticalBrush", Color.FromRgb(0xFF, 0x99, 0xA4));
        StatusText.Text = message;
        NextButton.IsEnabled = false;
    }

    // The "What CADScriptBuilder can do" list: ticks for what works now, clocks for what's coming.
    // To update it, just edit the lists here.
    private void BuildSupportList()
    {
        AddSection("Layers", done: true,
            "Rename layers, one at a time or by pattern (find and replace)",
            "Create new layers",
            "Purge empty layers",
            "Set the current layer",
            "Freeze and thaw",
            "Turn on and off",
            "Lock and unlock",
            "Set colour, linetype, lineweight and transparency",
            "Set descriptions",
            "Plot on and off");

        AddSection("Safety", done: true,
            "Every step checked against your drawing before anything is made",
            "Steps that can't work are held back (with Add anyway if you mean it)",
            "Heads-up notes for steps that would change nothing",
            "Preview of every change, then a Yes/No before it runs",
            "One undo (U) reverses the whole script",
            "A summary after each run, including anything that didn't happen",
            "Out-of-date scripts are removed automatically");

        AddSection("Workflow", done: true,
            "Drag-in scan that syncs with your drawing",
            "Re-syncs with the drawing by itself after every run",
            "Refresh button for changes made by hand",
            "Change drawing without losing your steps",
            "Dropdowns that only show layers that make sense for the block",
            "Select several layers at once, in the order you tick them",
            "Drag steps to reorder, Delete to remove, double-click to edit",
            "Save plans and reuse them on other drawings",
            "Settings: hide blocks you don't use, clear steps after a run",
            "Drag-out script, straight into Civil 3D",
            "Stays on top of Civil 3D (pin), and snaps side by side",
            "Command line version that runs saved plans");

        AddSection("Coming soon", done: false,
            "AI mode: describe what you want in plain English",
            "Drag-and-drop block builder",
            "Standards check (Uniclass, house and client naming)",
            "Civil 3D styles and label styles",
            "Merge and delete layers",
            "Drawing cleanup: purge, audit, xrefs",
            "Layouts and plotting",
            "Direct connection to Civil 3D, no dragging",
            "Automatic updates");
    }

    private void AddSection(string heading, bool done, params string[] items)
    {
        var title = new TextBlock
        {
            Text = heading,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 4)
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        SupportPanel.Children.Add(title);

        var icon = done ? SymbolRegular.CheckmarkCircle24 : SymbolRegular.Clock24;
        var iconColour = done
            ? GetBrush("SystemFillColorSuccessBrush", Color.FromRgb(0x6C, 0xCB, 0x5F))
            : GetBrush("TextFillColorSecondaryBrush", Color.FromRgb(0x9E, 0x9E, 0x9E));

        foreach (var item in items)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };

            row.Children.Add(new Wpf.Ui.Controls.SymbolIcon
            {
                Symbol = icon,
                FontSize = 16,
                Foreground = iconColour,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            var text = new TextBlock { Text = item, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            text.SetResourceReference(TextBlock.ForegroundProperty,
                done ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
            row.Children.Add(text);

            SupportPanel.Children.Add(row);
        }
    }

    // A colour from the current theme, or a fallback if the theme doesn't have it
    private Brush GetBrush(string key, Color fallback)
    {
        return TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
    }
}

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

    // The scan script, copied next to the .exe by Gui.csproj
    private readonly string scanScript = Path.Combine(AppContext.BaseDirectory, "csb_scan.lsp");

    // Where the scan script writes its results (the same path as inside the .lsp)
    private readonly string layersFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "repos", "cadscriptbuilder", "src", "layers.txt");

    private readonly FileSystemWatcher watcher;
    private readonly DispatcherTimer settleTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private Scan? scan;

    public ScanPage()
    {
        InitializeComponent();

        // Watch the folder for layers.txt being written
        var folder = Path.GetDirectoryName(layersFile)!;
        Directory.CreateDirectory(folder);
        watcher = new FileSystemWatcher(folder, "layers.txt");
        watcher.Created += (sender, e) => Dispatcher.Invoke(() => FileChanged());
        watcher.Changed += (sender, e) => Dispatcher.Invoke(() => FileChanged());
        watcher.EnableRaisingEvents = true;

        settleTimer.Tick += (sender, e) => ReadScan();
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
            ShowError($"Can't find {scanScript}. Try rebuilding the app.");
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

        // Stop watching: this page's job is done
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        settleTimer.Stop();

        NextClicked?.Invoke(scan);
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

    // A colour from the current theme, or a fallback if the theme doesn't have it
    private Brush GetBrush(string key, Color fallback)
    {
        return TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
    }
}

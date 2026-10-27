using Core;

namespace Gui;

// Page 1: hand the user the scan script, wait for layers.txt, tick green.
public class ScanPage : UserControl
{
    // Tells the window "Next was clicked, and here's the scan"
    public event Action<Scan>? NextClicked;

    // The scan script, copied next to the .exe by Gui.csproj
    private readonly string scanScript = Path.Combine(AppContext.BaseDirectory, "csb_scan.lsp");

    // Where the scan script writes its results (the same path as inside the .lsp)
    private readonly string layersFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "repos", "cadscriptbuilder", "src", "layers.txt");

    private readonly Label statusLabel = new();
    private readonly Button nextButton = new();
    private readonly FileSystemWatcher watcher;
    private readonly System.Windows.Forms.Timer settleTimer = new() { Interval = 500 };
    private Scan? scan;

    public ScanPage()
    {
        var title = new Label
        {
            Text = "CADScriptBuilder",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, 30),
            Size = new Size(560, 40)
        };

        var instructions = new Label
        {
            Text = "To get started, drag this file\ninto your Civil 3D drawing",
            Font = new Font("Segoe UI", 11),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, 85),
            Size = new Size(560, 50)
        };

        var tile = new Label
        {
            Text = "SCAN\n\ncsb_scan.lsp",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Cursor = Cursors.Hand,
            Location = new Point(220, 150),
            Size = new Size(120, 120)
        };

        statusLabel.Font = new Font("Segoe UI", 11);
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        statusLabel.Location = new Point(0, 295);
        statusLabel.Size = new Size(560, 50);

        nextButton.Text = "Next →";
        nextButton.Location = new Point(220, 370);
        nextButton.Size = new Size(120, 34);

        Controls.Add(title);
        Controls.Add(instructions);
        Controls.Add(tile);
        Controls.Add(statusLabel);
        Controls.Add(nextButton);

        // What happens when things are clicked
        tile.MouseDown += (sender, e) => StartDrag();
        nextButton.Click += (sender, e) => GoNext();

        // Watch the folder for layers.txt being written
        var folder = Path.GetDirectoryName(layersFile)!;
        Directory.CreateDirectory(folder);
        watcher = new FileSystemWatcher(folder, "layers.txt")
        {
            SynchronizingObject = this,
            EnableRaisingEvents = true
        };
        watcher.Created += (sender, e) => FileChanged();
        watcher.Changed += (sender, e) => FileChanged();
        settleTimer.Tick += (sender, e) => ReadScan();

        statusLabel.ForeColor = Color.Gray;
        statusLabel.Text = "Waiting for scan...";
        nextButton.Enabled = false;
    }

    // Let the user drag the scan script out of the app, like a file from Explorer
    private void StartDrag()
    {
        if (!File.Exists(scanScript))
        {
            ShowError($"Can't find {scanScript}. Try rebuilding the app.");
            return;
        }

        var data = new DataObject(DataFormats.FileDrop, new[] { scanScript });
        DoDragDrop(data, DragDropEffects.Copy);
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

        statusLabel.ForeColor = Color.ForestGreen;
        statusLabel.Text = $"✔ Synced with {scan.Drawing}\n{scan.Layers.Count} layers";
        nextButton.Enabled = true;
    }

    private void GoNext()
    {
        if (scan is null)
        {
            return;
        }

        // Stop watching: this page's job is done
        watcher.EnableRaisingEvents = false;
        settleTimer.Stop();

        NextClicked?.Invoke(scan);
    }

    private void ShowError(string message)
    {
        statusLabel.ForeColor = Color.Firebrick;
        statusLabel.Text = message;
        nextButton.Enabled = false;
    }
}
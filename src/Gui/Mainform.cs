namespace Gui;

// The window. It shows one page at a time:
// first the scan page, then (after Next) the steps page.
public class MainForm : Form
{
    public MainForm()
    {
        Text = "CADScriptBuilder";
        ClientSize = new Size(560, 460);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var scanPage = new ScanPage();
        scanPage.NextClicked += scan => ShowPage(new StepsPage(scan));
        ShowPage(scanPage);
    }

    // Swap whatever page is showing for a new one
    private void ShowPage(Control page)
    {
        Controls.Clear();
        page.Dock = DockStyle.Fill;
        Controls.Add(page);
    }
}
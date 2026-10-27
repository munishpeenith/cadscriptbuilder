using Wpf.Ui.Appearance;

namespace Gui;

// The window. It shows one page at a time:
// first the scan page, then (after Next) the steps page.
public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        // Follow Windows' light/dark setting, now and if it changes
        ApplicationThemeManager.ApplySystemTheme();
        SystemThemeWatcher.Watch(this);

        var scanPage = new ScanPage();
        scanPage.NextClicked += scan => PageHost.Content = new StepsPage(scan);
        PageHost.Content = scanPage;
    }
}

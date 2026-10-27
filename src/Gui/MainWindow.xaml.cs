using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Gui;

// The window. It shows one page at a time:
// first the scan page, then (after Next) the steps page.
public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        // Always dark, with our own blue for buttons and highlights
        ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica, false);
        ApplicationAccentColorManager.Apply(Color.FromRgb(0x00, 0x78, 0xD4), ApplicationTheme.Dark);

        var scanPage = new ScanPage();
        scanPage.NextClicked += scan => PageHost.Content = new StepsPage(scan);
        PageHost.Content = scanPage;
    }
}
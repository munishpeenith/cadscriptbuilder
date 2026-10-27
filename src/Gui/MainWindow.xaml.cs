using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using System.Windows;

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

        ShowScanPage();
    }
        // The pin in the title bar: keep this window floating above Civil 3D, or not
    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinIcon.Filled = Topmost;
        PinButton.ToolTip = Topmost
            ? "Unpin: stop keeping this window on top"
            : "Pin: keep this window on top of Civil 3D";
    }

    private void ShowScanPage()
    {
        var scanPage = new ScanPage(canGoBack: stepsPage is not null);

        scanPage.NextClicked += scan =>
        {
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
}

using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using PlainTextBlock = System.Windows.Controls.TextBlock;

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

        BuildHelp();
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

    // The ? in the title bar: open or close the instructions
    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        HelpPopup.IsOpen = !HelpPopup.IsOpen;
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

        AddHelpSection("Editing steps",
            "Drag a step up or down to reorder it.",
            "Delete removes the selected step. Clear all removes every step.",
            "Double-click a step to edit it, then press Update. Esc cancels.");

        AddHelpSection("Plans and drawings",
            "Save keeps your steps as a plan. Open loads a plan onto any drawing.",
            "Changed the drawing by hand? Press ↻ (top right) and drag the mini SCAN tile in to refresh.",
            "Change (top right) switches to a different drawing. Your steps are kept and re-checked.");

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

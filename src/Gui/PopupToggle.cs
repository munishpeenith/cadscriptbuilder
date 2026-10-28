using System.Windows.Controls.Primitives;

namespace Gui;

// Makes a button open AND close its popup.
// The problem: clicking the button while its popup is open first closes the popup
// (it was a click outside it), then the button's Click opens it straight back up.
// The fix: remember when each popup last closed (a dict of popup -> time), and if it
// closed a moment ago, that was this same click, so leave it closed.
public static class PopupToggle
{
    private static readonly Dictionary<Popup, DateTime> lastClosed = [];

    public static void Toggle(Popup popup, Action? beforeOpen = null)
    {
        if (!lastClosed.ContainsKey(popup))
        {
            lastClosed[popup] = DateTime.MinValue;
            popup.Closed += (sender, e) => lastClosed[popup] = DateTime.Now;
        }

        if (popup.IsOpen)
        {
            popup.IsOpen = false;
            return;
        }

        if ((DateTime.Now - lastClosed[popup]).TotalMilliseconds < 300)
        {
            return;   // this click just closed it
        }

        beforeOpen?.Invoke();
        popup.IsOpen = true;
    }
}

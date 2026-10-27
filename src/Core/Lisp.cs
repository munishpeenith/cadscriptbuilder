namespace Core;

// Helpers for writing AutoLISP text safely
public static class Lisp
{
    // Make text safe inside a LISP string: \ becomes \\ and " becomes \"
    public static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // Text as a LISP string, quotes included: C-ROAD becomes "C-ROAD"
    public static string Quote(string text) => $"\"{Escape(text)}\"";
}

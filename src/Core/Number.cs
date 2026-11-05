using System.Globalization;

namespace Core;

// Numbers typed by the user or read from a scan, always with a dot for decimals
public static class Number
{
    public static double? Parse(string text) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    // A number as LISP reads it. Always has a decimal point, so LISP treats it as a real: 2 becomes 2.0
    public static string ToLisp(double value)
    {
        var text = value.ToString("0.0#########", CultureInfo.InvariantCulture);
        return text;
    }

    // A number for messages: 2.0 shows as 2, 0.50 as 0.5
    public static string Show(double value) => value.ToString("0.##########", CultureInfo.InvariantCulture);
}

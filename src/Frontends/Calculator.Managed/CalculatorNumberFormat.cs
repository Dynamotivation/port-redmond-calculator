using System.Globalization;

namespace Calculator.Managed;

/// <summary>
/// Captures the user's regional number format once so every calculator surface
/// uses the same separators, regardless of the UI framework or host platform.
/// </summary>
public sealed record CalculatorNumberFormat(
    string DecimalSeparator,
    string NumberGroupSeparator,
    string NumberGrouping)
{
    public static CalculatorNumberFormat FromCulture(CultureInfo? culture = null)
    {
        var numberFormat = (culture ?? CultureInfo.CurrentCulture).NumberFormat;
        var decimalSeparator = FirstCharacterOrDefault(numberFormat.NumberDecimalSeparator, ".");
        var groupSeparator = numberFormat.NumberGroupSeparator ?? string.Empty;
        var grouping = ToCalculatorGrouping(numberFormat.NumberGroupSizes);
        return new CalculatorNumberFormat(decimalSeparator, groupSeparator, grouping);
    }

    public string LocalizeCanonicalNumber(string value) =>
        DecimalSeparator == "." ? value : value.Replace(".", DecimalSeparator, StringComparison.Ordinal);

    public string DelocalizeNumber(string value) =>
        DecimalSeparator == "." ? value : value.Replace(DecimalSeparator, ".", StringComparison.Ordinal);

    private static string FirstCharacterOrDefault(string? value, string fallback) =>
        string.IsNullOrEmpty(value) ? fallback : value[..1];

    private static string ToCalculatorGrouping(int[] groupSizes)
    {
        var supportedSizes = groupSizes.TakeWhile(size => size > 0).ToArray();
        return supportedSizes.Length == 0
            ? "0;0"
            : string.Join(';', supportedSizes.Append(0));
    }
}

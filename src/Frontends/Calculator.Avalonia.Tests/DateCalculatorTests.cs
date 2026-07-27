using System.Globalization;
using Calculator.Managed;

namespace Calculator.Avalonia.Tests;

internal static class DateCalculatorTests
{
    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("date difference matches source pivot algorithm", DifferenceMatchesSourcePivotAlgorithm),
        ("date difference ignores input order", DifferenceIgnoresInputOrder),
        ("date arithmetic preserves source operation order", ArithmeticPreservesSourceOperationOrder),
        ("date arithmetic reports source bounds", ArithmeticReportsBounds),
        ("date mode preserves the shared from date", ModePreservesSharedFromDate),
    ];

    private static DateCalculatorViewModel Create() =>
        new(
            new DateCalculatorStrings(
                "Difference between dates",
                "Add or subtract days",
                "Calculation mode",
                "From",
                "To",
                "Difference",
                "Add",
                "Subtract",
                "Years",
                "Months",
                "Days",
                "Date",
                "Same dates",
                "day",
                "days",
                "week",
                "weeks",
                "month",
                "months",
                "year",
                "years",
                "Date out of Bound",
                "Calculation failed",
                "Difference %1",
                "Resulting date %1"),
            CultureInfo.GetCultureInfo("en-US"));

    private static void DifferenceMatchesSourcePivotAlgorithm()
    {
        var viewModel = Create();
        viewModel.FromDate = new DateTime(2024, 1, 1);
        viewModel.ToDate = new DateTime(2025, 2, 10);

        Assert(
            viewModel.DateDiffResult == "1 year, 1 month, 1 week, 2 days",
            $"unexpected all-unit result '{viewModel.DateDiffResult}'");
        Assert(
            viewModel.DateDiffResultInDays == "406 days",
            $"unexpected day result '{viewModel.DateDiffResultInDays}'");
        Assert(!viewModel.IsDiffInDays, "both source result rows should be visible");
    }

    private static void DifferenceIgnoresInputOrder()
    {
        var viewModel = Create();
        viewModel.FromDate = new DateTime(2025, 2, 10);
        viewModel.ToDate = new DateTime(2024, 1, 1);

        Assert(
            viewModel.DateDiffResult == "1 year, 1 month, 1 week, 2 days",
            "date difference should be unsigned");
        Assert(viewModel.DateDiffResultInDays == "406 days", "day difference should be positive");
    }

    private static void ArithmeticPreservesSourceOperationOrder()
    {
        var viewModel = Create();
        viewModel.SelectedCalculationIndex = 1;
        viewModel.StartDate = new DateTime(2024, 1, 31);
        viewModel.MonthsOffset = 1;
        Assert(
            viewModel.DateResult.Contains("February 29, 2024", StringComparison.Ordinal),
            $"adding one month should clamp to leap day, was '{viewModel.DateResult}'");

        viewModel.StartDate = new DateTime(2024, 3, 31);
        viewModel.IsAddMode = false;
        viewModel.DaysOffset = 1;
        Assert(
            viewModel.DateResult.Contains("February 29, 2024", StringComparison.Ordinal),
            $"subtraction must apply days before months, was '{viewModel.DateResult}'");
    }

    private static void ArithmeticReportsBounds()
    {
        var viewModel = Create();
        viewModel.SelectedCalculationIndex = 1;
        viewModel.StartDate = new DateTime(DateCalculatorViewModel.MinimumYear, 1, 1);
        viewModel.IsAddMode = false;
        viewModel.DaysOffset = 1;

        Assert(viewModel.DateResult == "Date out of Bound", "underflow should use the source error");
    }

    private static void ModePreservesSharedFromDate()
    {
        var viewModel = Create();
        var date = new DateTime(2030, 7, 14);
        viewModel.FromDate = date;
        viewModel.SelectedCalculationIndex = 1;
        Assert(viewModel.StartDate == date, "add/subtract should inherit the difference From date");

        var replacement = new DateTime(2031, 8, 15);
        viewModel.StartDate = replacement;
        viewModel.SelectedCalculationIndex = 0;
        Assert(viewModel.FromDate == replacement, "difference should inherit the add/subtract From date");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

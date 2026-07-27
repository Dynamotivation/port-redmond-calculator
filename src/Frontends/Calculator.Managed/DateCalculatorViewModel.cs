using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Calculator.Managed;

[Flags]
internal enum DateUnit
{
    Year = 0x01,
    Month = 0x02,
    Week = 0x04,
    Day = 0x08,
}

internal readonly record struct DateDifference(int Year, int Month, int Week, int Day);

/// <summary>
/// Cross-platform counterpart of Microsoft's DateCalculationEngine.
/// </summary>
/// <remarks>
/// The source uses Windows.Globalization.Calendar in UTC. This implementation
/// keeps the same operation order and pivot algorithm but delegates calendar
/// arithmetic to the current .NET culture's Calendar, making the logic usable
/// by every frontend without a Windows compatibility facade.
/// </remarks>
internal sealed class DateCalculationEngine
{
    private readonly Calendar _calendar;

    public DateCalculationEngine(Calendar calendar) => _calendar = calendar;

    public DateTime? AddDuration(DateTime startDate, DateDifference duration)
    {
        try
        {
            var result = startDate;
            if (duration.Year != 0) result = _calendar.AddYears(result, duration.Year);
            if (duration.Month != 0) result = _calendar.AddMonths(result, duration.Month);
            if (duration.Day != 0) result = _calendar.AddDays(result, duration.Day);
            return result;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public DateTime? SubtractDuration(DateTime startDate, DateDifference duration)
    {
        try
        {
            // This deliberately differs from AddDuration: Microsoft subtracts
            // the smaller units first, then months, then years.
            var result = startDate;
            if (duration.Day != 0) result = _calendar.AddDays(result, -duration.Day);
            if (duration.Month != 0) result = _calendar.AddMonths(result, -duration.Month);
            if (duration.Year != 0) result = _calendar.AddYears(result, -duration.Year);
            return result.Year >= 1601 ? result : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public DateDifference? TryGetDateDifference(DateTime first, DateTime second, DateUnit outputFormat)
    {
        var startDate = first <= second ? first.Date : second.Date;
        var endDate = first <= second ? second.Date : first.Date;
        var pivotDate = startDate;
        var remainingDays = DifferenceInDays(startDate, endDate);
        var differences = new int[4];

        if ((outputFormat & (DateUnit.Year | DateUnit.Month | DateUnit.Week)) != 0)
        {
            int daysInMonth;
            int approximateDaysInYear;
            try
            {
                daysInMonth = _calendar.GetDaysInMonth(
                    _calendar.GetYear(startDate),
                    _calendar.GetMonth(startDate),
                    _calendar.GetEra(startDate));
                approximateDaysInYear = _calendar.GetDaysInYear(
                    _calendar.GetYear(endDate),
                    _calendar.GetEra(endDate));
            }
            catch (ArgumentException)
            {
                return new DateDifference(0, 0, 0, remainingDays);
            }

            var approximateDays = new[] { approximateDaysInYear, daysInMonth, 7 };
            var units = new[] { DateUnit.Year, DateUnit.Month, DateUnit.Week };
            for (var unitIndex = 0; unitIndex < units.Length; unitIndex++)
            {
                var unit = units[unitIndex];
                if (!outputFormat.HasFlag(unit))
                {
                    continue;
                }

                var unitStart = pivotDate;
                differences[unitIndex] = remainingDays / approximateDays[unitIndex];
                try
                {
                    pivotDate = Adjust(unitStart, unit, differences[unitIndex]);
                }
                catch (ArgumentException)
                {
                    return null;
                }

                var passedEnd = false;
                while (true)
                {
                    var delta = DifferenceInDays(pivotDate, endDate);
                    if (delta < 0)
                    {
                        if (differences[unitIndex] == 0)
                        {
                            return null;
                        }

                        differences[unitIndex]--;
                        try
                        {
                            pivotDate = Adjust(unitStart, unit, differences[unitIndex]);
                        }
                        catch (ArgumentException)
                        {
                            return null;
                        }
                        passedEnd = true;
                    }
                    else if (delta > 0)
                    {
                        if (passedEnd)
                        {
                            break;
                        }

                        try
                        {
                            pivotDate = Adjust(unitStart, unit, differences[unitIndex] + 1);
                            differences[unitIndex]++;
                        }
                        catch (ArgumentException)
                        {
                            return null;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                try
                {
                    pivotDate = Adjust(unitStart, unit, differences[unitIndex]);
                }
                catch (ArgumentException)
                {
                    return null;
                }

                remainingDays = DifferenceInDays(pivotDate, endDate);
                if (remainingDays < 0)
                {
                    return null;
                }
            }
        }

        differences[3] = remainingDays;
        return new DateDifference(differences[0], differences[1], differences[2], differences[3]);
    }

    private DateTime Adjust(DateTime date, DateUnit unit, int difference) => unit switch
    {
        DateUnit.Year => _calendar.AddYears(date, difference),
        DateUnit.Month => _calendar.AddMonths(date, difference),
        DateUnit.Week => _calendar.AddWeeks(date, difference),
        _ => date,
    };

    private static int DifferenceInDays(DateTime first, DateTime second) =>
        checked((int)(second.Date - first.Date).TotalDays);
}

public sealed record DateCalculatorStrings(
    string DifferenceOption,
    string AddSubtractOption,
    string CalculationModeAutomationName,
    string From,
    string To,
    string Difference,
    string Add,
    string Subtract,
    string Years,
    string Months,
    string Days,
    string Date,
    string SameDates,
    string Day,
    string DaysUnit,
    string Week,
    string Weeks,
    string Month,
    string MonthsUnit,
    string Year,
    string YearsUnit,
    string OutOfBounds,
    string CalculationFailed,
    string DifferenceAutomationFormat,
    string ResultAutomationFormat);

/// <summary>
/// Date difference and add/subtract state from the original DateCalculator.
/// </summary>
public sealed partial class DateCalculatorViewModel : ObservableObject
{
    public const int MinimumYear = 1601;
    // The source picker is capped at 2550 for a UWP rendering bug, but results
    // may continue through the underlying calendar's full range.
    public const int MaximumYear = 2550;
    private readonly DateCalculationEngine _engine;
    private readonly CultureInfo _culture;
    private readonly string _listSeparator;
    private DateTime _lastFromDate;
    private DateTime _lastToDate;
    private DateTime _lastStartDate;

    public DateCalculatorViewModel(DateCalculatorStrings strings, CultureInfo? culture = null)
    {
        Strings = strings;
        _culture = culture ?? CultureInfo.CurrentCulture;
        _engine = new DateCalculationEngine(_culture.DateTimeFormat.Calendar);
        _listSeparator = _culture.TextInfo.ListSeparator + " ";
        var today = DateTime.Today;
        _lastFromDate = _lastToDate = _lastStartDate = today;
        FromDate = ToDate = StartDate = today;
        OffsetValues = new ReadOnlyCollection<string>(
            Enumerable.Range(0, 1000).Select(value => value.ToString(_culture)).ToArray());
        Recalculate();
    }

    public DateCalculatorStrings Strings { get; }
    public IReadOnlyList<string> CalculationOptions =>
        [Strings.DifferenceOption, Strings.AddSubtractOption];
    public IReadOnlyList<string> OffsetValues { get; }
    public DateTime DisplayDateStart { get; } = new(MinimumYear, 1, 1);
    public DateTime DisplayDateEnd { get; } = new(MaximumYear, 12, 31);
    public DayOfWeek FirstDayOfWeek => _culture.DateTimeFormat.FirstDayOfWeek;
    public string DateFormat => _culture.DateTimeFormat.ShortDatePattern;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDateDiffMode))]
    [NotifyPropertyChangedFor(nameof(IsAddSubtractMode))]
    public partial int SelectedCalculationIndex { get; set; }

    public bool IsDateDiffMode => SelectedCalculationIndex == 0;
    public bool IsAddSubtractMode => !IsDateDiffMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSubtractMode))]
    public partial bool IsAddMode { get; set; } = true;

    public bool IsSubtractMode
    {
        get => !IsAddMode;
        set
        {
            if (value)
            {
                IsAddMode = false;
            }
        }
    }

    [ObservableProperty]
    public partial DateTime? FromDate { get; set; }

    [ObservableProperty]
    public partial DateTime? ToDate { get; set; }

    [ObservableProperty]
    public partial DateTime? StartDate { get; set; }

    [ObservableProperty]
    public partial int YearsOffset { get; set; }

    [ObservableProperty]
    public partial int MonthsOffset { get; set; }

    [ObservableProperty]
    public partial int DaysOffset { get; set; }

    [ObservableProperty]
    public partial bool IsDiffInDays { get; private set; } = true;

    [ObservableProperty]
    public partial string DateDiffResult { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DateDiffResultAutomationName { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DateDiffResultInDays { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DateResult { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DateResultAutomationName { get; private set; } = string.Empty;

    partial void OnSelectedCalculationIndexChanged(int value)
    {
        if (value == 0)
        {
            FromDate = StartDate;
        }
        else
        {
            StartDate = FromDate;
        }
        Recalculate();
    }

    partial void OnIsAddModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSubtractMode));
        Recalculate();
    }

    partial void OnFromDateChanged(DateTime? value)
    {
        if (value is null)
        {
            FromDate = _lastFromDate;
            return;
        }
        _lastFromDate = value.Value.Date;
        Recalculate();
    }

    partial void OnToDateChanged(DateTime? value)
    {
        if (value is null)
        {
            ToDate = _lastToDate;
            return;
        }
        _lastToDate = value.Value.Date;
        Recalculate();
    }

    partial void OnStartDateChanged(DateTime? value)
    {
        if (value is null)
        {
            StartDate = _lastStartDate;
            return;
        }
        _lastStartDate = value.Value.Date;
        Recalculate();
    }

    partial void OnYearsOffsetChanged(int value) => Recalculate();
    partial void OnMonthsOffsetChanged(int value) => Recalculate();
    partial void OnDaysOffsetChanged(int value) => Recalculate();

    private void Recalculate()
    {
        if (FromDate is null || ToDate is null || StartDate is null)
        {
            return;
        }

        if (IsDateDiffMode)
        {
            var days = _engine.TryGetDateDifference(FromDate.Value, ToDate.Value, DateUnit.Day);
            var all = _engine.TryGetDateDifference(
                FromDate.Value,
                ToDate.Value,
                DateUnit.Year | DateUnit.Month | DateUnit.Week | DateUnit.Day);
            UpdateDifference(days, all ?? days);
            return;
        }

        var duration = new DateDifference(YearsOffset, MonthsOffset, 0, DaysOffset);
        var result = IsAddMode
            ? _engine.AddDuration(StartDate.Value, duration)
            : _engine.SubtractDuration(StartDate.Value, duration);
        DateResult = result is null || result.Value.Year < MinimumYear
            ? Strings.OutOfBounds
            : result.Value.ToString("D", _culture);
        DateResultAutomationName = FormatAutomation(Strings.ResultAutomationFormat, DateResult);
    }

    private void UpdateDifference(DateDifference? days, DateDifference? all)
    {
        if (days is null)
        {
            IsDiffInDays = false;
            DateDiffResultInDays = string.Empty;
            DateDiffResult = Strings.CalculationFailed;
        }
        else if (days.Value.Day == 0)
        {
            IsDiffInDays = true;
            DateDiffResultInDays = string.Empty;
            DateDiffResult = Strings.SameDates;
        }
        else if (all is null || (all.Value.Year == 0 && all.Value.Month == 0 && all.Value.Week == 0))
        {
            IsDiffInDays = true;
            DateDiffResultInDays = string.Empty;
            DateDiffResult = FormatDays(days.Value.Day);
        }
        else
        {
            IsDiffInDays = false;
            DateDiffResult = FormatDifference(all.Value);
            DateDiffResultInDays = FormatDays(days.Value.Day);
        }

        DateDiffResultAutomationName =
            FormatAutomation(Strings.DifferenceAutomationFormat, DateDiffResult);
    }

    private string FormatDifference(DateDifference difference)
    {
        var parts = new List<string>(4);
        AddPart(parts, difference.Year, Strings.Year, Strings.YearsUnit);
        AddPart(parts, difference.Month, Strings.Month, Strings.MonthsUnit);
        AddPart(parts, difference.Week, Strings.Week, Strings.Weeks);
        AddPart(parts, difference.Day, Strings.Day, Strings.DaysUnit);
        return string.Join(_listSeparator, parts);
    }

    private string FormatDays(int days) =>
        $"{days.ToString(_culture)} {(days == 1 ? Strings.Day : Strings.DaysUnit)}";

    private void AddPart(List<string> parts, int value, string singular, string plural)
    {
        if (value > 0)
        {
            parts.Add($"{value.ToString(_culture)} {(value == 1 ? singular : plural)}");
        }
    }

    private static string FormatAutomation(string format, string value) =>
        format.Replace("%1", value, StringComparison.Ordinal);
}

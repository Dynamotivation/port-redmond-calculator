using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Calculator.Managed;

/// <summary>
/// History entries and the operations on them.
/// </summary>
/// <remarks>
/// Whether history is docked is not decided here. The shell measures the window
/// and calls <see cref="SetDocked"/>; this type only reacts to it, which is why
/// the combined visibility rules — the ones that also depend on the active mode
/// and on compact overlay — stay on the shell view model.
///
/// Recalling or deleting an entry mutates the shared calculator session, so the
/// session owner supplies a callback to re-read everything afterwards rather
/// than this type reaching back into the shell.
/// </remarks>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly NativeCalculator _calculator;
    private readonly Func<bool> _isScientificNotation;
    private readonly Action _synchronize;

    public HistoryViewModel(
        NativeCalculator calculator,
        Func<bool> isScientificNotation,
        Action synchronize,
        HistoryStrings strings)
    {
        _calculator = calculator;
        _isScientificNotation = isScientificNotation;
        _synchronize = synchronize;
        Strings = strings;
    }

    public HistoryStrings Strings { get; }

    public ObservableCollection<CalculatorHistoryEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial bool HasEntries { get; private set; }

    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCloseButtonVisible))]
    public partial bool IsDocked { get; private set; }

    public bool IsCloseButtonVisible => !IsDocked;

    [RelayCommand]
    private void Toggle()
    {
        if (!IsDocked)
        {
            IsOpen = !IsOpen;
        }
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private void Clear()
    {
        _calculator.HistoryClear();
        _synchronize();
    }

    [RelayCommand]
    private void DeleteEntry(CalculatorHistoryEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _calculator.HistoryRemove(entry.NativeIndex);
        _synchronize();
    }

    [RelayCommand]
    private void SelectEntry(CalculatorHistoryEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _calculator.HistoryRecall(entry.NativeIndex, _isScientificNotation());
        _synchronize();
        if (!IsDocked)
        {
            IsOpen = false;
        }
    }

    /// <summary>Docking closes the overlay; the two presentations are exclusive.</summary>
    public void SetDocked(bool value)
    {
        IsDocked = value;
        if (value)
        {
            IsOpen = false;
        }
    }

    public void CloseOverlay() => IsOpen = false;

    internal void Refresh(IReadOnlyList<CalculatorHistoryEntry> entries)
    {
        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(entry);
        }

        HasEntries = Entries.Count != 0;
    }
}

/// <summary>Localized strings the history surfaces need.</summary>
public sealed record HistoryStrings(
    string AutomationName,
    string EmptyText,
    string ClearTooltip,
    string ToggleTooltip,
    string DeleteItemName);

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Calculator.Managed;

/// <summary>
/// The stored memory values and the operations on them, shared by the compact
/// memory row and the popup that lists individual entries.
/// </summary>
/// <remarks>
/// Every operation goes through the shared calculator session; this type holds
/// no memory of its own. As with history, mutating the session means the owner
/// has to re-read it, so a synchronise callback is supplied rather than the
/// child calling back into the shell.
/// </remarks>
public sealed partial class MemoryViewModel : ObservableObject
{
    private readonly NativeCalculator _calculator;
    private readonly Action _synchronize;

    public MemoryViewModel(NativeCalculator calculator, Action synchronize, MemoryStrings strings)
    {
        _calculator = calculator;
        _synchronize = synchronize;
        Strings = strings;
    }

    public MemoryStrings Strings { get; }

    public ObservableCollection<CalculatorMemoryEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial bool HasEntries { get; private set; }

    [RelayCommand]
    private void Store() { _calculator.MemoryStore(); _synchronize(); }

    [RelayCommand]
    private void Recall() { _calculator.MemoryRecall(); _synchronize(); }

    [RelayCommand]
    private void Add() { _calculator.MemoryAdd(); _synchronize(); }

    [RelayCommand]
    private void Subtract() { _calculator.MemorySubtract(); _synchronize(); }

    [RelayCommand]
    private void Clear() { _calculator.MemoryClear(); _synchronize(); }

    [RelayCommand]
    private void ClearAll() { _calculator.MemoryClearAll(); _synchronize(); }

    [RelayCommand]
    private void RecallEntry(CalculatorMemoryEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _calculator.MemoryRecall(entry.Index);
        _synchronize();
    }

    [RelayCommand]
    private void AddToEntry(CalculatorMemoryEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _calculator.MemoryAdd(entry.Index);
        _synchronize();
    }

    [RelayCommand]
    private void SubtractFromEntry(CalculatorMemoryEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _calculator.MemorySubtract(entry.Index);
        _synchronize();
    }

    [RelayCommand]
    private void ClearEntry(CalculatorMemoryEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _calculator.MemoryClear(entry.Index);
        _synchronize();
    }

    internal void Refresh(IEnumerable<CalculatorMemoryEntry> entries)
    {
        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(entry);
        }

        HasEntries = Entries.Count != 0;
    }
}

/// <summary>Localized strings the memory surfaces need.</summary>
public sealed record MemoryStrings(
    string PopupTooltip,
    string ClearTooltip,
    string StoreTooltip,
    string RecallTooltip,
    string AddTooltip,
    string SubtractTooltip,
    string ClearItemName,
    string AddToItemName,
    string SubtractFromItemName);

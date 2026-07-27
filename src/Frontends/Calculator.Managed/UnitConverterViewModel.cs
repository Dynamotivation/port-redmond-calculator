using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Calculator.Managed;

/// <summary>
/// The unit converter: categories, unit selection, both value displays, the
/// suggestion list and the converter keypad commands.
/// </summary>
/// <remarks>
/// This owns the native unit converter outright — it is a separate engine from
/// the calculator session, so nothing here touches NativeCalculator.
///
/// Picking a category is also a navigation event: the shell's mode and selected
/// navigation item have to follow it. Rather than reach up, this type raises
/// <see cref="CategorySelected"/> and lets the shell decide what that means.
/// </remarks>
public sealed partial class UnitConverterViewModel : ObservableObject, IDisposable
{
    private readonly NativeUnitConverter _converter;
    private bool _synchronizingSelection;

    public UnitConverterViewModel(NativeUnitConverter converter, string groupName)
    {
        _converter = converter;
        GroupName = groupName;

        Replace(Categories, _converter.Categories);
        _synchronizingSelection = true;
        SelectedCategory = Categories.FirstOrDefault();
        _synchronizingSelection = false;
        if (SelectedCategory is not null)
        {
            _converter.SelectCategory(SelectedCategory.Id);
        }

        SynchronizeAll();
    }

    /// <summary>Raised when the user picks a category, which is also a mode change.</summary>
    public event Action<UnitConverterCategory>? CategorySelected;

    public string GroupName { get; }

    public ObservableCollection<UnitConverterCategory> Categories { get; } = [];
    public ObservableCollection<UnitConverterUnit> Definitions { get; } = [];
    public ObservableCollection<string> Suggestions { get; } = [];

    [ObservableProperty]
    public partial string FromDisplay { get; private set; } = "0";

    [ObservableProperty]
    public partial string ToDisplay { get; private set; } = "0";

    [ObservableProperty]
    public partial UnitConverterCategory? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial UnitConverterUnit? SelectedFromUnit { get; set; }

    [ObservableProperty]
    public partial UnitConverterUnit? SelectedToUnit { get; set; }

    [RelayCommand]
    private void SendCommand(string commandName)
    {
        _converter.SendCommand(Enum.Parse<UnitConverterCommand>(commandName, ignoreCase: false));
        SynchronizeDisplays();
    }

    /// <summary>
    /// Maps the shared Microsoft shortcut identifiers onto the converter
    /// engine. The converter deliberately accepts only its numeric editing
    /// subset even though digits live in the shared calculator catalog.
    /// </summary>
    public bool TryDispatchShortcut(string shortcutId)
    {
        var command = shortcutId switch
        {
            "num0Button" => UnitConverterCommand.Zero,
            "num1Button" => UnitConverterCommand.One,
            "num2Button" => UnitConverterCommand.Two,
            "num3Button" => UnitConverterCommand.Three,
            "num4Button" => UnitConverterCommand.Four,
            "num5Button" => UnitConverterCommand.Five,
            "num6Button" => UnitConverterCommand.Six,
            "num7Button" => UnitConverterCommand.Seven,
            "num8Button" => UnitConverterCommand.Eight,
            "num9Button" => UnitConverterCommand.Nine,
            "decimalSeparatorButton" => UnitConverterCommand.Decimal,
            "converterNegateButton" => UnitConverterCommand.Negate,
            "backSpaceButton" => UnitConverterCommand.Backspace,
            "clearButton" or "clearEntryButton" => UnitConverterCommand.Clear,
            _ => (UnitConverterCommand?)null,
        };
        if (command is null)
        {
            return false;
        }

        _converter.SendCommand(command.Value);
        SynchronizeDisplays();
        return true;
    }

    /// <summary>
    /// Replays pasted numeric text through the converter engine instead of
    /// parsing it into a framework-specific numeric type. This preserves the
    /// same locale-aware decimal and precision behavior as keypad entry.
    /// </summary>
    public bool TryPaste(string? text, string decimalSeparator)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var acceptedAny = false;
        var pendingNegate = false;
        var negateApplied = false;
        foreach (var character in text)
        {
            UnitConverterCommand? command = character switch
            {
                >= '0' and <= '9' => (UnitConverterCommand)(character - '0'),
                '.' or ',' when decimalSeparator.Contains(character, StringComparison.Ordinal)
                    => UnitConverterCommand.Decimal,
                '-' or '\u2212' => null,
                _ => null,
            };
            var isNegate = character is '-' or '\u2212';
            if (command is null && !isNegate)
            {
                continue;
            }

            if (!acceptedAny)
            {
                _converter.SendCommand(UnitConverterCommand.Clear);
                acceptedAny = true;
            }

            if (isNegate)
            {
                pendingNegate = !negateApplied;
                continue;
            }

            _converter.SendCommand(command!.Value);
            if (pendingNegate && !negateApplied)
            {
                _converter.SendCommand(UnitConverterCommand.Negate);
                negateApplied = true;
            }
        }

        if (acceptedAny)
        {
            SynchronizeDisplays();
        }
        return acceptedAny;
    }

    [RelayCommand]
    private void Swap()
    {
        _converter.SwitchActive(ToDisplay);
        SynchronizeAll();
    }

    /// <summary>
    /// Selects a category on behalf of the navigation pane. The first visit to a
    /// category has no units chosen yet, which is why the engine is only asked
    /// to re-select when nothing is set.
    /// </summary>
    public void SelectCategoryForMode(int categoryId)
    {
        var category = Categories.FirstOrDefault(value => value.Id == categoryId);
        if (category is null)
        {
            return;
        }

        SelectedCategory = category;
        if (_converter.SelectedUnits.FromUnitId < 0)
        {
            _converter.SelectCategory(category.Id);
            SynchronizeAll();
        }
    }

    public void Dispose() => _converter.Dispose();

    partial void OnSelectedCategoryChanged(UnitConverterCategory? value)
    {
        if (value is null || _synchronizingSelection)
        {
            return;
        }

        _converter.SelectCategory(value.Id);
        CategorySelected?.Invoke(value);
        SynchronizeAll();
    }

    partial void OnSelectedFromUnitChanged(UnitConverterUnit? value) => ApplySelectedUnits();

    partial void OnSelectedToUnitChanged(UnitConverterUnit? value) => ApplySelectedUnits();

    private void ApplySelectedUnits()
    {
        if (_synchronizingSelection || SelectedFromUnit is null || SelectedToUnit is null)
        {
            return;
        }

        _converter.SetUnits(SelectedFromUnit.Id, SelectedToUnit.Id);
        SynchronizeDisplays();
    }

    private void SynchronizeAll()
    {
        _synchronizingSelection = true;
        try
        {
            var units = _converter.Units.Where(unit => !unit.IsWhimsical).ToArray();
            Replace(Definitions, units);
            var selected = _converter.SelectedUnits;
            SelectedFromUnit = units.FirstOrDefault(unit => unit.Id == selected.FromUnitId);
            SelectedToUnit = units.FirstOrDefault(unit => unit.Id == selected.ToUnitId);
        }
        finally
        {
            _synchronizingSelection = false;
        }

        SynchronizeDisplays();
    }

    private void SynchronizeDisplays()
    {
        FromDisplay = _converter.FromDisplay;
        ToDisplay = _converter.ToDisplay;
        var abbreviations = _converter.Units.ToDictionary(unit => unit.Id, unit => unit.Abbreviation);
        Replace(Suggestions, _converter.Suggestions.Select(suggestion =>
            abbreviations.TryGetValue(suggestion.UnitId, out var abbreviation)
                ? $"{suggestion.Value} {abbreviation}"
                : suggestion.Value));
    }

    private static void Replace<T>(ObservableCollection<T> target, System.Collections.Generic.IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}

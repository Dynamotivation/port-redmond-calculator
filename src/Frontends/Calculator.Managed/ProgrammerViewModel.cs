using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Calculator.Managed;

/// <summary>
/// Programmer-only state: radix, word size, shift mode, the four radix
/// displays and the 64-bit flip surface.
/// </summary>
/// <remarks>
/// Which digits the keypad may accept follows from the radix, so those
/// predicates live here rather than on the shell. Error state does not: it
/// belongs to the shared session, so it is read through a callback and folded
/// into the enablement rules.
///
/// The shift commands go back through the shell's command path rather than
/// straight to the engine, because a shift is an ordinary calculator command
/// and has to pass the same error handling as any other.
/// </remarks>
public sealed partial class ProgrammerViewModel : ObservableObject
{
    private readonly NativeCalculator _calculator;
    private readonly Action _synchronize;
    private readonly Action<CalculatorCommand> _executeCommand;
    private readonly Func<bool> _isError;
    private readonly Func<string> _primaryDisplay;

    public ProgrammerViewModel(
        NativeCalculator calculator,
        Action synchronize,
        Action<CalculatorCommand> executeCommand,
        Func<bool> isError,
        Func<string> primaryDisplay,
        ProgrammerStrings strings)
    {
        _calculator = calculator;
        _synchronize = synchronize;
        _executeCommand = executeCommand;
        _isError = isError;
        _primaryDisplay = primaryDisplay;
        Strings = strings;

        BuildBitGroups();
    }

    public ProgrammerStrings Strings { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHexadecimal))]
    [NotifyPropertyChangedFor(nameof(IsDecimal))]
    [NotifyPropertyChangedFor(nameof(IsOctal))]
    [NotifyPropertyChangedFor(nameof(IsBinary))]
    [NotifyPropertyChangedFor(nameof(AreHexDigitsEnabled))]
    [NotifyPropertyChangedFor(nameof(AreEightAndNineEnabled))]
    [NotifyPropertyChangedFor(nameof(AreTwoThroughSevenEnabled))]
    public partial CalculatorProgrammerRadix SelectedRadix { get; private set; } = CalculatorProgrammerRadix.Decimal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WordSizeLabel))]
    public partial CalculatorProgrammerWordSize SelectedWordSize { get; private set; } = CalculatorProgrammerWordSize.Qword;

    [ObservableProperty]
    public partial bool IsBitFlipMode { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsArithmeticShift))]
    [NotifyPropertyChangedFor(nameof(IsLogicalShift))]
    [NotifyPropertyChangedFor(nameof(IsRotateShift))]
    [NotifyPropertyChangedFor(nameof(IsRotateCarryShift))]
    public partial CalculatorProgrammerShiftMode SelectedShiftMode { get; private set; } = CalculatorProgrammerShiftMode.Arithmetic;

    [ObservableProperty] public partial string HexDisplay { get; private set; } = "0";
    [ObservableProperty] public partial string DecimalDisplay { get; private set; } = "0";
    [ObservableProperty] public partial string OctalDisplay { get; private set; } = "0";
    [ObservableProperty] public partial string BinaryDisplay { get; private set; } = "0";

    public ObservableCollection<CalculatorProgrammerBitGroup> BitGroups { get; } = [];

    public bool IsHexadecimal => SelectedRadix == CalculatorProgrammerRadix.Hexadecimal;
    public bool IsDecimal => SelectedRadix == CalculatorProgrammerRadix.Decimal;
    public bool IsOctal => SelectedRadix == CalculatorProgrammerRadix.Octal;
    public bool IsBinary => SelectedRadix == CalculatorProgrammerRadix.Binary;
    public bool IsArithmeticShift => SelectedShiftMode == CalculatorProgrammerShiftMode.Arithmetic;
    public bool IsLogicalShift => SelectedShiftMode == CalculatorProgrammerShiftMode.Logical;
    public bool IsRotateShift => SelectedShiftMode == CalculatorProgrammerShiftMode.Rotate;
    public bool IsRotateCarryShift => SelectedShiftMode == CalculatorProgrammerShiftMode.RotateCarry;

    public bool AreHexDigitsEnabled => IsHexadecimal && !_isError();
    public bool AreEightAndNineEnabled =>
        SelectedRadix is CalculatorProgrammerRadix.Decimal or CalculatorProgrammerRadix.Hexadecimal && !_isError();
    public bool AreTwoThroughSevenEnabled => SelectedRadix != CalculatorProgrammerRadix.Binary && !_isError();

    public string WordSizeLabel => SelectedWordSize.ToString().ToUpperInvariant();

    [RelayCommand]
    private void SelectRadix(string radixName)
    {
        var radix = Enum.Parse<CalculatorProgrammerRadix>(radixName, ignoreCase: false);
        SelectedRadix = radix;
        _calculator.SendCommand(radix switch
        {
            CalculatorProgrammerRadix.Hexadecimal => CalculatorCommand.Hex,
            CalculatorProgrammerRadix.Decimal => CalculatorCommand.Dec,
            CalculatorProgrammerRadix.Octal => CalculatorCommand.Oct,
            _ => CalculatorCommand.Bin,
        });
        _synchronize();
    }

    [RelayCommand]
    private void CycleWordSize() =>
        SelectWordSize((SelectedWordSize switch
        {
            CalculatorProgrammerWordSize.Qword => CalculatorProgrammerWordSize.Dword,
            CalculatorProgrammerWordSize.Dword => CalculatorProgrammerWordSize.Word,
            CalculatorProgrammerWordSize.Word => CalculatorProgrammerWordSize.Byte,
            _ => CalculatorProgrammerWordSize.Qword,
        }).ToString());

    [RelayCommand]
    private void SelectWordSize(string wordSizeName)
    {
        SelectedWordSize = Enum.Parse<CalculatorProgrammerWordSize>(wordSizeName, ignoreCase: false);
        _calculator.SendCommand(SelectedWordSize switch
        {
            CalculatorProgrammerWordSize.Qword => CalculatorCommand.Qword,
            CalculatorProgrammerWordSize.Dword => CalculatorCommand.Dword,
            CalculatorProgrammerWordSize.Word => CalculatorCommand.Word,
            _ => CalculatorCommand.Byte,
        });
        OnPropertyChanged(nameof(WordSizeLabel));
        _synchronize();
    }

    [RelayCommand]
    private void ToggleBitFlip() => IsBitFlipMode = !IsBitFlipMode;

    [RelayCommand]
    private void SelectShiftMode(string modeName) =>
        SelectedShiftMode = Enum.Parse<CalculatorProgrammerShiftMode>(modeName, ignoreCase: false);

    [RelayCommand]
    private void ExecuteLeftShift() =>
        _executeCommand(SelectedShiftMode switch
        {
            CalculatorProgrammerShiftMode.Rotate => CalculatorCommand.RotateLeft,
            CalculatorProgrammerShiftMode.RotateCarry => CalculatorCommand.RotateLeftCarry,
            _ => CalculatorCommand.LeftShift,
        });

    [RelayCommand]
    private void ExecuteRightShift() =>
        _executeCommand(SelectedShiftMode switch
        {
            CalculatorProgrammerShiftMode.Logical => CalculatorCommand.LogicalRightShift,
            CalculatorProgrammerShiftMode.Rotate => CalculatorCommand.RotateRight,
            CalculatorProgrammerShiftMode.RotateCarry => CalculatorCommand.RotateRightCarry,
            _ => CalculatorCommand.RightShift,
        });

    [RelayCommand]
    private void FlipBit(CalculatorProgrammerBit? bit)
    {
        if (bit is null || !bit.IsEnabled || _isError())
        {
            return;
        }

        _calculator.SendCommand((CalculatorCommand)(700 + bit.Index));
        _synchronize();
    }

    /// <summary>
    /// Entering Programmer starts from decimal, a 64-bit word, the arithmetic
    /// shift and the numeric keypad, matching the source application.
    /// </summary>
    public void ResetForModeEntry()
    {
        SelectedRadix = CalculatorProgrammerRadix.Decimal;
        SelectedWordSize = CalculatorProgrammerWordSize.Qword;
        IsBitFlipMode = false;
        SelectedShiftMode = CalculatorProgrammerShiftMode.Arithmetic;
        OnPropertyChanged(nameof(WordSizeLabel));
    }

    /// <summary>
    /// Re-sends the current radix and word size. A paste resets the engine, so
    /// the mode has to be re-established before the pasted text is replayed.
    /// </summary>
    public void ApplyRadixAndWordSize()
    {
        _calculator.SendCommand(SelectedRadix switch
        {
            CalculatorProgrammerRadix.Hexadecimal => CalculatorCommand.Hex,
            CalculatorProgrammerRadix.Decimal => CalculatorCommand.Dec,
            CalculatorProgrammerRadix.Octal => CalculatorCommand.Oct,
            _ => CalculatorCommand.Bin,
        });
        _calculator.SendCommand(SelectedWordSize switch
        {
            CalculatorProgrammerWordSize.Qword => CalculatorCommand.Qword,
            CalculatorProgrammerWordSize.Dword => CalculatorCommand.Dword,
            CalculatorProgrammerWordSize.Word => CalculatorCommand.Word,
            _ => CalculatorCommand.Byte,
        });
    }

    internal void Refresh()
    {
        var isError = _isError();

        if (!isError)
        {
            HexDisplay = _calculator.GetResultForRadix(16);
            DecimalDisplay = _calculator.GetResultForRadix(10);
            OctalDisplay = _calculator.GetResultForRadix(8);
            BinaryDisplay = _calculator.GetResultForRadix(2);
            if (IsBinary && BinaryDisplay != "0")
            {
                // The source pads the binary display to a whole nibble.
                var binaryDigitCount = BinaryDisplay.Count(character => character is '0' or '1');
                var padding = (4 - binaryDigitCount % 4) % 4;
                BinaryDisplay = new string('0', padding) + BinaryDisplay;
            }
        }
        else
        {
            HexDisplay = DecimalDisplay = OctalDisplay = BinaryDisplay = _primaryDisplay();
        }

        var rawBinary = isError ? string.Empty : _calculator.GetResultForRadix(2, 64, false);
        rawBinary = new string(rawBinary.Where(character => character is '0' or '1').ToArray());
        var width = (int)SelectedWordSize;
        foreach (var group in BitGroups)
        {
            foreach (var bit in group.Bits)
            {
                bit.IsEnabled = bit.Index < width && !isError;
                var sourceIndex = rawBinary.Length - 1 - bit.Index;
                bit.IsSet = sourceIndex >= 0 && rawBinary[sourceIndex] == '1';
            }
        }

        // Digit enablement depends on error state, which this type does not own.
        OnPropertyChanged(nameof(AreHexDigitsEnabled));
        OnPropertyChanged(nameof(AreEightAndNineEnabled));
        OnPropertyChanged(nameof(AreTwoThroughSevenEnabled));
    }

    private void BuildBitGroups()
    {
        for (var highBit = 63; highBit >= 3; highBit -= 4)
        {
            var bits = new ObservableCollection<CalculatorProgrammerBit>();
            for (var bit = highBit; bit > highBit - 4; bit--)
            {
                bits.Add(new CalculatorProgrammerBit(bit));
            }

            BitGroups.Add(new CalculatorProgrammerBitGroup(
                (highBit - 3).ToString(CultureInfo.InvariantCulture),
                bits));
        }
    }
}

/// <summary>Localized strings for the programmer operator panel.</summary>
public sealed record ProgrammerStrings(
    string BitwiseName,
    string BitShiftName,
    string ArithmeticShiftName,
    string LogicalShiftName,
    string RotateCircularShiftName,
    string RotateCarryShiftName);

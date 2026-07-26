using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Calculator.Managed;

public enum CalculatorProgrammerRadix
{
    Hexadecimal = 16,
    Decimal = 10,
    Octal = 8,
    Binary = 2,
}

public enum CalculatorProgrammerWordSize
{
    Qword = 64,
    Dword = 32,
    Word = 16,
    Byte = 8,
}

public enum CalculatorProgrammerShiftMode
{
    Arithmetic,
    Logical,
    Rotate,
    RotateCarry,
}

public sealed partial class CalculatorProgrammerBit(int index) : ObservableObject
{
    public int Index { get; } = index;

    [ObservableProperty]
    public partial bool IsSet { get; internal set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; internal set; } = true;
}

public sealed record CalculatorProgrammerBitGroup(
    string Label,
    ObservableCollection<CalculatorProgrammerBit> Bits);

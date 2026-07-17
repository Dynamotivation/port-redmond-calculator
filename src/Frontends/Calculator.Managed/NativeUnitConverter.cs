using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel.Resources;

namespace Calculator.Managed;

public enum UnitConverterCommand
{
    Zero,
    One,
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Decimal,
    Negate,
    Backspace,
    Clear,
    Reset,
    None,
}

public sealed record UnitConverterCategory(int Id, string Name, bool SupportsNegative);

public sealed record UnitConverterUnit(
    int Id,
    string Name,
    string Abbreviation,
    bool IsConversionSource,
    bool IsConversionTarget,
    bool IsWhimsical);

public sealed record UnitConverterSuggestion(int UnitId, string Value);

public sealed unsafe class NativeUnitConverter : IDisposable
{
    private nint handle;

    public NativeUnitConverter(ResourceLoader resourceLoader, string regionCode)
    {
        ArgumentNullException.ThrowIfNull(resourceLoader);
        ArgumentException.ThrowIfNullOrWhiteSpace(regionCode);

        var resources = resourceLoader.GetAllStrings().ToArray();
        var nativeEntries = new NativeResourceEntry[resources.Length];
        var allocations = new List<nint>(resources.Length * 2);
        try
        {
            for (var index = 0; index < resources.Length; index++)
            {
                var (key, value) = resources[index];
                var nativeKey = Marshal.StringToCoTaskMemUTF8(key);
                var nativeValue = Marshal.StringToCoTaskMemUTF8(value);
                allocations.Add(nativeKey);
                allocations.Add(nativeValue);
                nativeEntries[index] = new NativeResourceEntry { Key = nativeKey, Value = nativeValue };
            }

            fixed (NativeResourceEntry* entries = nativeEntries)
            {
                ThrowIfFailed(NativeMethods.UnitConverterCreate(entries, (nuint)nativeEntries.Length, regionCode, out handle));
            }
        }
        finally
        {
            foreach (var allocation in allocations)
            {
                Marshal.FreeCoTaskMem(allocation);
            }
        }
    }

    public string FromDisplay => ReadUtf8(NativeMethods.UnitConverterGetFromDisplay);
    public string ToDisplay => ReadUtf8(NativeMethods.UnitConverterGetToDisplay);

    public IReadOnlyList<UnitConverterCategory> Categories
    {
        get
        {
            ThrowIfFailed(NativeMethods.UnitConverterGetCategoryCount(Handle, out var count));
            var result = new UnitConverterCategory[checked((int)count)];
            for (nuint index = 0; index < count; index++)
            {
                ThrowIfFailed(NativeMethods.UnitConverterGetCategoryInfo(Handle, index, out var info));
                var name = ReadUtf8((nint value, byte* buffer, nuint size, out nuint required) =>
                    NativeMethods.UnitConverterGetCategoryName(value, index, buffer, size, out required));
                result[checked((int)index)] = new UnitConverterCategory(info.Id, name, info.SupportsNegative != 0);
            }
            return result;
        }
    }

    public IReadOnlyList<UnitConverterUnit> Units
    {
        get
        {
            ThrowIfFailed(NativeMethods.UnitConverterGetUnitCount(Handle, out var count));
            var result = new UnitConverterUnit[checked((int)count)];
            for (nuint index = 0; index < count; index++)
            {
                ThrowIfFailed(NativeMethods.UnitConverterGetUnitInfo(Handle, index, out var info));
                var name = ReadUtf8((nint value, byte* buffer, nuint size, out nuint required) =>
                    NativeMethods.UnitConverterGetUnitName(value, index, buffer, size, out required));
                var abbreviation = ReadUtf8((nint value, byte* buffer, nuint size, out nuint required) =>
                    NativeMethods.UnitConverterGetUnitAbbreviation(value, index, buffer, size, out required));
                result[checked((int)index)] = new UnitConverterUnit(
                    info.Id, name, abbreviation, info.IsConversionSource != 0, info.IsConversionTarget != 0, info.IsWhimsical != 0);
            }
            return result;
        }
    }

    public (int FromUnitId, int ToUnitId) SelectedUnits
    {
        get
        {
            ThrowIfFailed(NativeMethods.UnitConverterGetSelectedUnits(Handle, out var from, out var to));
            return (from, to);
        }
    }

    public IReadOnlyList<UnitConverterSuggestion> Suggestions
    {
        get
        {
            ThrowIfFailed(NativeMethods.UnitConverterGetSuggestionCount(Handle, out var count));
            var result = new UnitConverterSuggestion[checked((int)count)];
            for (nuint index = 0; index < count; index++)
            {
                var unitId = -1;
                var value = ReadUtf8((nint nativeHandle, byte* buffer, nuint size, out nuint required) =>
                    NativeMethods.UnitConverterGetSuggestion(nativeHandle, index, out unitId, buffer, size, out required));
                result[checked((int)index)] = new UnitConverterSuggestion(unitId, value);
            }
            return result;
        }
    }

    public ulong MaxDigitsReachedCount
    {
        get
        {
            ThrowIfFailed(NativeMethods.UnitConverterGetMaxDigitsReachedCount(Handle, out var count));
            return count;
        }
    }

    public void SelectCategory(int categoryId) => ThrowIfFailed(NativeMethods.UnitConverterSelectCategory(Handle, categoryId));
    public void SetUnits(int fromUnitId, int toUnitId) => ThrowIfFailed(NativeMethods.UnitConverterSetUnits(Handle, fromUnitId, toUnitId));
    public void SendCommand(UnitConverterCommand command) => ThrowIfFailed(NativeMethods.UnitConverterSendCommand(Handle, (int)command));
    public void SwitchActive(string currentValue) => ThrowIfFailed(NativeMethods.UnitConverterSwitchActive(Handle, currentValue));

    public void Dispose()
    {
        if (handle != 0)
        {
            NativeMethods.UnitConverterDestroy(handle);
            handle = 0;
        }
        GC.SuppressFinalize(this);
    }

    private nint Handle => handle != 0 ? handle : throw new ObjectDisposedException(nameof(NativeUnitConverter));

    private string ReadUtf8(GetString getString)
    {
        ThrowIfFailed(getString(Handle, null, 0, out var requiredSize));
        if (requiredSize <= 1)
        {
            return string.Empty;
        }
        var buffer = new byte[checked((int)requiredSize)];
        fixed (byte* pointer = buffer)
        {
            ThrowIfFailed(getString(Handle, pointer, requiredSize, out _));
        }
        return Encoding.UTF8.GetString(buffer, 0, buffer.Length - 1);
    }

    private static void ThrowIfFailed(NativeStatus status)
    {
        if (status == NativeStatus.Ok)
        {
            return;
        }
        var error = Marshal.PtrToStringUTF8(NativeMethods.GetLastError());
        throw new InvalidOperationException($"Native unit converter failed with {status}: {error}");
    }

    private delegate NativeStatus GetString(nint handle, byte* buffer, nuint bufferSize, out nuint requiredSize);
}

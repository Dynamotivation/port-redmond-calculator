using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel.Resources;

namespace Calculator.Managed;

public sealed unsafe class NativeCalculator : IDisposable
{
    private nint _handle;

    public NativeCalculator(ResourceLoader resourceLoader, CalculatorNumberFormat numberFormat)
    {
        ArgumentNullException.ThrowIfNull(resourceLoader);
        ArgumentNullException.ThrowIfNull(numberFormat);
        if (NativeMethods.AbiVersion() != 1)
        {
            throw new NotSupportedException("Unsupported native Calculator ABI version.");
        }

        var localizedResources = resourceLoader.GetAllStrings().ToDictionary(entry => entry.Key, entry => entry.Value);
        localizedResources["sDecimal"] = numberFormat.DecimalSeparator;
        localizedResources["sThousand"] = numberFormat.NumberGroupSeparator;
        localizedResources["sGrouping"] = numberFormat.NumberGrouping;
        var resources = localizedResources.ToArray();
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

            unsafe
            {
                fixed (NativeResourceEntry* entries = nativeEntries)
                {
                    ThrowIfFailed(NativeMethods.Create(entries, (nuint)nativeEntries.Length, 0, out _handle));
                }
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

    public string PrimaryDisplay => ReadUtf8(NativeMethods.GetPrimaryDisplay);
    public string ExpressionDisplay => ReadUtf8(NativeMethods.GetExpressionDisplay);

    public string GetResultForRadix(uint radix, int precision = 64, bool groupDigitsPerRadix = true) =>
        ReadUtf8((nint handle, byte* buffer, nuint bufferSize, out nuint requiredSize) =>
            NativeMethods.GetResultForRadix(
                handle, radix, precision, groupDigitsPerRadix ? 1 : 0,
                buffer, bufferSize, out requiredSize));
    public bool IsError => NativeMethods.IsError(Handle) != 0;
    public bool IsInputEmpty => NativeMethods.IsInputEmpty(Handle) != 0;
    public CalculatorEventState EventState
    {
        get
        {
            ThrowIfFailed(NativeMethods.GetEventState(Handle, out var state));
            return state;
        }
    }

    public IReadOnlyList<string> MemoryValues
    {
        get
        {
            ThrowIfFailed(NativeMethods.GetMemoryCount(Handle, out var count));
            var values = new string[checked((int)count)];
            for (nuint index = 0; index < count; index++)
            {
                values[checked((int)index)] = ReadUtf8((nint handle, byte* buffer, nuint size, out nuint required) =>
                    NativeMethods.GetMemoryValue(handle, index, buffer, size, out required));
            }
            return values;
        }
    }

    public IReadOnlyList<CalculatorHistoryEntry> History
    {
        get
        {
            ThrowIfFailed(NativeMethods.GetHistoryCount(Handle, out var count));
            var values = new CalculatorHistoryEntry[checked((int)count)];
            for (nuint index = 0; index < count; index++)
            {
                var expression = ReadUtf8((nint handle, byte* buffer, nuint size, out nuint required) =>
                    NativeMethods.GetHistoryExpression(handle, index, buffer, size, out required));
                var result = ReadUtf8((nint handle, byte* buffer, nuint size, out nuint required) =>
                    NativeMethods.GetHistoryResult(handle, index, buffer, size, out required));
                values[checked((int)index)] = new CalculatorHistoryEntry(expression, result);
            }
            return values;
        }
    }

    public void SendCommand(CalculatorCommand command)
    {
        ThrowIfFailed(NativeMethods.SendCommand(Handle, (int)command));
    }

    public void Reset(bool clearMemory = true)
    {
        ThrowIfFailed(NativeMethods.Reset(Handle, clearMemory ? 1 : 0));
    }

    public void SetMode(CalculatorMode mode) => ThrowIfFailed(NativeMethods.SetMode(Handle, mode));

    public void MemoryStore() => ThrowIfFailed(NativeMethods.MemoryStore(Handle));
    public void MemoryRecall(nuint index = 0) => ThrowIfFailed(NativeMethods.MemoryRecall(Handle, index));
    public void MemoryAdd(nuint index = 0) => ThrowIfFailed(NativeMethods.MemoryAdd(Handle, index));
    public void MemorySubtract(nuint index = 0) => ThrowIfFailed(NativeMethods.MemorySubtract(Handle, index));
    public void MemoryClear(nuint index = 0) => ThrowIfFailed(NativeMethods.MemoryClear(Handle, index));
    public void MemoryClearAll() => ThrowIfFailed(NativeMethods.MemoryClearAll(Handle));
    public void HistoryRemove(nuint index) => ThrowIfFailed(NativeMethods.HistoryRemove(Handle, index));
    public void HistoryClear() => ThrowIfFailed(NativeMethods.HistoryClear(Handle));

    public void Dispose()
    {
        if (_handle != 0)
        {
            NativeMethods.Destroy(_handle);
            _handle = 0;
        }
        GC.SuppressFinalize(this);
    }

    private nint Handle => _handle != 0 ? _handle : throw new ObjectDisposedException(nameof(NativeCalculator));

    private unsafe string ReadUtf8(GetString getString)
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
        throw new InvalidOperationException($"Native Calculator failed with {status}: {error}");
    }

    private unsafe delegate NativeStatus GetString(nint handle, byte* buffer, nuint bufferSize, out nuint requiredSize);
}

public sealed record CalculatorHistoryEntry(string Expression, string Result);

using System.Runtime.InteropServices;

namespace Calculator.Managed;

internal enum NativeStatus
{
    Ok = 0,
    InvalidArgument = 1,
    EngineError = 2,
    InternalError = 3,
    BufferTooSmall = 4,
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeResourceEntry
{
    public nint Key;
    public nint Value;
}

[StructLayout(LayoutKind.Sequential)]
public struct CalculatorEventState
{
    public ulong NoRightParenthesisCount;
    public ulong MaxDigitsReachedCount;
    public ulong BinaryOperatorReceivedCount;
    public ulong HistoryItemAddedCount;
    public ulong MemoryItemChangedCount;
    public ulong InputChangedCount;
    public uint ParenthesisCount;
    public uint LastHistoryItemIndex;
    public uint LastMemoryItemIndex;
}

internal static unsafe partial class NativeMethods
{
    private const string LibraryName = "calculator_engine";

    [LibraryImport(LibraryName, EntryPoint = "calculator_native_abi_version")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial uint AbiVersion();

    [LibraryImport(LibraryName, EntryPoint = "calculator_create")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus Create(
        NativeResourceEntry* resources,
        nuint resourceCount,
        nint callbacks,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "calculator_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void Destroy(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "calculator_reset")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus Reset(nint handle, int clearMemory);

    [LibraryImport(LibraryName, EntryPoint = "calculator_send_command")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus SendCommand(nint handle, int command);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_primary_display")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus GetPrimaryDisplay(nint handle, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_expression_display")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus GetExpressionDisplay(nint handle, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_is_error")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int IsError(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_event_state")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus GetEventState(nint handle, out CalculatorEventState result);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_memory_count")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus GetMemoryCount(nint handle, out nuint count);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_memory_value")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus GetMemoryValue(nint handle, nuint index, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_memory_store")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus MemoryStore(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "calculator_memory_recall")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus MemoryRecall(nint handle, nuint index);

    [LibraryImport(LibraryName, EntryPoint = "calculator_memory_add")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus MemoryAdd(nint handle, nuint index);

    [LibraryImport(LibraryName, EntryPoint = "calculator_memory_subtract")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus MemorySubtract(nint handle, nuint index);

    [LibraryImport(LibraryName, EntryPoint = "calculator_memory_clear")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus MemoryClear(nint handle, nuint index);

    [LibraryImport(LibraryName, EntryPoint = "calculator_memory_clear_all")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus MemoryClearAll(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_history_count")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus GetHistoryCount(nint handle, out nuint count);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_history_expression")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus GetHistoryExpression(nint handle, nuint index, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_history_result")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus GetHistoryResult(nint handle, nuint index, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_history_remove")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus HistoryRemove(nint handle, nuint index);

    [LibraryImport(LibraryName, EntryPoint = "calculator_history_clear")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus HistoryClear(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_last_error")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial nint GetLastError();
}

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

[StructLayout(LayoutKind.Sequential)]
internal struct NativeUnitCategoryInfo
{
    public int Id;
    public int SupportsNegative;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeUnitInfo
{
    public int Id;
    public int IsConversionSource;
    public int IsConversionTarget;
    public int IsWhimsical;
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

    [LibraryImport(LibraryName, EntryPoint = "calculator_set_mode")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus SetMode(nint handle, CalculatorMode mode);

    [LibraryImport(LibraryName, EntryPoint = "calculator_send_command")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus SendCommand(nint handle, int command);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_primary_display")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus GetPrimaryDisplay(nint handle, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_expression_display")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus GetExpressionDisplay(nint handle, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_result_for_radix")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus GetResultForRadix(
        nint handle, uint radix, int precision, int groupDigitsPerRadix,
        byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_is_error")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int IsError(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_is_input_empty")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int IsInputEmpty(nint handle);

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

    [LibraryImport(LibraryName, EntryPoint = "calculator_history_recall")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus HistoryRecall(nint handle, nuint index, int scientificNotationEnabled);

    [LibraryImport(LibraryName, EntryPoint = "calculator_history_remove")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus HistoryRemove(nint handle, nuint index);

    [LibraryImport(LibraryName, EntryPoint = "calculator_history_clear")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus HistoryClear(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_create", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterCreate(
        NativeResourceEntry* resources,
        nuint resourceCount,
        string regionCode,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void UnitConverterDestroy(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_category_count")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetCategoryCount(nint handle, out nuint count);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_category_info")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetCategoryInfo(nint handle, nuint index, out NativeUnitCategoryInfo result);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_category_name")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetCategoryName(nint handle, nuint index, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_select_category")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterSelectCategory(nint handle, int categoryId);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_unit_count")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetUnitCount(nint handle, out nuint count);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_unit_info")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetUnitInfo(nint handle, nuint index, out NativeUnitInfo result);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_unit_name")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetUnitName(nint handle, nuint index, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_unit_abbreviation")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetUnitAbbreviation(nint handle, nuint index, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_selected_units")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetSelectedUnits(nint handle, out int fromUnitId, out int toUnitId);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_set_units")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterSetUnits(nint handle, int fromUnitId, int toUnitId);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_send_command")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterSendCommand(nint handle, int command);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_switch_active", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterSwitchActive(nint handle, string currentValue);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_from_display")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetFromDisplay(nint handle, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_to_display")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetToDisplay(nint handle, byte* buffer, nuint bufferSize, out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_suggestion_count")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetSuggestionCount(nint handle, out nuint count);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_suggestion")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetSuggestion(
        nint handle,
        nuint index,
        out int unitId,
        byte* buffer,
        nuint bufferSize,
        out nuint requiredSize);

    [LibraryImport(LibraryName, EntryPoint = "calculator_unit_converter_get_max_digits_reached_count")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeStatus UnitConverterGetMaxDigitsReachedCount(nint handle, out ulong count);

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_last_error")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial nint GetLastError();
}

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

    [LibraryImport(LibraryName, EntryPoint = "calculator_get_last_error")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial nint GetLastError();
}

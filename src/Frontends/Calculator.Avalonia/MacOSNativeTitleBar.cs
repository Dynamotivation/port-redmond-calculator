using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Calculator.Avalonia;

internal static class MacOSNativeTitleBar
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private const nint TitleVisible = 0;
    private const nint TitleHidden = 1;

    public static void Apply(Window window, bool enabled)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var platformHandle = window.TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == IntPtr.Zero)
        {
            return;
        }

        var nsWindow = platformHandle.Handle;
        SendBool(nsWindow, Selector("setTitlebarAppearsTransparent:"), enabled);
        SendNInt(nsWindow, Selector("setTitleVisibility:"), enabled ? TitleHidden : TitleVisible);
    }

    private static IntPtr Selector(string name) => sel_registerName(name);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendNInt(IntPtr receiver, IntPtr selector, nint value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);
}

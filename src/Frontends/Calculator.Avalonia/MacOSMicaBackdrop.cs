using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Calculator.Avalonia;

internal sealed class MacOSMicaBackdrop : IDisposable
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private const nint NSWindowBelow = -1;
    private const nint NSWindowStyleMaskResizable = 1 << 3;
    private const nint NSViewWidthSizable = 2;
    private const nint NSViewHeightSizable = 16;
    private const nint NSVisualEffectMaterialHeaderView = 10;
    private const nint NSVisualEffectBlendingModeBehindWindow = 0;
    private const nint NSVisualEffectStateActive = 1;

    private IntPtr _effectView;

    private MacOSMicaBackdrop(IntPtr effectView) => _effectView = effectView;

    public static MacOSMicaBackdrop? Attach(Window window, double cornerRadius)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        var platformHandle = window.TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == IntPtr.Zero)
        {
            return null;
        }

        var nsWindow = platformHandle.Handle;
        var styleMask = (nint)SendIntPtr(nsWindow, Selector("styleMask"));
        SendNInt(nsWindow, Selector("setStyleMask:"), styleMask | NSWindowStyleMaskResizable);

        var contentView = SendIntPtr(nsWindow, Selector("contentView"));
        var frameView = SendIntPtr(contentView, Selector("superview"));
        if (contentView == IntPtr.Zero || frameView == IntPtr.Zero)
        {
            return null;
        }

        var effectClass = objc_getClass("NSVisualEffectView");
        var effectView = SendIntPtr(effectClass, Selector("alloc"));
        effectView = SendRectReturningIntPtr(
            effectView,
            Selector("initWithFrame:"),
            new NSRect(0, 0, window.Bounds.Width, window.Bounds.Height));

        SendNInt(effectView, Selector("setAutoresizingMask:"), NSViewWidthSizable | NSViewHeightSizable);
        SendNInt(effectView, Selector("setMaterial:"), NSVisualEffectMaterialHeaderView);
        SendNInt(effectView, Selector("setBlendingMode:"), NSVisualEffectBlendingModeBehindWindow);
        SendNInt(effectView, Selector("setState:"), NSVisualEffectStateActive);
        SendBool(effectView, Selector("setWantsLayer:"), true);

        var layer = SendIntPtr(effectView, Selector("layer"));
        SendDouble(layer, Selector("setCornerRadius:"), cornerRadius);
        SendBool(layer, Selector("setMasksToBounds:"), true);

        SendBool(nsWindow, Selector("setOpaque:"), false);
        var nsColorClass = objc_getClass("NSColor");
        var clearColor = SendIntPtr(nsColorClass, Selector("clearColor"));
        SendIntPtrArgument(nsWindow, Selector("setBackgroundColor:"), clearColor);

        SendAddSubview(frameView, Selector("addSubview:positioned:relativeTo:"), effectView, NSWindowBelow, contentView);
        return new MacOSMicaBackdrop(effectView);
    }

    public void Dispose()
    {
        if (_effectView == IntPtr.Zero)
        {
            return;
        }

        SendVoid(_effectView, Selector("removeFromSuperview"));
        SendVoid(_effectView, Selector("release"));
        _effectView = IntPtr.Zero;
    }

    private static IntPtr Selector(string name) => sel_registerName(name);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NSRect(double X, double Y, double Width, double Height);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendIntPtr(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendRectReturningIntPtr(IntPtr receiver, IntPtr selector, NSRect rect);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendNInt(IntPtr receiver, IntPtr selector, nint value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendDouble(IntPtr receiver, IntPtr selector, double value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendIntPtrArgument(IntPtr receiver, IntPtr selector, IntPtr value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendAddSubview(
        IntPtr receiver,
        IntPtr selector,
        IntPtr view,
        nint positioned,
        IntPtr relativeTo);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(IntPtr receiver, IntPtr selector);
}

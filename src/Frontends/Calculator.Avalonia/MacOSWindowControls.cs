using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Calculator.Avalonia;

internal sealed class MacOSWindowControls : IDisposable
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private const nint NSWindowAbove = 1;
    private const nint NSViewMinYMargin = 8;
    private const nint NativeButtonStyleMask = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3);

    private readonly IntPtr[] _buttons;

    private MacOSWindowControls(IntPtr[] buttons) => _buttons = buttons;

    public static MacOSWindowControls? Attach(Window window)
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
        var contentView = SendIntPtr(nsWindow, Selector("contentView"));
        var frameView = SendIntPtr(contentView, Selector("superview"));
        if (contentView == IntPtr.Zero || frameView == IntPtr.Zero)
        {
            return null;
        }

        var frameBounds = SendRect(frameView, Selector("bounds"));
        var windowClass = objc_getClass("NSWindow");
        var buttons = new IntPtr[3];
        var actions = new[] { "performClose:", "performMiniaturize:", "zoom:" };

        for (nint buttonType = 0; buttonType < buttons.Length; buttonType++)
        {
            var button = SendStandardWindowButton(
                windowClass,
                Selector("standardWindowButton:forStyleMask:"),
                buttonType,
                NativeButtonStyleMask);
            if (button == IntPtr.Zero)
            {
                DisposeButtons(buttons);
                return null;
            }

            SendVoid(button, Selector("retain"));
            SendIntPtrArgument(button, Selector("setTarget:"), nsWindow);
            SendIntPtrArgument(button, Selector("setAction:"), Selector(actions[(int)buttonType]));
            SendNInt(button, Selector("setAutoresizingMask:"), NSViewMinYMargin);

            var buttonFrame = SendRect(button, Selector("frame"));
            var origin = new NSPoint(
                14 + (buttonType * 20),
                frameBounds.Height - buttonFrame.Height - 14);
            SendPoint(button, Selector("setFrameOrigin:"), origin);
            SendAddSubview(frameView, Selector("addSubview:positioned:relativeTo:"), button, NSWindowAbove, contentView);
            buttons[(int)buttonType] = button;
        }

        return new MacOSWindowControls(buttons);
    }

    public void Dispose() => DisposeButtons(_buttons);

    private static void DisposeButtons(IntPtr[] buttons)
    {
        for (var index = 0; index < buttons.Length; index++)
        {
            if (buttons[index] == IntPtr.Zero)
            {
                continue;
            }

            SendVoid(buttons[index], Selector("removeFromSuperview"));
            SendVoid(buttons[index], Selector("release"));
            buttons[index] = IntPtr.Zero;
        }
    }

    private static IntPtr Selector(string name) => sel_registerName(name);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NSPoint(double X, double Y);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NSRect(double X, double Y, double Width, double Height);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendIntPtr(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern NSRect SendRect(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendStandardWindowButton(
        IntPtr receiver,
        IntPtr selector,
        nint buttonType,
        nint styleMask);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendPoint(IntPtr receiver, IntPtr selector, NSPoint point);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendNInt(IntPtr receiver, IntPtr selector, nint value);

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

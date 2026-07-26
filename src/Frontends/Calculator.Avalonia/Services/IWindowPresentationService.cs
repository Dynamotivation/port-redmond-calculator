using Calculator.Managed;

namespace Calculator.Avalonia.Services;

/// <summary>
/// Applies presentation preferences to the host window.
/// </summary>
/// <remarks>
/// This is the seam that keeps native window APIs out of the view layer. The
/// settings page edits preferences; something behind this interface turns them
/// into NSWindow calls, Avalonia window properties, or nothing at all. No
/// control may reach for AppKit directly, and a test host can substitute a
/// portable implementation to render window styling without a real window.
/// </remarks>
public interface IWindowPresentationService
{
    /// <summary>
    /// Applies backdrop, corner and control-style preferences together. They
    /// are one call because the macOS implementation has to sequence teardown
    /// and setup across them when the title bar changes kind.
    /// </summary>
    void ApplyAppearance(PlatformAppearancePreferences preferences);

    /// <summary>
    /// Called once the window is on screen; native decorations cannot be
    /// applied before a platform handle exists.
    /// </summary>
    void OnWindowOpened(PlatformAppearancePreferences preferences);

    /// <summary>Shrinks to the compact always-on-top presentation.</summary>
    void EnterCompactOverlay();

    /// <summary>Restores the placement captured on the way in.</summary>
    void ExitCompactOverlay();

    void Dispose();
}

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Calculator.Avalonia;

/// <summary>
/// Removes the part of a page covered by the translucent navigation pane.
/// The clip is discarded completely when the pane is closed.
/// </summary>
public sealed class NavigationCulledGrid : Grid
{
    public static readonly StyledProperty<double> CulledWidthProperty =
        AvaloniaProperty.Register<NavigationCulledGrid, double>(nameof(CulledWidth));

    public double CulledWidth
    {
        get => GetValue(CulledWidthProperty);
        set => SetValue(CulledWidthProperty, value);
    }

    public NavigationCulledGrid()
    {
        SizeChanged += (_, _) => UpdateClip();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CulledWidthProperty)
        {
            UpdateClip();
        }
    }

    private void UpdateClip()
    {
        var width = Math.Clamp(CulledWidth, 0, Bounds.Width);
        if (width <= 0.01)
        {
            Clip = null;
            return;
        }

        Clip = new RectangleGeometry(
            new Rect(width, 0, Math.Max(0, Bounds.Width - width), Bounds.Height));
    }
}

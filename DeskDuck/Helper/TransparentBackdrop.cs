using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using CompositionObject = Windows.UI.Composition.CompositionObject;
using Compositor = Windows.UI.Composition.Compositor;

namespace DeskDuck.Helper;

/// <summary>
/// Custom <see cref="SystemBackdrop"/> that renders a fully transparent background,
/// allowing the desktop and all underlying windows to show through the duck overlay.
/// This class belongs in the UI project because it directly extends a WinUI type
/// (<see cref="SystemBackdrop"/>) and should not be part of the framework-agnostic Core layer.
/// </summary>
public partial class TransparentBackdrop : SystemBackdrop
{
    /// <summary>
    /// Called when this backdrop is applied to a target window.
    /// Retrieves the underlying WinRT <see cref="Compositor"/> via the
    /// <see cref="CompositionObject"/> interface and assigns a fully transparent
    /// color brush so no backdrop color is painted.
    /// </summary>
    protected override void OnTargetConnected(
        ICompositionSupportsSystemBackdrop connectedTarget,
        XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        if (connectedTarget is CompositionObject compositionObject)
        {
            Compositor compositor = compositionObject.Compositor;
            var transparentBrush = compositor.CreateColorBrush(new Color { A = 0, R = 0, G = 0, B = 0 });
            connectedTarget.SystemBackdrop = transparentBrush;
        }
    }
}

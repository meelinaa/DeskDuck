
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using CompositionObject = Windows.UI.Composition.CompositionObject;
using Compositor = Windows.UI.Composition.Compositor;

namespace DeskDuck.Helper;

/// <summary>
/// Custom SystemBackdrop that renders a fully transparent background,
/// allowing the desktop and other windows to show through.
/// </summary>
public partial class TransparentBackdrop : SystemBackdrop
{
    protected override void OnTargetConnected(
        ICompositionSupportsSystemBackdrop connectedTarget,
        XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        // In WinUI 3, ICompositionSupportsSystemBackdrop is a WinRT interface.
        // Under the hood, we can retrieve the Windows.UI.Composition.Compositor.
        if (connectedTarget is CompositionObject compositionObject)
        {
            Compositor compositor = compositionObject.Compositor;
            var transparentBrush = compositor.CreateColorBrush(new Windows.UI.Color { A = 0, R = 0, G = 0, B = 0 });
            connectedTarget.SystemBackdrop = transparentBrush;
        }
    }
}

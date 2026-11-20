using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-13026] The picture of a drag of ours (<see cref="Themia.Sdk.WinUI.ItemDrag.RunningPicture"/>: Explorer's ghost of the
/// dragged icons) drawn by the surface itself while the drag is over it, on the ghost layer under the pointer, and XAML's
/// own copy of it hidden there (<c>DragUIOverride.IsContentVisible</c>). Over a fence that shows where the items would land
/// ([S-13021]) XAML's copy went (the owner, 2026-11-19: "when it shows the ghost preview in the fence it should not stop
/// showing the drag preview"); the surface's copy is there wherever the drag is on Themia's desktop, the fence's ghosts
/// beside it. Over another app's window the system's picture shows as before.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-13026] The drag's picture on the ghost layer, made on the first drag that has one.</summary>
    private Image? _dragImage;

    /// <summary>[S-13026] The picture <see cref="_dragImage"/> shows now (its pixels are copied once per drag).</summary>
    private DragPicture? _dragImageOf;

    /// <summary>
    /// [S-13026] Show the running drag's picture with its hot spot on <paramref name="pointer"/> (the root's DIPs). False when
    /// there is none to show (a drag from outside, a picture that could not be made): XAML's own picture stays then.
    /// </summary>
    private bool ShowDragPicture(DragPicture? picture, Point pointer)
    {
        if (picture is not { Drawable: true })
        {
            HideDragPicture();
            return false;
        }

        try
        {
            var image = _dragImage ??= MakeDragImage();
            if (!ReferenceEquals(_dragImageOf, picture))
            {
                var bitmap = new WriteableBitmap(picture.Width, picture.Height);
                picture.Bgra.CopyTo(bitmap.PixelBuffer);
                bitmap.Invalidate();
                image.Source = bitmap;

                // Its pixels one to one on the display it was composed for.
                image.Width = picture.Width / picture.PixelsPerDip;
                image.Height = picture.Height / picture.PixelsPerDip;
                _dragImageOf = picture;
            }

            var (hotX, hotY) = picture.Anchor;
            Canvas.SetLeft(image, pointer.X - hotX);
            Canvas.SetTop(image, pointer.Y - hotY);
            image.Visibility = Visibility.Visible;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "the drag's picture could not be drawn on the surface; XAML's own shows instead", ex);
            HideDragPicture();
            return false;
        }
    }

    /// <summary>[S-13026] [S-13046] Whether the drag's picture shows on this surface now (a check's question).</summary>
    internal bool ShowsDragPicture => _dragImage is { Visibility: Visibility.Visible };

    /// <summary>[S-13026] The drag left this surface or ended: its picture goes.</summary>
    private void HideDragPicture()
    {
        if (_dragImage is { } image)
        {
            image.Visibility = Visibility.Collapsed;
        }

        _dragImageOf = null;
    }

    private Image MakeDragImage()
    {
        // Never hit-testable: the drop is found under it.
        var image = new Image { IsHitTestVisible = false, Stretch = Stretch.Fill, Visibility = Visibility.Collapsed };
        _ghostLayer.Children.Add(image);
        return image;
    }
}

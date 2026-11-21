using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Diagnostics;
using Themia.Core.Files;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6631] The one icon slot of a file item (the desktop's icons, a fence's found files, a folder's rows and cells, the
/// recent files, a drive's face): a square of one side holding a placeholder (the kind's glyph, an emoji; none) under the
/// picture. A picture the host has cached is laid in at once (<see cref="Show"/>, no flash of the glyph on a redraw);
/// otherwise it is asked (<see cref="LoadAsync"/>) and laid in when it comes, unless a newer ask or
/// <see cref="Cancel"/> came meanwhile (a request counter: the newest wins, an older answer is dropped). The placeholder
/// shows while there is no picture and stays when there is none. <see cref="Ghost"/> is the picture's pixels for a drag's
/// ghost ([S-5564]). UI thread.
/// </summary>
public sealed partial class IconSlot : Grid
{
    private const string Scope = "icon";

    private FrameworkElement? _placeholder;
    private int _request;

    /// <param name="side">The square's side in DIPs; NaN: as large as its picture (a face that sizes it itself).</param>
    /// <param name="placeholder">Drawn until, or instead of, the picture; null: nothing.</param>
    public IconSlot(double side, FrameworkElement? placeholder = null)
    {
        Image = new Image { Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
        Side = side;
        Placeholder = placeholder;
        Children.Add(Image);
    }

    /// <summary>The picture's element: its source is <see cref="Source"/>; a view may watch it decode (<c>ImageOpened</c>).</summary>
    public Image Image { get; }

    /// <summary>The picture laid in, or null while the placeholder stands.</summary>
    public ImageSource? Source => Image.Source;

    /// <summary>The square's side in DIPs (the slot's and its picture's); NaN: the picture's own.</summary>
    public double Side
    {
        get => Width;
        set
        {
            Width = value;
            Height = value;
            Image.Width = value;
            Image.Height = value;
        }
    }

    /// <summary>What stands until, or instead of, the picture: put under it, shown while there is none.</summary>
    public FrameworkElement? Placeholder
    {
        get => _placeholder;
        set
        {
            if (_placeholder is not null)
            {
                Children.Remove(_placeholder);
            }

            _placeholder = value;
            if (value is not null)
            {
                Children.Insert(0, value);
                value.Visibility = Image.Source is null ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// [S-5564] The picture's pixels for a drag's ghost, read now: a bitmap's (the host's icons), or null for none or a
    /// picture of another kind (its view renders a copy instead).
    /// </summary>
    public GhostIcon? Ghost => Image.Visibility == Visibility.Visible ? ItemDrag.IconOf(Image.Source) : null;

    /// <summary>Lay <paramref name="source"/> in at once (the placeholder steps back), or with null take the picture out (the placeholder stands).</summary>
    public void Show(ImageSource? source)
    {
        Image.Source = source;
        Image.Visibility = source is null ? Visibility.Collapsed : Visibility.Visible;
        if (_placeholder is not null)
        {
            _placeholder.Visibility = source is null ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Build the slot anew: no picture, nothing laid over it (a view's own extra pictures, a layout's panes), any answer on
    /// its way dropped, <paramref name="placeholder"/> standing.
    /// </summary>
    public void Reset(FrameworkElement? placeholder)
    {
        Cancel();
        Children.Clear();
        _placeholder = null;
        Show(null);
        Children.Add(Image);
        Placeholder = placeholder;
    }

    /// <summary>A new ask: its number, which <see cref="Answers"/> tells is still the newest (a view loading in steps of its own).</summary>
    public int Ask() => ++_request;

    /// <summary>Whether <paramref name="request"/> (from <see cref="Ask"/>) is still the newest: its answer may be laid in.</summary>
    public bool Answers(int request) => request == _request;

    /// <summary>
    /// Ask for the picture: <paramref name="load"/>'s answer is laid in when it comes, unless a newer ask or a
    /// <see cref="Cancel"/> came meanwhile; no answer (null) leaves what stands. Returns whether this ask was still the
    /// newest when it was answered. A failing load is logged, the placeholder kept: an icon is decoration.
    /// </summary>
    public async Task<bool> LoadAsync(Func<Task<ImageSource?>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        var request = Ask();
        try
        {
            var source = await load().ConfigureAwait(true);
            if (!Answers(request))
            {
                return false;
            }

            if (source is not null)
            {
                Show(source);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not load an item's icon", ex);
            return false;
        }
    }

    /// <summary>Drop any answer still on its way (the item went, or a new picture is built).</summary>
    public void Cancel() => _request++;
}

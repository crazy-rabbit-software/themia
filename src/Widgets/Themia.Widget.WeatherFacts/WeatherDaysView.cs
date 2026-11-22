using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.App.Services;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13913] The weather forecast's own view: no body (its card has no background and never joins: its days are its parts'
/// cards), so it draws nothing but, with no part yet, how to add one. Its days' outlines in edit mode are the surface's
/// ([S-13922]), drawn where the cards are; its parts and their days are cards of their own, drawn by the host.
/// </summary>
internal sealed partial class WeatherDaysView : Grid, IWidgetView
{
    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
    // [S-14260] The family's state row: an item from the card's edge to edge, on its top (WR-1, WR-3).
    private readonly StatusBlock _status = StatusBlock.Line();
    private RepeaterShown? _shown;

    public WeatherDaysView()
    {
        Children.Add(_canvas);
        _status.Hide();
        Children.Add(_status);
        _status.HorizontalAlignment = HorizontalAlignment.Left;
        _status.VerticalAlignment = VerticalAlignment.Top;
        SizeChanged += (_, _) => Lay();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        // [S-15520] A repeater no host laid out (a picture of the kind alone) holding nothing says how it is used.
        _shown = config is WeatherDaysConfig repeater
            ? repeater.Shown ?? (repeater.Members.Count == 0 ? new RepeaterShown([], new RepeaterBox(0, 0, 1, 1), Str.Series_NoMembers.Text(), false) : null)
            : null;
        Render();
    }

    public void Detach() => Strings.Changed -= OnLanguage;

    private void OnLanguage(Locale locale) => Render();

    private void Render()
    {
        if (_shown is { Status: { } sentence, StatusSlot: not null })
        {
            _status.Show(StatusKind.Empty, Glyphs.Weather, sentence);
        }
        else
        {
            _status.Hide();
        }

        Lay();
    }

    /// <summary>The slots' outlines (edit mode) and the sentence in its slot, at the card's size.</summary>
    private void Lay()
    {
        _canvas.Children.Clear();
        var (w, h) = (ActualWidth, ActualHeight);
        if (_shown is not { } shown || !(w > 0) || !(h > 0))
        {
            return;
        }

        if (shown.StatusSlot is { } at)
        {
            // [S-12815] On the card's top edge, as a list's first row (ListEdge: the card's top inset taken back).
            _status.Margin = new Thickness(at.X * w, (at.Y * h) + ListEdge.Margin.Top, 0, 0);
            _status.Width = Math.Max(0, at.W * w);
        }
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Elements;

/// <summary>
/// [S-10101] Own text on the one text face (<see cref="TextView"/>): the person's words, one line cut at the card's edge;
/// with none yet, its name, unavailable, and why in the face's compact line ([S-16431]) until some are typed.
/// </summary>
internal sealed partial class TextElementView : TextView
{
    public TextElementView(IWidgetHost host)
        : base(host, ElementKinds.Text)
    {
    }

    protected override void OnAttach(Ticker ticker, WidgetConfig config) => OnApply(config);

    protected override void OnApply(WidgetConfig config) => Present(ElementKinds.Shown(config));

    protected override void OnDetach()
    {
    }
}

/// <summary>[S-10103] A divider: one hairline in the track role through its box's middle, along its long side, edge to edge.</summary>
internal sealed partial class DividerElementView : Grid, IWidgetView
{
    /// <summary>Its least side: one grid cell.</summary>
    public const double Least = WidgetSizing.Floor;

    /// <summary>A new one's run.</summary>
    public const double Run = 200;

    private readonly Rectangle _line = new() { Fill = Family.Brush(ColourRole.Track) };

    public DividerElementView()
    {
        Children.Add(_line);
        SizeChanged += (_, _) => Lay();
    }

    public void Attach(Ticker ticker, WidgetConfig config) => Lay();

    public void ApplyConfig(WidgetConfig config) => Lay();

    public void Detach()
    {
    }

    /// <summary>Across a box wider than tall, down one taller than wide.</summary>
    private void Lay()
    {
        var across = ActualWidth >= ActualHeight;
        _line.Width = across ? double.NaN : FamilyShape.Hairline;
        _line.Height = across ? FamilyShape.Hairline : double.NaN;
        _line.HorizontalAlignment = across ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        _line.VerticalAlignment = across ? VerticalAlignment.Center : VerticalAlignment.Stretch;
    }
}

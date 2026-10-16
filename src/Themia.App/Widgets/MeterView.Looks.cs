using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Widgets.Meters;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6300] The meter's styles other than the history: a gauge round the reading, or the kind's
/// items as bars or tiles under it (<see cref="MeterLook"/>). The reading is the same hero readout
/// on the same ladder in every look; only the part beside it changes, and what one look builds or
/// watches is released when another is chosen ([S-6005]).
/// </summary>
internal sealed partial class MeterView
{
    /// <summary>The air under a gauge or a bar part, above the card's bottom edge.</summary>
    private const double BodyBottom = Fluent.Space3;

    /// <summary>Between two bar rows.</summary>
    private const double ItemRowSpacing = Fluent.Space3;

    /// <summary>Use <paramref name="look"/>: build its part (once), let go of the previous one's, and watch its items.</summary>
    private void UseLook(MeterLook look)
    {
        if (look != _look || (_body is null && look != MeterLook.Graph))
        {
            _look = look;

            if (_body is not null)
            {
                Children.Remove(_body);
            }

            _items?.Dispose();
            _items = null;
            _itemList = [];
            _dial = null;
            _bars = null;
            _tiles = null;
            _body = look switch
            {
                MeterLook.Gauge => _dial = new GaugeDial(),
                MeterLook.Columns => _bars = new BarList(BarListShape.Columns),
                MeterLook.Rows => _bars = new BarList(BarListShape.Rows),
                MeterLook.Tiles => _tiles = new TileGrid(),
                _ => null,
            };

            if (_body is not null)
            {
                // Before the reading, so a gauge's bowl draws under the number.
                Children.Insert(0, _body);
            }

            _heroStep = null;
        }

        WatchItems();
    }

    /// <summary>[S-6300] The items of a bar or tile look, from the hub; nothing for the history or the gauge.</summary>
    private void WatchItems()
    {
        var wants = _look is MeterLook.Columns or MeterLook.Rows or MeterLook.Tiles;

        if (!wants || _detached)
        {
            _items?.Dispose();
            _items = null;
            return;
        }

        if (_items is not null)
        {
            return;
        }

        if (AppServices.System is not { } hub)
        {
            Log.Warn(Scope, $"no system hub for {_kind}'s items");
            return;
        }

        _items = _part.WatchItems(hub, OnItems);

        if (_items?.Current is { } current)
        {
            ShowItems(current);
        }
    }

    private void OnItems(IReadOnlyList<MeterItem> items)
    {
        if (!_detached)
        {
            ShowItems(items);
        }
    }

    /// <summary>Put the items on the bars or tiles; a different count is a different layout.</summary>
    private void ShowItems(IReadOnlyList<MeterItem> items)
    {
        var recount = items.Count != _itemList.Count;
        _itemList = items;

        if (_bars is not null)
        {
            _bars.SetCount(items.Count);

            for (var i = 0; i < items.Count; i++)
            {
                _bars.SetItem(i, items[i].Fraction, items[i].Fraction is null ? MeterBrushes.Unknown : MeterBrushes.Pressure(items[i].Pressure), items[i].Label, items[i].Value);
            }
        }
        else if (_tiles is not null)
        {
            _tiles.SetCount(items.Count);

            for (var i = 0; i < items.Count; i++)
            {
                _tiles.SetItem(i, LoadTint.Of(items[i].Fraction));
            }
        }

        if (recount)
        {
            Lay();
        }
    }

    /// <summary>
    /// [S-6302] The gauge as large as the tile allows under the header, the reading in its bowl at
    /// the largest step the bowl's chord takes. A tile too small for a gauge worth drawing is the
    /// reading alone, as the history's tiny tile is.
    /// </summary>
    private void LayGauge()
    {
        var boxWidth = Math.Max(0, _area.Width - (2 * HeroLayout.SideInset));
        var boxHeight = Math.Max(0, _area.Height - HeroLayout.TopInset - BodyBottom);
        var gauge = _area.IsMeasured ? GaugeLayout.Fit(boxWidth, boxHeight) : null;

        SetRow(_reading, 0);
        SetRowSpan(_reading, 2);
        SetColumnSpan(_reading, 2);
        ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = new GridLength(0);

        if (gauge is not { } g || _dial is null)
        {
            if (_dial is not null)
            {
                _dial.Visibility = Visibility.Collapsed;
            }

            LayAlone();
            return;
        }

        _dial.Visibility = Visibility.Visible;
        _dial.Lay(g);
        SetRow(_dial, 0);
        SetRowSpan(_dial, 2);
        SetColumnSpan(_dial, 2);
        _dial.HorizontalAlignment = HorizontalAlignment.Center;
        _dial.VerticalAlignment = VerticalAlignment.Center;
        _dial.Margin = new Thickness(HeroLayout.SideInset, HeroLayout.TopInset, HeroLayout.SideInset, BodyBottom);

        // The reading's line stands on the dial's bottom edge (the centre line, plus half a stroke),
        // centred across it: the number sits in the bowl, its baseline about on the centre line.
        var under = BodyBottom + ((boxHeight - g.Height) / 2);
        _reading.HorizontalAlignment = HorizontalAlignment.Center;
        _reading.VerticalAlignment = VerticalAlignment.Bottom;
        _reading.Margin = new Thickness(HeroLayout.SideInset, 0, HeroLayout.SideInset, under);

        var step = GaugeLayout.Reading(g, _part.Reading, FluentType.Body, FluentType.Display);
        ApplyHero(step, beside: MeterFace.TokenFits(step, 2 * g.InnerRadius, _part.Reading, _badged));
    }

    /// <summary>
    /// [S-6303]..[S-6305] The items under the reading (beside it on a wide strip), where the history
    /// would be: the same <see cref="MeterFace"/> plan, the part kept inside the content inset
    /// because bars and tiles are not a trace ([S-1824] is the history's).
    /// </summary>
    private void LayItems()
    {
        if (_body is null)
        {
            LayAlone();
            return;
        }

        var plan = MeterFace.Plan(_area, true, _part.Reading, _part.HasBadge, Fluent.Space2, GraphFloor);
        var side = plan.Arrangement == MeterFaceArrangement.Side;
        var alone = plan.Arrangement == MeterFaceArrangement.ReadingOnly || _itemList.Count == 0;

        PlaceReading(plan, side, alone);
        _body.Visibility = alone ? Visibility.Collapsed : Visibility.Visible;

        if (alone)
        {
            return;
        }

        SetRow(_body, side ? 0 : 1);
        SetRowSpan(_body, side ? 2 : 1);
        SetColumn(_body, side ? 1 : 0);
        SetColumnSpan(_body, 1);
        _body.Margin = side
            ? new Thickness(0, BodyBottom, HeroLayout.SideInset, BodyBottom)
            : new Thickness(HeroLayout.SideInset, Fluent.Space2, HeroLayout.SideInset, BodyBottom);

        var width = side
            ? Math.Max(0, _area.Width - plan.ReadingColumn - HeroLayout.SideInset)
            : Math.Max(0, _area.Width - (2 * HeroLayout.SideInset));
        var height = side
            ? Math.Max(0, _area.Height - (2 * BodyBottom))
            : Math.Max(0, plan.GraphHeight - BodyBottom);

        if (_bars is not null && _look == MeterLook.Columns)
        {
            _bars.VerticalAlignment = VerticalAlignment.Stretch;
            _bars.LayColumns(BarLayout.Columns(width, _bars.Count));
        }
        else if (_bars is not null)
        {
            // A row reads right under the number it explains (beside it on a strip, centred on it).
            _bars.VerticalAlignment = side ? VerticalAlignment.Center : VerticalAlignment.Top;
            var rows = BarLayout.Rows(height, _bars.Count, ItemRowSpacing, width, _part.ItemLineCharacters);
            _bars.LayRows(rows);

            if (!side)
            {
                // [S-6304] The rows are short: what they leave goes to the number (grown as a
                // reading alone grows), and the two stand together, centred in the tile.
                var block = BarLayout.Total(rows, rows.Shown);
                var room = Math.Max(0, _area.Height - HeroLayout.TopInset - Fluent.Space2 - block - BodyBottom);
                var step = MeterFace.Reading(width, room, _part.Reading, _badged, FluentType.Display);
                step = HeroLayout.Grow(step, width, room, _part.Reading.Value, _part.Reading.Unit);
                var top = Math.Max(HeroLayout.TopInset, (_area.Height - step.LineHeight - Fluent.Space2 - block) / 2);
                _reading.Margin = new Thickness(HeroLayout.SideInset, top, HeroLayout.SideInset, 0);
                ApplyHero(step, MeterFace.TokenFits(step, width, _part.Reading, _badged));
            }
        }
        else if (_tiles is not null)
        {
            _tiles.Lay(BarLayout.Tiles(width, height, _tiles.Count));
        }
    }

    /// <summary>The reading alone, the history look's tiny tile: centred in the whole height at the tier's step.</summary>
    private void LayAlone()
    {
        var plan = MeterFace.Plan(_area, true, _part.Reading, _part.HasBadge, Fluent.Space2, GraphFloor);
        PlaceReading(plan with { Arrangement = MeterFaceArrangement.ReadingOnly }, side: false, alone: true);
    }

    /// <summary>The reading where a <see cref="MeterFace"/> plan puts it, as the history look places it.</summary>
    private void PlaceReading(MeterFacePlan plan, bool side, bool alone)
    {
        ColumnDefinitions[0].Width = side ? new GridLength(plan.ReadingColumn) : new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        SetRow(_reading, 0);
        SetRowSpan(_reading, alone || side ? 2 : 1);
        SetColumnSpan(_reading, side ? 1 : 2);
        _reading.HorizontalAlignment = HorizontalAlignment.Stretch;
        _reading.VerticalAlignment = alone || side ? VerticalAlignment.Center : VerticalAlignment.Top;
        _reading.Margin = alone || side
            ? new Thickness(HeroLayout.SideInset, 0, HeroLayout.SideInset, 0)
            : new Thickness(HeroLayout.SideInset, HeroLayout.TopInset, HeroLayout.SideInset, 0);

        ApplyHero(plan.Reading, plan.TokenBeside);
    }

    /// <summary>[S-3860] The readout at a step, touched only when the step or the token's place changed.</summary>
    private void ApplyHero(TypeStep step, bool beside)
    {
        if (step == _heroStep && beside == _tokenBeside)
        {
            return;
        }

        _heroStep = step;
        _tokenBeside = beside;
        _tokenGap.Text = beside ? "  " : string.Empty;
        _reading.Lay(step, _badged, trailingBeside: beside, linesBeside: false);
    }
}

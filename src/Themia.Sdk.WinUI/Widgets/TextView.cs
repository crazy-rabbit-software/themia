using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-14930]..[S-14935] The one face of every text widget (owner, 2026-11-21: "for text like the rain in x minutes or the
/// song name or album name make a shared class in the background that all of those widgets use. similar to how the metric
/// one works"): the kind's glyph leading its line where the kind has one, and its text on the Title step (a detail's
/// regular, secondary), one line cut with its ellipsis or wrapping to the kind's lines. One block in the family's inset,
/// scaled as one (<see cref="InkFit"/>, WR-7, WR-14) until its ink fills the card's height or the width stops it, standing
/// at the card's content alignment across ([S-6583]) and centred on the other axis (owner, 2026-11-19, of the one-line
/// faces: "Centred is fine"); its line takes the width the box gives it at that scale (<see cref="TextRules.Room"/>), so a
/// longer text is cut at the card's edge, never drawn past it. The whole text is one item, the card edge to edge, lit under
/// the pointer where it opens something ([S-14935], <see cref="ItemLight"/>); a click, Enter and the card menu's first line
/// open its kind's source. Without a text the face stays: the kind's words or the family's why, drawn unavailable
/// ([S-14932]); a text kept through a failed refresh stays and the status's problem glyph says so. A family subclasses it
/// for its data and hands each read to <see cref="Present"/>; the face, its fit and what a click opens are this base's alone.
/// </summary>
public abstract partial class TextView : Grid, IWidgetView, IWidgetOpens, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys,
    IWidgetCorners, IWidgetAligned, IWidgetSurfaceAware
{
    private const string Scope = "texts";

    private readonly IWidgetHost _host;
    private readonly TextKind _kind;
    private readonly KitStatus _status;

    /// <summary>[S-14935] The text's one item: the card from its top edge to its foot, side to side, the face inside it.</summary>
    private readonly Border _item = new();
    private readonly Face _face = new();
    private readonly ItemLight _light;

    /// <summary>[S-14930] The block (the lead and the line), scaled as one in the card's inset.</summary>
    private readonly InkFit _fit = new();
    private readonly StackPanel _block = new() { Orientation = Orientation.Horizontal };
    private readonly FontIcon? _lead;
    private readonly TextBlock _text;

    private TextShown _shown = TextShown.None();
    private WidgetConfig? _config;
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private ContentAlign _align = ContentAlign.Left;
    private bool _hidden;

    // [S-12813] The status glyph's width over the card's top end corner while it shows (0: none).
    private double _statusRoom;

    /// <param name="host">The card's host: the status glyph's account link, the launcher a click opens through.</param>
    /// <param name="kind">The kind: its glyph, its lines, its tone, its words for none and what it opens.</param>
    protected TextView(IWidgetHost host, TextKind kind)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _kind = kind ?? throw new ArgumentNullException(nameof(kind));
        _status = new KitStatus(host);

        // [S-14935] The item stands on the card's top edge as a list's first row does, and runs to its foot.
        ItemMark.Mark(_item);
        ListEdge.Apply(_item);
        _light = new ItemLight(_item);
        _item.Child = _face;
        _face.Room = width =>
        {
            _statusRoom = width;
            Lay();
        };

        // [S-14934] The lead: the kind's glyph at the step's size, in a box of the first line's height so it stands on that
        // line (its ink counts as the line's, PartInk), the glyph gap after it.
        var step = TextRules.StepOf(kind.Tone);
        if (kind.Lead)
        {
            _lead = Family.Glyph(kind.Glyph, step.FontSize, ColourRole.TextSecondary);
            _lead.VerticalAlignment = VerticalAlignment.Center;
            _block.Children.Add(new Border
            {
                Child = _lead,
                Height = step.LineHeight,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0),
            });
        }

        _text = Family.Text(step, Role(available: true));
        _text.TextTrimming = TextTrimming.CharacterEllipsis;
        _text.TextWrapping = TextRules.Lines(kind.Lines) > 1 ? TextWrapping.WrapWholeWords : TextWrapping.NoWrap;
        _text.MaxLines = TextRules.Lines(kind.Lines);
        _block.Children.Add(_text);
        _fit.Child = _block;
        _fit.Margin = new Thickness(FamilySpace.Inset);
        _face.Children.Add(_fit);

        Children.Add(_item);
        _status.Place(this);
    }

    /// <summary>[S-14935] What a click opens: the kind's source for the card's config and what it shows; null for none now.</summary>
    public CardOpen? Opens => _config is { } config && _kind.Opens is { } open ? open.Target(config, _shown) : null;

    /// <summary>[S-14935] The card menu's own lines: the line that opens the source first, then the family's.</summary>
    public IReadOnlyList<WidgetAction> ContextActions
        => _kind.Opens is { } open && Opens is not null
            ? [new WidgetAction(Glyphs.Open, open.Label.Text(), () => Open()), .. OwnActions]
            : OwnActions;

    /// <summary>The family's own lines on the card menu (Refresh, where the source has one).</summary>
    protected virtual IReadOnlyList<WidgetAction> OwnActions => [];

    /// <summary>[S-14935] A click takes the keyboard only where Enter opens something.</summary>
    public bool WantsKeys => Opens is not null && !_hidden;

    /// <summary>Whether the card is a preview (the add page's picture, a render): it reads and presses nothing of the machine.</summary>
    protected bool IsPreview => _surface?.IsPreview == true;

    public void Bind(IWidgetSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        OnBind(surface);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _config = config;
        Draw();
        OnAttach(ticker, config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config;
        OnApply(config);
        Draw();
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _light.Set(false);
        OnDetach();
    }

    /// <summary>[S-14930] The card's box: the line's room in it.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>[S-6583] The block at the start, the middle or the end of the card.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        _align = align;
        Lay();
    }

    /// <summary>[S-11883] The hover fill reaches the card's edges: it takes the card's corners.</summary>
    public void ApplyCorners(CornerRadius corners) => _item.CornerRadius = corners;

    /// <summary>[S-14935] Enter opens the source while the card has the keyboard; nothing else is the text's.</summary>
    public bool HandleKey(WidgetKey key) => key == WidgetKey.Enter && Open();

    /// <summary>The card's surface is known: a family that presses or reads the machine asks it whether this is a preview.</summary>
    protected virtual void OnBind(IWidgetSurface surface)
    {
    }

    /// <summary>Hold the family's data for <paramref name="config"/>, presenting what it holds at once.</summary>
    protected abstract void OnAttach(Ticker ticker, WidgetConfig config);

    /// <summary>A changed config: what the data is about may differ (another place).</summary>
    protected abstract void OnApply(WidgetConfig config);

    /// <summary>Let go of the data. Safe twice.</summary>
    protected abstract void OnDetach();

    /// <summary>[S-14931] Put one read on the face.</summary>
    protected void Present(TextShown shown)
    {
        _shown = shown ?? throw new ArgumentNullException(nameof(shown));
        Draw();
    }

    /// <summary>
    /// [S-1464] Paint nothing (a family's hide-when-idle), or paint again. Never in a preview card: it always draws; edit mode
    /// always shows it (the card's rule).
    /// </summary>
    protected void Hide(bool hide)
    {
        hide &= !IsPreview;
        if (hide == _hidden)
        {
            return;
        }

        _hidden = hide;
        _surface?.SetHidden(hide);
    }

    /// <summary>Another language: the family writes its text again, the face the kind's words.</summary>
    private void OnLanguage(Locale locale)
    {
        if (_config is { } config)
        {
            OnApply(config);
        }

        Draw();
    }

    private ColourRole Role(bool available)
        => !available ? ColourRole.TextDisabled : _kind.Tone == TextTone.Secondary ? ColourRole.TextSecondary : ColourRole.TextPrimary;

    private void Draw()
    {
        var (text, available) = TextRules.Drawn(_shown, _kind.None?.Text() ?? string.Empty);
        _text.Text = text;
        _text.Foreground = Family.Brush(Role(available));
        if (_lead is not null)
        {
            _lead.Foreground = Family.Brush(available ? ColourRole.TextSecondary : ColourRole.TextDisabled);
        }

        // [S-14932] Why there is none is the face's tooltip; a text the card cuts is whole in its tooltip.
        ToolTipService.SetToolTip(_item, !available ? (_shown.Why is { Length: > 0 } why ? why : null) : text);
        if (available)
        {
            _status.Show(_shown.Problems, busy: false);
        }
        else
        {
            _status.Clear();
        }

        _light.Set(Opens is not null);
        Lay();

        // [S-14201] Another text may ink lower in the same box (a descender): the fit places the face on its ink again.
        _fit.InvalidateArrange();
    }

    /// <summary>
    /// [S-14930] [S-14931] Lay the block in the card's inset: the line's room at the scale the box gives it, the block at the
    /// alignment across, leaving the status glyph its room at the end while one shows ([S-12813]).
    /// </summary>
    private void Lay()
    {
        // The item stands on the card's top edge: the card's box is the view's and the inset over it.
        var height = _area.IsMeasured ? _area.Height - ListEdge.Margin.Top : 0;
        var end = FamilySpace.Inset + _statusRoom;
        _fit.Margin = new Thickness(FamilySpace.Inset, FamilySpace.Inset, end, FamilySpace.Inset);
        _fit.Align = Aligned.Horizontal(_align);
        _text.TextAlignment = Aligned.Text(_align);
        var room = _area.IsMeasured
            ? TextRules.Room(_area.Width - FamilySpace.Inset - end, height - (2 * FamilySpace.Inset), _kind.Lines, _kind.Lead)
            : double.PositiveInfinity;
        if (!_text.MaxWidth.Equals(room))
        {
            _text.MaxWidth = room;
        }
    }

    /// <summary>
    /// [S-12813] The face: the status glyph over the card's top end corner tells it its width (<see cref="IStatusRoom"/>), so
    /// the block leaves it room.
    /// </summary>
    private sealed partial class Face : Grid, IStatusRoom
    {
        public Action<double>? Room { get; set; }

        public void StatusRoom(double width) => Room?.Invoke(width);
    }

    /// <summary>[S-14935] Open the source through the host's launcher; said in the log where nothing opened.</summary>
    private bool Open()
    {
        if (Opens is not { } target)
        {
            return false;
        }

        var outcome = _host.Launcher.OpenCard(target);
        if (outcome != LaunchOutcome.Opened)
        {
            Log.Warn(Scope, $"{_kind.Id}: opening its source opened nothing ({outcome})");
        }

        return true;
    }
}

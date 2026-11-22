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
/// scaled as one (<see cref="ReadingFit"/>, WR-7, WR-14) until its ink fills the card's height or the width stops it, standing
/// at the card's content alignment across ([S-6583]) and centred on the other axis (owner, 2026-11-19, of the one-line
/// faces: "Centred is fine"); its line takes the width the box gives it at that scale (<see cref="TextRules.Room"/>), so a
/// longer text is cut at the card's edge, never drawn past it. The whole text is one item, the card edge to edge, lit under
/// the pointer where it opens something ([S-14935], <see cref="ItemLight"/>); a click, Enter and the card menu's first line
/// open its kind's source. Without a text the face stays: the kind's words for none, else "--", drawn unavailable
/// ([S-14932], [S-15713]), and a card with the room says why in one Caption line at its foot ([S-15710]); a text kept
/// through a failed refresh, or a failure with none, is the status's problem glyph's ([S-15711]). A family subclasses it
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
    private readonly ReadingFit _fit = new();
    private readonly StackPanel _block = new() { Orientation = Orientation.Horizontal };
    private readonly FontIcon? _lead;
    private readonly TextBlock _text;

    /// <summary>[S-15710] Why there is no text, one Caption line at the card's foot where the card has the room.</summary>
    private readonly TextBlock _why = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);

    private TextShown _shown = TextShown.None();
    private WidgetConfig? _config;

    /// <summary>[S-15712] The config a click opens for in place of the card's: a preview's sample place; null for the card's.</summary>
    private WidgetConfig? _openFor;
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private ContentAlign _align = ContentAlign.Left;
    private bool _hidden;

    // [S-12813] The status glyph's width over the card's top end corner while it shows (0: none).
    private double _statusRoom;

    // [S-15710] Why there is no text, while there is none and a reason is given.
    private string? _whyText;

    // [S-15713] Whether the face draws "--" for a reading it does not have.
    private bool _dashes;

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
        // line (its ink counts as the line's, Ink), the glyph gap after it.
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
        _fit.Parts.Add(_block);
        _fit.Margin = new Thickness(FamilySpace.Inset);
        _face.Children.Add(_fit);

        // [S-15710] The why's line: at the foot of the inset, as wide as it, cut with its ellipsis; never a target of its own.
        _why.TextWrapping = TextWrapping.NoWrap;
        _why.TextTrimming = TextTrimming.CharacterEllipsis;
        _why.VerticalAlignment = VerticalAlignment.Bottom;
        _why.IsHitTestVisible = false;
        _why.Visibility = Visibility.Collapsed;
        _face.Children.Add(_why);

        Children.Add(_item);
        _status.Place(this);
    }

    /// <summary>[S-14935] What a click opens: the kind's source for the card's config and what it shows; null for none now.</summary>
    public CardOpen? Opens => (_openFor ?? _config) is { } config && _kind.Opens is { } open ? open.Target(config, _shown) : null;

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
        _openFor = null;
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
    /// [S-15712] A preview's sample stands somewhere the card's config does not say (the sample place): a click opens, and the
    /// item lights, for <paramref name="config"/> until the card's config changes.
    /// </summary>
    protected void OpenFor(WidgetConfig config)
    {
        _openFor = config ?? throw new ArgumentNullException(nameof(config));
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
        _dashes = !available && text == TextRules.NoText;
        _text.Foreground = Family.Brush(Role(available));
        if (_lead is not null)
        {
            _lead.Foreground = Family.Brush(available ? ColourRole.TextSecondary : ColourRole.TextDisabled);
        }

        // [S-14932] Why there is none is the face's tooltip; a text the card cuts is whole in its tooltip. [S-15710] It is
        // the line at the foot too where the card has the room (Lay); [S-15711] a failure the status mark's, as a stale text's.
        _whyText = !available && _shown.Why is { Length: > 0 } why ? why : null;
        ToolTipService.SetToolTip(_item, !available ? _whyText : text);
        _why.Text = _whyText ?? string.Empty;
        _status.Show(_shown.Problems, busy: false);

        _light.Set(Opens is not null);
        Lay();

        // [S-14201] Another text may ink lower in the same box (a descender): the fit places the face on its ink again.
        _fit.Refit();
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

        // [S-15710] The why's line takes its band at the foot; the face fills what is over it. [S-15716] A card too short for
        // it there says it beside the face, at the end, where the width holds it.
        var why = _whyText is not null && _area.IsMeasured && TextRules.SaysWhy(_area.Width, height, _kind.Lines);
        var band = why ? WhyLine.Band : 0;
        var beside = _whyText is not null && _area.IsMeasured && !why ? Beside(height, end) : null;
        _why.Visibility = why || beside is not null ? Visibility.Visible : Visibility.Collapsed;
        _why.Margin = beside is not null ? new Thickness(0, 0, end, 0) : new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        _why.Width = beside ?? double.NaN;
        _why.HorizontalAlignment = beside is not null ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        _why.VerticalAlignment = beside is not null ? VerticalAlignment.Center : VerticalAlignment.Bottom;
        _why.TextAlignment = beside is not null ? TextAlignment.Left : Aligned.Text(_align);
        end += beside is { } taken ? taken + FamilySpace.ColumnGap : 0;
        _fit.Margin = new Thickness(FamilySpace.Inset, FamilySpace.Inset, end, FamilySpace.Inset + band);
        _fit.Align = _align;
        _text.TextAlignment = Aligned.Text(_align);
        var room = _area.IsMeasured
            ? TextRules.Room(_area.Width - FamilySpace.Inset - end, height - (2 * FamilySpace.Inset) - band, _kind.Lines, _kind.Lead, Natural())
            : double.PositiveInfinity;

        // [S-15713] "--" is drawn at the scale the line it stands for has in the room, never blown up by its thin ink.
        _fit.Most = _dashes && _area.IsMeasured ? TextRules.DashScale(height - (2 * FamilySpace.Inset) - band, _kind.Lines) : double.PositiveInfinity;
        if (!_text.MaxWidth.Equals(room))
        {
            _text.MaxWidth = room;
        }
    }

    /// <summary>[S-15715] The text's own width at its step, uncut.</summary>
    private double Natural()
    {
        var max = _text.MaxWidth;
        _text.MaxWidth = double.PositiveInfinity;
        _text.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var natural = _text.DesiredSize.Width;
        _text.MaxWidth = max;
        return natural;
    }

    /// <summary>
    /// [S-15716] The why's width beside the face in a card <paramref name="height"/> tall whose face ends <paramref name="end"/>
    /// in from its end edge: what the width leaves after the face as drawn ("--" at its scale, <see cref="TextRules.DashScale"/>).
    /// </summary>
    private double? Beside(double height, double end)
    {
        var infinite = new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity);
        _why.Width = double.NaN;
        _why.Visibility = Visibility.Visible;
        _why.Measure(infinite);
        var sentence = _why.DesiredSize.Width - _why.Margin.Left - _why.Margin.Right;
        var max = _text.MaxWidth;
        _text.MaxWidth = double.PositiveInfinity;
        _text.Measure(infinite);
        var face = _text.DesiredSize.Width * (_dashes ? TextRules.DashScale(height - (2 * FamilySpace.Inset), _kind.Lines) : 1);
        _text.MaxWidth = max;
        return TextRules.WhyBeside(_area.Width - FamilySpace.Inset - end, height, face + TextRules.LeadRoom(_kind.Lead), sentence);
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

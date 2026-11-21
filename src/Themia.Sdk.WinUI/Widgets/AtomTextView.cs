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
/// [S-15101] [S-15105] The one face of every text atom (owner, 2026-11-21: "i want you to think about a proper design
/// system ... the compositions just look off, the spacing does not look right between the atoms and text overflow and stuff
/// like that is not handled properly"): a text kind's line (<see cref="TextKind"/>: a song's title, its artist, a time)
/// as an <see cref="AtomText"/>, in the card's box less the universal spacing on every side, filling that room's height,
/// cut at its end with its ellipsis, at the card's content alignment across ([S-6583]; the start unless the kind's config
/// says otherwise). It owns its card's top edge ([S-5700]): the spacing is its own on all four sides. The whole text is
/// one item, the card edge to edge, lit under the pointer where it opens something ([S-14935], <see cref="ItemLight"/>); a
/// click, Enter and the card menu's first line open its kind's source. Without a text the face stays: the kind's words
/// or the family's why, drawn unavailable ([S-14932]). A family subclasses it for its data and hands each read to
/// <see cref="Present"/>.
/// </summary>
public abstract partial class AtomTextView : Grid, IWidgetView, IWidgetOpens, IWidgetContextActions, IWidgetKeys,
    IWidgetCorners, IWidgetAligned, IWidgetSurfaceAware, IWidgetTopEdge
{
    private const string Scope = "atoms";

    private readonly IWidgetHost _host;
    private readonly TextKind _kind;

    /// <summary>[S-14935] The text's one item: the card from its top edge to its foot, side to side, the line inside it.</summary>
    private readonly Border _item = new();
    private readonly ItemLight _light;
    private readonly AtomText _text;

    private TextShown _shown = TextShown.None();
    private WidgetConfig? _config;
    private IWidgetSurface? _surface;
    private bool _hidden;

    /// <param name="host">The card's host: the launcher a click opens through.</param>
    /// <param name="kind">The kind: its tone, its words for none and what it opens.</param>
    protected AtomTextView(IWidgetHost host, TextKind kind)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _kind = kind ?? throw new ArgumentNullException(nameof(kind));

        ItemMark.Mark(_item);
        _light = new ItemLight(_item);
        _text = new AtomText(Role(available: true), strong: kind.Tone == TextTone.Primary);
        _item.Child = _text;
        Children.Add(_item);
    }

    /// <summary>[S-5700] It owns its top edge: the universal spacing is its own on every side.</summary>
    public bool OwnsTopEdge => true;

    /// <summary>[S-14935] What a click opens: the kind's source for the card's config and what it shows; null for none now.</summary>
    public CardOpen? Opens => _config is { } config && _kind.Opens is { } open ? open.Target(config, _shown) : null;

    /// <summary>[S-14935] The card menu's own lines: the line that opens the source first, then the family's.</summary>
    public IReadOnlyList<WidgetAction> ContextActions
        => _kind.Opens is { } open && Opens is not null
            ? [new WidgetAction(Glyphs.Open, open.Label.Text(), () => Open()), .. OwnActions]
            : OwnActions;

    /// <summary>The family's own lines on the card menu.</summary>
    protected virtual IReadOnlyList<WidgetAction> OwnActions => [];

    /// <summary>[S-14935] A click takes the keyboard only where Enter opens something.</summary>
    public bool WantsKeys => Opens is not null && !_hidden;

    /// <summary>Whether the card is a preview (the add page's picture, a render): it reads and presses nothing of the machine.</summary>
    protected bool IsPreview => _surface?.IsPreview == true;

    /// <summary>The line: a family whose text is a running reading asks it for tabular figures (<see cref="AtomText.Figures"/>).</summary>
    protected AtomText Line => _text;

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

    /// <summary>[S-6583] [S-15105] The line at the start, the middle or the end of the room.</summary>
    public void ApplyAlign(ContentAlign align) => _text.Align = align;

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

    /// <summary>A changed config: what the data is about may differ.</summary>
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

        // [S-14932] Why there is none is the face's tooltip; a text the card cuts is whole in its tooltip.
        ToolTipService.SetToolTip(_item, !available ? (_shown.Why is { Length: > 0 } why ? why : null) : text);
        _light.Set(Opens is not null);
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

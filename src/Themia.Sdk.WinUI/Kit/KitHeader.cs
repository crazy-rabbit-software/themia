using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7064] The header extras of every widget that shows a source's data (the
/// <c>IWidgetHeaderContent.HeaderTrailing</c> of a kit view, built in its constructor, [S-400a]),
/// from leading to trailing: the problem glyph, the busy ring, the widget's own view buttons (Mail's
/// compose and sort, each from its width on), the count badge, each at the one icon size. The problem glyph is the one stale rule ([S-7042]): it shows only while a copy is on screen
/// that the last attempt of some source could not improve on, its tooltip one line per source and
/// problem ("Work: No network · updated 10:42"); a click on it opens Settings › Accounts when the
/// problem is one only the user can mend there. The age is never drawn here: it is on the Refresh
/// line. The ring shows only while a fetch for one of the widget's own sources is out ([S-2661]).
/// </summary>
public sealed partial class KitHeader : StackPanel
{
    private const string Scope = "kit";

    /// <summary>A view button's glyph: a step under the icon size, so the button with its padding sits in the header's line.</summary>
    private const double ButtonGlyph = 14;

    private readonly IWidgetHost _host;
    private readonly FontIcon _problem;
    private readonly ProgressRing _ring;
    private readonly TextBlock _countText = new();
    private readonly Border _badge;
    private bool _offersAccounts;
    private double _width;

    /// <summary>The view buttons drawn, in order, with what each last showed.</summary>
    private readonly List<(Border Element, IDisposable Hover, FontIcon Icon)> _buttons = [];
    private IReadOnlyList<KitHeaderButton> _shownButtons = [];

    public KitHeader(IWidgetHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;
        Orientation = Orientation.Horizontal;
        Spacing = Fluent.Space2;
        VerticalAlignment = VerticalAlignment.Center;

        _problem = Fluent.Icon(Glyphs.Warning, "SystemFillColorCautionBrush");
        _problem.Visibility = Visibility.Collapsed;
        _problem.Tapped += OnProblemTapped;

        _ring = new ProgressRing
        {
            Width = Fluent.IconSize,
            Height = Fluent.IconSize,
            IsActive = false,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _badge = Fluent.Badge(_countText);
        _badge.Visibility = Visibility.Collapsed;

        Children.Add(_problem);
        Children.Add(_ring);
        Children.Add(_badge);
        Visibility = Visibility.Collapsed;
    }

    /// <summary>The card's width: the view buttons that fit it show ([S-7064]).</summary>
    public void Size(double width) => _width = width;

    /// <summary>
    /// Show the header's state: the problem lines (none hides the glyph), whether a fetch is out,
    /// whether the problem is mended on the Accounts page, the count (null for no badge) and the
    /// widget's own view buttons.
    /// </summary>
    public void Show(IReadOnlyList<string> problems, bool busy, bool offersAccounts = false, long? count = null, IReadOnlyList<KitHeaderButton>? buttons = null)
    {
        ArgumentNullException.ThrowIfNull(problems);

        _offersAccounts = offersAccounts;
        var shown = ShowButtons(buttons ?? []);
        var parts = KitHeaderParts.Of(problems.Count, busy, count, shown);
        _problem.Visibility = parts.Problem ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_problem, parts.Problem ? string.Join("\n", problems) : null);

        _ring.IsActive = busy;
        _ring.Visibility = parts.Ring ? Visibility.Visible : Visibility.Collapsed;

        _countText.Text = parts.Badge ? KitText.Count(Strings.Current, count!.Value) : string.Empty;
        _badge.Visibility = parts.Badge ? Visibility.Visible : Visibility.Collapsed;

        // With nothing to show the header takes no room: a visible empty panel would still take the
        // spacing of the panel it sits in (the exchange rates' day stood one gap in from the edge).
        Visibility = parts.Any ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Nothing to say: every part hidden, the ring stopped.</summary>
    public void Clear() => Show([], busy: false);

    public void Detach()
    {
        _problem.Tapped -= OnProblemTapped;
        Clear();

        foreach (var (element, hover, _) in _buttons)
        {
            element.Tapped -= OnButtonTapped;
            hover.Dispose();
            Children.Remove(element);
        }

        _buttons.Clear();
        _shownButtons = [];
    }

    /// <summary>The buttons that fit, in their order between the ring and the badge; how many show.</summary>
    private int ShowButtons(IReadOnlyList<KitHeaderButton> buttons)
    {
        while (_buttons.Count < buttons.Count)
        {
            var icon = Fluent.Icon(Glyphs.Info, "TextFillColorSecondaryBrush", ButtonGlyph);
            var element = new Border
            {
                Child = icon,
                Padding = new Thickness(Fluent.Space1),
                CornerRadius = new CornerRadius(Fluent.ControlRadius),
                Tag = _buttons.Count,
            };

            ItemFill.Paint(element, ItemRole.Row);
            element.Tapped += OnButtonTapped;
            _buttons.Add((element, Fluent.Hover(element), icon));
            Children.Insert(Children.IndexOf(_badge), element);
        }

        var shown = 0;
        for (var i = 0; i < _buttons.Count; i++)
        {
            var (element, _, icon) = _buttons[i];
            var visible = i < buttons.Count && KitHeaderParts.Fits(buttons[i].NeedsWidth, _width);
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (i < buttons.Count)
            {
                icon.Glyph = buttons[i].Glyph;
                ToolTipService.SetToolTip(element, buttons[i].Tooltip);
            }

            shown += visible ? 1 : 0;
        }

        _shownButtons = buttons;
        return shown;
    }

    /// <summary>A view button: its action, or with several choices the menu under it that asks which.</summary>
    private void OnButtonTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is not Border { Tag: int index } element || index >= _shownButtons.Count)
        {
            return;
        }

        args.Handled = true;
        var button = _shownButtons[index];

        if (button.Choices is { Count: > 1 } choices)
        {
            ItemMenu.Open(element, choices.Select(c => new ItemMenuEntry(c.Glyph, c.Label, c.Run)));
        }
        else if (button.Choices is [var only])
        {
            only.Run();
        }
        else
        {
            button.Run?.Invoke();
        }
    }

    private void OnProblemTapped(object sender, TappedRoutedEventArgs args)
    {
        if (!_offersAccounts)
        {
            return;
        }

        args.Handled = true;
        if (!_host.ShowAccounts())
        {
            Log.Info(Scope, "no settings window here to show the Accounts page in");
        }
    }
}

/// <summary>
/// [S-7064] One of a widget's own header buttons (Mail's compose and sort): a glyph, its tooltip, the
/// card width it needs, and what it does — an action, or choices the header asks between in a menu
/// under it (one choice runs at once).
/// </summary>
public sealed record KitHeaderButton(string Glyph, string Tooltip, double NeedsWidth, Action? Run = null, IReadOnlyList<KitHeaderChoice>? Choices = null);

/// <summary>[S-7064] One choice of a header button's menu.</summary>
public sealed record KitHeaderChoice(string Glyph, string Label, Action Run);

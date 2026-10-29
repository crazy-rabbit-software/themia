using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7064] The header extras of every widget that shows a source's data (the
/// <c>IWidgetHeaderContent.HeaderTrailing</c> of a kit view, built in its constructor, [S-400a]): the
/// Header block's trailing parts ([S-6710]), from leading to trailing the problem glyph, the busy ring,
/// an informing label (the exchange rates' day), the widget's own header buttons (at most two, 24 with
/// 16 glyphs: Mail's compose and sort, the calendar's back and forward, the task list's picker) and the
/// neutral count pill. The header folds them to its width (the label first, then the buttons from the
/// last, then the count), so nothing here measures a width of its own. The problem glyph is the one
/// stale rule ([S-7042]): it shows only while a copy is on screen that the last attempt of some source
/// could not improve on, its tooltip one line per source and problem ("Work: No network · updated
/// 10:42"); a click on it opens ⚙ › Accounts when the problem is one only the user can mend there. The
/// age is never drawn here: it is on the Refresh line. The ring shows only while a fetch for one of the
/// widget's own sources is out ([S-2661]).
/// </summary>
public sealed partial class KitHeader : HeaderParts
{
    private const string Scope = "kit";

    private readonly IWidgetHost _host;
    private readonly FontIcon _problem;
    private readonly ProgressRing _ring;
    private readonly TextBlock _countText = new();
    private readonly Border _count;
    private readonly List<(Border Element, FontIcon Icon)> _buttons = [];
    private IReadOnlyList<KitHeaderButton> _shownButtons = [];
    private TextBlock? _label;
    private bool _offersAccounts;

    /// <param name="label">An informing label the widget keeps beside the extras (the exchange rates' day), the first part to give way.</param>
    public KitHeader(IWidgetHost host, TextBlock? label = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;
        _problem = Add(HeaderPartKind.Problem, HeaderBlock.Problem(string.Empty));
        _problem.Tapped += OnProblemTapped;
        _ring = Add(HeaderPartKind.Busy, HeaderBlock.Busy());
        _ring.IsActive = false;

        if (label is not null)
        {
            Family.Step(label, FamilyType.Caption);
            label.Foreground = Family.Brush(ColourRole.TextSecondary);
            label.VerticalAlignment = VerticalAlignment.Center;
            _label = Add(HeaderPartKind.Label, label);
        }

        for (var i = 0; i < HeaderRules.MaxButtons; i++)
        {
            var index = i;
            var element = Add(HeaderPartKind.Button, HeaderBlock.Button(string.Empty, string.Empty, () => OnButton(index)));
            _buttons.Add((element, (FontIcon)element.Child));
        }

        _count = Add(HeaderPartKind.Count, HeaderBlock.Count(_countText));
        Show([], busy: false);
    }

    /// <summary>
    /// Show the header's state: the problem lines (none hides the glyph), whether a fetch is out,
    /// whether the problem is mended in ⚙ › Accounts, the count (null or none for no pill) and the
    /// widget's own header buttons (at most two).
    /// </summary>
    public void Show(IReadOnlyList<string> problems, bool busy, bool offersAccounts = false, long? count = null, IReadOnlyList<KitHeaderButton>? buttons = null)
    {
        ArgumentNullException.ThrowIfNull(problems);

        _offersAccounts = offersAccounts;
        var wants = HeaderRules.Wants(problems.Count, busy, count, buttons?.Count ?? 0);
        Want(_problem, wants.Problem);
        ToolTipService.SetToolTip(_problem, problems.Count > 0 ? string.Join("\n", problems) : null);

        _ring.IsActive = busy;
        Want(_ring, wants.Busy);

        var shown = buttons ?? [];
        if (shown.Count > HeaderRules.MaxButtons)
        {
            Log.Warn(Scope, $"a header takes {HeaderRules.MaxButtons} buttons; {shown.Count - HeaderRules.MaxButtons} left out");
            shown = [.. shown.Take(HeaderRules.MaxButtons)];
        }

        _shownButtons = shown;
        for (var i = 0; i < _buttons.Count; i++)
        {
            var (element, icon) = _buttons[i];
            var wanted = i < wants.Buttons;
            if (wanted)
            {
                icon.Glyph = shown[i].Glyph;
                ToolTipService.SetToolTip(element, shown[i].Tooltip);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element, shown[i].Tooltip);
            }

            Want(element, wanted);
        }

        _countText.Text = wants.Count ? KitText.Count(Strings.Current, count!.Value) : string.Empty;
        Want(_count, wants.Count);

        // With nothing to show the extras take no room, not even the gap before them; a widget's label
        // is its own to fill and always stands.
        Visibility = wants.Any || _label is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Nothing to say: every part hidden, the ring stopped (a label the widget keeps stays).</summary>
    public void Clear() => Show([], busy: false);

    public void Detach()
    {
        _problem.Tapped -= OnProblemTapped;
        _shownButtons = [];
        Clear();
    }

    /// <summary>A header button: its action, or with several choices the menu under it that asks which.</summary>
    private void OnButton(int index)
    {
        if (index >= _shownButtons.Count)
        {
            return;
        }

        var button = _shownButtons[index];
        if (button.Choices is { Count: > 1 } choices)
        {
            ItemMenu.Open(_buttons[index].Element, choices.Select(c => new ItemMenuEntry(c.Glyph, c.Label, c.Run)));
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
            Log.Info(Scope, "nothing here opens ⚙ › Accounts");
        }
    }
}

/// <summary>
/// [S-7064] One of a widget's own header buttons (Mail's compose and sort): a glyph, its tooltip, and
/// what it does — an action, or choices the header asks between in a menu under it (one choice runs
/// at once). Where it shows is the header's fold ([S-6710]).
/// </summary>
public sealed record KitHeaderButton(string Glyph, string Tooltip, Action? Run = null, IReadOnlyList<KitHeaderChoice>? Choices = null);

/// <summary>[S-7064] One choice of a header button's menu.</summary>
public sealed record KitHeaderChoice(string Glyph, string Label, Action Run);

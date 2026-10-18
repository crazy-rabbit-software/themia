using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7064] The header extras of every widget that shows a source's data (the
/// <c>IWidgetHeaderContent.HeaderTrailing</c> of a kit view, built in its constructor, [S-400a]),
/// from leading to trailing: the problem glyph, the busy ring, the count badge, each at the one icon
/// size. The problem glyph is the one stale rule ([S-7042]): it shows only while a copy is on screen
/// that the last attempt of some source could not improve on, its tooltip one line per source and
/// problem ("Work: No network · updated 10:42"); a click on it opens Settings › Accounts when the
/// problem is one only the user can mend there. The age is never drawn here: it is on the Refresh
/// line. The ring shows only while a fetch for one of the widget's own sources is out ([S-2661]).
/// </summary>
public sealed partial class KitHeader : StackPanel
{
    private const string Scope = "kit";

    private readonly IWidgetHost _host;
    private readonly FontIcon _problem;
    private readonly ProgressRing _ring;
    private readonly TextBlock _countText = new();
    private readonly Border _badge;
    private bool _offersAccounts;

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

    /// <summary>
    /// Show the header's state: the problem lines (none hides the glyph), whether a fetch is out,
    /// whether the problem is mended on the Accounts page, and the count (null for no badge).
    /// </summary>
    public void Show(IReadOnlyList<string> problems, bool busy, bool offersAccounts = false, long? count = null)
    {
        ArgumentNullException.ThrowIfNull(problems);

        _offersAccounts = offersAccounts;
        var parts = KitHeaderParts.Of(problems.Count, busy, count);
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

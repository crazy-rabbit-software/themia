using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-588] The calendar's and contacts' empty states: a glyph over a sentence, and — when the user
/// is the one who can fix it — a link to Settings › Accounts beneath, centred together.
/// <para>
/// "Account gone", "sign in again" and "access not granted" each need the Accounts page; a blank
/// tile or a sentence with no way forward is the failure the original filed bugs about. Loading,
/// offline and "nothing on" need no link and show none. No account at all is the card's Set up
/// button instead ([S-1862]).
/// </para>
/// </summary>
internal sealed partial class OfficeStatePanel : Grid
{
    private const string Scope = "office-widget";

    /// <summary>The link's line: body text in a hyperlink button's padding.</summary>
    private const double LinkHeight = 32;

    private readonly TextBlock _message = new();
    private readonly HyperlinkButton _link;
    private readonly StackPanel _empty;

    private readonly IWidgetHost _widgetHost;

    public OfficeStatePanel(string glyph, IWidgetHost host)
    {
        _widgetHost = host;
        var empty = Fluent.EmptyState(glyph, _message, out _);

        _link = new HyperlinkButton
        {
            Content = Str.WidgetsA_Office_OpenAccounts.Text(),
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1),
            Visibility = Visibility.Collapsed,
        };
        _link.Click += OnLinkClicked;

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        SetRow(empty, 1);
        SetRow(_link, 2);
        Children.Add(empty);
        Children.Add(_link);

        _empty = empty;
        SizeChanged += (_, _) => Fit();
    }

    /// <summary>
    /// The link is the way out of the state, so it keeps its line: the sentence's panel is capped at
    /// what is left, and sheds its glyph, then lines, to fit ([S-642]). An Auto row alone would
    /// measure it with no limit and push the link off the card.
    /// </summary>
    private void Fit()
    {
        var link = _link.Visibility == Visibility.Visible ? LinkHeight : 0;
        _empty.MaxHeight = Math.Max(0, ActualHeight - link);

        // A narrow tile gets the short form, rather than a link cut off mid-word.
        _link.Content = (ActualWidth is > 0 and < 200 ? Str.WidgetUi_AccountsLabel : Str.WidgetsA_Office_OpenAccounts).Text();
    }

    /// <summary>Show a sentence, with the Accounts link when <paramref name="offerAccounts"/>.</summary>
    public void Show(string message, bool offerAccounts)
    {
        _message.Text = message;
        _link.Visibility = offerAccounts ? Visibility.Visible : Visibility.Collapsed;
        Visibility = Visibility.Visible;
        Fit();
    }

    public void Hide() => Visibility = Visibility.Collapsed;

    public void Detach() => _link.Click -= OnLinkClicked;

    /// <summary>Open Settings on the Accounts page, the one place accounts are added and mended.</summary>
    public void OpenAccounts()
    {
        if (!_widgetHost.ShowAccounts())
        {
            Log.Info(Scope, "no settings window here to show the Accounts page in");
        }
    }

    private void OnLinkClicked(object sender, RoutedEventArgs args) => OpenAccounts();
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.App.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7060] [S-588] Every kit widget's empty and problem states (and the calendar's and contacts'):
/// the widget's glyph over a sentence, and — when the user is the one who can fix it — a link to
/// Settings › Accounts beneath, centred together.
/// <para>
/// "Account gone", "sign in again" and "access not granted" each need the Accounts page; a blank
/// tile or a sentence with no way forward is the failure the original filed bugs about. Loading,
/// offline and "nothing on" need no link and show none. No account at all is the card's Set up
/// button instead ([S-1862]).
/// </para>
/// </summary>
public sealed partial class KitStatePanel : Grid
{
    private const string Scope = "kit";

    /// <summary>The link's line: body text in a hyperlink button's padding.</summary>
    private const double LinkHeight = 32;

    private readonly TextBlock _message = new();
    private readonly HyperlinkButton _link;
    private readonly HyperlinkButton _grant;
    private Uri? _grantPage;
    private readonly StackPanel _empty;

    private readonly IWidgetHost _widgetHost;

    public KitStatePanel(string glyph, IWidgetHost host)
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

        // [S-9919] Beside the Accounts link: where a service that grants per resource is widened.
        _grant = new HyperlinkButton
        {
            Content = Str.Kit_GrantPage.Text(),
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1),
            Visibility = Visibility.Collapsed,
        };
        _grant.Click += OnGrantClicked;

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        SetRow(empty, 1);
        SetRow(_link, 2);
        SetRow(_grant, 3);
        Children.Add(empty);
        Children.Add(_link);
        Children.Add(_grant);

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
        var link = (_link.Visibility == Visibility.Visible ? LinkHeight : 0) + (_grant.Visibility == Visibility.Visible ? LinkHeight : 0);
        _empty.MaxHeight = Math.Max(0, ActualHeight - link);

        // A narrow tile gets the short form, rather than a link cut off mid-word.
        _link.Content = (ActualWidth is > 0 and < 200 ? Str.WidgetUi_AccountsLabel : Str.WidgetsA_Office_OpenAccounts).Text();
    }

    /// <summary>Show a sentence, with the Accounts link when <paramref name="offerAccounts"/>.</summary>
    public void Show(string message, bool offerAccounts, Uri? grant = null)
    {
        _message.Text = message;
        _link.Visibility = offerAccounts ? Visibility.Visible : Visibility.Collapsed;
        _grantPage = grant;
        _grant.Visibility = grant is null ? Visibility.Collapsed : Visibility.Visible;
        Visibility = Visibility.Visible;
        Fit();
    }

    /// <summary>[S-7040] A kit screen's state: its sentence, with the Accounts link when it offers one.</summary>
    public void Show(Themia.Sdk.Kit.KitScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        Show(screen.Sentence, screen.OffersAccounts, screen.Grant);
    }

    public void Hide() => Visibility = Visibility.Collapsed;

    public void Detach()
    {
        _link.Click -= OnLinkClicked;
        _grant.Click -= OnGrantClicked;
    }

    /// <summary>Open Settings on the Accounts page, the one place accounts are added and mended.</summary>
    public void OpenAccounts()
    {
        if (!_widgetHost.ShowAccounts())
        {
            Log.Info(Scope, "no settings window here to show the Accounts page in");
        }
    }

    private void OnLinkClicked(object sender, RoutedEventArgs args) => OpenAccounts();

    /// <summary>[S-9919] The grant page, through the host's one launcher; a preview opens nothing.</summary>
    private void OnGrantClicked(object sender, RoutedEventArgs args)
    {
        if (_grantPage is { } page && !_widgetHost.IsPreview)
        {
            _widgetHost.Launcher.Open(page.AbsoluteUri);
        }
    }
}

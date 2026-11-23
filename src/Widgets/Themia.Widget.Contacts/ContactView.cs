using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;
using Themia.Widget.Contacts;

namespace Themia.App.Widgets;

/// <summary>
/// [S-16973] One person on the item base's face (<see cref="ItemFace"/>; owner, 2026-11-23: "contact should get a single too"):
/// the person the config picks, their monogram, name, role, address and number, read through the contacts' one feed. A
/// click writes to them (else calls them), a right-click shows their menu; no one picked, or the one picked no longer
/// there, keeps the face: "--" and why (WR-10). The card lights as the person, for them pointed at on the view too.
/// </summary>
internal sealed partial class ContactView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware
{
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(15);

    private readonly IWidgetHost _host;
    private readonly ItemFace _face;
    private readonly ContactsFeed _feed;
    private readonly OpensLight _opensLight;
    private readonly WidgetLinks _links = new();
    private LinkCard<PersonLink>? _card;
    private IWidgetSurface? _surface;
    private IDisposable? _poll;
    private ContactConfig _config = new();
    private Contact? _shown;
    private bool _attached;

    /// <summary>[S-11850] A sample shows the first of its people when none is picked.</summary>
    private bool _sample;

    public ContactView(IWidgetHost host)
    {
        _host = host;
        _feed = new ContactsFeed(() => _config, () => 500);
        _feed.Changed += Render;
        _face = new ItemFace(host);
        _face.List.Opened = _ => Write();
        _face.List.CanOpen = () => _shown is not null && _surface is not { EditMode: true } && !host.IsPreview;
        _face.List.Menu = item => _shown is { } person ? ContactMenu.Of(person, host, () => _ = _feed.RefreshAsync(force: true)) : [];
        Children.Add(_face);
        _opensLight = new OpensLight(this, () => _surface?.EditMode != true);
    }

    /// <inheritdoc />
    public CardOpen? Opens => ContactsModule.Opens(_config.AccountIds, _host.AccountsWith(HostFeatures.Contacts));

    private LinkCard<PersonLink> Card => _card ??= new LinkCard<PersonLink>(_links, LinkTopics.HoveredPerson, this);

    public IReadOnlyList<WidgetAction> ContextActions => [new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _ = _feed.RefreshAsync(force: true))];

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as ContactConfig ?? new ContactConfig();
        _attached = true;
        _feed.Start();
        if (_surface is not { IsPreview: true })
        {
            _feed.Source = _host.Office;
        }

        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _links.Join(_surface?.Links);
        Card.Publishes(this);
        _opensLight.Attach();
        _poll = ticker.Subscribe(Poll, () => _ = _feed.RefreshAsync(force: false));
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ContactConfig ?? new ContactConfig();
        Render();
        _ = _feed.RefreshAsync(force: false);
    }

    public void ApplySize(WidgetArea area) => _face.Size(area);

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        Strings.Changed -= OnLanguage;
        _poll?.Dispose();
        _poll = null;
        _feed.Stop();
        Card.Release();
        _links.Leave();
        _opensLight.Detach();
    }

    private void OnLanguage(Locale locale) => Render();

    /// <summary>[S-16973] The person picked on the face; none: the face with why.</summary>
    private void Render()
    {
        if (!_attached)
        {
            return;
        }

        var locale = Strings.Current;
        var screen = _feed.Screen(locale);
        var ready = screen.Kind == KitScreenKind.Items;
        if (ready && _feed.Held is null)
        {
            ContactsSeen.Last = _feed.People;
        }

        _shown = !ready ? null
            : _config.Person.Length > 0 ? _feed.People.FirstOrDefault(c => c.Id == _config.Person)
            : _sample ? _feed.People.FirstOrDefault() : null;
        var why = !ready ? screen.Sentence
            : _config.Person.Length == 0 ? ContactsStr.Contacts_Pick.Text(locale)
            : ContactsStr.WidgetsA_Contacts_None.Text(locale);
        var item = _shown is { } person ? ContactItems.Item(person) : ContactItems.Nobody(why);
        Card.Key = _shown is { } key ? LinkKeys.PersonLink(key.Email, key.Name) : null;
        _face.Show(item, new ItemDraw(_config.HiddenParts, ItemShape.Of(ContactItems.Parts, _config.HiddenParts)));
    }

    /// <summary>[S-1277] A click: write to them, else call them.</summary>
    private void Write()
    {
        if (_shown is { } person && ContactRules.EnterLink(person) is { } link)
        {
            _host.Launcher.OpenContactLink(link.Kind, link.Value);
        }
    }
}

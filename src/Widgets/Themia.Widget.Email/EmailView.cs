using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Email;

namespace Themia.App.Widgets;

/// <summary>
/// [S-540] Recent mail from the accounts signed in to Themia: Microsoft (Graph), Google (Gmail) and
/// IMAP, merged into one list.
/// <para>
/// A kit view ([S-6421]): the host's one <see cref="App.Services.Mail.IMailSource"/> read through
/// <see cref="MailKitSource"/>, drawn by the kit's binding — the list in the look the style asks for
/// ([S-7032]..[S-7034]), the kit's screens and state panel ([S-7040]), its header with the one stale
/// rule ([S-7042]) and Mail's compose and sort buttons ([S-558], [S-1289]). Fetching, the cadence
/// and the back-off are the service's ([S-546]). A click focuses a row, a double-click opens it,
/// right-click offers what its account allows ([S-544]).
/// </para>
/// </summary>
internal sealed partial class EmailView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys
{
    private const string Scope = "email-widget";

    /// <summary>[S-1289] Below this width the sort button would squeeze the folder name; the menu still has it.</summary>
    private const double SortNeedsWidth = 200;

    /// <summary>[S-558] Below this width the compose button would squeeze the folder name; the menu still has it.</summary>
    private const double ComposeNeedsWidth = 240;

    /// <summary>Only the relative times ("4m") move between fetches; the feed itself is pushed.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    private readonly IWidgetHost _widgetHost;
    private readonly KitBinding _binding;

    private MailKitSource? _source;
    private Ticker? _ticker;
    private IWidgetSurface? _surface;
    private EmailConfig _config = new();
    private EmailStyle _style;

    public EmailView(IWidgetHost host)
    {
        _widgetHost = host;
        _style = _config.Style;
        _binding = new KitBinding(host, MailSpec.Spec(_style, () => _config.Folder), Glyphs.Mail, ActionsOf, Handle)
        {
            HeaderButtons = Buttons,
        };

        Children.Add(_binding.List);
        Children.Add(_binding.State);
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _binding.Header;

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        _binding.Surface(surface);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _config = config as EmailConfig ?? new EmailConfig();
        UseStyle();
        UseSource(new MailKitSource(_widgetHost.Mail, () => _config));
        _binding.Attach(ticker, _source!, _config, Tick);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as EmailConfig ?? new EmailConfig();
        UseStyle();

        if (_source is not null)
        {
            _binding.Apply(_source, _config, Tick, show: true);
        }
    }

    /// <summary>[S-6403] A style with another look draws through a list made for it.</summary>
    private void UseStyle()
    {
        if (_config.Style == _style)
        {
            return;
        }

        _style = _config.Style;
        var old = _binding.List;
        var list = _binding.UseSpec(MailSpec.Spec(_style, () => _config.Folder));
        Children[Children.IndexOf(old)] = list;
    }

    /// <summary>The source the binding reads; the old one lets go of its feed.</summary>
    private void UseSource(MailKitSource source)
    {
        _source?.Dispose();
        _source = source;
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area) => _binding.Size(area);

    /// <summary>[S-1288] [S-7020] A key from the shell's keyboard lease: the kit's list's one key map.</summary>
    public bool HandleKey(WidgetKey key) => _binding.HandleKey(key);

    // -----------------------------------------------------------------------------------------
    // Actions
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-544] A row's actions, read fresh for every menu: what its account allows.</summary>
    private IReadOnlyList<KitAction> ActionsOf(KitItem item)
        => _source?.Message(item.Id) is { } message ? MailSpec.Actions(message, _source.CanChange(message)) : [];

    /// <summary>Carry out what only Mail knows: open, reply and the changes ([S-7013]); a refusal is a header note.</summary>
    private void Handle(KitItem item, KitAction action)
    {
        if (_source is not { } source || source.Message(item.Id) is not { } message)
        {
            return;
        }

        switch (action.Id)
        {
            case MailSpec.OpenId when !source.Open(message):
                source.Problem(message.Provider == Themia.Core.Accounts.HostProviders.Imap
                    ? EmailStr.WidgetsB_Email_ImapNoWebPage.Text()
                    : EmailStr.WidgetsB_Email_CouldNotOpen.Text());
                return;

            case MailSpec.ReplyId when !source.Reply(message):
                source.Problem(EmailStr.WidgetsB_Email_NoReplyAddress.Text());
                return;
        }

        if (MailSpec.Change(action.Id) is { } change)
        {
            Change(message, change);
        }
    }

    private async void Change(MailMessage message, MailChange change)
    {
        if (_source is not { } source)
        {
            return;
        }

        try
        {
            if (await source.ChangeAsync(message, change).ConfigureAwait(true) is { } problem)
            {
                source.Problem(problem);
            }
        }
        catch (Exception ex)
        {
            // async void from a menu click: nothing above this may see an exception.
            Log.Error(Scope, $"{change} failed", ex);
            source.Problem(Str.WidgetsB_Email_ChangeFailed.Text());
        }
    }

    /// <summary>[S-558] Start a new message from <paramref name="account"/>; a refusal is a header note.</summary>
    private void Compose(MailAccount account)
    {
        if (_source is { } source && !source.Compose(account))
        {
            source.Problem(EmailStr.WidgetsB_Email_CouldNotCompose.Text());
        }
    }

    /// <summary>
    /// [S-558] [S-1289] Mail's header buttons: write a new email (any screen with an account to write
    /// from; several accounts ask which), and on the list the sort toggle saying the current order.
    /// </summary>
    private IReadOnlyList<KitHeaderButton> Buttons(KitScreen screen)
    {
        var buttons = new List<KitHeaderButton>();
        var writers = _source?.Targets ?? [];

        if (writers.Count > 0)
        {
            buttons.Add(new KitHeaderButton(
                Glyphs.MailCompose,
                EmailStr.WidgetsB_Email_Compose.Text(),
                ComposeNeedsWidth,
                Choices: [.. writers.Select(a => new KitHeaderChoice(Glyphs.Mail, a.Label, () => Compose(a)))]));
        }

        if (screen.Kind == KitScreenKind.Items)
        {
            buttons.Add(new KitHeaderButton(
                Glyphs.Sort,
                (_config.SortOrder == MailSortOrder.NewestFirst ? EmailStr.WidgetsB_Email_SortNewestTip : EmailStr.WidgetsB_Email_SortOldestTip).Text(),
                SortNeedsWidth,
                () => Edit(c => c.SortOrder = c.SortOrder == MailSortOrder.NewestFirst ? MailSortOrder.OldestFirst : MailSortOrder.NewestFirst)));
        }

        return buttons;
    }

    /// <inheritdoc />
    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            // [S-542] Refresh (with the list's age, [S-559]) and the folder (which mail, not how it
            // looks), the current one checked; [S-558] a new email, from each account read.
            // [S-1746] The view and the order are how the widget looks: they are in its settings only.
            var actions = new List<WidgetAction>(_binding.CardActions);

            var writers = _source?.Targets ?? [];
            foreach (var account in writers)
            {
                actions.Add(new(Glyphs.MailCompose, writers.Count == 1
                    ? EmailStr.WidgetsB_Email_Compose.Text()
                    : EmailStr.WidgetsB_Email_ComposeFrom.Format(("account", account.Label)), () => Compose(account)));
            }

            foreach (var folder in EmailFolders.Listable)
            {
                actions.Add(new(_config.Folder == folder ? Glyphs.Done : FolderGlyph(folder), folder.Label(), () => Edit(c => c.Folder = folder)));
            }

            return actions;
        }
    }

    internal static string FolderGlyph(MailFolder folder) => folder switch
    {
        MailFolder.Sent => Glyphs.Send,
        MailFolder.Drafts => Glyphs.Edit,
        MailFolder.Deleted => Glyphs.Delete,
        MailFolder.Junk => Glyphs.Junk,
        _ => Glyphs.Mail,
    };

    /// <summary>Change this widget's own config from its menu or header, save it, and redraw.</summary>
    private void Edit(Action<EmailConfig> change)
    {
        change(_config);
        _surface?.RequestSave();
        ApplyConfig(_config);
    }

    public void Detach()
    {
        _binding.Dispose();
        _source?.Dispose();
        _source = null;
        _ticker = null;
    }
}

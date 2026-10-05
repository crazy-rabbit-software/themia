using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Developer;
using Themia.App.Shell;
using Themia.Core.Developer;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-610] One repository at a glance: its stars, forks and open issues, its language, last push
/// and default branch, the newest workflow run's verdict in the header, and lists of recent
/// commits, pull requests, workflow runs and issues.
/// <para>
/// The ladder ([S-741]..[S-745]): the three numbers, stacked on the smallest tile and on one line
/// from 180 wide; the release tag (in the header from 400 wide); the language · push · branch line;
/// the description from an Expanded height; then the lists, sharing what is left by quota — a
/// heading and a row for each list first, then a row at a time — so a mid-sized tile shows a little
/// of each rather than all of one. From 520 wide the lists sit in two columns (commits and runs |
/// pull requests and issues); at a Huge height each row takes a second line for its author, state,
/// comments, labels or trigger, and the latest release's name and date join the summary. A wide,
/// short strip puts the numbers in a column and the newest of each list beside them. With no
/// account at all it reads a public repository anonymously ([S-746]).
/// </para>
/// </summary>
internal sealed partial class GitHubView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys
{
    /// <summary>The numbers' column when the lists sit beside them on a wide strip.</summary>
    private const double SummaryColumnWidth = 170;

    /// <summary>Under this width the header's verdict is its coloured dot alone; the title needs the room.</summary>
    private const double CiWordNeedsWidth = 300;

    /// <summary>[S-745] From this width the release tag moves up into the header, beside the verdict.</summary>
    private const double TagInHeaderWidth = 400;

    /// <summary>The summary's line gap (its panel's spacing).</summary>
    private const double SummaryGap = Fluent.Space1;

    /// <summary>The stats line: body-strong text beside a 14px icon.</summary>
    private const double StatHeight = 20;

    private readonly DevChrome _chrome = new(Glyphs.Code);
    private readonly StackPanel _summary = new() { Spacing = SummaryGap };
    private readonly DevColumns _lists = new();
    private readonly List<IDisposable> _hovers = [];

    /// <summary>[S-810] The rows' keyboard cursor, fed by the shell's keyboard lease.</summary>
    private readonly RowCursor _cursor = new();

    private readonly FontIcon _ciDot = Fluent.Icon(Glyphs.Dot, "TextFillColorTertiaryBrush", 8);
    private readonly TextBlock _ciText = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly StackPanel _ci;
    private readonly TextBlock _tagText = Fluent.Text(FluentType.Caption, "AccentTextFillColorPrimaryBrush");
    private readonly StackPanel _tag;
    private readonly StackPanel _trailing;

    private IGitHubSource? _source;
    private IDevFeed<RepoActivity>? _feed;
    private string? _feedKey;
    private IDisposable? _clock;
    private IWidgetSurface? _surface;
    private GitHubConfig _config = new();
    private ReleaseItem? _release;
    private WidgetArea _area;
    private bool _detached;

    public GitHubView()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _summary.Padding = new Thickness(Fluent.Space4, 0, Fluent.Space4, Fluent.Space2);

        _ciDot.VerticalAlignment = VerticalAlignment.Center;
        _ciText.VerticalAlignment = VerticalAlignment.Center;
        _ciText.TextTrimming = TextTrimming.None;
        _ci = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space1,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Children = { _ciDot, _ciText },
        };

        _tagText.TextTrimming = TextTrimming.None;
        _tagText.VerticalAlignment = VerticalAlignment.Center;
        _tag = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space1,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Children = { Fluent.Icon(Glyphs.Tag, "AccentTextFillColorPrimaryBrush", 12), _tagText },
        };
        _tag.DoubleTapped += OnTagDoubleTapped;

        _trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space3,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _chrome.HeaderTrailing, _tag, _ci },
        };

        SetColumnSpan(_chrome.StatePanel, 2);
        SetRowSpan(_chrome.StatePanel, 2);

        Children.Add(_summary);
        Children.Add(_lists);
        Children.Add(_chrome.StatePanel);
    }

    /// <summary>[S-610] The newest run's verdict, the release tag on a wide tile, and the warning when a refresh failed.</summary>
    public FrameworkElement? HeaderTrailing => _trailing;

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle(null);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        Strings.Changed += OnLanguage;
        _source = AppServices.GitHub;

        if (_source is not null)
        {
            _source.AccountsChanged += OnAccountsChanged;
        }

        ApplyConfig(config);
        _clock = ticker.Subscribe(TimeSpan.FromMinutes(1), Render);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as GitHubConfig ?? new GitHubConfig();
        Reopen();
        Render();
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        _chrome.Fit(area.Height);
        Render();
    }

    private void Reopen()
    {
        var repo = GitHubRules.ParseRepo(_config.Repo);

        if (_source is null || repo is null)
        {
            CloseFeed();
            return;
        }

        var query = new RepoQuery(
            [.. _config.AccountIds],
            repo,
            _config.FetchCount(_config.EffectiveCommitCount),
            _config.FetchCount(_config.EffectivePullCount),
            _config.PullFilter,
            _config.PullOrder,
            _config.FetchCount(_config.EffectiveIssueCount),
            _config.FetchCount(_config.EffectiveRunCount),
            _config.RefreshInterval);

        if (_feed is not null && query.Key == _feedKey)
        {
            return;
        }

        CloseFeed();
        _feedKey = query.Key;
        _feed = _source.OpenRepo(query);
        _feed.Updated += Render;
    }

    private void CloseFeed()
    {
        if (_feed is null)
        {
            return;
        }

        _feed.Updated -= Render;
        _feed.Dispose();
        _feed = null;
        _feedKey = null;
    }

    private void OnAccountsChanged() => Render();

    private void Render()
    {
        RenderRows();

        // [S-810] Once the rows are in the tree, the keyboard focus goes back to its row.
        _cursor.Restore();
    }

    /// <summary>[S-810] A key from the shell's keyboard lease, which a click on the widget gives it.</summary>
    public bool HandleKey(WidgetKey key) => !_detached && _cursor.HandleKey(key);

    private void RenderRows()
    {
        if (_detached)
        {
            return;
        }

        ClearRows();
        _summary.Children.Clear();

        var now = DateTimeOffset.Now;
        var snapshot = _feed?.Snapshot ?? DevFeedSnapshot<RepoActivity>.Empty;
        var activity = snapshot.Value;
        var screen = DevScreens.Decide(
            "GitHub", _source?.Accounts ?? [], _config.AccountIds, single: true, snapshot.Outcomes, activity is not null, string.Empty, now, Strings.Current,
            anonymous: true, savedAt: snapshot.SavedAt);

        // [S-614] A repository to show comes after an account to show it with. None at all is the
        // card's Set up button ([S-1862]); one that is not a repository says so.
        if (GitHubRules.ParseRepo(_config.Repo) is null
            && screen.Kind is not (DevScreenKind.NoAccounts or DevScreenKind.AccountsGone or DevScreenKind.SignInRequired))
        {
            screen = new DevScreen(
                DevScreenKind.NeedsSetup,
                string.IsNullOrWhiteSpace(_config.Repo)
                    ? string.Empty
                    : Str.WidgetsA_Github_NotARepo.Format(("repo", _config.Repo.Trim())),
                false,
                null);
        }

        if (!_chrome.Show(screen, _area.Height) || activity is null)
        {
            _summary.Visibility = Visibility.Collapsed;
            _lists.Visibility = Visibility.Collapsed;
            _ci.Visibility = Visibility.Collapsed;
            _tag.Visibility = Visibility.Collapsed;
            _release = null;
            PublishTitle(null);
            return;
        }

        PublishTitle(activity.Repo.FullName);
        ShowRun(activity.Runs.Count > 0 ? activity.Runs[0] : null);
        Lay(activity, now);
    }

    /// <summary>
    /// The ladder, against the area the card gave. An area not measured yet is laid out as a
    /// large tile; the real one follows within the same layout pass.
    /// </summary>
    private void Lay(RepoActivity activity, DateTimeOffset now)
    {
        var width = _area.IsMeasured ? _area.Width : 560;
        var height = _area.IsMeasured ? _area.Height : 376;
        var area = new WidgetArea(width, height);

        var side = area.Shape == AreaShape.Wide && width >= 400;
        _release = activity.Release;
        var tagInHeader = _release is not null && width >= TagInHeaderWidth;
        _tag.Visibility = tagInHeader ? Visibility.Visible : Visibility.Collapsed;
        _tagText.Text = _release?.Tag ?? string.Empty;
        ToolTipService.SetToolTip(_tag, _release is null ? null : Str.WidgetsA_Github_LatestRelease.Format(("tag", _release.Tag)));
        _ciText.Visibility = width >= CiWordNeedsWidth ? Visibility.Visible : Visibility.Collapsed;

        var inline = !side && width >= 180;
        var repo = activity.Repo;

        // The summary's own budget: the whole height beside the lists on a strip, else shared.
        var summaryWidth = (side ? SummaryColumnWidth : width) - (Fluent.Space4 * 2);
        var room = new Disclosure(summaryWidth, height - Fluent.Space2, SummaryGap);
        room.Reserve(inline ? StatHeight : (StatHeight * 3) + 4);

        var tagLine = !tagInHeader && _release is not null && width >= 180 && room.Take(FluentType.Caption.LineHeight);
        var metaLines = side ? 2 : 1;
        var meta = room.Take(FluentType.Caption.LineHeight * metaLines);
        // One line when the estimate says it fits on one, else two (it wraps, then trims).
        var descriptionFits = repo.Description is { } words && FluentType.EstimateWidth(FluentType.Caption, words.Length) <= summaryWidth ? 1 : 2;
        var description = !side && repo.Description is not null && area.HeightClass >= SizeClass.Expanded
            && room.Take(FluentType.Caption.LineHeight * descriptionFits);
        var releaseLine = !side && _release is { } release && (release.Name.Length > 0 || release.PublishedAt is not null)
            && area.AtLeast(SizeClass.Huge, SizeClass.Huge) && room.Take(FluentType.Caption.LineHeight);

        // [S-741] The lists take the rest, by quota.
        var twoLine = !side && area.HeightClass >= SizeClass.Huge;
        // The summary's bottom padding is already spent; the list's own bottom padding is not.
        var listHeight = side ? height - Fluent.Space2 : room.Remaining - Fluent.Space2;
        var shown = PlaceLists(activity, now, width, listHeight, side, twoLine);

        // A tile too short for any list spends what is left on the description instead.
        var descriptionLines = description ? descriptionFits : 0;
        if (shown == 0 && !side && repo.Description is not null && !description)
        {
            descriptionLines = Math.Min(2, Disclosure.WholeRows(room.Remaining - SummaryGap, FluentType.Caption.LineHeight));
        }

        Place(side);
        BuildSummary(activity, now, inline, side, tagLine, meta, descriptionLines, releaseLine);

        _summary.Visibility = Visibility.Visible;
        _lists.Visibility = shown > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Numbers over the lists, or beside them on a wide strip.</summary>
    private void Place(bool side)
    {
        SetRow(_summary, 0);
        SetColumn(_summary, 0);
        SetColumnSpan(_summary, side ? 1 : 2);
        SetRowSpan(_summary, side ? 2 : 1);
        _summary.Width = side ? SummaryColumnWidth : double.NaN;

        SetRow(_lists, side ? 0 : 1);
        SetColumn(_lists, side ? 1 : 0);
        SetColumnSpan(_lists, side ? 1 : 2);
        SetRowSpan(_lists, side ? 2 : 1);
    }

    private void ShowRun(RunItem? run)
    {
        if (run is null)
        {
            _ci.Visibility = Visibility.Collapsed;
            return;
        }

        _ciDot.Foreground = DevBrushes.Run(run.State);
        _ciText.Text = GitHubRules.RunLabel(run.State, Strings.Current);
        ToolTipService.SetToolTip(_ci, Str.WidgetsA_Github_RunOn.Format(("run", run.Name), ("branch", run.Branch), ("state", GitHubRules.RunLabel(run.State, Strings.Current))));
        _ci.Visibility = Visibility.Visible;
    }

    private void BuildSummary(RepoActivity activity, DateTimeOffset now, bool inline, bool side, bool tagLine, bool meta, int descriptionLines, bool releaseLine)
    {
        var repo = activity.Repo;
        var stats = new[]
        {
            (Glyphs.Star, GitHubRules.Compact(repo.Stars), Str.WidgetsA_Github_Stars),
            (Glyphs.PullRequest, GitHubRules.Compact(repo.Forks), Str.WidgetsA_Github_Forks),
            (Glyphs.Issue, GitHubRules.Compact(repo.OpenIssues), Str.WidgetsA_Github_OpenItems),
        };

        var line = new StackPanel { Orientation = inline ? Orientation.Horizontal : Orientation.Vertical, Spacing = inline ? Fluent.Space4 : 2 };

        foreach (var (glyph, value, what) in stats)
        {
            var icon = Fluent.Icon(glyph, "TextFillColorSecondaryBrush", 14);
            var text = Fluent.Text(FluentType.BodyStrong);
            text.Text = value;
            text.TextTrimming = TextTrimming.None;

            var stat = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, Children = { icon, text } };
            ToolTipService.SetToolTip(stat, what.Format(("value", value)));
            line.Children.Add(stat);
        }

        _summary.Children.Add(line);

        if (tagLine && _release is { } release)
        {
            var tag = Fluent.Text(FluentType.Caption, "AccentTextFillColorPrimaryBrush");
            tag.Text = release.Tag;

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space1,
                Children = { Fluent.Icon(Glyphs.Tag, "AccentTextFillColorPrimaryBrush", 12), tag },
            };
            ToolTipService.SetToolTip(row, Str.WidgetsA_Github_LatestRelease.Format(("tag", release.Tag)));
            row.DoubleTapped += OnTagDoubleTapped;
            _summary.Children.Add(row);
        }

        if (meta)
        {
            var parts = new List<string>(3);
            if (repo.Language is { } language)
            {
                parts.Add(language);
            }

            if (repo.PushedAt is { } pushed)
            {
                parts.Add(Str.WidgetsA_Github_Pushed.Format(("when", GitHubRules.Ago(pushed, now, Strings.Current))));
            }

            parts.Add(repo.DefaultBranch);

            var text = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            text.Text = string.Join(" · ", parts);
            text.TextWrapping = side ? TextWrapping.Wrap : TextWrapping.NoWrap;
            text.MaxLines = side ? 2 : 1;
            _summary.Children.Add(text);
        }

        if (descriptionLines > 0 && repo.Description is { } description)
        {
            // [S-745] What the repository is, in its own words.
            var text = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            text.Text = description;
            text.TextWrapping = TextWrapping.Wrap;
            text.MaxLines = descriptionLines;
            ToolTipService.SetToolTip(text, description);
            _summary.Children.Add(text);
        }

        if (releaseLine && _release is { } latest)
        {
            // [S-745] The release's own name and age, its tag being in the header already.
            var parts = new List<string>(3) { latest.Tag };
            if (latest.Name.Length > 0)
            {
                parts.Add(latest.Name);
            }

            if (latest.PublishedAt is { } published)
            {
                parts.Add(Str.WidgetsA_Github_Released.Format(("when", GitHubRules.Ago(published, now, Strings.Current))));
            }

            var text = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            text.Text = string.Join(" · ", parts);
            _summary.Children.Add(text);
        }
    }

    /// <summary>One list as the quota sees it: a heading and its rows.</summary>
    private sealed record Section(string Title, IReadOnlyList<DevRowText> Rows);

    /// <summary>
    /// [S-741][S-742] The lists in one or two columns, each column's height shared by quota.
    /// Returns how many rows were placed.
    /// </summary>
    private int PlaceLists(RepoActivity activity, DateTimeOffset now, double width, double height, bool side, bool twoLine)
    {
        var commits = new Section(Str.Github_CommitsLabel.Text(), [.. activity.Commits.Select(c => CommitText(c, now))]);
        var pulls = new Section(Str.Github_PrsLabel.Text(), [.. activity.Pulls.Select(p => PullText(p, now))]);
        var issues = new Section(Str.Github_IssuesLabel.Text(), [.. activity.Issues.Select(i => IssueText(i, now))]);
        var runs = new Section(Str.WidgetsA_Github_WorkflowRuns.Text(), _config.EffectiveRunCount == 0 ? [] : [.. activity.Runs.Select(r => RunText(r, now, twoLine))]);

        var listWidth = side ? width - SummaryColumnWidth : width;
        var columns = side ? 1 : DevLayout.Columns(listWidth);
        _lists.Reset(columns);

        IReadOnlyList<Section>[] plan = columns == 2
            ? [[commits, runs], [pulls, issues]]
            : [[commits, pulls, runs, issues]];

        var columnWidth = listWidth / columns;
        var rowHeight = twoLine ? DevRows.TwoLineHeight : DevRows.OneLineHeight;
        var layout = twoLine
            ? new DevRowLayout(true, true, true, true, columnWidth >= 300, columnWidth >= 360)
            : DevRows.LayoutFor(columnWidth, 0, twoLineAllowed: false);

        var placed = 0;
        for (var column = 0; column < columns; column++)
        {
            placed += Fill(_lists[column], plan[column], height, rowHeight, layout, headings: !side);
        }

        return placed;
    }

    private int Fill(StackPanel list, IReadOnlyList<Section> sections, double height, double rowHeight, DevRowLayout layout, bool headings)
    {
        var available = sections.Select(s => s.Rows.Count).ToArray();
        var quota = DevLayout.Quota(height, rowHeight, headings ? DevRows.SectionHeight : 0, available);

        // A heading over the only list shown says nothing the rows do not: give its room to a row.
        if (headings && quota.Count(q => q > 0) == 1)
        {
            var only = Array.FindIndex(quota, q => q > 0);
            quota = new int[available.Length];
            quota[only] = DevLayout.Quota(height, rowHeight, 0, [available[only]])[0];
            headings = false;
        }

        var placed = 0;
        for (var i = 0; i < sections.Count; i++)
        {
            if (quota[i] == 0)
            {
                continue;
            }

            if (headings)
            {
                list.Children.Add(DevRows.Section(sections[i].Title));
            }

            foreach (var row in sections[i].Rows.Take(quota[i]))
            {
                Func<List<ItemMenuEntry>>? menu = row.Url is { } url ? () => DevRows.LinkMenu(url, null) : null;
                var built = DevRows.Build(row, layout, _hovers, menu);
                list.Children.Add(built);
                _cursor.Add(built, row.Url, menu);
                placed++;
            }
        }

        return placed;
    }

    private static DevRowText CommitText(CommitItem commit, DateTimeOffset now)
        => new(commit.Message, commit.Sha, DevBrushes.Secondary, commit.Author, GitHubRules.Ago(commit.At, now, Strings.Current), false, commit.HtmlUrl);

    private static DevRowText PullText(PullItem pull, DateTimeOffset now)
        => new(pull.Title, $"#{pull.Number}", DevBrushes.Pull(pull.State, pull.Draft), pull.Author, GitHubRules.Ago(pull.CreatedAt, now, Strings.Current), pull.Draft, pull.HtmlUrl,
            (pull.Draft ? Str.WidgetsA_Github_Draft : pull.State switch { PullState.Merged => Str.WidgetsA_Github_Merged, PullState.Closed => Str.WidgetsA_Github_Closed, _ => Str.WidgetsA_Github_Open }).Text());

    private static DevRowText IssueText(IssueRow issue, DateTimeOffset now)
        => new(issue.Title, $"#{issue.Number}", DevBrushes.Success, GitHubRules.ItemDetail(issue.Author, issue.Comments, Strings.Current), GitHubRules.Ago(issue.CreatedAt, now, Strings.Current), false, issue.HtmlUrl,
            Labels: issue.Labels);

    /// <summary>A run: on one line its branch rides in the title; on two it is the second line's meta.</summary>
    private static DevRowText RunText(RunItem run, DateTimeOffset now, bool twoLine)
        => new(run.Branch.Length > 0 && !twoLine ? $"{run.Name} · {run.Branch}" : run.Name, "●", DevBrushes.Run(run.State), run.Branch, GitHubRules.Ago(run.At, now, Strings.Current), false, run.HtmlUrl,
            run.Actor.Length > 0 && run.Event.Length > 0 ? $"{run.Event} · {run.Actor}" : run.Event);

    private void OnTagDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs args)
    {
        if (_release is { } release)
        {
            DevRows.Open(release.HtmlUrl);
            args.Handled = true;
        }
    }

    /// <summary>The repository's name in the card's header; the user's own label still wins (the card decides).</summary>
    private void PublishTitle(string? fullName) => _surface?.SetHeaderTitle(fullName);

    /// <inheritdoc />
    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            var actions = new List<WidgetAction>(2) { new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _feed?.Refresh()) };

            // [S-749] The repository's own page, once there is one to open.
            if (_feed?.Snapshot.Value?.Repo.HtmlUrl is { } url)
            {
                actions.Add(new WidgetAction(Glyphs.OpenExternal, Str.WidgetsA_Github_OpenRepository.Text(), () => DevRows.Open(url)));
            }

            return actions;
        }
    }

    private void ClearRows()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();
        _cursor.Clear();
        _lists.Reset(_lists.Count);
    }

    /// <summary>[S-1109] Everything on the tile is worded from the snapshot in hand; nothing is refetched.</summary>
    private void OnLanguage(Locale locale) => Render();

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        Strings.Changed -= OnLanguage;

        _detached = true;
        _tag.DoubleTapped -= OnTagDoubleTapped;
        _chrome.Detach();
        ClearRows();
        _summary.Children.Clear();

        _clock?.Dispose();
        _clock = null;

        CloseFeed();

        if (_source is not null)
        {
            _source.AccountsChanged -= OnAccountsChanged;
            _source = null;
        }

        _surface = null;
    }
}

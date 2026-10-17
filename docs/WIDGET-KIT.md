# The widget kit

The widget kit is the set of shared parts a new widget is built from, plus the checks that keep
every widget built from them honest. It exists so that an agent asked to "build widgets for
<an online service>" gets menus, rows, states, keys, styles, settings and sizes right the first
time, by composing parts that are already right, instead of copying the nearest existing widget
and its quirks. The service layer knows no service: a service is a descriptor (data) that picks
from closed sets of typed strategies, plus its own wire types and rules (section 4.7). GitHub is
the first pilot user; Linear and Todoist are the second and third, and section 12 shows on paper
that all three fit without the kit knowing any of them.

Status: **designed, not built** (phase 1 of the service-widget skill). Nothing below exists in
`src/` yet unless it is marked *exists*. The plan of commits is kept outside the repository; this
file is the design an implementation must follow, and the guide new agents read afterwards.

Scope of a service widget: **read-only.** A widget shows what the service holds. Anything that
would change it (merge a pull request, close an issue, mark a notification done) is an item
action that opens the service's own web page at the right place ("Merge on GitHub", "Complete in
Todoist"). Themia never sends a write for a kit service.

Read `CLAUDE.md` first. This file adds to it and never overrides it.

---

## 1. What exists today (inventory)

The kit is distilled from the widgets that already show a service's data. What each does now:

| Widget | Data path | Rows and list | Keys | Item menu attached by | Open gesture | States | Header extras | Preview |
|---|---|---|---|---|---|---|---|---|
| Mail (`EmailView*`) | `MailService` (`IMailSource`/`IMailFeed`, own merge, own probe/fetch), shelves | own rows in three styles (list, inline, table with column header), `RowFocus`, `ScrollFade`, hover quick actions | own `EmailView.Keys` over `ListKeys` | `ItemMenu.ShowAt` from its own `RightTapped` | double-click, Enter | `MailScreens.Decide` (Core), own `HyperlinkButton` to Accounts, own `FitEmptyState` (literal 36) | warning glyph (14 px), busy ring (16), compose, sort, unread badge | `EmailView.Preview.cs` fixture source |
| Tasks (`TasksView*`) | `TaskService`/`TaskFeed`, `PendingWrites` | own rows, **real XAML focus** with system focus visuals (not `RowFocus`), drag reorder, inline editor, add footer | `TaskLayout` key map | `ItemMenu.Show` | Enter/Space ticks | own `_screen`, own sentence per failure | warning glyph (16) with "Last refresh failed: … Showing items from …", busy ring | `TasksView.Preview.cs` |
| Calendar agenda (`CalendarView.Agenda`) | `IOfficeSource`, shelves | own rows, **paged** not scrolled, day headings, two columns from a width | `CalendarFit.Key` | `MakeOpenable` (own) | own | `OfficeStatePanel` (glyph, sentence, Accounts link) | warning glyph (16), `SyncFreshness.Stamp` | `CalendarView.Preview.cs` |
| Contacts (`ContactsView`) | `IOfficeSource` | own rows and cards, **selection** model, search box | `ContactRules.MoveSelection`, type-ahead | `ItemMenu.ShowAt` | Enter writes/calls | `OfficeStatePanel` | warning glyph, `SyncFreshness.Stamp` | none |
| RSS (`RssView*`) | `FeedSync` over `AnswerSync<T>`, **plus its own `RefreshSchedule`** | own rows in columns, `RowFocus`, `ScrollFade`, `FeedLayout` plan, favicons | `ItemCursor.Move` | `ItemMenu.Attach` | double-click, Enter; opens with **its own `Process.Start`** | `Fluent.EmptyState`, joined failure sentences | age text, caution colour when stale, 12 px spinner | none |
| Stocks, Exchange rates | `WebSync` over `AnswerSync<T>` | hero figure, chart band | none ([S-1996]) | none | none | `Fluent.EmptyState` | age text in caution colour (own copy of the freshness rule) | `StocksView.Preview.cs`, `OnlineViews.Preview.cs` |
| Weather now | `WebSync` | hero, styles (`WeatherNowStyles`) | none | none | none | `Fluent.EmptyState` | age via `OnlineParts` | `WeatherNowView.Preview.cs` |
| Clock | none | hero (`HeroFit`, `ClockText`), six styles | none | none | none | none | none | `ClockView.Preview.cs` |
| Meters (`MeterView`) | `SystemHub` | the meter framework (`MeterFace`, `IMeterHost`), already a kit of its own | lane rule [S-1564] | card actions only | none | own | none | `MeterView.Preview.cs` |
| Folder | `FolderListing`, watcher | Explorer semantics (multi-select, lasso, drag, rename) | rich, grid aware | `ItemMenu` + Explorer commands | Enter/double-click | own | busy ring (14) | `FolderView.Preview.cs` |

Shared pieces that already exist and that the kit builds on (*exists*):

- Core: `FetchStatus`/`FetchState`/`Backoff` (`Widgets/FetchState.cs`), `Disclosure`, `WidgetArea`,
  `WidgetTiers`, `ListKeys`, `ItemCursor`, `TypeAhead`, `ListFade`, `WidgetSetup`, `IStyled`/`WidgetStyles`,
  `IShownSettings<T>`/`SettingRules`, `PageSections.Keep`, `ItemKinds`, `ContentAlignment`, `AddTree`,
  `WidgetKeywords`, `SyncSchedule`, `SyncFreshness`, `SyncInterests`, `SyncVault`/`SealedBlob`,
  `UpdateCadence`, `RateLimitRules`, `AccountHttpRules` (`AccountError`, `AccountException.ToFetchStatus`,
  `RedirectRules`, `ErrorBody`), `AccountProviders`, `OAuthFlows`, `Secrets`/`Pkce`, `KeyChord`/`KeyChordText`,
  `ContextMenuLayout`/`QuickCommand`, `LaunchTargets`.
- App: `ItemMenu`/`ItemMenuEntry` (the one menu builder, [S-1740]), `RowFocus`, `ScrollFade`,
  `Fluent` (type ramp, spacing, rows, icons, `EmptyState`, `Hover`), `ItemFill`, `EmptyStatePanel`,
  `OfficeStatePanel`, `AddFooterRow`, `OnlineParts`, `FluentLayout` (settings rows, `ListEditor`,
  `Themed`), `Glyphs`, `ShellLauncher` (the one place that opens anything), `AnswerSync<T>`,
  `WebCache<T>`, `AccountService` (`SendAsync` with conditional headers, `AccountReply.Headers`,
  `IsNotModified`, token refresh), `SyncHost`, `Ticker`.
- History: the GitHub widgets on this branch before `e09beef` (read with `git show e09beef^:<path>`):
  `Core/Developer/GitHubPolling.cs` (conditional requests, `X-Poll-Interval`, anonymous floor,
  `NextPoll`), `App/Services/Developer/GitHubService.cs` (one client, ETag body cache keyed by
  account and address, rate-limit block per account, anonymous requests), `DevFeeds.cs`
  (`DevFeedHub`: shared feeds keyed by query, a slot per account, merge, saved copies), the
  GitHub device-code sign-in (`AccountService.SignIn.cs`, `DeviceCodeSession` in `OAuthFlows.cs`),
  `OAuthClients.GitHub`, and the provider `AccountProvider.GitHub` / `AccountFeatures.GitHub` /
  `AccountAuth.BearerToken`. The kit's service source takes that design's good parts (one client,
  conditional requests, a rate-limit block, shared feeds) but not its shape: what was GitHub's code
  (Link paging, `X-Poll-Interval`, `X-RateLimit-*` names, device flow) becomes one member each of the
  strategy sets of section 4.7, chosen by GitHub's descriptor, beside the members Linear and Todoist
  need.

## 2. Inconsistencies the kit removes

These are what an agent copying "the nearest widget" gets wrong today. Each is a reason for a
part or a check below.

1. **Stale data says it four ways** ([S-2612]): Mail "Could not refresh Work: no network. Updated
   10:42."; Calendar and Contacts "Work: No network · updated Mon 10:42" (`SyncFreshness.Stamp`);
   Tasks "Last refresh failed: … Showing items from …" (`WidgetUi_RefreshFailedSince`, also used by
   RSS's tooltip); RSS, Weather and Stocks put the age in the header in the caution colour, each
   with its own copy of the rule (`RssView.UpdateFreshness`, `StocksView.UpdateFreshness`,
   `OnlineParts`). `SyncFreshness.Line` exists for exactly this and is used only by a test.
2. **Four failure vocabularies:** `FetchStatus`, `AccountError`, `SyncProblem` (Office) and
   `MailScreenKind`, and Mail has its own sentence per `FetchStatus` (`WidgetsB_Email_Status_*`)
   beside `FetchState.Message`.
3. **Three focus models in lists:** `RowFocus.Mark` (Mail, RSS, Folder, Recent files), real XAML
   focus with system focus visuals (Tasks), and a selection (Contacts). Two cursor maps do the same
   job and disagree at the edges: with no cursor yet, Up lands on the last item in `ItemCursor.Move`
   (RSS, screen time) and on the first in `ListKeys.Decide` (Mail, Clipboard, Recent files,
   Notifications, details, removable drives); only `ItemCursor` takes Left/Right, only `ListKeys`
   names Enter, Delete, F2 and Menu.
4. **Three ways to attach an item menu:** `ItemMenu.Attach` (RSS, Recent files), `ItemMenu.Show`
   from a hand-wired `RightTapped` (Tasks), `ItemMenu.ShowAt` with the pointer position (Mail,
   Contacts). They behave the same only by care.
5. **Opening an item:** double-click (Mail, RSS, old GitHub [S-627]) versus single click (Recent
   files, Notifications). Recent files also checks `EditMode` itself, which [S-1751]'s card guard
   already does.
6. **A second launcher:** `RssView.Open` calls `Process.Start` itself, although `ShellLauncher` says
   it is "the one place in this app that opens anything".
7. **Two schedules for one feed:** RSS keeps a `RefreshSchedule` (App) for its empty text while
   `AnswerSync` keeps the real `SyncSchedule` (Core).
8. **Header extras differ in size and order:** warning glyph 14 px (Mail) or 16 (Calendar, Contacts,
   Tasks); busy ring 12 (RSS), 14 (Folder) or 16 (Mail); order warning-busy-buttons-badge in Mail,
   busy-age in RSS.
9. **Empty and problem states:** Mail builds its own Accounts link and height rule beside
   `OfficeStatePanel`, which does the same for Calendar and Contacts.
10. **Account rows on settings pages exist three times:** `MailAccountRows`, `OfficeAccountRows`
    (Calendar, Contacts) and Tasks' `AccountListRow`.
11. **Ladder numbers live in views:** `AgendaNarrowBelow = 260` (`CalendarView.Agenda`),
    `WarningNeedsWidth`/`SortNeedsWidth` (Mail), literal sizes 12/14/36; the rule says breakpoints
    and costs are Core's.
12. **Preview fixtures live inside views** (`*.Preview.cs` partials, `#if DEBUG`), which [S-1863]
    forbids in spirit ("no `#if DEBUG` sample data inside widget views or services").
13. **Literals the checks do not catch yet:** `FontSize = 12` (`HabitTrackerView.Rows`), hard-coded
    icon and ring sizes, colour fallbacks are allowed only as `Themed(key, fallback)` but nothing
    enforces it.
14. **Registration points are not all listed or checked.** `CLAUDE.md`'s "Adding a widget touches"
    misses `AddTree`, `WidgetKeywords` (and `.Providers`), `SyncInterests`, `UpdateSource`,
    `ItemKinds`/`ContentAlignment` declarations, and says the settings dispatch is in
    `WidgetSettings.cs` (it is `ConfigPages.Widget.cs`). Nothing checks that every `WidgetKind` is
    in `WidgetCatalog.All` and `CreateView` (an unregistered kind silently draws a clock).
15. **Seven copies of `FindRoot()`** in the source-scanning tests.
16. **Tag hygiene:** [S-1902] retires the GitHub tags only partly ([S-510]..[S-512], [S-861]..[S-871]);
    the GitHub section's [S-615]..[S-629] and [S-745]..[S-749] were removed without a retirement
    line, and [S-610]..[S-614] are now Tasks' tags. New kit and service tags start at fresh blocks
    (section 11).
17. **Service code is shaped by its first provider.** `RateLimitRules` reads GitHub's header names
    (`X-RateLimit-Remaining`, reset in epoch seconds) for every reply; `SignInWithBrowserAsync`
    picks Microsoft or Google with ternaries; the build-time client secret is Google's alone
    (`THEMIA_GOOGLE_CLIENT_SECRET` in the csproj target); `AccountException.ToFetchStatus` folds
    sign-in-required, forbidden and not-found into `NotConfigured`/`BadResponse`, and its `RetryAt`
    is computed but never read. A third provider today means editing each of those. Section 4.7
    replaces them with descriptor-chosen strategies.

## 3. The kit at a glance

```
Core  (rules, pure, tested)                         App  (WinUI, thin)
-----------------------------------------------     -------------------------------------------
KitItem, KitLead, KitBadge, KitTone, KitTime        KitList          rows, columns, groups, overflow
KitAction, KitActionKind  ---- KitMenu.Entries -->  KitMenu          -> ItemMenuEntry, KeyChord
KitKeys (one key map)                               KitHeader        busy ring, problem glyph, badge
KitPlan = KitLadder.Plan(area, spec, show)          KitStatePanel    loading / empty / problem + link
KitScreen = KitScreens.Decide(...)                  KitBinding       source <-> KitList, Detach-safe
KitFreshness (the one stale rule)                   ServiceSync      one per app: entries, cadence, shelves
IKitConfig  (setup, interest, shown rows)           ServiceClient    one generic client, AccountService
ServiceDescriptor (data) picks strategies:          ServiceSignIn    runs the chosen auth strategy
  Transport, Paging, Auth, Freshness,               KitRows          standard settings rows
  RateLimitScheme, ErrorScheme                      Gallery/Fixtures debug-only preview sources
Services/<Service>/  descriptor, wire, rules, queries   (no App code per service)
```

A kit widget's own code is: its config (Core), its service's descriptor and rules (Core, once per
service), a `KitSpec` (Core: which fields, which actions, which sentences), a view of about a
hundred lines that hands the spec and the source to `KitList`, and a settings page made of
`KitRows`. A service has **no App code of its own**: the client, sign-in, cadence and storage are
generic and read the descriptor.

## 4. Core parts

All in `src/Themia.Core/Widgets/Kit/` (namespace `Themia.Core.Widgets.Kit`) unless noted. Pure,
no UI types, tested in `tests/Themia.Core.Tests/Kit*Tests.cs`.

### 4.1 The item

```csharp
/// One row's worth of a service's data, already validated and cut to its bounds.
public sealed record KitItem(
    string Id,                       // stable across refreshes: the cursor, hover and menus follow it
    string Title,                    // never markup; empty -> the spec's "Untitled" sentence
    string? Subtitle = null,         // one line: repository, sender, list name
    string? Preview = null,          // wraps to the plan's PreviewLines
    KitLead Lead = default,          // what leads the row
    KitWhen? When = null,            // drawn per KitTime
    KitTone Tone = KitTone.Neutral,  // the row's accent: unread, failing check, overdue
    bool Emphasis = false,           // unread / needs you: BodyStrong title
    IReadOnlyList<KitBadge>? Badges = null,
    Uri? Link = null,                // the primary open target; https, on one of the descriptor's WebHosts
    KitGroup? Group = null);         // group; its heading is drawn by the kit

public readonly record struct KitLead(KitLeadKind Kind, string? Value = null, KitTone Tone = KitTone.Neutral);
public enum KitLeadKind { None, Icon, Initials, SiteIcon, StatusDot, Avatar }   // Icon value is a KitIcon name
public readonly record struct KitBadge(string Text, KitTone Tone = KitTone.Neutral);  // "Draft", "3", a label
public enum KitTone { Neutral, Accent, Success, Caution, Critical }               // -> theme brushes in App
public enum KitTime { None, Age, Clock, Date, Due }                               // via LocaleFormat only

/// An instant, or a day without a time (a Todoist or Linear due date, an all-day event).
/// A day is never turned into midnight UTC: "due today" must not move with the time zone.
public readonly record struct KitWhen(DateTimeOffset? At, DateOnly? Day);

/// A group: a day (heading "Today", "Tomorrow", "Mon 20 Oct" via LocaleFormat) or a named key
/// whose heading the rules give (a repository, a project, a cycle).
public readonly record struct KitGroup(string Key, string? Heading = null, DateOnly? Day = null);

/// One answer of a source: the items, the optional summary line of a KitSpec with Summary, and,
/// when the items are empty for a reason the rules know ("No active cycle"), which of the
/// widget's own sentences says so instead of the spec's plain empty sentence.
public sealed record KitPage(IReadOnlyList<KitItem> Items, KitSummary? Summary = null, Str? EmptySentence = null);

/// The line above the list: "Cycle 42 · ends Fri", "main · checks passing". Fraction draws the
/// thin progress band under it (cycle progress, a milestone); never a control.
public sealed record KitSummary(string Text, string? Detail = null, double? Fraction = null, KitTone Tone = KitTone.Neutral);
```

Rules (each a test): an item without an id is dropped at the boundary, ids are unique within a
list, strings are cut to their bounds, a link that `LaunchTargets.Classify` would not open or whose
host is not one of the service descriptor's `WebHosts` is dropped (the row stays, without open
actions), a `KitWhen` holds exactly one of `At` and `Day`, a `Fraction` is clamped to 0..1 and
NaN is dropped, counts are written by one formatter (`KitText.Count`: 999, 1.2k, 12k, 3.4M, in the
language's digits). Service markup in titles (Todoist's Markdown links, GitHub's backticks) is
reduced to plain text by the service's rules, never rendered.

`KitIcon` is a closed Core enum of the glyphs kit items may lead with (pull request, issue, commit,
check passed, check failed, comment, review, release, mail, task, event, feed, ...). App maps each
member to a `Glyphs` entry in one switch; a source-scan test fails when a member has no mapping.
Core never carries code points.

### 4.2 Typed actions

```csharp
public enum KitActionKind
{
    Open,        // the item's Link; the primary action unless the spec names another
    OpenTo,      // "<verb> on <Service>": a web page where the user does what Themia will not
    CopyLink,    // the Link to the clipboard
    CopyText,    // a named text (a SHA, an issue number, an address)
    Toggle,      // a local state flip owned by the widget (a task's tick); never a service write
    Rename,      // F2; only for a widget with local items (Tasks, a local list)
    Delete,      // Delete; danger; only for local items or a queued change the widget owns
    Refresh,     // the widget's sources now
}

public sealed record KitAction(
    KitActionKind Kind,
    Str Label,                       // "Open", "Merge on GitHub", "Copy commit SHA"
    KitIcon Icon = KitIcon.Default,  // Default -> the kind's own glyph
    Uri? Target = null,              // Open, OpenTo: validated https
    string? Text = null,             // CopyText
    bool Disabled = false,
    string? Id = null);              // Toggle/Rename/Delete: which handler of the view runs
```

`KitMenu.Lines(item, actions)` (Core, tested) turns an item's actions into ordered menu lines, and
App's `KitMenu.Entries` turns those into `ItemMenuEntry`s. Nothing else builds a kit item menu.

| Kind | Menu place | Icon row ([S-1740]) | Shortcut shown ([S-1753]) | Gesture under the lease |
|---|---|---|---|---|
| Open | first line | no | Enter | double-click, Enter |
| OpenTo | second group, after a separator, in spec order | no | none | none |
| CopyLink, CopyText | copy group | CopyLink is `QuickCommand.Copy` (first copy only) | Ctrl+C for the first | Ctrl+C |
| Toggle | first group, a check line | no (toggles never go to the row) | Space | Space, the lead's own click |
| Rename | edit group | `QuickCommand.Rename` | F2 | F2 |
| Delete | last group | `QuickCommand.Delete`, danger | Delete | Delete |
| Refresh | last line, "Refresh" or "Refresh · updated 4 min ago" | no | none | none |

Menus never change how a widget looks ([S-1746]): there is no `KitActionKind` for a view, sort,
column or "show" switch, so a spec cannot express one. The Menu key and Shift+F10 open the
focused row's menu at the row ([S-1748]); a right-click opens it at the pointer.

Web actions ("open to do X") are how write scope stays out of Themia. A service's rules produce
them from the item: a pull request offers `OpenTo("Review on GitHub", <pr>/files)` and
`OpenTo("Merge on GitHub", <pr>#partial-pull-merging)`, a Linear issue `OpenTo("Change status in
Linear", <issue url>)`, a Todoist task `OpenTo("Complete in Todoist", https://app.todoist.com/app/task/<id>)`.
The label says where the click goes. The target goes through `ShellLauncher` like every other open,
and only to an https address on the descriptor's `WebHosts`. App deep links (`todoist://task?id=`)
are not offered: a custom scheme opens whatever registered it, and the web page reaches the same
place.

### 4.3 One key map

`KitKeys.Decide(WidgetKey key, KitCursor cursor, KitListShape shape) -> KitKeyResult` is the one
list key map, built on `ListKeys.Decide` (which already names the commands) with `ItemCursor`'s
Left/Right for columns. The two disagree on Up with no cursor (first or last item); the kit keeps
`ListKeys`' answer (the first item), which six widgets already use, and RSS's keyboard change is a
tagged SPEC line of its own when RSS migrates (no pixel changes). Up/Down (and Left/Right across columns), PgUp/PgDn, Home/End move; Enter
runs the primary action; Space a `Toggle`; F2 `Rename`; Delete `Delete`; Ctrl+C the first copy;
Menu opens the menu; Escape drops the cursor ([S-813]). With `Overflow = Page`, Left/Right and
PgUp/PgDn page instead ([S-3624]). Optional type-ahead goes through the existing `TypeAhead`.

### 4.4 The ladder

```csharp
public sealed record KitSpec(
    KitRowLook Looks,                // which row looks the widget allows (flags)
    KitShow Optional,                // which optional fields exist: Subtitle, Preview, When, Lead, Badges, Group headings
    KitOverflow Overflow,            // Scroll (default) or Page
    bool Summary = false,            // a summary line above the list (a repository's name and check state)
    int MaxColumns = 1);

public readonly record struct KitPlan(
    KitRowLook Look,                 // Line, TwoLine, Rich (title + subtitle + preview), Table
    bool Lead, bool Subtitle, int PreviewLines, bool When, bool Badges,
    bool Summary, bool Headings, int Columns, int Rows);

public static class KitLadder
{
    public static KitPlan Plan(WidgetArea area, KitSpec spec, KitShow userShows, int count);
}
```

`Plan` is the only place a kit widget's breakpoints live, and it follows [S-660]..[S-666]:
classify on the area, measure on the deflated box, `Reserve` one row of the plainest look (the
must-show part), then `Take` the optional rungs in this fixed priority order: the summary line,
the lead, the subtitle, the second column of the time, badges, preview lines, a second (third)
column at Huge width; then `TakeRows` for whole rows ([S-639]), never a partial one. Costs are
`FluentType` line heights plus `Fluent` spacing, measured on worst-case text ("Wednesday 23:59",
the longest German age). Rows are a fixed Fluent height per look ([S-213]); a bigger tile shows
more rows, never taller ones. Concise first ([S-666]): no rung may add a control, a detail pane,
counts footer or another widget's job. `FeedLayout`, `MailLayout`'s row parts and `CalendarFit`'s
agenda rules become specs over this one ladder when those widgets migrate.

### 4.5 Screens: the one state machine

```csharp
public enum KitScreenKind
{
    Items,            // draw the list (possibly with a problem in the header)
    Loading,          // nothing held yet, a fetch is out
    Empty,            // the source answered with nothing: the spec's own sentence ("No reviews waiting")
    NoMatch,          // a filter left nothing: "No pull requests match these filters"
    AccountsGone,     // the widget names accounts and none is signed in any more -> Accounts link
    SignInRequired,   // every account it reads needs signing in again -> Accounts link
    Forbidden,        // signed in, the service refused this data (scope, SSO) -> Accounts link
    Problem,          // nothing held and the last attempt failed: FetchState.Message(status) + detail
}

public sealed record KitScreen(KitScreenKind Kind, string? Sentence, bool OffersAccounts, IReadOnlyList<string> HeaderProblems);

public static class KitScreens
{
    public static KitScreen Decide(
        IReadOnlyList<KitAccount> signedIn, IReadOnlyList<string> chosen,
        IReadOnlyList<KitOutcome> outcomes, int itemCount, bool filtered, DateTimeOffset now);
}

public sealed record KitOutcome(string Owner, FetchStatus Status, KitAccountState Account, string? Detail,
    DateTimeOffset? HeldAt, DateTimeOffset? RetryAt);
public enum KitAccountState { Ok, Gone, SignInRequired, Forbidden }
```

It is `MailScreens.Decide` and `SyncProblem` generalised: one decision for every kit widget, so
"no account at all" (the card's Set up, [S-1861]), "account gone", "sign in again", loading,
offline, rate limited (with the time it will ask again) and empty each have exactly one look and
one sentence. Views never see an `AccountError`, an HTTP status or a GraphQL error code.

**The one failure vocabulary.** `FetchStatus` gains the three members the inventory found folded
away: `SignInRequired` (401 after the one renewal, a revoked grant, a GraphQL authentication
code), `Forbidden` (403 that is not a throttle, a missing scope, SSO not authorised, a GraphQL
forbidden code) and `NotFound` (404: a repository renamed, a project deleted; not retried on the
normal cadence, held like a refusal). `AccountException.ToFetchStatus` maps to them instead of
`NotConfigured`/`BadResponse`, and `FetchState.Message` has one sentence each. Every service's
answer reaches this vocabulary through exactly one function, `ServiceFailures.Classify(descriptor,
reply)` (section 4.7), so an HTTP 400 carrying Linear's `RATELIMITED` and a GitHub 403 with
`x-ratelimit-remaining: 0` are both `RateLimited` with a `RetryAt`, and nothing downstream knows
which service it was. `KitAccountState` is derived from the status (`SignInRequired` and
`Forbidden` name the account; `Gone` comes from the account list, not a reply).

**The one stale rule** (`KitFreshness`, built on `SyncFreshness`): while a stored copy is on screen
and the last attempt for some owner failed, the header shows the problem glyph, and its tooltip has
one line per owner and problem, folded as [S-2664] folds them: `"Work: No network · updated Mon
10:42"` (`SyncFreshness.Stamp`). The card menu's Refresh line says the age ("Refresh · updated 4 min
ago", as Mail does). The age itself is never drawn in the header in the caution colour any more.
This is a behaviour change for Mail, Tasks, RSS, Weather and Stocks and ships as its own SPEC
change after the migrations (section 10, open question Q3).

### 4.6 The config contract

```csharp
/// A widget built on the kit. Implemented by its config, discovered by the shared rules,
/// so a new kit widget adds no line to WidgetSetup, SyncInterests or the settings dispatch.
public interface IKitConfig
{
    /// What it reads: the service feature it needs (None for a web source without accounts).
    AccountFeatures Needs { get; }

    /// False while it has nothing to read (no repository, no query): the card's Set up ([S-1860]).
    bool Configured { get; }

    /// The chosen accounts; empty is "every account offering Needs" ([S-623] in the old GitHub section, Mail's [S-549]).
    IReadOnlyList<string> AccountIds { get; }

    /// What the background sync keeps fresh for it ([S-2608]); null when not configured.
    KitInterest? Interest(RefreshPreferences refresh);

    /// The fields the user may turn on or off (IShownSettings<KitShow> drives the page's rows).
    KitShow Shows { get; }
}
```

`WidgetSetup.Needed` gains one arm (`IKitConfig kit => !kit.Configured || (kit.Needs != None &&
Lacks(signedIn, kit.Needs))`), and `SyncInterests.From` one loop over `IKitConfig` configs that
collects `KitInterest`s; neither grows per kind again. Styles stay `IStyled` exactly as
[S-6000]..[S-6010] say; a kit style is a different `KitSpec` (row looks, columns), never a second
setting.

### 4.7 The service layer (Core): a descriptor choosing typed strategies

**Principle.** The kit's service code knows *strategies*, never *services*. Everything a service
does differently on the wire (how it is asked, paged, signed into, throttled, how it fails, how it
says "nothing changed") is one member of a closed set below, and the service's
`ServiceDescriptor` picks members and fills in their parameters: header names, endpoints, scopes,
codes. The descriptor is data (a `static readonly` record in the service's folder, no virtual
methods, no callbacks). What cannot be data (building an address from a query, mapping a wire
shape to `KitItem`s) is the service's rules, pure functions with fixed signatures the kit calls.
Every strategy is AOT-clean: JSON through the service's own source-generated context, GraphQL
documents as `const string`s, no reflection, no `dynamic`, no runtime schema.

Shared code lives in `src/Themia.Core/Services/` (namespace `Themia.Core.Services`); one folder per
service, `src/Themia.Core/Services/<Service>/`. Sign-in is the one part shared with the non-kit
providers: Microsoft and Google get a `ProviderSignIn` entry (the descriptor's `SignIn` list and
token header, nothing else) when their sign-in moves onto `ServiceSignIn` (section 10, step 11);
their data paths are not kit services and stay as they are.

#### The descriptor

```csharp
public sealed record ServiceDescriptor(
    AccountProvider Provider,                    // the closed set of [S-500]; the descriptor table is keyed by it
    AccountFeatures Feature,                     // what widgets ask for ([S-502]); one flag per kit service
    UpdateSource Cadence,                        // its line in Settings > Widgets > Updates ([S-3780])
    TransportKind Transport,                     // RestJson | GraphQL | FormPost
    Uri ApiRoot,                                 // https; the GraphQL endpoint for GraphQL
    ApiRootRule Root,                            // Fixed | PerAccount (an Enterprise server, Q6)
    ProviderSignIn SignIn,                       // ServiceAuth members in the order the Accounts page offers
                                                 // them, and the default TokenScheme (Bearer | Raw)
    IReadOnlyList<HeaderPin> Pinned,             // Accept, an API version; never a credential
    RateLimitScheme Limits,
    ErrorScheme Errors,
    FreshnessHints Freshness,                    // the default; a query may narrow it
    string? PollIntervalHeader,                  // e.g. "X-Poll-Interval"; null when the service sends none
    AnonymousRule Anonymous,                     // None | PublicReads(TimeSpan floor)
    int MaxPages, int ItemBound,                 // the hard bounds of any one answer
    IReadOnlyList<string> WebHosts,              // hosts an item link or OpenTo may name
    TimeSpan IdleFloor);                         // the shortest cadence the service's budget allows
```

`ServiceDescriptors.For(AccountProvider)` is one switch over the closed enum (a test fails when a
kit provider has none). `AccountProviders` (labels, wire names, features) stays the one table of
providers and reads its sign-in kinds from the descriptor instead of naming them.

#### Strategy sets

**Transport** (`TransportKind`, service-level):

| Member | Request | Answer | Who uses it |
|---|---|---|---|
| `RestJson` | GET (conditional headers when held) to a rules-built address | the body is the wire type, parsed with the service's `JsonTypeInfo<T>` | GitHub, Todoist REST |
| `GraphQL` | POST `application/json` `{"query": <const>, "variables": {...}}` to `ApiRoot`; the body is serialised by the rules through the service's context (`GraphQLRequest<TVars>`) | `GraphQLResponse<TData>` (`data`, `errors[]` with `message`, `path`, `extensions.code`); `errors` go to the `ErrorScheme`, `data` to the rules; **partial data** (a 200 with both) is used when the list's own path is present, and the errors are logged once and become the outcome's `Detail` | Linear |
| `FormPost` | POST `application/x-www-form-urlencoded` with rules-built fields | as `RestJson` | Todoist Sync (designed, built only if Q12 picks Sync) |

A transport owns only the envelope: content type, how a body is framed, where errors live. It
never reads a service field by name.

**Paging** (`PagingKind`, per query, because one service mixes them):

| Member | Next page | Where the cursor comes from | Who uses it |
|---|---|---|---|
| `None` | never | | a single resource, a summary query |
| `LinkHeader` | the `rel="next"` address of RFC 8288 `Link` (`LinkPaging.Next`): https only, same host as the request, at most `MaxPages` | the reply's headers | GitHub REST |
| `BodyCursor` | the same request with the cursor set (a query parameter for REST, a variable such as `after` for GraphQL) | the rules' map returns it (`endCursor` while `hasNextPage`; Todoist `next_cursor` until null) | Linear `pageInfo`, Todoist `results`/`next_cursor` |
| `PageToken` | as `BodyCursor`, parameter named by the query | `nextPageToken` from the map | Google style; no kit service yet, kept because it is the same code as `BodyCursor` with another name |
| `OffsetLimit` | `offset += limit` until a short page | counted by the pager | services without cursors |

`ServicePager.Next(paging, request, reply, chunk, heldCount, bound)` is the one loop: it stops at
the widget's count, at `MaxPages`, at `ItemBound`, on an empty page, and on a cursor that repeats
(a loop is a bad response, never an endless fetch). Page sizes are the query's (`PageSize`, capped
by the service's documented maximum in the query: GitHub notifications 50, search 100, Todoist
200, Linear the GraphQL `first`).

**Auth** (`ServiceAuth`, a closed set of sealed records; a service lists the ones it offers):

| Member | Flow | Secret | Stored as |
|---|---|---|---|
| `OAuthPkceLoopback(AuthorizeUrl, TokenUrl, ClientId, Scopes, ScopeSeparator, Loopback, ClientSecret, Refresh)` | the existing browser flow ([S-1780]..[S-1784]): system browser, loopback listener, `state`, PKCE S256 | `ClientSecret = None` (Microsoft, Linear), or `BuildTime` (Google, a confidential Todoist app): read from `BuildSecrets.ClientSecret(Provider)`, never in source | `OAuthRefresh` (or `Token` when the service issues no refresh token) |
| `OAuthDeviceCode(DeviceUrl, TokenUrl, ClientId, Scopes, ScopeSeparator, Refresh)` | RFC 8628: show `user_code` and open `verification_uri`; poll at `interval`, +5 s on `slow_down`, stop on `expired_token`/`access_denied` (`DeviceCodeSession` restored from `e09beef^`) | none | `Token`, or `OAuthRefresh` when the answer carries a refresh token |
| `PastedToken(TokenPage, Shape, Scheme)` | the user creates a token on the service's page (`TokenPage`, opened by the Accounts page) and pastes it; validated by `Shape` (length, alphabet) and then by the identity request | none | `Token`, DPAPI-sealed through `Secrets` like every secret |
| `Anonymous` | no sign-in; only where `AnonymousRule.PublicReads` | none | no account; entries keyed by `anonymous` |

Parameters that differ between services and are data, not code:

- `ScopeSeparator`: space (RFC 6749; GitHub, Microsoft, Google) or comma (Linear, Todoist).
- `LoopbackRule(Host, Path, Port)`: `Host` is `Localhost` (Microsoft matches only that) or
  `Loopback4` (`127.0.0.1`, Google, GitHub); `Port` is `Any` (the provider ignores the port of a
  registered loopback redirect: Microsoft, Google, GitHub) or `Fixed(ports)` (the provider matches
  the registered URI exactly: the listener tries the listed ports in order and the registration
  lists the same ports). The listener is the existing raw-socket one; it gains the port list.
- `RefreshRule(Rotates, ReuseGrace)`: a rotating refresh token (Linear, Todoist, GitHub expiring
  tokens, Microsoft) is persisted atomically **before** the new access token is used, inside the
  existing single-flight refresh; a test drives "refresh answered, write failed" and asserts the
  old pair is not reused after the grace (Todoist revokes every token of the user and app on reuse
  outside its 60 s grace).
- `TokenScheme`: `Bearer` sends `Authorization: Bearer <t>`; `Raw` sends `Authorization: <t>`
  (Linear personal API keys). Chosen per auth member, because Linear's OAuth tokens are Bearer and
  its API keys are not; the descriptor's `TokenHeader` is the default.
- The identity request: every service has one query (`<Service>Queries.Identity`) whose map
  returns the account id and display name. It runs after every sign-in and on a pasted token
  before it is stored (a token that fails it is not saved).

Sign-in is one App class, `ServiceSignIn`, with one method per auth member; the Accounts page lists
the providers and, under each, the members of its `SignIn` list (a device-code panel, a paste box
with the token page link, a browser button). No provider is named in App sign-in code; the
ternaries of `SignInWithBrowserAsync` go when Microsoft and Google move onto descriptors.

Build-time secrets are generalised once: the csproj target reads one variable,
`THEMIA_CLIENT_SECRETS` (`google=…;todoist=…`), validates each value's alphabet as today, and
generates `BuildSecrets.ClientSecret(AccountProvider)`; Core's `ClientSecretRules.Parse` holds the
parsing rule and is tested. `THEMIA_GOOGLE_CLIENT_SECRET` is replaced by it (one path; `CLAUDE.md`
and `docs/STORE.md` updated in the same commit). A service whose descriptor says `BuildTime` and
whose secret is absent from the build offers its other sign-in members only, and logs that once.

**Freshness** (`FreshnessHints` flags plus `Incremental`, per query, defaulting to the descriptor):

| Member | What it sends | What a "nothing changed" looks like | Who uses it |
|---|---|---|---|
| `ETag` | `If-None-Match` with the held `ETag` (GET only) | `304`: the held body is reused; a 304 with nothing held is `BadResponse` | GitHub |
| `LastModified` | `If-Modified-Since` with the held `Last-Modified` (GET only) | `304` | GitHub notifications |
| `PollIntervalHeader` | nothing; the reply's header (named by `PollIntervalHeader`) stretches the next poll, capped at an hour | | GitHub notifications (`X-Poll-Interval`) |
| `None` | nothing | every answer is new; the cadence alone keeps it cheap | Linear, Todoist REST |
| `Incremental.SyncToken` | the held sync token (`*` for a full read) | a delta: upserts and removals by id (`is_deleted`), merged onto the stored copy; a rejected token or a full-sync flag replaces the copy; a full read at least daily | Todoist Sync (if Q12 picks it) |
| `Incremental.Probe` | a small rules-built probe (the newest `updatedAt` of the set) before the real query | the probe's value is unchanged: the full query is skipped | Linear, only if the complexity budget bites (off by default) |

An "updated since" delta (fetch only items changed after a high-water mark) is **rejected**: an
item that leaves a filtered set (unassigned, moved to another team) never shows up in the delta,
so the stored copy would keep it. Only a service-side sync token or a full re-read is correct.
Conditional bodies are held in memory per `account + request key` (bounded, oldest out); a 304 is
answered from there and never parsed as an empty list.

**Rate limits** (`RateLimitScheme`, service-level, read by the existing `RateLimitRules`, which
loses its GitHub constants):

```csharp
public sealed record RateLimitScheme(
    IReadOnlyList<RateBudget> Budgets,       // each a (limit, remaining, reset) header triple; any spent budget blocks
    ResetUnit Reset,                         // EpochSeconds | EpochMillis | DeltaSeconds
    string? BucketHeader,                    // the reply names its bucket ("x-ratelimit-resource"); null: the request's
    IReadOnlySet<int> ThrottleStatuses,      // statuses that are a throttle when a budget is spent or a wait is named
    IReadOnlySet<int> RetryAfterIgnoredOn);  // statuses whose Retry-After is not a wait (Todoist sends one on 401)

public sealed record RateBudget(string LimitHeader, string RemainingHeader, string ResetHeader);
```

A throttle is known from the headers (`ThrottleStatuses` with a spent budget or a `Retry-After`),
or from the body through the `ErrorScheme` (a GraphQL `RATELIMITED` on an HTTP 400, a Todoist
`retry_after` in `error_extra`, GitHub's "secondary rate limit" message on a 403 with budget left).
The block is per **account and bucket**: every request declares its bucket (`core`, `search`,
`graphql`, `sync-full`, `sync-partial`), the reply's `BucketHeader` confirms it, and a spent search
budget does not stop the core reads. The wait is `Retry-After`, else the bucket's reset, else
`RateLimitRules.DefaultWait`, never past `MaxWait`; the outcome carries it as `RetryAt`. A success
that spent the last request of a budget blocks until its reset too. Header values that do not
parse are absent, never zero. `MaxWait` stays two hours (Todoist's windows are 15 minutes,
GitHub's and Linear's an hour). The Microsoft and Google schemes are `Retry-After` only, which is
what `AccountHttp` honours today; kit requests ask `AccountHttp` for no retries of their own,
because the bucket block is the one retry rule.

**Errors** (`ErrorScheme`, service-level; `ServiceFailures.Classify` is the one mapping):

```csharp
public sealed record ErrorScheme(
    ErrorBodyKind Body,                                // None | GraphQLErrors | JsonCode
    string? CodeField,                                 // JsonCode: the field holding the code ("error_tag")
    IReadOnlyDictionary<string, FetchStatus> Codes,    // "RATELIMITED" -> RateLimited, "UNAUTHORIZED" -> SignInRequired
    IReadOnlyList<MessageRule> Messages,               // a message substring -> status ("secondary rate limit" -> RateLimited)
    string? RetryAfterField);                          // a body field holding seconds to wait ("error_extra.retry_after")
```

Order: transport failure (`Offline`, `Unreachable`, `TimedOut`) → throttle (rate-limit scheme,
then body codes and messages) → body code → status (`401` → `SignInRequired` after the one
renewal `AccountService` already does, `403` → `Forbidden`, `404`/`410` → `NotFound`, `5xx` →
`BadResponse`, other non-2xx → `BadResponse`) → parse (a body the wire type rejects →
`BadResponse`, logged with the first path that failed, never half-built). A GraphQL `200` with
errors and no data at the list's path is classified by its codes like any failure. A code the table
does not know is `BadResponse` and logged once with the code, so a new code is visible rather than
silently read as "empty". To read error bodies, kit requests take the bounded `AccountReply` (status,
headers, body) from `AccountService.SendAsync` instead of its exception for statuses other than
401; token renewal stays in `AccountService`.

#### Per service: what the service writes

```
src/Themia.Core/Services/<Service>/
  <Service>Descriptor.cs   the ServiceDescriptor: strategies and their parameters (data)
  <Service>Wire.cs         response (and GraphQL variable) shapes + <Service>WireContext ([JsonSerializable] each closed type)
  <Service>Queries.cs      enum <Service>List { ... } + the query record whose Key names everything that changes the
                           answer and never the token; Identity; GraphQL documents as const strings
  <Service>Rules.cs        Build(query, page) -> ServiceRequest; Map(query, wire, now) -> KitChunk (items, next cursor,
                           summary); Actions(item) -> KitAction list; input validation (a repository, a team key)
```

```csharp
public sealed record ServiceRequest(HttpVerb Verb, Uri Address, ServiceBody? Body, string Bucket, string Key);
public sealed record ServiceBody(ReadOnlyMemory<byte> Utf8, string ContentType);   // built by the rules through their context
public sealed record KitChunk(IReadOnlyList<KitItem> Items, string? NextCursor, KitSummary? Summary,
    IReadOnlyList<string>? Removed = null, Str? EmptySentence = null);   // Removed: ids a delta deleted (SyncToken only)
public abstract record PageAt { First; Cursor(string Value); Offset(int Value); Address(Uri Next); }   // closed

/// The kit's whole contract with a service's rules: one per query kind, registered in the descriptor's query table.
public interface IServiceQuery<TWire>
{
    PagingKind Paging { get; }
    FreshnessHints Freshness { get; }
    JsonTypeInfo<TWire> Wire { get; }
    IReadOnlyList<ServiceStep> Steps { get; }                    // usually one; see below
    ServiceRequest Build(PageAt page, IReadOnlyDictionary<string, string> fromSteps);
    KitChunk Map(TWire wire, IReadOnlyDictionary<string, string> fromSteps, DateTimeOffset now);
}
```

A query may need values from an earlier request (GitHub: a repository's default branch before its
workflow runs; Todoist: project names for the tasks' `project_id`). `ServiceStep` is that: an
earlier request of the same service, paged and fetched through the same conditional, rate-limit
and error path and held like any entry, whose map yields a table of named strings handed to the
next `Build` and to `Map` (a default branch is a 304 almost always; projects change rarely). At
most three steps; a query that needs more is two widgets.

`KitMerge` (newest first, the same item seen through two accounts once, cut to the count) and the
item rules of section 4.1 apply to every service's chunks; the descriptor's `WebHosts` and
`ItemBound` are enforced there, not in the rules.

#### The rule for "generic enough"

Adding a service may add only:

1. its descriptor entry (`<Service>Descriptor.cs` and its arm in `ServiceDescriptors.For`);
2. its wire types and JSON context;
3. its rules (request builders, wire → `KitItem`/`KitAction`/`KitSummary` mapping, input validation);
4. its queries enum and GraphQL documents;
5. its `AccountProvider`/`AccountFeatures` members, its `UpdateSource` member, its build secret name
   in `THEMIA_CLIENT_SECRETS` if its descriptor says `BuildTime`, its locale keys, its fixtures.

If a service needs anything else (a new branch in `ServiceClient`, a header read by name in shared
code, a sign-in screen of its own, a special case in `RateLimitRules`), the kit is wrong: a
strategy member or a parameter is added to the kit first, in its own commit, with tests, and the
service then uses it. A conformance check holds the line (section 8): no file under
`Core/Services/*.cs`, `Core/Widgets/Kit/`, `App/Services/Kit/` or `App/Widgets/Kit/` names a
service (`GitHub`, `Linear`, `Todoist`, any `AccountProvider` member) outside the descriptor table.

Accounts: each service is a new `AccountProvider` member with its `AccountFeatures` flag (one flag
per kit service: widgets ask for the service's data, never the provider; a task service's flag is
not `Tasks`, because the Tasks widget would then try to write to it) and `AccountAuth` gains
`Token` (a token without refresh: pasted, or a non-expiring OAuth grant) beside `OAuthRefresh` and
`Password` (the closed sets of [S-500]/[S-502]).

## 5. App parts

All in `src/Themia.App/Widgets/Kit/` and `src/Themia.App/Services/Kit/`. Non-generic XAML types:
a generic class deriving from a WinRT type is a CsWinRT/AOT risk, so generics stay in Core and the
App parts take `KitItem`s.

### 5.1 `KitList` (the list part)

A `sealed partial class KitList : Grid` that a kit view puts in its slot and feeds. It owns:

- Rows: full-bleed (`ItemFill.Paint(row, ItemRole.Row)`, the inset as the row's own padding,
  [S-190]), no separators ([S-190b]), group headings as labels with the inset ([S-191]), columns from
  the plan, whole rows only, `ScrollFade` under a scroll, or pages for `Overflow = Page`.
- Fields: lead (icon, initials, site icon through `FaviconCache`, status dot, avatar), title (Body,
  BodyStrong when emphasised, wrapping to the look's lines), subtitle and when (Caption, tertiary),
  preview (Caption, secondary), badges, tooltips (whole title, subtitle and link).
- Focus: one model, `RowFocus` (the cursor is the item id, so it survives a refresh, a resize and
  a language change). A click focuses; a double-click or Enter runs the primary action ([S-1288],
  [S-1306]). The card's [S-1751] guard makes it inert in edit mode; `KitList` adds no check of its own.
- Menus: `ItemMenu.Attach(row, () => KitMenu.Entries(item, spec.Actions(item)))`, so every row's
  menu is read fresh on each right-tap; the Menu key goes through `ItemMenu.Open`.
- Keys: implements the body of `IWidgetKeys.HandleKey`/`HandleText` through `KitKeys`; the view
  forwards to it (one line each).
- Hover actions: an action flagged by the spec as inline (Mail's quick actions, [S-1284]) shows on
  hover and on the focused row, only at the width the plan allows.
- Detach: every hook disposed, every handler unsubscribed, safe twice.

### 5.2 `KitHeader`

The `IWidgetHeaderContent.HeaderTrailing` of a kit view, built in the view's constructor
([S-400a]): from leading to trailing, the problem glyph (`Glyphs.Warning`, caution brush,
`Fluent.IconSize`, tooltip from `KitFreshness`, a click opens Accounts when the screen offers it),
the busy ring (`Fluent.IconSize`, only while a fetch for one of the widget's own keys is out,
[S-2661]), optional view buttons from the spec (Mail's compose), and the count badge. Sheds in that
order's reverse on narrow tiles by `KitLadder`, not by numbers in the view. A side header drops it
([S-408]).

### 5.3 `KitStatePanel`

`OfficeStatePanel` and Mail's empty host merged: the widget's glyph over `KitScreen.Sentence`, and
the Accounts link (`AppServices.ShowAccounts`) when `OffersAccounts`, shed to fit the height
through `EmptyStatePanel` ([S-642]). Loading shows the glyph alone (no sentence flicker). The Set up
state is not here: the card owns it ([S-1862]).

### 5.4 `KitBinding` and the view

```csharp
internal sealed partial class GitHubReviewsView : Grid, IWidgetView, IWidgetHeaderContent,
    IWidgetSurfaceAware, IWidgetSizeAware, IWidgetContextActions, IWidgetKeys
{
    private readonly KitList _list = new();
    private readonly KitHeader _header = new();
    private KitBinding? _binding;

    public GitHubReviewsView() => Children.Add(_list);
    public FrameworkElement? HeaderTrailing => _header;
    public void Attach(Ticker ticker, WidgetConfig config) => _binding = KitBinding.Open(this, _list, _header, config, GitHubReviews.Spec);
    public void ApplyConfig(WidgetConfig config) => _binding?.Apply(config);
    public void ApplySize(WidgetArea area) => _binding?.Size(area);
    public void Bind(IWidgetSurface surface) => _binding?.Surface(surface);
    public IReadOnlyList<WidgetAction> ContextActions => _binding?.CardActions ?? [];
    public bool HandleKey(WidgetKey key) => _list.HandleKey(key);
    public bool HandleText(char c) => _list.HandleText(c);
    public void Detach() { _binding?.Dispose(); _binding = null; }
}
```

`KitBinding` subscribes to the source in `Attach`, unsubscribes in `Detach`, re-renders on
`Strings.Changed` and on its source's `Updated` for its own keys only, asks the shared ticker for
a one-minute tick only while the list shows ages, publishes the header title through
`IWidgetSurface.SetHeaderTitle` (the user's own label still wins, [S-403]), and builds the card
actions (Refresh with its age, plus any spec card actions such as Mail's folder, [S-1758]).

### 5.5 `ServiceSync` and `ServiceClient` (the service source)

Both are generic: they read the descriptor and call the service's rules, and neither has a
subclass or a branch per service.

`ServiceSync` is `AnswerSync<T>` grown the parts `DevFeedHub` had, and it replaces both:

- **One per app** (`AppServices.Services`), the only code that asks a kit service for anything
  ([S-2600]). Entries are keyed by `provider + account id + query key`; a widget reading several
  accounts gets one entry per account and the Core merge (`KitMerge`).
- **Interests, not subscriptions, drive it** ([S-2608]): `SyncHost.InterestsChanged` reconciles the
  set from `SyncInterests.Kit`; a widget's `Show` only asks for an entry never fetched.
- **Cadence:** the descriptor's `UpdateSource` from `AppPreferences.Refresh` ([S-3780]), never below
  its `IdleFloor`; stretched by the poll-interval header when the descriptor names one and by the
  anonymous floor, through `ServicePolling.NextPoll(fetchedAt, cadence, pollInterval,
  blockedUntil)`; one shared ticker subscription at a minute; `SyncSchedule` backs off with the
  shared `Backoff`, wakes on `SyncHost.NetworkAvailable`, goes now on Refresh unless the entry's
  bucket is blocked.
- **Single-flight:** one fetch per entry at a time (`SyncSchedule.Busy`); a Refresh while one is out
  joins it. A query's earlier steps are entries of their own, shared by every query that needs
  them. Token refresh is already single-flight in `AccountService`.
- **Rate limits:** every reply goes through `RateLimitRules` with the descriptor's scheme; a block
  is per account and bucket; blocked entries do not ask until `RetryAt`, which the outcome carries
  so the header says when it will ask again ([S-518]).
- **Incremental entries** (`SyncToken`): the token is stored with the entry's items in its shelf and
  sent with the next request; a delta is merged by the rules' map (`KitChunk` with removals); a
  rejected token or a day without a full read starts over with a full read.
- **Stored copies:** the merged items per entry are kept in a sealed shelf of a new kind `service`,
  one per account ([S-2602], [S-2604]); validated on load by the service's rules (ids once, bounds,
  links re-checked against `WebHosts`); purged on sign-out ([S-2607]); swept with the rest ([S-2665]).
  The old `DevFeedHub` wrote plain JSON under `%LOCALAPPDATA%\…\Cache\GitHub`; the kit never does.
- **Logging:** one line per change of state per entry (as `AnswerSync.Note`), never per attempt; no
  token, no private repository, project or task name above Debug ([S-983]). A 401 means renew or
  sign in again, never remove the account.

`ServiceClient` is **one class**, not one per service. For a request it: builds the address and
body through the query's `Build`; attaches the descriptor's pinned headers and, through
`AccountService`, the credential in the auth member's `TokenScheme` (or none for `Anonymous`);
adds conditional headers when the query's freshness allows and a body is held; sends through
`AccountService.SendAsync` (no retries of its own); passes the reply through
`ServiceFailures.Classify`; on success parses with the query's `JsonTypeInfo` (unwrapping the
GraphQL envelope for `GraphQL`), maps through `Map`, and asks `ServicePager.Next` for the next page.
The API root is the descriptor's, or the account's own when `ApiRootRule.PerAccount` (an Enterprise
server, Q6), validated as https on a public host. Anonymous reads (only where the descriptor says
`PublicReads`) use the same client without credentials; a 401 there reads as `Forbidden` (there is
no sign-in to mend).

### 5.6 Standard settings rows (`KitRows`)

In `src/Themia.App/Shell/Config/KitRows.cs`, built only from `FluentLayout`, each with its
`SettingRow` for search:

- `KitRows.Accounts(rows, feature, config, save, openAccounts)`: "Every account" plus one toggle
  per signed-in account offering the feature, or one row with "Add an account" when there is none.
  Replaces `MailAccountRows`, `OfficeAccountRows` and Tasks' account rows when those migrate.
- `KitRows.Query(...)`: the closed set of the service's lists as a choice row (`MultiChoice`), and
  for free input (a repository, a label) `FluentLayout.ListEditor` with the Core validator's verdict
  under the box, committed on Enter or leaving the box, never repaired ([S-1302]'s pattern).
- `KitRows.Count(...)`: how many items, a slider within the service's bound.
- `KitRows.Shows(...)`: one toggle per optional field in `KitShow`, offered through
  `IShownSettings<KitShow>` only where the current style draws it ([S-6070]).

No refresh row ([S-3780]): the service's cadence is a line in Settings › Widgets › Updates, added
by its `UpdateSource`. No title, style, header, background, scale or alignment row: those are the
properties panel's ([S-6536]).

### 5.7 Preview states and fixtures

Kit views carry no preview code. `--widget-png <Kind> --state <name>` swaps the widget's source
for a `FixtureSource` (debug builds only, `src/Themia.App/Shell/Gallery/Fixtures/`, compiled under
`#if DEBUG` like the rest of `Gallery/`) that replays recorded answers through the real service
rules. Every kit widget gets the same state names for free: `items`, `many` (more than fit),
`loading`, `empty`, `nomatch`, `stale` (items with an offline problem), `signin`, `gone`,
`forbidden`, `ratelimited`, `problem`, `hovered`, `focused`, `accounts` (two accounts merged).
Recorded answers live once, in `tests/Themia.Core.Tests/Fixtures/<Service>/*.json`, copied into
the debug build by an item in the App's csproj under `Condition="'$(Configuration)' == 'Debug'"`,
so Release ships none. They are scrubbed: no tokens, no real e-mail addresses, no private
repository, project or task names (a check scans them, section 8). Every service records the same
set: a first page, a paged answer (Link header, `pageInfo`, or `next_cursor`), an empty answer, a
`304` where its freshness allows one, a throttle as that service signals it, a 401, a 403 and an
error body in its own error format (a GraphQL `errors` array, a Todoist `error_tag`).

## 6. Building a service widget (the recipe)

Example: "Pull requests waiting on my review" for GitHub. The steps are the same for any service;
section 12 walks them for Linear and Todoist.

1. **Read** `SPEC.md` sections *Accounts and sign-in*, *Offline sync*, *Context menus*, *Responsive
   layout* and the kit's own section (S-7000 block). Read the service's own developer docs (primary
   sources only) for: sign-in (device flow? PKCE? is a client secret required for the token
   exchange or the refresh? which loopback redirects are accepted, and is the port free?), read-only
   scopes, the transport, paging, conditional requests, how a throttle is signalled (status, headers,
   body code, reset unit), the error format, and the web address of each item. Write the facts with
   their source URLs into the service's SPEC section.
2. **Service** (once per service): fill in its descriptor by picking a member of each strategy set
   of section 4.7 (sign-in members in the order the Accounts page should offer them; device flow or
   PKCE without a secret before a build-time secret, a pasted token always last). If a fact does
   not fit any member, stop: the kit grows the member first (section 4.7's rule). Add the
   `AccountProvider`/`AccountFeatures`/`UpdateSource` members. Write `<Service>Wire.cs`,
   `<Service>Rules.cs`, `<Service>Queries.cs` (with the identity query) and record the fixtures of
   section 5.7. No App code: sign-in, client, cadence and storage are generic.
3. **Scaffold** the widget: `scripts/new-widget.ps1 -Kind GitHubReviews -Service GitHub -Shape List`
   (section 7). It writes every registration point and a failing test file.
4. **Spec the widget** in Core: the `KitSpec` (row looks, optional fields, overflow, columns), the
   item mapping (which wire fields become title, subtitle, lead, badges, when), the actions per item
   (Open; the "<verb> on <Service>" web actions; copy), the empty and no-match sentences, the card actions.
5. **Test first** where it is logic: the mapping on fixtures, the merge across accounts, the query
   key, the ladder plan at all seven tiers (`WidgetTiers`), the screen decisions. Names mirror tags:
   `S7104_a_draft_pull_request_carries_the_draft_badge`.
6. **View and page**: the generated view is complete for a kit list; the page is `KitRows` calls.
   Write the strings into the widget's locale area (`Locales/<kind>/en.json`, new keys in
   `$englishOnly` until translated, Q8).
7. **SPEC**: the widget's section with tagged lines for every behaviour (fresh tags in the service
   block), owning files and tests, and what needs a manual pass.
8. **Check**: `dotnet build` (Debug, warnings as errors), the scoped tests, the conformance tests
   (section 8), the render checklist (section 9), `publish.ps1` once (AOT, source-generated JSON),
   and say what still needs a live pass (a real account, each sign-in member of the descriptor,
   rate limits on the wire).

## 7. The scaffolder

`scripts/new-widget.ps1 -Kind <Name> [-Service <Service>] [-Shape List|Stat|Custom] [-Glyph <name>]
[-Folder <AddTree shelf>]`. PowerShell, idempotent (refuses to run when the kind exists), edits by
anchors rather than line numbers, and prints every file it touched. It touches every registration
point a widget has, which is the corrected version of the list in `CLAUDE.md`:

| # | Point | File | What it writes |
|---|---|---|---|
| 1 | Kind | `Core/Model/WidgetKind.cs` | the next free number, with a summary |
| 2 | Config | `Core/Model/Configs/<Name>Config.cs` | `sealed class … : WidgetConfig, IKitConfig` (and `IStyled` when asked), `Clone`, `Repair` |
| 3 | JSON | `Core/Model/WidgetConfig.cs` (`JsonDerivedType`, `CreateDefault` arm), `Core/Storage/AppStateJsonContext.cs` | discriminator in camelCase |
| 4 | Catalogue | `App/Widgets/WidgetCatalog.cs` | `All` entry (label, description, glyph, default size) and `CreateView` arm |
| 5 | View | `App/Widgets/<Name>View.cs` | the kit view of section 5.4 |
| 6 | Settings page | `App/Shell/Config/WidgetSettings.<Name>.cs` and the arm in `ConfigPages.Widget.cs` | `KitRows` calls |
| 7 | Glyph | `App/Shell/Glyphs.cs` | only when the glyph is new |
| 8 | Add menu | `Core/Widgets/AddTree.cs` | the entry in the chosen shelf |
| 9 | Search | `Core/Widgets/WidgetKeywords.cs` | keywords and `Providers` |
| 10 | Strings | `Core/Localization/Locales/<kind>/en.json` | label, description, sentences, `$englishOnly` |
| 11 | Setup, sync | nothing: `IKitConfig` covers `WidgetSetup` and `SyncInterests` | |
| 12 | Items, alignment | nothing for a list (`ItemKinds` default has items; a list never aligns); a `Stat` adds the kind to `ItemKinds.Without` and `ContentAlignment` | |
| 13 | SPEC | `SPEC.md` | a section skeleton with the next free tags and "Needs a manual pass" |
| 14 | Tests | `tests/Themia.Core.Tests/<Name>Tests.cs` | one failing test per skeleton tag |
| 15 | Fixtures | `tests/Themia.Core.Tests/Fixtures/<Service>/` | the folder and a README line in the SPEC section |

A `-Service` that does not exist yet also scaffolds the service folder of section 4.7 (a descriptor
with every strategy field marked `TODO` so it does not compile until each is chosen), the
`AccountProvider`/`AccountFeatures` members, the `UpdateSource` member, the descriptor's arm in
`ServiceDescriptors.For` and the fixture folder, and stops there with a list of what the agent must
write by hand (the descriptor's choices, wire types, rules, queries). It never writes App code for a
service, because there is none.

## 8. Conformance checks

Source-scanning tests in `tests/Themia.Core.Tests/KitConformanceTests.cs` and
`RegistrationTests.cs`, over one shared `RepoFiles` helper that replaces the seven copies of
`FindRoot()` (a refactor commit of its own). Comments are not code (lines starting `//` are
skipped, as `ItemKindsTests` does). Each check starts with an explicit allow list of today's
offenders, named with the reason, and the list only shrinks.

| Check | Rule | Offenders today |
|---|---|---|
| Menus | no `new MenuFlyout`, `MenuFlyoutItem` or `CommandBarFlyout` under `App/Widgets/` except `ItemMenu.cs` ([S-1740]) | none in Widgets; Shell drop-downs (`EditBar`, `ViewBar`, `PropertiesStrip`, `AccountsPage`) are not context menus and are out of scope |
| One launcher | no `Process.Start`/`UseShellExecute` outside `Services/ShellLauncher.cs` | `RssView.cs`; allowed with reasons: `DataPurge`, `Files/GitStatus`, `Files/ShellFileOps`, `ConfigPages.About`, `ConfigPages.Uninstall` |
| Colours | under `App/Widgets/`, a `Colors.X` or `Color.FromArgb` only as the fallback argument of `FluentLayout.Themed(...)`; none at all in `Widgets/Kit/` | to be listed when the test is written (Contacts' initials palette, chart washes) |
| Type | no `FontSize =` literal and no `new FontFamily(` in views; sizes come from `FluentType`/`Fluent.Apply` | `HabitTrackerView.Rows.cs` |
| Text | no user-visible string literal (`Text = "`, `Content = "`, `PlaceholderText = "`, a literal tooltip) in `App/Widgets/` and `App/Shell/Config/` | example URLs in placeholders (allowed by pattern: a literal that parses as an https URL) |
| Kit views stay thin | a view whose config implements `IKitConfig` may not mention `ScrollViewer`, `RowFocus`, `ItemMenu`, `ProgressRing`, `Fluent.EmptyState`, `ItemCursor`, `ListKeys` or `Process` | none (no kit widget yet) |
| Kit actions | no `ItemMenuEntry` constructed in a kit view; every `KitActionKind` and `KitIcon` member has its mapping in `KitMenu`/`KitIcons` | |
| Registration | every `WidgetKind` member has an `All` entry and a `CreateView` arm of its own (not the default), an `AddTree` entry, `WidgetKeywords`, and either a page arm in `ConfigPages.Widget.cs` or no rows by [S-1750] | to be measured |
| SPEC per kind | every kind is named in a SPEC section's owning files; every non-retired tag of a kit widget's section has a test named `S<tag>_…` or is listed under "Needs a manual pass" | kit widgets only at first; other kinds when they migrate |
| Strings | already: every key in all 30 languages or `$englishOnly`, placeholders match ([S-1105]); added: a kit widget's area holds every key its spec uses | |
| Fixtures | every file under `Fixtures/` parses, and none contains a token shape (`ghp_`, `gho_`, `ghu_`, `ghr_`, `github_pat_`, `lin_api_`, `lin_oauth_`, a 40-hex run as Todoist tokens are), `Bearer `, `Authorization`, an `@` address outside `example.com`/`users.noreply.github.com` | |
| Generic kit | no file under `Core/Services/*.cs` (the shared layer), `Core/Widgets/Kit/`, `App/Services/Kit/`, `App/Widgets/Kit/` or `ServiceSignIn` names a kit service or any `AccountProvider` member, except `ServiceDescriptors.For`; no service-specific header name (anything but the standard `ETag`, `Last-Modified`, `Link`, `Retry-After`) and no error code literal outside a descriptor | none (nothing built yet); `RateLimitRules`' GitHub header constants move into GitHub's descriptor when the check lands |
| Descriptors | every kit `AccountProvider` has a descriptor; every descriptor's `WebHosts` are https hosts, its `SignIn` list is non-empty, a `BuildTime` secret names a key `ClientSecretRules` accepts, and every query of the service names a bucket the scheme can block | |
| Polling | every `ServiceClient` request goes through the conditional helper (when the query's freshness allows), `RateLimitRules` and `ServiceFailures.Classify`; a test per service drives its recorded throttle, 401, 403, a `304` where it has one, and its error body through the Core rules | |

Behaviour the checks cannot see stays in the render checklist and the manual pass.

## 9. Render checklist and review rubric

For every kit widget, before calling it done (outputs outside `artifacts/`, e.g.
`TestResults/render/<kind>/`):

- `--widget-png <Kind> <out.png>` (all seven tiers on one page) × `--theme light` and `dark` × `--lang en`,
  `de` (longest words), `ar` (right to left, own digits), `ja` (no spaces to wrap at).
- The same at `--state` `items`, `many`, `loading`, `empty`, `stale`, `signin`, `ratelimited`, `hovered`, `focused`.
- Each style (`--style`) when the kind has styles; `--header left` and `--header hidden` once.
- `--settings-png Widget:<Kind>` in both themes and in `de`; with `--search` for one row.
- `--menu-png` showing the item menu (icon row present only with two or more row commands, [S-1741]).
- The showcase views (`--add-showcase-views`) on the live desktop: a manual pass, said so.

Look at every PNG against this rubric; a "no" is a defect:

1. Rows reach both card edges, the text keeps its inset, no separators, no box in a box ([S-190]).
2. Every tier shows whole rows; nothing clipped mid-line; the smallest tier still says what the
   widget is (one row, or the state sentence).
3. A larger tier adds rows, columns or the item's own detail, never a control, pane or footer ([S-666]).
4. Type is on the Fluent ramp, spacing on the 4 px grid, icons 16 px, one busy ring size ([S-210], [S-212]).
5. Light and dark both legible; no colour that does not follow the theme; caution and critical
   only for real problems.
6. German fits (ellipsis on titles is fine, on a state sentence it is not); Arabic mirrors and uses
   its digits; ages and times read as the language writes them.
7. Each state looks like every other kit widget's same state: same glyph place, same sentence
   style, same Accounts link.
8. The header shows the title, the problem glyph only with a problem, the ring only while busy.
9. Hovered and focused rows are visibly different from each other and from rest.
10. The settings page has only rows that change something in the current style, grouped, with
    short descriptions; no row the properties panel already has.

## 10. Migration plan

Migrated widgets lose their old code in the same commit (one path). Each migration is a refactor
with **zero visible change**: before and after renders of the checklist in section 9 are taken and
compared pixel for pixel (a small `--widget-png` diff script under `scripts/`), and the widget's
existing tests stay green without edits. Anything that would change what the user sees is split out
as a behaviour commit with its SPEC edit and test first.

Order, smallest risk first:

1. **Groundwork (refactors).** `RepoFiles` test helper; `KitConformanceTests` with today's allow
   lists. The key maps are not merged here: they differ at one edge (section 4.3), so the merge
   rides with the kit's key map and RSS's migration, as its own behaviour commit.
2. **Core kit.** `KitItem`, `KitAction`, `KitMenu.Lines`, `KitKeys`, `KitLadder`, `KitScreens`,
   `KitFreshness`, `IKitConfig` with the `WidgetSetup`/`SyncInterests` arms; tested; no consumer yet
   beyond tests (new code, not a second path).
3. **App kit with its first consumer: RSS.** `KitList`, `KitHeader`, `KitStatePanel`, `KitMenu`,
   `KitBinding` land together with RSS on them; `FeedLayout` becomes RSS's `KitSpec`; `RssView.Rows`
   and the view's own `RefreshSchedule` and `Process.Start` go. RSS is web-only, read-only, already
   double-click/Enter, and its rows are the richest simple case (columns, site icons, wrapping).
   The header age stays as it is in this step (it is RSS's look until step 7).
4. **Mail.** Needs the `Table` look with its column header, inline hover actions and the compose
   header button; `MailScreens` becomes a `KitScreens` caller and then goes; Mail's own accounts
   link and `FitEmptyState` go. The service (`MailService`, shelves, pending writes) is untouched:
   only the view moves.
5. **Calendar agenda.** `Overflow = Page`, day groups, two columns; only the Agenda style moves, the
   other four styles keep their views but share `KitHeader` and `KitStatePanel` (replacing
   `OfficeStatePanel`).
6. **Tasks.** The largest: the tick as a `Toggle` lead, rename and delete as typed actions, the
   inline editor and drag reorder as `KitList` extension points, the add footer ([S-3850]), and its
   focus moving from XAML focus to `RowFocus`. Settings' account rows become `KitRows.Accounts`
   (with Mail's and the Office pages' in the same commit series).
7. **Behaviour: the one stale rule** ([S-2612] rewritten, tests first): every kit widget and
   Weather/Stocks say staleness the same way (section 4.5, Q3).
8. **Behaviour: one open gesture** if the user picks it (Q2): Recent files and Notifications follow.
9. **Behaviour: the one failure vocabulary** (tests first): `FetchStatus` gains `SignInRequired`,
   `Forbidden`, `NotFound`; `AccountException.ToFetchStatus` stops folding them; their sentences.
   Mail, Tasks and the Office widgets say "sign in again" or "not allowed" where they said a generic
   problem: a visible change, with its SPEC lines.
10. **Service core, no service yet.** The strategy sets and the descriptor record, `ServicePager`
    (with `LinkPaging`), `ServicePolling`, `RateLimitScheme` with `RateLimitRules` reading it (its
    GitHub constants become the first scheme in a test; the existing rate-limit tests move to that
    scheme unchanged in what they assert), `ErrorScheme` with `ServiceFailures.Classify`, the
    GraphQL envelope, `KitMerge`. Tested on synthetic descriptors that mimic each of the three
    pilots' recorded replies, so a GitHub-shaped assumption fails a Linear-shaped test here, before
    any service exists.
11. **Sign-in by strategy (refactor for Microsoft and Google).** `ServiceSignIn` and a generic
    Accounts page add flow; Microsoft and Google get descriptors with `OAuthPkceLoopback`
    (`Localhost`/`Any`, no secret; `Loopback4`/`Any`, `BuildTime`); the ternaries go; the loopback
    listener takes a port list; `THEMIA_CLIENT_SECRETS` replaces `THEMIA_GOOGLE_CLIENT_SECRET`
    (`CLAUDE.md`, `docs/STORE.md`); `OAuthDeviceCode` (`DeviceCodeSession` restored from `e09beef^`
    with its tests), `PastedToken` and `AccountAuth.Token` land with tests and no provider yet. Zero
    visible change for the two existing providers; a live sign-in pass for both.
12. **`ServiceSync` and `ServiceClient`** with the `service` shelf kind (validation, purge, sweep).
13. **GitHub**, the first pilot (section 12), behind the user's answers to Q4..Q7: descriptor, wire,
    rules, queries, fixtures, then one widget per commit.
14. **Linear**, the second pilot: descriptor, wire, rules, queries, fixtures, widgets. Any line it
    needs outside its own folder, its provider members and its strings is a kit defect: fixed in the
    kit first, in its own commit.
15. **Todoist**, the third pilot, behind Q11..Q12; the same rule.

Contacts (cards and search), Folder (Explorer semantics), the calendar grids and the meters stay
off the kit; they already have their own frameworks or needs no shared list serves.

## 11. Design decisions and open questions

### Decisions

- **Shapes.** One shape is built now: **List** (`KitList`), with an optional summary line, groups,
  columns, scroll or page overflow, and four row looks (line, two-line, rich, table). It covers
  Mail, Tasks, the agenda, RSS, notifications, recent files and every list of section 12. **Stat** (one
  figure with a caption and a band, for a repository's stars or a service quota) is designed (it is
  `HeroFit` plus `KitHeader` and `KitStatePanel`) and built when the second widget needs it, not
  before. **Rejected:** a separate Grid shape (a grid of the same items is the List with columns;
  the photo, shortcut and folder grids have gestures no shared part should guess), a Tabbed shape
  (two lists are two widgets), a master-detail shape (a detail pane is the service's own job,
  [S-666]), and putting the calendar grids or meters on the kit (they have their own rules already).
- **Composition, not inheritance.** Views own a `KitList` and a `KitBinding`; no generic XAML base
  class (CsWinRT and AOT), no virtual hooks to override and get wrong.
- **Read-only services, web actions for writes.** A kit service never sends a write; the item
  offers "<verb> on <Service>" opening the right web page through `ShellLauncher`. Tasks and Mail
  keep their own writes (they are the user's own data), through their existing pending-write queues.
- **The offline store, not a cache folder.** Service items live in sealed shelves per account
  ([S-2602]); conditional-request bodies live in memory only.
- **Cadence is global per service** ([S-3780]): no per-widget refresh row; conditional requests
  make a short cadence cheap where the service answers 304 for free.
- **Tags.** The kit takes `[S-7000]..[S-7099]`; GitHub `[S-7100]..[S-7199]`, Linear
  `[S-7200]..[S-7299]`, Todoist `[S-7300]..[S-7399]`; each later service the next hundred. The
  retired GitHub tags (including the unlisted [S-615]..[S-629] and
  [S-745]..[S-749], to be added to [S-1902]'s retirement line) are not reused.
- **Services are data plus rules.** The service layer is closed strategy sets chosen by a
  descriptor (section 4.7); a service writes no App code and no branch in shared code. Proven on
  paper against GitHub, Linear and Todoist (section 12) before anything is built.
- **No "updated since" deltas.** Incremental reads only through a service's own sync token; a
  high-water filter misses items that leave a filtered set.
- **Web links only.** Item links and web actions open https pages on the descriptor's `WebHosts`;
  app URL schemes are not offered.
- **Fixtures out of views.** Preview states come from recorded answers replayed through the real
  rules; `*.Preview.cs` partials disappear from widgets as they migrate.

### Open questions for the user

- **Q1. Row click.** A click focuses and a double-click (or Enter) opens the item's page, as Mail
  and RSS do today. Keep that for every kit widget, or open on a single click as Recent files and
  Notifications do? (The kit needs one answer; R7-OPEN's retirement says the card itself opens
  nothing, which is not changed either way.)
- **Q2. Make the others follow.** Whatever Q1 answers, should Recent files and Notifications move to
  it (a visible change), or keep their single click as "launcher" widgets?
- **Q3. The one stale rule.** Proposed: a problem glyph with "Work: No network · updated 10:42"
  lines in its tooltip, and the age only on the Refresh menu line. This drops the caution-coloured
  age text from RSS, Weather and Stocks headers. Agree, or keep the age text (then for every kit
  widget, on tiers that have room)?
- **Q4. Which GitHub widgets first?** Candidates: review requests, assigned issues and pull
  requests, notifications, a repository's open pull requests, a repository's issues, releases,
  workflow runs. The old set was repository, assigned issues, notifications, reviews.
- **Q5. GitHub sign-in and scopes** (now from GitHub's docs, section 12.1). Facts: device flow
  works for OAuth apps and GitHub Apps and needs no client secret, for the token or (GitHub Apps)
  its refresh; the browser flow needs the client secret at the token exchange even with PKCE, so it
  is not offered; OAuth-app scopes have no read-only grant for private repositories (`repo` is full
  read/write); the notifications API accepts only classic-style tokens (OAuth app tokens, classic
  PATs), not GitHub App or fine-grained tokens. Options: (a) OAuth app, device flow, `repo read:org
  notifications`: every widget, broad scope (Themia still never writes); (b) GitHub App, device
  flow, read-only permissions, expiring tokens refreshed without a secret, installed per
  organisation or repository: no notifications widget; (c) both, the App for repositories and
  search and an optional OAuth grant only for notifications; (d) a pasted fine-grained token, as a
  fallback beside (a)..(c). Recommended: (b) with (d), and notifications later through (c) only if
  wanted.
- **Q6. GitHub Enterprise.** Support an Enterprise server address per account (the old widgets did,
  through `ApiBaseUrl`; the descriptor has `ApiRootRule.PerAccount` for it), or github.com only for
  now?
- **Q7. Without an account.** The old repository widget read public repositories anonymously (60
  requests an hour per IP, 15-minute floor). Keep anonymous public reads for repository widgets
  (`AnonymousRule.PublicReads`), or always require an account?
- **Q8. Languages.** Ship a new service widget with all 30 languages translated in the same change,
  or English first (keys in `$englishOnly`) and translations in a follow-up?
- **Q9. Which services after GitHub?** *Resolved:* GitHub, Linear and Todoist, in that order. The
  paper test (section 12) shaped the strategy sets: GraphQL transport and body-cursor paging are in
  the kit from the start, not added later.
- **Q10. Migration appetite.** The plan moves Mail, Tasks and the agenda onto the kit as zero-change
  refactors before GitHub. Is that order right, or should GitHub come right after RSS (the kit
  proven on one old and one new widget) with Mail and Tasks later?
- **Q11. Todoist sign-in.** Todoist's console-registered apps are confidential (client secret at
  the token exchange); public clients (PKCE, no secret) exist only through dynamic registration or a
  client ID metadata document, whose redirect URIs must be https ("localhost allowed for testing").
  Options: (a) a console app under Lometics with a build-time secret (as Google), if its redirect
  settings accept a loopback address; (b) a client ID metadata document hosted on a Lometics https
  address, public client with PKCE, if Todoist accepts a loopback redirect for it in production; (c)
  the personal API token only (full access: Todoist has no read-only personal token). Recommended:
  try (b) at registration, else (a); (c) always offered last. Personal tokens are full read/write,
  OAuth can ask `data:read`.
- **Q12. Todoist REST or Sync.** The REST endpoints (`/api/v1/tasks/filter?query=today | overdue`)
  evaluate Todoist's own filter language server-side and page with `next_cursor`; the Sync endpoint
  gives incremental deltas (1000 partial and 100 full syncs per user per 15 minutes) but then
  "today" and "upcoming" must be computed locally from due dates, recurrence and the user's time
  zone. Proposed: REST for the first widgets (no filter engine in Themia); Sync only if a widget
  needs data REST cannot filter.
- **Q13. Linear's loopback redirect.** Linear's docs do not say whether a registered
  `http://127.0.0.1/...` redirect matches any port. If not, the descriptor uses
  `LoopbackPort.Fixed` with two or three registered ports. To be checked when the app is
  registered.

## 12. Paper test: GitHub, Linear, Todoist

Each service below is written as if it were being added today, using only what section 4.7 allows
a service to add. Facts come from the services' own developer docs (read 2026-10-17, sources in
section 13); a fact marked *unverified* was not stated in the docs and is checked against a
recorded reply or at registration. The "kit changes this service forced" lists are the changes the
first draft of the kit needed; each is folded into sections 4 and 5 above, so every list ends with
**open: none**.

### 12.1 GitHub (REST v3)

**Facts.** REST JSON at `https://api.github.com/`, version header `X-GitHub-Api-Version` (supported:
`2026-03-10` and `2022-11-28`, the latter until 2028-03-10) [G1]. Paging by `Link` with `rel="next"`,
`per_page` usually at most 100 (notifications 50) [G2][G5]. Conditional requests with `ETag`/
`If-None-Match` and `Last-Modified`/`If-Modified-Since`; an authorised 304 does not count against
the primary limit; respect `x-poll-interval` [G3][G5]. Limits: 5,000 requests an hour signed in,
60 anonymous per IP; headers `x-ratelimit-limit`, `-remaining`, `-used`, `-reset` (epoch seconds),
`-resource`; a primary or secondary limit answers 403 or 429; secondary: `retry-after`, else the
reset when remaining is 0, else at least a minute [G4]. Search has its own budget: 30 a minute
signed in, 10 anonymous, at most 1,000 results; a GitHub App user token needs `is:issue` or
`is:pull-request` in an issues search (422 otherwise) [G6]. Sign-in: device flow for OAuth apps and
GitHub Apps (enabled in the app's settings; `POST https://github.com/login/device/code`, poll
`https://github.com/login/oauth/access_token` with `grant_type=urn:ietf:params:oauth:grant-type:device_code`,
interval 5 s, +5 s on `slow_down`, codes live 15 minutes), **no client secret** [G7]; the browser
flow requires the client secret at the token exchange (PKCE S256 is supported in addition, not
instead) [G7]; a loopback redirect may use any port of the registered `http://127.0.0.1/path` [G7].
GitHub App user tokens expire after 8 hours with a 6-month refresh token; refreshing needs the
client secret "unless the user access token was generated using the device flow" [G8][G9]. OAuth-app
scopes: none read-only for private repositories (`repo` is full access) [G10]. Notifications accept
only classic-style tokens and the `notifications` or `repo` scope; 304s via `Last-Modified` are free
[G5].

**Chosen strategies.**

| Field | GitHub |
|---|---|
| Provider, feature, cadence | `AccountProvider.GitHub`, `AccountFeatures.GitHub`, `UpdateSource.GitHub` (1, 2, 5, 15 min; default 5) |
| Transport | `RestJson` |
| ApiRoot, rule | `https://api.github.com/`, `PerAccount` (Enterprise `https://<host>/api/v3/`, Q6) |
| SignIn | 1. `OAuthDeviceCode(device/code, oauth/access_token, client id, scopes by Q5, Space, Refresh)`: an OAuth app's token does not expire (stored as `Token`); a GitHub App's rotates and refreshes without a secret; 2. `PastedToken(https://github.com/settings/personal-access-tokens/new, fine-grained shape, Bearer)`; 3. `Anonymous` if Q7 keeps it |
| Token header | `Bearer` |
| Pinned | `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2026-03-10` |
| Limits | Budgets `[(x-ratelimit-limit, x-ratelimit-remaining, x-ratelimit-reset)]`, `EpochSeconds`, bucket header `x-ratelimit-resource`, throttle statuses {403, 429}, Retry-After honoured on all |
| Errors | `JsonCode` none (`message` only); Messages `["secondary rate limit" -> RateLimited]` |
| Freshness | `ETag`; notifications `LastModified | PollIntervalHeader`; poll header `X-Poll-Interval` |
| Anonymous | `PublicReads(15 min)` (Q7) or `None` |
| Bounds | MaxPages 5, ItemBound 200 |
| WebHosts | `github.com` (plus the Enterprise host of a `PerAccount` root) |
| Identity | `GET /user` -> `id`, `login` |

**Widgets.**

1. *Review requests* (`GitHubList.ReviewRequests`). `GET /search/issues?q=is:pr is:open
   review-requested:@me archived:false&sort=updated&per_page=<n>`; bucket `search`; `LinkHeader`;
   `ETag`. Map: Id `pr:<id>`; Title `title`; Subtitle `<owner>/<repo> #<number>` (from
   `repository_url`); Lead `Icon(PullRequest)`; Badges `Draft` when `draft`; When `Age(updated_at)`;
   Link `html_url`. Actions: Open; `OpenTo("Review on GitHub", <html_url>/files)`; CopyLink;
   `CopyText(#<number>)`. Empty: "No reviews waiting".
2. *CI status* of a repository (`GitHubList.WorkflowRuns`, input `owner/name` validated by the rules).
   Step 1 `GET /repos/{o}/{r}` -> `default_branch` (`ETag`). Query `GET
   /repos/{o}/{r}/actions/runs?branch=<default_branch>&per_page=<n>` (`ETag`, `LinkHeader`). Map:
   Id `run:<id>`; Title workflow `name`; Subtitle first line of `head_commit.message`; Lead
   `StatusDot` with tone from `status`/`conclusion` (success -> Success, failure/timed_out -> Critical,
   in progress/queued -> Accent, cancelled/skipped -> Neutral); When `Age(updated_at)`; Link `html_url`.
   Summary: `"<branch> · <latest conclusion>"` with its tone. Actions: Open; `OpenTo("Re-run on
   GitHub", html_url)`; `CopyText(head_sha)`. Empty sentence from the rules: "No workflow runs on
   <branch>".
3. *Notifications* (`GitHubList.Notifications`, only with Q5 (a) or (c)). `GET
   /notifications?per_page=50`; `LastModified | PollIntervalHeader`; `LinkHeader`. Map: Id
   `n:<id>`; Title `subject.title`; Subtitle `repository.full_name`; Badges `reason`; Emphasis
   `unread`; When `Age(updated_at)`; Link: `subject.url` (an API address) rewritten by the rules to
   its `github.com` page, else the repository page. Actions: Open; `OpenTo("Mark done on GitHub",
   https://github.com/notifications)`.

Refresh: the cadence choice, stretched by `X-Poll-Interval` for notifications; 304s keep a 2-minute
cadence cheap; search stays far under 30 a minute even with several widgets.

**Kit changes this service forced** (folded in): rate-limit blocks per account **and bucket**
(search's budget is not core's); a secondary limit recognised by its message (`ErrorScheme.Messages`);
the poll-interval header named in the descriptor instead of in shared code; dependent requests
(`ServiceStep`) for the default branch; the summary line with a tone (`KitSummary`); a per-account
API root (`ApiRootRule`); device flow as an auth member with no secret. **Open: none.**

**Registration (Lometics organisation, later, browser-assisted).** Q5 decides which app.
- OAuth app: `https://github.com/organizations/<lometics-org>/settings/applications/new`; a homepage
  URL; an authorisation callback URL is required by the form but unused by device flow (the Lometics
  site is fine); tick *Enable Device Flow*. Client id goes into the descriptor; the client secret is
  not needed and is not stored anywhere.
- GitHub App: `https://github.com/organizations/<lometics-org>/settings/apps/new`; webhook off; tick
  *Enable Device Flow*; keep *Expire user authorization tokens* on; repository permissions read-only
  (Metadata, Pull requests, Issues, Actions, Checks); no organisation write permission; installable
  by any account. Device-flow refresh needs no secret, so none is stored.
- No client secret is unavoidable for GitHub.

### 12.2 Linear (GraphQL)

**Facts.** GraphQL at `https://api.linear.app/graphql`; OAuth tokens as `Authorization: Bearer
<token>`, personal API keys as `Authorization: <API_KEY>` without `Bearer`; errors in a standard
`errors` array with `extensions`; a query can "partially succeed" with HTTP 200 [L1]. Relay paging:
`first`/`after`, `pageInfo { hasNextPage endCursor }`, default 50, `orderBy: updatedAt` [L2].
Filters with comparators and ISO 8601 relative durations (`-P2W`) [L3]. Limits: OAuth 5,000 requests
and 2,000,000 complexity points an hour per user, API keys 2,500 and 3,000,000, at most 10,000
points per query; headers `X-RateLimit-Requests-Limit/-Remaining/-Reset`,
`X-RateLimit-Complexity-Limit/-Remaining/-Reset`, `X-Complexity`; reset in **UTC epoch
milliseconds**; a rate-limited request answers **HTTP 400** with `extensions.code` `RATELIMITED`
[L4]. No conditional requests are documented [L1]. OAuth: authorise at
`https://linear.app/oauth/authorize`, token at `https://api.linear.app/oauth/token`, PKCE (`S256` or
`plain`); with PKCE the client secret is optional at the exchange and only `client_id` is needed to
refresh; scopes comma-separated, `read` always present; access tokens live 24 hours with rotating
refresh tokens (a lost refresh answer can be replayed within 30 minutes); no device flow is
documented; the redirect URI must be configured on the app and repeated at the exchange; loopback
port rules are not documented (Q13) [L5]. Personal API keys are created in Settings › Account ›
Security & Access and can be restricted to certain permissions (such as read) and teams; OAuth apps
are created by an admin in Settings › Administration › API [L6].

**Chosen strategies.**

| Field | Linear |
|---|---|
| Provider, feature, cadence | `AccountProvider.Linear`, `AccountFeatures.Linear`, `UpdateSource.Linear` (1, 2, 5, 15 min; default 2) |
| Transport | `GraphQL` |
| ApiRoot, rule | `https://api.linear.app/graphql`, `Fixed` |
| SignIn | 1. `OAuthPkceLoopback(linear.app/oauth/authorize, api.linear.app/oauth/token, client id, "read", Comma, Loopback4 "/callback" Any or Fixed (Q13), ClientSecret None, Refresh(rotates, 30 min grace))`; 2. `PastedToken(https://linear.app/settings/account/security, lin_api_ shape, Raw)` |
| Token header | `Bearer` (the pasted member overrides with `Raw`) |
| Pinned | none (the transport sets `Content-Type`) |
| Limits | Budgets `[(X-RateLimit-Requests-Limit, -Remaining, -Reset), (X-RateLimit-Complexity-Limit, -Remaining, -Reset)]`, `EpochMillis`, no bucket header (bucket `graphql`), throttle statuses {400, 429} when a budget is spent |
| Errors | `GraphQLErrors`; Codes `RATELIMITED -> RateLimited` (documented); authentication and forbidden codes mapped from recorded replies (*unverified* names) |
| Freshness | `None` (`Incremental.Probe` available, off) |
| Anonymous | `None` (Linear has unauthenticated limits, but no public data a widget would read) |
| Bounds | MaxPages 4, ItemBound 200 |
| WebHosts | `linear.app` |
| Identity | `query { viewer { id name } organization { urlKey } }` |

**Widgets.**

1. *My issues* (`LinearList.Assigned`).
   ```graphql
   query Assigned($first: Int!, $after: String) {
     viewer { assignedIssues(first: $first, after: $after, orderBy: updatedAt,
         filter: { state: { type: { nin: ["completed", "canceled"] } } }) {
       nodes { id identifier title url priority dueDate updatedAt state { name type } team { key } }
       pageInfo { hasNextPage endCursor } } }
   }
   ```
   `BodyCursor` (`after` from `endCursor` while `hasNextPage`). Map: Id `id`; Title `title`; Subtitle
   `"<identifier> · <state.name>"`; Lead `Icon` by `priority` (1 urgent -> Critical, 2 high ->
   Caution, others Neutral); When `dueDate` as `KitWhen.Day` with `KitTime.Due` (overdue ->
   Critical tone), else `Age(updatedAt)`; Group `team.key` when the user turns groups on; Link `url`.
   Actions: Open; `OpenTo("Change status in Linear", url)`; `CopyText(identifier)`; CopyLink.
2. *Cycle progress* (`LinearList.ActiveCycle`, input: a team key validated by the rules).
   ```graphql
   query Cycle($team: String!, $first: Int!, $after: String) {
     teams(filter: { key: { eq: $team } }) { nodes { activeCycle { number name progress endsAt
       issues(first: $first, after: $after, filter: { assignee: { isMe: { eq: true } } }) {
         nodes { id identifier title url priority state { name type } }
         pageInfo { hasNextPage endCursor } } } } }
   }
   ```
   (`progress`, `isMe` are schema fields to confirm when recording, *unverified* in the docs read.)
   Summary: `"Cycle <number> · ends <endsAt as LocaleFormat day>"`, `Fraction = progress`. Items as
   in widget 1. No active cycle: no summary, no items, `EmptySentence = "No active cycle"`.

Refresh: default 2 minutes = 30 requests an hour per widget, far under 5,000; a two-page answer
of 50 issues costs well under 10,000 points. `X-Complexity` is logged at Debug so a heavy query is
visible.

**Kit changes this service forced** (folded in): the `GraphQL` transport with partial data; a
throttle recognised from the body code before the status (an HTTP 400 that is not a bad request);
reset units (`EpochMillis`); several budgets in one scheme; the token header form per auth member
(`Raw` API keys); the scope separator; a fixed-port loopback in case Q13 says so; date-only due
dates (`KitWhen`); a summary with a fraction; a cursor read by the rules' map, so nested
connections page like top-level ones; a rules-chosen empty sentence. **Open: none.**

**Registration (later).** In the Lometics Linear workspace, an admin opens Settings ›
Administration › API › OAuth applications › New. Callback URL(s): `http://127.0.0.1/callback` if
Linear ignores the loopback port, else two or three fixed ports (Q13); make the application
available to other workspaces (*unverified* setting name). Scopes are requested at sign-in (`read`
only). PKCE means the client secret Linear shows is **not** shipped and not needed; no secret is
unavoidable.

### 12.3 Todoist (API v1)

**Facts.** The current API is the unified **Todoist API v1** at `https://api.todoist.com/api/v1/`,
which "unifies the Sync API v9 and the REST API v2"; the older docs remain only "for reference"
[T1]. (Third-party reports say REST v2 now answers 410 Gone [T7].) Paging: `results` plus
`next_cursor` (null at the end), `limit` default 50, maximum 200, cursors tied to the user and the
parameters; `/tasks` and `/tasks/filter` among the paginated endpoints [T2]. `GET
/api/v1/tasks/filter?query=today | overdue` evaluates Todoist's filter language server-side [T3].
Task web URLs are `https://app.todoist.com/app/task/<id>` (the old `url` field was removed) [T4].
Limits: 1,000 partial and 100 full sync requests per user per 15 minutes; no header budget is
documented for the REST-style endpoints; 429 for too many requests, with `retry_after` (seconds) in
the error body's `error_extra`; a 401 (`error_tag: "UNAUTHORIZED"`) may carry `Retry-After`, which
is **not** a wait: refresh the token instead [T5]. No conditional requests are documented [T1].
Sign-in: authorise at `https://app.todoist.com/oauth/authorize`, token at
`https://api.todoist.com/oauth/access_token`; console-registered apps get a client id and **client
secret** needed for the flow; public clients (`token_endpoint_auth_method: none`, PKCE) are
available through dynamic registration or a client ID metadata document, whose redirect URIs "must
be HTTPS (localhost allowed for testing)"; scopes comma-separated, `data:read` is read-only; new
apps get 1-hour access tokens with refresh tokens that rotate on every refresh, and reuse outside a
60-second grace revokes all the user's tokens for the app; no device flow is documented; personal
API tokens come from the integrations settings and are sent as Bearer [T6]. Sync: `POST
/api/v1/sync`, form-encoded, `sync_token` (`*` for a full read) and `resource_types` [T5].

**Chosen strategies.**

| Field | Todoist |
|---|---|
| Provider, feature, cadence | `AccountProvider.Todoist`, `AccountFeatures.Todoist`, `UpdateSource.Todoist` (1, 5, 15, 30 min; default 5) |
| Transport | `RestJson` (`FormPost` only if Q12 picks Sync) |
| ApiRoot, rule | `https://api.todoist.com/api/v1/`, `Fixed` |
| SignIn | 1. `OAuthPkceLoopback(app.todoist.com/oauth/authorize, api.todoist.com/oauth/access_token, client id (console id, or the metadata document's https URL), "data:read", Comma, loopback per Q11, ClientSecret BuildTime (console app) or None (metadata document), Refresh(rotates, 60 s grace))`; 2. `PastedToken(Todoist Settings › Integrations › Developer, 40-hex shape, Bearer)` |
| Token header | `Bearer` |
| Pinned | none |
| Limits | Budgets none (no headers documented), `DeltaSeconds`, no bucket header (buckets `rest`, and `sync-full`/`sync-partial` if Q12), throttle statuses {429}, Retry-After ignored on {401} |
| Errors | `JsonCode` with `CodeField = "error_tag"`; Codes `UNAUTHORIZED -> SignInRequired`, `INVALID_ARGUMENT_VALUE -> BadResponse` (documented), scope and not-found tags from recorded replies (*unverified*); `RetryAfterField = "error_extra.retry_after"` |
| Freshness | `None` (`Incremental.SyncToken` if Q12 picks Sync) |
| Anonymous | `None` |
| Bounds | MaxPages 3 (at `limit=200`), ItemBound 300 |
| WebHosts | `app.todoist.com` |
| Identity | `GET /api/v1/user` -> `id`, `full_name` |

**Widgets.**

Both use one step: `GET /api/v1/projects?limit=200` (`BodyCursor`), mapped to a table project id ->
name, shared by every Todoist query of the account.

1. *Today* (`TodoistList.Today`). `GET /api/v1/tasks/filter?query=today | overdue&limit=<n>`;
   `BodyCursor` (`cursor` from `next_cursor`). Map: Id `id`; Title `content` with Markdown reduced to
   plain text; Subtitle the project's name from the step; Lead `StatusDot` by `priority` (4 = p1 ->
   Critical, 3 -> Caution, 2 -> Accent, 1 -> none); When `due.date` as `KitWhen.Day`, or
   `due.datetime` as `KitWhen.At` (a floating time without offset is read in the PC's zone), with
   `KitTime.Due`, overdue -> Critical tone; Badges the first two `labels`; Link
   `https://app.todoist.com/app/task/<id>`. Sorted by due, then priority, then the API's order.
   Actions: Open; `OpenTo("Complete in Todoist", link)`; `OpenTo("Reschedule in Todoist", link)`;
   `CopyText(content)`. Empty: "Nothing due today".
2. *Upcoming* (`TodoistList.Upcoming`, a choice of 3, 7 or 14 days). `query=<n> days`, otherwise as
   Today; Group `KitGroup(Day: due day)` so the kit draws "Today", "Tomorrow", "Mon 20 Oct" headings.

Refresh: default 5 minutes; with no documented REST budget the `IdleFloor` is 1 minute, and a 429
blocks the account's `rest` bucket for `retry_after`.

**Kit changes this service forced** (folded in): `BodyCursor` paging for a REST body; Retry-After
ignored on a 401; a retry time read from the error body (`RetryAfterField`); error codes in a named
JSON field (`JsonCode`); a build-time client secret for any provider (`THEMIA_CLIENT_SECRETS`, not
Google's alone); rotating refresh tokens persisted before use, with the reuse grace as data; step
results available to `Map` (project names); day groups drawn by the kit (`KitGroup.Day`);
date-only and floating due times (`KitWhen`); comma scopes; links built by the rules and checked
against `WebHosts`; app URL schemes deliberately not offered. **Open: none.**

**Registration (later).** Todoist App Management Console
(`https://app.todoist.com/app/settings/integrations/app-management`), signed in as the Lometics
account. Per Q11: (b) host a client ID metadata document at a Lometics https address (`client_id` =
that URL, `token_endpoint_auth_method: "none"`, `redirect_uris` with the loopback address, scope
`data:read`) and test whether Todoist accepts a loopback redirect outside "testing"; or (a) create a
console app with a loopback redirect URL and ship its secret as `todoist=` in
`THEMIA_CLIENT_SECRETS` (the client secret is unavoidable on this path). Whether a console app's
redirect may be `http://127.0.0.1` and whether its port must match are *unverified*: check in the
console.

### 12.4 Result

| | GitHub | Linear | Todoist |
|---|---|---|---|
| Transport | RestJson | GraphQL | RestJson |
| Paging | LinkHeader | BodyCursor (`pageInfo`) | BodyCursor (`next_cursor`) |
| Auth | OAuthDeviceCode; PastedToken; Anonymous (Q7) | OAuthPkceLoopback (no secret); PastedToken (Raw) | OAuthPkceLoopback (BuildTime secret or metadata document); PastedToken |
| Freshness | ETag; LastModified + poll header | None | None |
| Rate limits | headers, epoch s, bucket header | two budgets, epoch ms, 400 + body code | 429 + body retry, Retry-After ignored on 401 |
| Errors | status + message rule | GraphQL codes | `error_tag` codes |
| Read-only scope | none for private repos on an OAuth app; GitHub App read-only permissions | `read` | `data:read` (OAuth only) |

Every row is a member of a section 4.7 set with parameters from the table above; no service needs
a line outside its folder, its provider members, its build secret name and its strings. The three
service SPEC sections ([S-7100], [S-7200], [S-7300] blocks) copy these facts with their sources.

## 13. Sources

Read 2026-10-17. Re-read before building each service; the descriptor's facts carry these links in
the service's SPEC section.

- [G1] GitHub, API versions: https://docs.github.com/en/rest/about-the-rest-api/api-versions
- [G2] GitHub, Using pagination in the REST API: https://docs.github.com/en/rest/using-the-rest-api/using-pagination-in-the-rest-api
- [G3] GitHub, Best practices for using the REST API: https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api
- [G4] GitHub, Rate limits for the REST API: https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api
- [G5] GitHub, Notifications: https://docs.github.com/en/rest/activity/notifications
- [G6] GitHub, Search: https://docs.github.com/en/rest/search/search
- [G7] GitHub, Authorizing OAuth apps: https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps
- [G8] GitHub, Generating a user access token for a GitHub App: https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-user-access-token-for-a-github-app
- [G9] GitHub, Refreshing user access tokens: https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/refreshing-user-access-tokens
- [G10] GitHub, Scopes for OAuth apps: https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/scopes-for-oauth-apps
- [L1] Linear, GraphQL API: https://linear.app/developers/graphql
- [L2] Linear, Pagination: https://linear.app/developers/pagination
- [L3] Linear, Filtering: https://linear.app/developers/filtering
- [L4] Linear, Rate limiting: https://linear.app/developers/rate-limiting
- [L5] Linear, OAuth 2.0 authentication: https://linear.app/developers/oauth-2-0-authentication
- [L6] Linear, API and webhooks: https://linear.app/docs/api-and-webhooks
- [T1] Todoist API v1, Migrating from v9: https://developer.todoist.com/api/v1/ (section "Migrating from v9")
- [T2] Todoist API v1, Pagination: https://developer.todoist.com/api/v1/ (section "Pagination")
- [T3] Todoist API v1, Get Tasks By Filter: https://developer.todoist.com/api/v1/ (section "Tasks")
- [T4] Todoist API v1, Task URLs: https://developer.todoist.com/api/v1/ (section "Migrating from v9 › Task URLs")
- [T5] Todoist API v1, Request limits, Sync, error responses: https://developer.todoist.com/api/v1/ (sections "Request limits", "Sync", "Refreshing access tokens")
- [T6] Todoist API v1, Authorization (OAuth, dynamic registration, client ID metadata document, scopes): https://developer.todoist.com/api/v1/ (section "Authorization")
- [T7] Secondary: Airbyte issue on REST v2 returning 410: https://github.com/airbytehq/airbyte/issues/86899

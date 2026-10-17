# The widget kit

The widget kit is the set of shared parts a new widget is built from, plus the checks that keep
every widget built from them honest. It exists so that an agent asked to "build widgets for
GitHub" (and later other online services) gets menus, rows, states, keys, styles, settings and
sizes right the first time, by composing parts that are already right, instead of copying the
nearest existing widget and its quirks.

Status: **designed, not built** (phase 1 of the service-widget skill). Nothing below exists in
`src/` yet unless it is marked *exists*. The plan of commits is kept outside the repository; this
file is the design an implementation must follow, and the guide new agents read afterwards.

Scope of a service widget: **read-only.** A widget shows what the service holds. Anything that
would change it (merge a pull request, close an issue, mark a notification done) is an item
action that opens the service's own web page at the right place ("Merge on GitHub"). Themia never
sends a write for a kit service.

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
  `AccountAuth.BearerToken`. The kit's service source is that design, generalised and moved onto
  the offline store.

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

## 3. The kit at a glance

```
Core  (rules, pure, tested)                         App  (WinUI, thin)
-----------------------------------------------     -------------------------------------------
KitItem, KitLead, KitBadge, KitTone, KitTime        KitList          rows, columns, groups, overflow
KitAction, KitActionKind  ---- KitMenu.Entries -->  KitMenu          -> ItemMenuEntry, KeyChord
KitKeys (one key map)                               KitHeader        busy ring, problem glyph, badge
KitPlan = KitLadder.Plan(area, spec, show)          KitStatePanel    loading / empty / problem + link
KitScreen = KitScreens.Decide(...)                  KitBinding       source <-> KitList, Detach-safe
KitFreshness (the one stale rule)                   ServiceSync      AnswerSync + accounts + ETag
IKitConfig  (setup, interest, shown rows)           ServiceClient    one per service, AccountService
Services/<Service>/  wire, rules, polling, paging   KitRows          standard settings rows
ServicePolling, LinkPaging, KitFixtures (tests)     Gallery/Fixtures debug-only preview sources
```

A kit widget's own code is: its config (Core), its service rules (Core, if the service is new),
a `KitSpec` (Core: which fields, which actions, which sentences), a view of about a hundred lines
that hands the spec and the source to `KitList`, and a settings page made of `KitRows`.

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
    DateTimeOffset? When = null,     // drawn per KitTime
    KitTone Tone = KitTone.Neutral,  // the row's accent: unread, failing check, overdue
    bool Emphasis = false,           // unread / needs you: BodyStrong title
    IReadOnlyList<KitBadge>? Badges = null,
    Uri? Link = null,                // the primary open target; https only (LaunchTargets)
    string? Group = null);           // group key; the spec names its heading

public readonly record struct KitLead(KitLeadKind Kind, string? Value = null, KitTone Tone = KitTone.Neutral);
public enum KitLeadKind { None, Icon, Initials, SiteIcon, StatusDot, Avatar }   // Icon value is a KitIcon name
public readonly record struct KitBadge(string Text, KitTone Tone = KitTone.Neutral);  // "Draft", "3", a label
public enum KitTone { Neutral, Accent, Success, Caution, Critical }               // -> theme brushes in App
public enum KitTime { None, Age, Clock, Date, Due }                               // via LocaleFormat only
```

Rules (each a test): an item without an id is dropped at the boundary, ids are unique within a
list, strings are cut to their bounds, a link that `LaunchTargets.Classify` would not open is
dropped (the row stays, without open actions), counts are written by one formatter (`KitText.Count`:
999, 1.2k, 12k, 3.4M, in the language's digits).

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
`OpenTo("Merge on GitHub", <pr>#partial-pull-merging)`, an issue `OpenTo("Close on GitHub",
<issue>)`, a notification `OpenTo("Mark done on GitHub", <notifications page>)`. The label says
where the click goes. The target goes through `ShellLauncher` like every other open.

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

public sealed record KitOutcome(string Owner, FetchStatus Status, KitAccountState Account, string? Detail, DateTimeOffset? HeldAt);
public enum KitAccountState { Ok, Gone, SignInRequired, Forbidden }
```

It is `MailScreens.Decide` and `SyncProblem` generalised: one decision for every kit widget, so
"no account at all" (the card's Set up, [S-1861]), "account gone", "sign in again", loading,
offline, rate limited (with the time it will ask again) and empty each have exactly one look and
one sentence. `KitAccountState` comes from `AccountError` once, at the service boundary
(`KitOutcome.From(AccountException)`), so views never see an `AccountError`.

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

### 4.7 Service rules (Core)

One folder per service: `src/Themia.Core/Services/<Service>/` (`GitHub/` first), holding:

- `<Service>Wire.cs`: the response shapes, source-generated JSON (`[JsonSerializable]` on a
  `<Service>WireContext`), nothing reflective.
- `<Service>Rules.cs`: addresses (`ApiRoot`, per-endpoint URL builders with escaped segments),
  input parsing (a repository is `owner/name` or its web address), and the mapping from wire to
  `KitItem` plus its `KitAction`s. Everything the widget shows is decided here, so it is tested on
  recorded answers.
- `<Service>Queries.cs`: the closed set of what a widget can ask (`enum GitHubList { ReviewRequests,
  Assigned, Created, Mentioned, Notifications, RepoPulls, RepoIssues, Releases }`) and the query
  record whose `Key` names everything that changes the answer and never the token.

Shared by every service (`src/Themia.Core/Services/`):

- `ServicePolling` (the old `GitHubPolling`, generalised): `Conditional(etag, lastModified)`,
  `PollInterval(header)` (capped at an hour), `NextPoll(fetchedAt, cadence, pollInterval,
  blockedUntil)`, `Cadence(refresh, anonymous)` with a per-service anonymous floor.
- `LinkPaging`: RFC 8288 `Link: <…>; rel="next"` parsing at the trust boundary (https only, same
  host as the request, at most `MaxPages`), and the bound: stop when the widget's count is reached.
- `ServiceDescriptor`: one table per service, as `AccountProviders` is one table per provider:
  name, `AccountProvider`, `AccountFeatures`, `UpdateSource`, auth kind, API root, anonymous
  floor, page size, item bound, the scopes asked, the web pages the actions open.

Accounts: each service is a new `AccountProvider` member with its `AccountFeatures` flag and
`AccountAuth` (the closed sets of [S-500]/[S-502]; widgets still ask for the feature, never the
provider). GitHub restores `AccountProvider.GitHub`, `AccountFeatures.GitHub`, `AccountAuth.BearerToken`
(or `OAuthRefresh`, see Q5), `OAuthClients.GitHub` and the device-code session (`DeviceCodeSession`,
`DevicePoll`, `DeviceStep` from `e09beef^:src/Themia.Core/Accounts/OAuthFlows.cs`) with their tests.

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

`ServiceSync` is `AnswerSync<T>` grown the parts `DevFeedHub` had, and it replaces both:

- **One per app** (`AppServices.Services`), the only code that asks a kit service for anything
  ([S-2600]). Entries are keyed by `account id + query key`; a widget reading several accounts gets
  one entry per account and a Core merge (`KitMerge`: newest first, the same item seen through two
  accounts once, cut to the count).
- **Interests, not subscriptions, drive it** ([S-2608]): `SyncHost.InterestsChanged` reconciles the
  set from `SyncInterests.Kit`; a widget's `Show` only asks for an entry never fetched.
- **Cadence:** the service's `UpdateSource` from `AppPreferences.Refresh` ([S-3780]); stretched by
  `X-Poll-Interval` and the anonymous floor through `ServicePolling.NextPoll`; one shared ticker
  subscription at a minute, as `AnswerSync` has; `SyncSchedule` backs off with the shared
  `Backoff`, wakes on `SyncHost.NetworkAvailable`, goes now on Refresh unless the provider said
  "slow down".
- **Single-flight:** one fetch per entry at a time (`SyncSchedule.Busy`); a Refresh while one is out
  joins it. Token refresh is already single-flight in `AccountService`.
- **Rate limits:** every reply goes through `RateLimitRules.Read`/`WaitUntil`; a throttle (or a
  success that spent the last request) blocks every entry of that account until the reset, at most
  `RateLimitRules.MaxWait`, and the outcome carries `RetryAt` so the header says when it will ask
  again ([S-518]).
- **Conditional requests:** `ServiceClient` keeps the last body with its `ETag`/`Last-Modified`
  per `account + address` in memory (bounded, oldest out, as `GitHubService.Cached`), sends
  `If-None-Match`/`If-Modified-Since`, and answers a `304` from it. A 304 with nothing held is a bad
  response, never an empty list.
- **Stored copies:** the merged items per entry are kept in a sealed shelf of a new kind `service`,
  one per account ([S-2602], [S-2604]); validated on load by the service's rules (ids once, bounds,
  links re-checked); purged on sign-out ([S-2607]); swept with the rest ([S-2665]). The old
  `DevFeedHub` wrote plain JSON under `%LOCALAPPDATA%\…\Cache\GitHub`; the kit never does.
- **Errors → states:** `AccountService.SendAsync` throws `AccountException`; `ServiceClient` maps it
  once to a `KitOutcome` (`FetchStatus` via `ToFetchStatus`, plus `KitAccountState`), logs one line
  per change of state (as `AnswerSync.Note`), never per attempt, and never logs a token or a
  private repository's name above Debug ([S-983]). A 401 means refresh or sign in again, never
  remove the account.
- **Anonymous reads** (only where the descriptor allows, for public data): the same client without
  credentials, a 401 read as `Forbidden` (there is no sign-in to mend), the anonymous floor applied.

`ServiceClient` is one class per service (`GitHubClient`), small: headers the service pins
(`Accept`, `X-GitHub-Api-Version`), the API root per account (Enterprise, Q6), and per-query
fetch functions that call Core rules for addresses and mapping.

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
repository names (a check scans them, section 8).

## 6. Building a service widget (the recipe)

Example: "Pull requests waiting on my review" for GitHub.

1. **Read** `SPEC.md` sections *Accounts and sign-in*, *Offline sync*, *Context menus*, *Responsive
   layout* and the kit's own section (S-7000 block). Read the service's API docs for the endpoints,
   rate limits, conditional request support and paging.
2. **Service** (once per service): add the provider to `AccountProviders` and a
   `ServiceDescriptor`; the sign-in (device flow first, PKCE with loopback where the service
   supports it, a pasted token as the last resort, Q5) in `AccountService.SignIn.cs`; the token is
   sealed by `Secrets` and attached by `AccountService` only. Add `UpdateSource.<Service>` with its
   choices. Write `<Service>Wire.cs`, `<Service>Rules.cs`, `<Service>Queries.cs` and record
   fixtures for every endpoint, including an error body, a `304`, a throttled reply and a paged one.
3. **Scaffold** the widget: `scripts/new-widget.ps1 -Kind GitHubReviews -Service GitHub -Shape List`
   (section 7). It writes every registration point and a failing test file.
4. **Spec the widget** in Core: the `KitSpec` (row looks, optional fields, overflow, columns), the
   item mapping (which wire fields become title, subtitle, lead, badges, when), the actions per item
   (Open; the "on GitHub" web actions; copy), the empty and no-match sentences, the card actions.
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
   and say what still needs a live pass (a real account, the device flow, rate limits on the wire).

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

A `-Service` that does not exist yet also scaffolds the service folder of section 4.7, the
`AccountProvider` member, the `UpdateSource` member and the fixture folder, and stops there with a
list of what the agent must write by hand (sign-in, wire types, rules).

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
| Fixtures | every file under `Fixtures/` parses, and none contains `ghp_`, `gho_`, `github_pat_`, `Bearer `, `Authorization`, an `@` address outside `example.com`/`users.noreply.github.com` | |
| Polling | every `ServiceClient` GET goes through the conditional helper and `RateLimitRules`; a test per service drives a recorded 304, 403-with-zero-remaining and 429 through the Core rules | |

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
9. **GitHub**, the first service built on the kit from scratch (section 6), behind the user's
   answers to Q4..Q7.

Contacts (cards and search), Folder (Explorer semantics), the calendar grids and the meters stay
off the kit; they already have their own frameworks or needs no shared list serves.

## 11. Design decisions and open questions

### Decisions

- **Shapes.** One shape is built now: **List** (`KitList`), with an optional summary line, groups,
  columns, scroll or page overflow, and four row looks (line, two-line, rich, table). It covers
  Mail, Tasks, the agenda, RSS, notifications, recent files and every GitHub list. **Stat** (one
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
- **Tags.** The kit takes `[S-7000]..[S-7099]`; GitHub `[S-7100]..[S-7199]`; each later service the
  next hundred. The retired GitHub tags (including the unlisted [S-615]..[S-629] and
  [S-745]..[S-749], to be added to [S-1902]'s retirement line) are not reused.
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
- **Q5. GitHub sign-in and scopes.** Read-only is not expressible for private repositories with a
  classic OAuth app (`repo` grants write). A GitHub App gives read-only permissions and device flow
  with expiring, refreshable tokens, but GitHub's notifications API does not accept GitHub App or
  fine-grained tokens (as GitHub documents it today; to be checked again before building). Options: (a) OAuth app, device flow, scopes `repo read:user notifications`
  (what the old widgets used; broad); (b) GitHub App, read-only, no notifications widget; (c) both
  (App for repositories and search, plus an optional OAuth grant only for notifications); (d) a
  pasted fine-grained token as an alternative.
- **Q6. GitHub Enterprise.** Support an Enterprise server address per account (the old widgets did,
  through `ApiBaseUrl`), or github.com only for now?
- **Q7. Without an account.** The old repository widget read public repositories anonymously (60
  requests an hour per address, 15-minute floor). Keep anonymous public reads for repository
  widgets, or always require an account?
- **Q8. Languages.** Ship a new service widget with all 30 languages translated in the same change,
  or English first (keys in `$englishOnly`) and translations in a follow-up?
- **Q9. Which services after GitHub?** The `ServiceDescriptor` assumes REST with OAuth or a token,
  JSON, conditional requests optional. GitLab, Jira, Linear (GraphQL), Azure DevOps and Todoist fit
  it with different amounts of work; knowing the next two decides whether GraphQL paging belongs in
  the kit now.
- **Q10. Migration appetite.** The plan moves Mail, Tasks and the agenda onto the kit as zero-change
  refactors before GitHub. Is that order right, or should GitHub come right after RSS (the kit
  proven on one old and one new widget) with Mail and Tasks later?

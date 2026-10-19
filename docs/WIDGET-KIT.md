# The widget kit

The widget kit is the set of shared parts a new widget is built from, plus the checks that keep
every widget built from them honest. It exists so that an agent asked to "build widgets for
<an online service>" gets menus, rows, states, keys, styles, settings and sizes right the first
time, by composing parts that are already right, instead of copying the nearest existing widget
and its quirks. The service layer knows no service: a service is a descriptor (data) that picks
from closed sets of typed strategies, plus its own wire types and rules (section 5.7). GitHub is
the first pilot user; Linear and Todoist are the second and third, and section 13 shows on paper
that all three fit without the kit knowing any of them.

**Widgets and providers are projects** (section 4). A new provider is a new project under
`src/Providers/`, a new widget a new project under `src/Widgets/`; each implements a small module
contract from `Themia.Sdk` / `Themia.Sdk.WinUI`, and the app finds every module at build time
(glob project references plus a source generator that writes a static registry). Adding one
changes nothing in the app, Core or the SDK. A spike proved the mechanism end to end under Native
AOT (section 4.11).

Status: the module architecture (M1..M4, section 11.1), the kit's core and UI parts with their
first widget, RSS (section 11.2 steps 1..3, 7 and 8), and the service layer (sections 5.7 and 6.5;
steps 9..12, "service layer as built" in 11.2) are built, and every widget is a module project
(the account widgets as group 8 of 11.1); Email, the calendar's agenda and Tasks draw through the
kit ("account widgets on the kit" in 11.2); the pilots (steps 13..15) are not. Where the build
differs from the design below, the "as built" paragraph of 11.2 says how and why, and it wins. The
plan of commits is kept outside the repository; this file is the design an implementation must
follow, and the guide new agents read afterwards.

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
  strategy sets of section 5.7, chosen by GitHub's descriptor, beside the members Linear and Todoist
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
    Section 4 removes the list: every registration point becomes a member of the module.
15. **Seven copies of `FindRoot()`** in the source-scanning tests.
16. **Tag hygiene:** [S-1902] retires the GitHub tags only partly ([S-510]..[S-512], [S-861]..[S-871]);
    the GitHub section's [S-615]..[S-629] and [S-745]..[S-749] were removed without a retirement
    line, and [S-610]..[S-614] are now Tasks' tags. New kit and service tags start at fresh blocks
    (section 12).
17. **Service code is shaped by its first provider.** `RateLimitRules` reads GitHub's header names
    (`X-RateLimit-Remaining`, reset in epoch seconds) for every reply; `SignInWithBrowserAsync`
    picks Microsoft or Google with ternaries; the build-time client secret is Google's alone
    (`THEMIA_GOOGLE_CLIENT_SECRET` in the csproj target); `AccountException.ToFetchStatus` folds
    sign-in-required, forbidden and not-found into `NotConfigured`/`BadResponse`, and its `RetryAt`
    is computed but never read. A third provider today means editing each of those. Section 5.7
    replaces them with descriptor-chosen strategies.

## 3. The kit at a glance

```
Themia.Sdk  (pure, tested; what modules may use)     Themia.Sdk.WinUI  (WinUI parts modules may use)
-----------------------------------------------      -------------------------------------------
KitItem, KitLead, KitBadge, KitTone, KitTime         KitList          rows, columns, groups, overflow
KitAction, KitActionKind  ---- KitMenu.Entries -->   KitMenu          -> ItemMenuEntry, KeyChord
KitKeys (one key map)                                KitHeader        busy ring, problem glyph, badge
KitPlan = KitLadder.Plan(area, spec, show)           KitStatePanel    loading / empty / problem + link
KitScreen = KitScreens.Decide(...)                   KitBinding       source <-> KitList, Detach-safe
KitFreshness (the one stale rule)                    KitRows          standard settings rows
IKitConfig  (setup, interest, shown rows)            IWidgetModule, IWidgetView & co, Fluent, Glyphs
ServiceDescriptor (data) picks strategies:
  Transport, Paging, Auth, Freshness,                Themia.App  (host, generic)
  RateLimitScheme, ErrorScheme, Replica              ServiceSync      one per app: entries, cadence, shelves
Module contracts, ids (WidgetId, ProviderId, ...)    ServiceClient    one generic client, AccountService
                                                     ServiceSignIn    runs the chosen auth strategy
                                                     ModuleRegistry   generated: every module compiled in

src/Providers/Themia.Provider.<Service>/   descriptor, wire, rules, queries, strings, fixtures (pure)
src/Widgets/Themia.Widget.<Name>/          config, KitSpec, view, settings rows, strings, SPEC.md, Tests/
```

A kit widget's own code is its project: its config, a `KitSpec` (which fields, which actions,
which sentences), a view of about a hundred lines that hands the spec and the source to
`KitList`, and settings rows made of `KitRows`. A service is its provider project: descriptor,
wire types, rules, queries. A service has **no App code of its own**: the client, sign-in,
cadence and storage are generic host code and read the descriptor.

## 4. Modules: widgets and providers as projects

### 4.1 The rule

A new provider is a new project; a widget is a project that implements a module contract. Adding
`src/Providers/Themia.Provider.<Name>/` or `src/Widgets/Themia.Widget.<Name>/` (the folder, its
csproj and its code, written by the scaffolder, section 8) is the whole change: no file outside
the folder is edited, not in the app, not in Core, not in the SDK, not `SPEC.md`, not a locale file,
not `Glyphs`. When a module needs something the SDK does not offer (a strategy member, a UI part, a
host service), that is an SDK change first, in its own commit with tests, exactly as section 5.7's
rule says for strategies; the module then uses it. Section 4.12 lists, honestly, what is left
outside the folder.

### 4.2 Assemblies and dependencies

```
 build time only:   Themia.Localization.Generator        Themia.Modules.Generator
                    (Locales/*.json -> <Name>Str)        ([assembly: ThemiaWidget/Provider] -> ModuleRegistry)

                  +------------------------------------------------------------------+
                  | Themia.Sdk  (net10.0, pure, AOT/trim-compatible)                  |
                  |  module contracts, ids, WidgetConfig base, IStyled, setup/liveness/|
                  |  interest contracts, Kit core (5.1-5.6), service strategies (5.7), |
                  |  localization runtime, LocaleFormat, Log, WidgetArea, Disclosure  |
                  +------------------------------------------------------------------+
                     ^             ^                    ^                     ^
                     |             |                    |                     |
     Themia.Provider.*      Themia.Sdk.WinUI      Themia.Core (net10.0)        |
     (net10.0, pure)        (WinUI library)       host rules: state, store,    |
          ^                  view contracts,      validation, shell rules,     |
          |                  Fluent, FluentLayout, account and offline stores  |
          |                  Glyphs, ItemMenu,           ^                     |
          |                  RowFocus, Kit parts,        |                     |
          |                  IWidgetHost                 |                     |
          |                     ^      ^                 |                     |
          +---- Themia.Widget.* -+      +------ Themia.App (exe) --------------+
                (WinUI library)                 shell, cards, host services,
                      ^                         ServiceSync/Client/SignIn,
                      |                         generated ModuleRegistry
                      +-- glob ProjectReference -----+
```

- **`Themia.Sdk`** references nothing of ours. It is the surface a module may use: the module
  contracts and ids (4.3, 4.5), `WidgetConfig` and the config contracts (`IStyled`,
  `IShownSettings<T>`, `IKitConfig`, `ISetupRule`, `IInterests`), the Kit core of sections
  5.1..5.6, the service strategy layer of 5.7, and the shared pure rules modules call today from
  Core (`Strings`/`Locale`/`LocaleFormat`, `Log`, `WidgetArea`, `Disclosure`, `WidgetTiers`,
  `ListKeys`, `TypeAhead`, `FetchState`, `SyncFreshness`, `LaunchTargets`, ...). Those files move
  out of Core into the SDK (refactor commits, section 11).
- **`Themia.Core`** becomes host-only: `AppState`, the atomic store and load validation, the shell
  and view rules, the account and offline stores, the sync host's rules. It references the SDK.
  **Modules never reference Core**, so a module cannot reach a host internal; a conformance test
  reads every module csproj.
- **`Themia.Sdk.WinUI`** holds the view contracts (`IWidgetView`, `IWidgetSurface`,
  `IWidgetSizeAware`, `IWidgetKeys`, `IWidgetHeaderContent`, `IWidgetContextActions`,
  `IWidgetAligned`, `IWidgetPreviewState`), `IWidgetModule`, `IWidgetHost`, `Ticker`, and the UI
  parts every widget uses (`Fluent`, `FluentLayout`, `Glyphs`, `ItemFill`, `ItemMenu`, `RowFocus`,
  `ScrollFade`, `EmptyStatePanel`, the `Kit*` parts of section 6). Today 151 widget files use
  `Themia.App.Shell`; these parts leave the app for this library (refactor commits, section 11).
- **Providers** reference the SDK only: a provider has no UI (section 5.7), so it builds and tests
  as plain `net10.0`.
- **Widgets** reference the SDK, `Themia.Sdk.WinUI` and the provider projects whose data they show
  (a GitHub widget references `Themia.Provider.GitHub` for its query enum). A widget never
  references another widget.
- **The app** references Core, `Themia.Sdk.WinUI` and every module, by glob.

**One project per widget** (Q16, decided). There are no *family* projects: code or a UI element
that two widgets share (the power widgets' action rows, the quick-setting toggle, the meters'
graph, a service's list rules) moves into Themia's core, that is the SDK (`Themia.Sdk`, or
`Themia.Sdk.WinUI` when it is UI), in its own commit, and both widget projects use it from there.
Shared code is never duplicated into widget projects and never put in a project that several
widgets reference. A service's widgets share their provider project (data and rules, no UI), which
is not a family: it declares a provider, not widgets.

### 4.3 Module contracts

```csharp
// Themia.Sdk
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ThemiaWidgetAttribute(string id, Type module) : Attribute;     // module: IWidgetModule
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ThemiaProviderAttribute(string id, Type module) : Attribute;   // module: IProviderModule

public sealed record WidgetDescriptor(
    WidgetId Id,
    Type ConfigType, Func<WidgetConfig> CreateDefault, IJsonTypeInfoResolver Json,  // 4.6
    LocalText Label, LocalText Description, string Glyph,                           // catalogue, picker
    double DefaultWidth, double DefaultHeight,
    AddPlacement Add,                          // the add menu shelf (closed SDK enum) and an order key
    IReadOnlyList<LocalText> Keywords,         // search ([S-943])
    IReadOnlyList<ProviderId> Providers,       // search by service name, the provider-gone screen
    WidgetTraits Traits,                       // HasItems, Aligns, CanHaveHeader, SetUpInPlace (flags)
    StringTable Strings);                      // its own table: completeness tests, settings search

public interface IProviderModule
{
    ProviderId Id { get; }
    LocalText Label { get; }
    ServiceDescriptor? Service { get; }        // a kit service (5.7); null for a host provider
    ProviderSignIn SignIn { get; }             // the auth members the Accounts page offers
    IReadOnlyList<FeatureId> Features { get; } // what widgets may ask of its accounts
    CadenceRow? Cadence { get; }               // its line in Settings > Widgets > Updates ([S-3780])
    string? BuildSecret { get; }               // the key in THEMIA_CLIENT_SECRETS, when BuildTime
    StringTable Strings { get; }
}

// Themia.Sdk.WinUI
public interface IWidgetModule
{
    WidgetDescriptor Descriptor { get; }
    FrameworkElement CreateView(IWidgetHost host);                                  // implements IWidgetView & co
    void SettingsRows(SettingsRows rows, WidgetConfig config, Action save) { }      // none: no page ([S-1750])
}
```

Behaviour the host asks of a widget stays on its **config**, as section 5.6 already does for
`IKitConfig`: `ISetupRule` (what `WidgetSetup.Needed` and `WidgetLiveness` ask), `IInterests`
(what `SyncInterests` collects), `IStyled` ([S-6000]..[S-6010], unchanged), `IShownSettings<T>`,
`Repair`. The host reads them off the instance, so no host switch grows per widget.

`IWidgetHost` is what a view may ask of the host instead of the static `AppServices` it reads today:
the shared `Ticker` and dispatcher, `Launcher` (`ShellLauncher`, the one place that opens
anything), `Accounts` (a read-only view of signed-in accounts and `ShowAccounts`), `Services` (the
`ServiceSync` source of section 6.5), `Notices`, `Clipboard`, `PlaceSearch`, `Links` and the
`IWidgetSurface`. A module that owns a service shared by its instances (RSS's feeds, the alarms)
declares an `IModuleService` the host creates once, on first use, and disposes at exit, so
"share services between widget instances" holds without a static.

### 4.4 Discovery at build time

- **Glob project references.** `Themia.App.csproj` (and the module test host, 4.9) reference
  `..\Widgets\*\*.csproj` and `..\Providers\*\*.csproj`, one folder level deep. A new folder is in
  the next build.
- **A registry generator.** `Themia.Modules.Generator` (Roslyn incremental generator,
  `netstandard2.0`, referenced as an analyzer, nothing ships) reads the assembly-level
  `[ThemiaWidget]`/`[ThemiaProvider]` attributes of every referenced assembly, and checks at build
  time: the id's shape (THEMIAMOD001), that no id is declared twice (002), that the type is public,
  non-abstract, implements its contract and has a public parameterless constructor (003), that
  the id is the project's suffix in camelCase (004, `Themia.Widget.WeatherNow` declares
  `weatherNow`), and that the hosting compilation declares no module itself (005: since the last
  widget left the app, 2026-10-18, it no longer reads its own attributes; Core keeps the host's own
  providers). It emits `ModuleRegistry` with one `new M()` per module in id order, plus the id
  lists. Static construction means Native AOT sees and keeps every module type: no reflection, no
  runtime loading, nothing to root by hand.
- **Shared csproj boilerplate.** `src/Widgets/Directory.Build.props` and
  `src/Providers/Directory.Build.props` set the target framework, `UseWinUI` (widgets), the
  package and SDK references, the strings generator with `ThemiaStringsEnum` defaulting to
  `<Suffix>Str`, remove `Tests\**` from the module's compile, compile `Preview\**` in Debug only,
  copy `Fixtures\**` into Debug builds only, and treat `SPEC.md` as a plain item. A module csproj is
  `<Project Sdk="Microsoft.NET.Sdk" />` plus its provider references.
- **Rejected: `[ModuleInitializer]` self-registration.** The spike measured it: under CoreCLR (every
  Debug build, F5, the test runner) a module's initializer did not run until the host touched the
  module ("initializers before touching the registry: []"); under Native AOT the same initializers
  ran eagerly at startup. Debug would see no modules and the shipped build all of them: two
  behaviours, rejected. **Rejected too:** loading plugin assemblies at run time (no
  `AssemblyLoadContext` under Native AOT, and nothing to distribute separately anyway), reflection
  scanning (trimmed away), and a hand-kept list (a main-code edit per module).

### 4.5 Ids: closed sets without enums

An enum in Core cannot be extended by another assembly, so the closed sets become ids:
`WidgetId`, `ProviderId`, `FeatureId` and `CadenceId`, each a `readonly record struct` over a
string in the SDK, with its own JSON converter. The set stays closed: the generator fixes it at
build time (and rejects bad or duplicate ids), and at run time the host installs the registry
(`ModuleCatalog.Install`) before anything is read, so every id from a file, the clipboard, a
drag or a command line is validated against the modules compiled in. Ids are camelCase and equal
to today's JSON discriminators (`clock`, `weatherNow`, `rss`), so state files keep their shape.

- **One id per widget, everywhere:** the config's discriminator in `state.json`, `--widget-png
  <id>`, the module's strings enum, its SPEC fragment, the showcase view.
- **`WidgetKind` goes.** It has 1,095 references in 134 files today; most are per-kind switches and
  tables that become descriptor data (`WidgetCatalog.All` and `CreateView`, `CreateDefault`,
  `AddTree`, `WidgetKeywords`, `ItemKinds`, `ContentAlignment`, `CanHaveHeader`, `WidgetSetup`,
  `SyncInterests`, the settings dispatch). `WidgetInstance.Kind` goes too: the config's
  discriminator *is* the kind, so the mismatch the store checks for today cannot happen. Host code
  never names a particular widget; a source scan of Core and the app for widget id literals has an
  allow list that ends empty.
- **An unknown id on load** (a settings file exported from a build with a module this one lacks, a
  hand edit) is **dropped** with a logged notice (Q15, decided): the widget leaves its view, the
  rest of the document loads, the log names the id and the view, and the next save no longer holds
  it. No placeholder card, no raw JSON kept. This is a deliberate exception to `CLAUDE.md`'s "never
  delete user data on a recoverable error": a widget whose code is not in this build is not a
  recoverable error (nothing this build can do brings it back), so it is treated like any other
  closed-set value the build does not know. `SPEC.md` states it in the store's section when the
  behaviour lands (M2: [S-6908], done). A known id with a broken config is repaired by its module to the default
  and logged, as today. The spike confirmed what has to change for that: System.Text.Json rejects
  an unknown discriminator for the whole document ("Read unrecognized type discriminator id
  'nope'"), so the store reads `widgets[]` element by element.
- **Providers.** `AccountProvider` becomes `ProviderId`. `AccountFeatures` becomes a set of
  `FeatureId`: the host's own features (`mail`, `calendar`, `contacts`, `tasks`) are SDK
  constants, a kit service's feature is its provider's id (one feature per kit service, section
  5.7). `UpdateSource` becomes the providers' `CadenceRow`s (plus the host's own rows for
  weather, markets and feeds while those are host sources), and `AppPreferences.Refresh` a map
  keyed by `CadenceId`, repaired to the row's default when an entry is missing. The offline
  store's service shelves are keyed by provider id under the one `service` kind (section 6.5); no
  shelf kind per provider. The Accounts page lists the registered providers that have sign-in
  members (`ServiceSignIn`, generic). Build-time secrets are looked up by the provider's
  `BuildSecret` key in `THEMIA_CLIENT_SECRETS` (section 5.7).
- **Microsoft and Google** become provider modules (`Themia.Provider.Microsoft`, `.Google`) once
  their sign-in runs on descriptors (section 11, step 11). Their mail, calendar, contacts and tasks
  sources stay host code and are keyed by `FeatureId`, not by provider.

### 4.6 JSON across assemblies

Each module has its own source-generated `JsonSerializerContext` with the host's options
(camelCase, `WhenWritingNull`; a conformance test asserts them). The host builds the state options
once, after the registry is installed:

```csharp
var options = new JsonSerializerOptions(AppStateJsonContext.Default.Options)
{
    TypeInfoResolver = JsonTypeInfoResolver.Combine([AppStateJsonContext.Default, .. moduleContexts])
        .WithAddedModifier(info =>
        {
            if (info.Type == typeof(WidgetConfig) && info.PolymorphismOptions is { } union)
            {
                foreach (var m in widgets)
                    union.DerivedTypes.Add(new JsonDerivedType(m.ConfigType, m.Id.Value));
            }
        }),
};
```

The `[JsonDerivedType]` list on `WidgetConfig` and the configs in `AppStateJsonContext` go; every
config, built-in ones included, joins the union this way (one path). Resolver combination,
contract modifiers and `JsonDerivedType(Type, string)` are AOT-safe: the spike's AOT publish had no
IL warnings, and the AOT executable round-tripped a built-in `ClockConfig` and a module's
`HelloConfig` through these options. One trap the spike hit: a module config's `Kind` override
without `[JsonIgnore]` fails at run time ("contains property 'kind' that conflicts with an existing
metadata property name"); the SDK base class seals the id property as ignored so no module can
repeat it. Whatever else a module persists (a provider's replica in its service shelf, a module
store) goes through the module's own context; the host serialises a module type only through the
combined options.

### 4.7 Localization per module

- The strings generator gains a module mode: a module's `Localization/Locales/<code>.json` (and
  area folders) become `public enum <Name>Str` in the module's namespace, its table class and
  `Text()`/`Format()`/`Plural()` extensions over `Strings.Current`. Spike-proven in Debug and under
  AOT with the generator reading two MSBuild properties (`ThemiaStringsEnum`, `RootNamespace`). The
  real build reuses the host's lookup (fallback to English, plural rules, the missing-key note)
  through a public SDK `StringTable`, so a module's strings behave exactly like `Str`.
- `LocalText` (a `StringTable` and an id) is how a descriptor hands its label, description and
  keywords to the host (catalogue, picker, search, settings search) without the host knowing the
  module's enum.
- The rules of [S-1105], [S-1106] and [S-1112] (every key in 30 languages or `$englishOnly`,
  placeholders and plurals match) run per module over its `StringTable`, in the module test host.
- Each module owns its keys; tables never mix, so no key prefix rule is needed.

### 4.8 Views, XAML and settings in a WinUI library

- **Code-only views** (all widgets but Clock) live in the module's WinUI class library.
  Spike-proven: drawn by `--widget-png hello` in Debug and from the AOT executable.
- **XAML in a module** works too: a `UserControl` compiled in a module library landed as
  `Themia.Widget.Bye\ByeBadge.xbf`, the existing `PublishCompiledXaml` glob copied it, and it drew in
  Debug and from the AOT executable. The MSIX package builds with a module library in it (M3:
  `package.ps1` puts `Themia.Widget.YearProgress.pri` beside `resources.pri`, as it does the
  toolkit's); installing it and a XAML module in it are a manual pass. Clock can move as it is.
- **Settings rows** come from `IWidgetModule.SettingsRows`, built only from `FluentLayout` and
  `KitRows` (both in `Themia.Sdk.WinUI`); the dispatch switch in `ConfigPages.Widget.cs` goes.
- **Glyphs** moves to `Themia.Sdk.WinUI`. A module whose glyph is not there declares its own code
  point constant in its project instead of editing `Glyphs`; the menu-art rule applies to it as to
  any glyph.
- **Preview states** (`IWidgetPreviewState`, section 6.7) live in the module's `Preview/` folder,
  compiled in Debug only by the shared props; the gallery finds a module by its id.

### 4.9 Tests

- **One module test host**, `tests/Themia.Modules.Tests` (`net10.0-windows`, xUnit), references
  every module by the same globs as the app, compiles every module's own tests
  (`<Compile Include="..\..\src\Widgets\*\Tests\**\*.cs" />`, the same for providers), and runs the
  registry generator. So a module keeps its tests in its folder (`Tests/`), one `dotnet test`
  covers all modules, and the conformance tests iterate exactly the modules that ship. Adding a
  module adds no test project. Spike: the host ran a module's own tests and three registry
  theories (7 passed); pure types of a WinUI library load in the test process without the Windows
  App SDK bootstrap.
- **Host conformance over the registry** (in that host): every module has an English label and
  description; its default config is its `ConfigType` and round-trips through the state options
  with its id as discriminator; its strings are complete or `$englishOnly`; it has a `SPEC.md`
  whose tags are in its block and each has a test `S<tag>_...` or a "Needs a manual pass" line; its
  glyph is set; its add-menu shelf exists; the providers it names are registered and the feature
  its config needs is offered by one; its csproj references only the SDK, `Themia.Sdk.WinUI` and
  providers; `Traits.HasItems` matches whether its view paints items (today's [S-6570] scan, per
  project) and `Traits.Aligns` whether it implements `IWidgetAligned` ([S-6581]); its view's
  `Detach` is idempotent (where the test host can build a view, else the render checklist).
- `tests/Themia.Core.Tests` stays for Core and the SDK's rules.

### 4.10 SPEC fragments

Every module carries `SPEC.md` in its folder: its section, its own tag block (`[S-7100]..[S-7199]`
for GitHub, and so on), its owning files, its tests and what needs a manual pass. The root
`SPEC.md` keeps the host's sections and says once that module sections live in
`src/Widgets/*/SPEC.md` and `src/Providers/*/SPEC.md`. A test fails on a tag used twice across all
SPEC files and on a module without a `SPEC.md`. The scaffolder picks the next free hundred across
all files.

### 4.11 The spike (2026-10-17)

A throwaway worktree at `945c980` (detached, removed afterwards; nothing of it is committed) built
the smallest end-to-end proof: `Themia.Sdk` (contracts; in the spike it sat *above* Core for
`WidgetConfig` and `Str`, the design puts it below), `Themia.Sdk.WinUI`, `Themia.Widget.Hello`
(code-only view, config, JSON context, one locale file), `Themia.Provider.Hello` (descriptor only),
the glob references and the registry generator in the app, `StateJson.Build` in Core, and a debug
render path. The widget card was bypassed (a bare card): the card needs `WidgetKind` to become an
id first, which is the large mechanical step of section 11, not a feasibility question.

| Check | Command | Result |
|---|---|---|
| Debug build, warnings as errors | `dotnet build src/Themia.App/Themia.App.csproj -c Debug --artifacts-path …` | pass, 0 warnings |
| Registry and JSON (JIT) | `Themia.exe --list-modules` | widgets `[hello]`, providers `[hello]`; `ClockConfig` and `HelloConfig` round-trip; unknown discriminator: `JsonException` |
| Module widget render (JIT) | `Themia.exe --widget-png hello out.png` | drawn: the module's own string, its provider's host name |
| Built-in render unaffected | `--widget-png Clock out.png 280 140` | drawn |
| Release AOT publish | `scripts/publish.ps1` (IlcTreatWarningsAsErrors) | pass, 0 warnings, 48.6 MB; 48.7 MB with the registry rooted from Release code |
| Debug AOT publish, then run | `publish.ps1 -Configuration Debug`, the published `Themia.exe --list-modules` and `--widget-png hello` | pass, 0 warnings; registry, JSON round trip and render all work in the native binary |
| A second module by folder only | copy to `Themia.Widget.Bye`, rebuild | registry `[bye, hello]` with no edit outside the folder |
| Build-time id checks | id `hello` twice; id `Bye_1` | `THEMIAMOD002`, `THEMIAMOD001` (beside a misleading `WMC9999` XAML-pass error the failed compile causes) |
| XAML in a module | `ByeBadge.xaml` in the module | drawn in Debug and from the AOT executable |
| Module initializers | probe before touching the registry | CoreCLR: none ran; AOT: all ran at startup (4.4) |
| Module test host | `dotnet test tests/Themia.Modules.Tests` | 7 passed |
| Core tests touched by the generator change | `dotnet test … --filter Locali\|Store\|Strings` | 569 passed |

Found on the way: the module config `kind` conflict (4.6); the generator's errors arrive with
XAML-compiler noise; and an old quirk unrelated to modules, `PublishCompiledXaml` globbing
`$(OutDir)**\*.xbf` also copies a previous `publish\` folder into `publish\publish\` on the second
publish (harmless, to be fixed in its own commit).

### 4.12 What "no change to main code" means

Adding a widget or a provider is creating its folder; the build, the registry, the JSON union,
the catalogue, the add menu, search, settings, the Accounts page, the cadence rows, the strings,
the tests and the SPEC index all pick it up. What is left outside the folder, and why:

1. **`Themia.slnx`** does not glob. Build, test and publish do not read it; the scaffolder adds one
   line so the project shows in an IDE.
2. **A Windows capability** a module needs that `Package.appxmanifest` lacks (none of the kit
   services needs one; the app is full trust) is a host change, reviewed as such.
3. **`THIRD-PARTY-NOTICES.md`** when a module bundles third-party files: the module keeps a
   `NOTICES.md` and the notices test collects the fragments (the root file is generated from them
   in the commit that adds this test).
4. **A build secret** (`todoist=` in `THEMIA_CLIENT_SECRETS`) is build environment, not source.
5. **SDK gaps** by design: a new strategy member, UI part or host service is an SDK change first.

### 4.13 What stays host code

The shell (surfaces, cards, edit mode, the properties panel, the settings window, the picker),
the generic service host (`ServiceSync`, `ServiceClient`, `ServiceSignIn`), the account and offline
stores, the mail, calendar, contacts and tasks sources of the host providers, the registry and
`ModuleCatalog`. None of it names a module.

## 5. SDK parts (pure)

All in `src/Themia.Sdk/Kit/` (namespace `Themia.Sdk.Kit`) unless noted: the SDK of section 4.2, so
every widget and provider project can use them. Pure, no UI types, tested in
`tests/Themia.Core.Tests/Kit*Tests.cs`.

### 5.1 The item

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

### 5.2 Typed actions

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

### 5.3 One key map

`KitKeys.Decide(WidgetKey key, KitCursor cursor, KitListShape shape) -> KitKeyResult` is the one
list key map, built on `ListKeys.Decide` (which already names the commands) with `ItemCursor`'s
Left/Right for columns. The two disagree on Up with no cursor (first or last item); the kit keeps
`ListKeys`' answer (the first item), which six widgets already use, and RSS's keyboard change is a
tagged SPEC line of its own when RSS migrates (no pixel changes). Up/Down (and Left/Right across columns), PgUp/PgDn, Home/End move; Enter
runs the primary action; Space a `Toggle`; F2 `Rename`; Delete `Delete`; Ctrl+C the first copy;
Menu opens the menu; Escape drops the cursor ([S-813]). With `Overflow = Page`, Left/Right and
PgUp/PgDn page instead ([S-3624]). Optional type-ahead goes through the existing `TypeAhead`.

### 5.4 The ladder

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

### 5.5 Screens: the one state machine

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

public sealed record KitScreen(KitScreenKind Kind, string? Sentence, bool OffersAccounts, bool OffersGrant, IReadOnlyList<string> HeaderProblems);

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
reply)` (section 5.7), so an HTTP 400 carrying Linear's `RATELIMITED` and a GitHub 403 with
`x-ratelimit-remaining: 0` are both `RateLimited` with a `RetryAt`, and nothing downstream knows
which service it was. `KitAccountState` is derived from the status (`SignInRequired` and
`Forbidden` name the account; `Gone` comes from the account list, not a reply).

**The one stale rule** (`KitFreshness`, built on `SyncFreshness`): while a stored copy is on screen
and the last attempt for some owner failed, the header shows the problem glyph, and its tooltip has
one line per owner and problem, folded as [S-2664] folds them: `"Work: No network · updated Mon
10:42"` (`SyncFreshness.Stamp`). The card menu's Refresh line says the age ("Refresh · updated 4 min
ago", as Mail does). The age itself is never drawn in the header in the caution colour any more.
This is a behaviour change for Mail, Tasks, RSS, Weather and Stocks and ships as its own SPEC
change after the migrations (section 11, step 7; decided, Q3).

### 5.6 The config contract

```csharp
/// A widget built on the kit. Implemented by its config, discovered by the shared rules,
/// so a new kit widget adds no line to WidgetSetup, SyncInterests or the settings dispatch.
public interface IKitConfig
{
    /// What it reads: the feature it needs (null for a web source without accounts). A kit
    /// service's feature is its provider's id (section 4.5).
    FeatureId? Needs { get; }

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

`WidgetSetup.Needed` gains one arm (`IKitConfig kit => !kit.Configured || (kit.Needs is { } need
&& Lacks(signedIn, need))`), and `SyncInterests.From` one loop over `IKitConfig` configs that
collects `KitInterest`s; neither grows per kind again (and with section 4, neither names a kind
at all: non-kit widgets answer through `ISetupRule` and `IInterests` on their configs). Styles stay `IStyled` exactly as
[S-6000]..[S-6010] say; a kit style is a different `KitSpec` (row looks, columns), never a second
setting.

### 5.7 The service layer (SDK): a descriptor choosing typed strategies

**Principle.** The kit's service code knows *strategies*, never *services*. Everything a service
does differently on the wire (how it is asked, paged, signed into, throttled, how it fails, how it
says "nothing changed") is one member of a closed set below, and the service's
`ServiceDescriptor` picks members and fills in their parameters: header names, endpoints, scopes,
codes. The descriptor is data (a `static readonly` record in the service's folder, no virtual
methods, no callbacks). What cannot be data (building an address from a query, mapping a wire
shape to `KitItem`s) is the service's rules, pure functions with fixed signatures the kit calls.
Every strategy is AOT-clean: JSON through the service's own source-generated context, GraphQL
documents as `const string`s, no reflection, no `dynamic`, no runtime schema.

Shared code lives in `src/Themia.Sdk/Services/` (namespace `Themia.Sdk.Services`); each service
is a provider project, `src/Providers/Themia.Provider.<Service>/` (section 4). Sign-in is the one part shared with the non-kit
providers: Microsoft and Google get a `ProviderSignIn` entry (the descriptor's `SignIn` list and
token header, nothing else) when their sign-in moves onto `ServiceSignIn` (section 11, step 11);
their data paths are not kit services and stay as they are.

#### The descriptor

```csharp
public sealed record ServiceDescriptor(
    ProviderId Provider,                         // the provider module's id (section 4.5)
    FeatureId Feature,                           // what widgets ask for ([S-502]); the provider's id for a kit service
    CadenceRow Cadence,                          // its line in Settings > Widgets > Updates ([S-3780])
    TransportKind Transport,                     // RestJson | GraphQL | FormPost
    Uri ApiRoot,                                 // https; the GraphQL endpoint for GraphQL
    ApiRootRule Root,                            // Fixed | PerAccount (an Enterprise server, Q6)
    ProviderSignIn SignIn,                       // ServiceAuth members in the order the Accounts page offers
                                                 // them, and the default TokenScheme (Bearer | Raw)
    IReadOnlyList<HeaderPin> Pinned,             // Accept, an API version; never a credential
    RateLimitScheme Limits,
    ErrorScheme Errors,
    FreshnessHints Freshness,                    // the default; a query may narrow it
    AnonymousRule Anonymous,                     // None | PublicReads(TimeSpan floor)
    int MaxPages, int ItemBound,                 // the hard bounds of any one answer
    IReadOnlyList<string> WebHosts,              // hosts an item link or OpenTo may name
    TimeSpan IdleFloor);                         // the shortest cadence the service's budget allows
```

The descriptor is the provider module's `Service` (section 4.3); the host finds it through the
generated registry, so there is no `ServiceDescriptors.For` switch and no provider table to edit.
`AccountProviders` (labels, wire names, features) becomes a view over the registered providers.

#### Strategy sets

**Transport** (`TransportKind`, service-level):

| Member | Request | Answer | Who uses it |
|---|---|---|---|
| `RestJson` | GET (conditional headers when held) to a rules-built address | the body is the wire type, parsed with the service's `JsonTypeInfo<T>` | GitHub, Todoist REST |
| `GraphQL` | POST `application/json` `{"query": <const>, "variables": {...}}` to `ApiRoot`; the body is serialised by the rules through the service's context (`GraphQLRequest<TVars>`) | `GraphQLResponse<TData>` (`data`, `errors[]` with `message`, `path`, `extensions.code`); `errors` go to the `ErrorScheme`, `data` to the rules; **partial data** (a 200 with both) is used when the list's own path is present, and the errors are logged once and become the outcome's `Detail` | Linear |
| `FormPost` | POST `application/x-www-form-urlencoded` with rules-built fields (`sync_token`, `resource_types` as a JSON array string) | as `RestJson` | Todoist Sync (Q12: decided) |

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
by the service's documented maximum in the query: GitHub search 100, Linear the GraphQL
`first`; Todoist Sync has no pages).

**Auth** (`ServiceAuth`, a closed set of sealed records; a service lists the ones it offers):

| Member | Flow | Secret | Stored as |
|---|---|---|---|
| `OAuthPkceLoopback(AuthorizeUrl, TokenUrl, ClientId, Scopes, ScopeSeparator, Loopback, ClientSecret, Refresh)` | the existing browser flow ([S-1780]..[S-1784]): system browser, loopback listener, `state`, PKCE S256 | `ClientSecret = None` (Microsoft, Linear), or `BuildTime` (Google, a confidential Todoist app): read from `BuildSecrets.ClientSecret(Provider)`, never in source | `OAuthRefresh` (or `Token` when the service issues no refresh token) |
| `OAuthDeviceCode(DeviceUrl, TokenUrl, ClientId, Scopes, ScopeSeparator, Refresh, GrantPage)` | RFC 8628: show `user_code` and open `verification_uri`; poll at `interval`, +5 s on `slow_down`, stop on `expired_token`/`access_denied` (`DeviceCodeSession` restored from `e09beef^`) | none | `Token`, or `OAuthRefresh` when the answer carries a refresh token |
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
- `GrantPage`: where the user widens what an account may read, when the service grants access per
  resource rather than per scope (a GitHub App is *installed* on chosen organisations and
  repositories: `https://github.com/apps/<slug>/installations/new`). The Accounts page shows it
  under the account, and a `Forbidden` or `NotFound` screen of that provider offers it beside the
  Accounts link (`KitScreen.OffersGrant`). Null for scope-based services.
- The identity request: every service has one query (`<Service>Queries.Identity`) whose map
  returns the account id and display name. It runs after every sign-in and on a pasted token
  before it is stored (a token that fails it is not saved).

Sign-in is one App class, `ServiceSignIn`, with one method per auth member; the Accounts page lists
the providers and, under each, the members of its `SignIn` list (a device-code panel, a paste box
with the token page link, a browser button). No provider is named in App sign-in code; the
ternaries of `SignInWithBrowserAsync` go when Microsoft and Google move onto descriptors.

Build-time secrets are generalised once: the csproj target reads one variable,
`THEMIA_CLIENT_SECRETS` (`google=…;todoist=…`), validates each value's alphabet as today, and
generates `BuildSecrets.ClientSecret(string key)`, where the key is the provider module's
`BuildSecret` (section 4.3); the SDK's `ClientSecretRules.Parse` holds the parsing rule and is
tested, and a conformance test fails when a provider says `BuildTime` without a `BuildSecret` key. `THEMIA_GOOGLE_CLIENT_SECRET` is replaced by it (one path; `CLAUDE.md`
and `docs/STORE.md` updated in the same commit). A service whose descriptor says `BuildTime` and
whose secret is absent from the build offers its other sign-in members only, and logs that once.

**Freshness** (`FreshnessHints` flags plus `Incremental`, per query, defaulting to the descriptor):

| Member | What it sends | What a "nothing changed" looks like | Who uses it |
|---|---|---|---|
| `ETag` | `If-None-Match` with the held `ETag` (GET only) | `304`: the held body is reused; a 304 with nothing held is `BadResponse` | GitHub |
| `None` | nothing | every answer is new; the cadence alone keeps it cheap | Linear |
| `Incremental.SyncToken` | the held sync token (`*` for a full read) | a delta: upserts and removals by id (`is_deleted`), merged onto the held replica; a rejected token or `full_sync: true` replaces it; a full read at least daily | Todoist Sync |
| `Incremental.Probe` | a small rules-built probe (the newest `updatedAt` of the set) before the real query | the probe's value is unchanged: the full query is skipped | Linear, only if the complexity budget bites (off by default) |

`LastModified` and a poll-interval header were members in the first draft for GitHub's
notifications; with no notifications widget (Q5) no service uses them, so they are not built (the
kit adds a member when a service needs it, not before).

An "updated since" delta (fetch only items changed after a high-water mark) is **rejected**: an
item that leaves a filtered set (unassigned, moved to another team) never shows up in the delta,
so the stored copy would keep it. Only a service-side sync token or a full re-read is correct.
Conditional bodies are held in memory per `account + request key` (bounded, oldest out); a 304 is
answered from there and never parsed as an empty list.

**Replicas** (`IServiceReplica<TReplica, TWire>`, for `Incremental.SyncToken`): a sync API answers
"what changed in the account", not "the list this widget shows", so the held thing is a typed
replica of the account's data, not a list of items. The provider writes three pure functions: `Merge(TReplica? held,
TWire delta) -> TReplica` (upserts by id, removals by `is_deleted`, a full answer replaces),
`Validate(TReplica) -> TReplica` (on load: bounds, ids once, links re-checked), and, per widget
query, `View(TReplica, TQuery, DateTimeOffset now, TimeZoneInfo zone) -> KitPage` (the filter, the
sort, the groups). One replica entry per account is shared by every widget of that provider; a
widget's list is a view computed from it, never a request of its own. The replica and its sync
token are stored together in the account's sealed `service` shelf (section 6.5), through the
provider's own JSON context.

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
src/Providers/Themia.Provider.<Service>/
  Themia.Provider.<Service>.csproj   <Project Sdk="Microsoft.NET.Sdk" /> (the shared props do the rest)
  <Service>Provider.cs     [assembly: ThemiaProvider("<id>", typeof(...))] + IProviderModule: label, features,
                           cadence row, sign-in members, build-secret key, Service = the descriptor
  <Service>Descriptor.cs   the ServiceDescriptor: strategies and their parameters (data)
  <Service>Wire.cs         response (and GraphQL variable) shapes + <Service>WireContext ([JsonSerializable] each closed type)
  <Service>Queries.cs      enum <Service>List { ... } + the query record whose Key names everything that changes the
                           answer and never the token; Identity; GraphQL documents as const strings
  <Service>Rules.cs        Build(query, page) -> ServiceRequest; Map(query, wire, now) -> KitChunk (items, next cursor,
                           summary); Actions(item) -> KitAction list; input validation (a repository, a team key);
                           for a replica: Merge, Validate, View
  Localization/Locales/    its strings (the provider's label, its sign-in hints, its rules' sentences)
  Fixtures/                recorded answers, scrubbed (section 6.7)
  Tests/                   its rule tests, compiled into the module test host (section 4.9)
  SPEC.md                  its section and tag block, with the facts and their source URLs
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
item rules of section 5.1 apply to every service's chunks; the descriptor's `WebHosts` and
`ItemBound` are enforced there, not in the rules.

#### The rule for "generic enough"

Adding a service may add only:

its provider project, and nothing outside it:

1. its module entry and descriptor (provider id, feature, cadence row, sign-in members, build
   secret key if its descriptor says `BuildTime`);
2. its wire types and JSON context;
3. its rules (request builders, wire → `KitItem`/`KitAction`/`KitSummary` mapping, a replica's
   merge and views, input validation);
4. its queries enum and GraphQL documents;
5. its strings, fixtures, tests and `SPEC.md`.

The only thing outside the folder is the build environment's secret (`todoist=` in
`THEMIA_CLIENT_SECRETS`), which is not source.

If a service needs anything else (a new branch in `ServiceClient`, a header read by name in shared
code, a sign-in screen of its own, a special case in `RateLimitRules`), the kit is wrong: a
strategy member or a parameter is added to the kit first, in its own commit, with tests, and the
service then uses it. A conformance check holds the line (section 9): no file outside
`src/Providers/` (the SDK, Core, the app, `Themia.Sdk.WinUI`) names a kit service (`GitHub`,
`Linear`, `Todoist`, any registered provider id).

Accounts: each service is a provider id with its own feature (one feature per kit service:
widgets ask for the service's data, never the provider; a task service's feature is not `tasks`,
because the Tasks widget would then try to write to it) and `AccountAuth` gains
`Token` (a token without refresh: pasted, or a non-expiring OAuth grant) beside `OAuthRefresh` and
`Password` (the closed sets of [S-500]/[S-502], now ids validated against the registry, section 4.5).

## 6. WinUI parts and the service host

The UI parts (6.1..6.4, 6.6) are in `src/Themia.Sdk.WinUI/Kit/`, so widget projects use them; the
service host (6.5) is in `src/Themia.App/Services/Kit/`, because only the host talks to services.
Non-generic XAML types: a generic class deriving from a WinRT type is a CsWinRT/AOT risk, so
generics stay in the SDK and the WinUI parts take `KitItem`s.

### 6.1 `KitList` (the list part)

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

### 6.2 `KitHeader`

The `IWidgetHeaderContent.HeaderTrailing` of a kit view, built in the view's constructor
([S-400a]): from leading to trailing, the problem glyph (`Glyphs.Warning`, caution brush,
`Fluent.IconSize`, tooltip from `KitFreshness`, a click opens Accounts when the screen offers it),
the busy ring (`Fluent.IconSize`, only while a fetch for one of the widget's own keys is out,
[S-2661]), optional view buttons from the spec (Mail's compose), and the count badge. Sheds in that
order's reverse on narrow tiles by `KitLadder`, not by numbers in the view. A side header drops it
([S-408]).

### 6.3 `KitStatePanel`

`OfficeStatePanel` and Mail's empty host merged: the widget's glyph over `KitScreen.Sentence`, and
the Accounts link (`AppServices.ShowAccounts`) when `OffersAccounts`, shed to fit the height
through `EmptyStatePanel` ([S-642]). Loading shows the glyph alone (no sentence flicker). The Set up
state is not here: the card owns it ([S-1862]).

### 6.4 `KitBinding` and the view

```csharp
// src/Widgets/Themia.Widget.GitHubReviews/GitHubReviewsModule.cs
[assembly: ThemiaWidget("gitHubReviews", typeof(GitHubReviewsModule))]

public sealed class GitHubReviewsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor { get; } = new(
        WidgetId.Of("gitHubReviews"), typeof(GitHubReviewsConfig), () => new GitHubReviewsConfig(),
        GitHubReviewsJson.Default, GitHubReviewsStr.Label.ToText(), GitHubReviewsStr.Description.ToText(),
        Glyphs.PullRequest, 340, 260, new(AddShelf.Online, "github.1"),
        [GitHubReviewsStr.Keywords.ToText()], [GitHubProvider.Id], WidgetTraits.HasItems, GitHubReviewsStrTables.Table);

    public FrameworkElement CreateView(IWidgetHost host) => new GitHubReviewsView(host);
    public void SettingsRows(SettingsRows rows, WidgetConfig config, Action save)
        => GitHubReviewsPage.Rows(rows, (GitHubReviewsConfig)config, save);
}

internal sealed partial class GitHubReviewsView : Grid, IWidgetView, IWidgetHeaderContent,
    IWidgetSurfaceAware, IWidgetSizeAware, IWidgetContextActions, IWidgetKeys
{
    private readonly IWidgetHost _host;
    private readonly KitList _list = new();
    private readonly KitHeader _header = new();
    private KitBinding? _binding;

    public GitHubReviewsView(IWidgetHost host) { _host = host; Children.Add(_list); }
    public FrameworkElement? HeaderTrailing => _header;
    public void Attach(Ticker ticker, WidgetConfig config) => _binding = KitBinding.Open(this, _host, _list, _header, config, GitHubReviews.Spec);
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

### 6.5 `ServiceSync` and `ServiceClient` (the service source)

Both are generic: they read the descriptor and call the service's rules, and neither has a
subclass or a branch per service.

`ServiceSync` is `AnswerSync<T>` grown the parts `DevFeedHub` had, and it replaces both:

- **One per app** (the host's `IWidgetHost.Services`), the only code that asks a kit service for anything
  ([S-2600]). Entries are keyed by `provider + account id + query key`; a widget reading several
  accounts gets one entry per account and the SDK merge (`KitMerge`).
- **Interests, not subscriptions, drive it** ([S-2608]): `SyncHost.InterestsChanged` reconciles the
  set from `SyncInterests.Kit`; a widget's `Show` only asks for an entry never fetched.
- **Cadence:** the provider's `CadenceRow` from `AppPreferences.Refresh` ([S-3780]), never below
  its `IdleFloor`; stretched by the anonymous floor, through `ServicePolling.NextPoll(fetchedAt,
  cadence, blockedUntil)`; one shared ticker subscription at a minute; `SyncSchedule` backs off with the
  shared `Backoff`, wakes on `SyncHost.NetworkAvailable`, goes now on Refresh unless the entry's
  bucket is blocked.
- **Single-flight:** one fetch per entry at a time (`SyncSchedule.Busy`); a Refresh while one is out
  joins it. A query's earlier steps are entries of their own, shared by every query that needs
  them. Token refresh is already single-flight in `AccountService`.
- **Rate limits:** every reply goes through `RateLimitRules` with the descriptor's scheme; a block
  is per account and bucket; blocked entries do not ask until `RetryAt`, which the outcome carries
  so the header says when it will ask again ([S-518]).
- **Replica entries** (`SyncToken`, section 5.7): one entry per account holds the provider's
  replica and its sync token, stored together in the shelf and sent with the next request; a delta
  goes through the provider's `Merge`; a rejected token, `full_sync: true` or a day without a full
  read starts over with a full read (its own `sync-full` bucket). Widgets of that provider read
  views of the replica (`View`), recomputed on every merge and on the day's rollover tick; they
  never cause a request of their own beyond waking the entry.
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

### 6.6 Standard settings rows (`KitRows`)

In `src/Themia.Sdk.WinUI/Kit/KitRows.cs`, built only from `FluentLayout`, each with its
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
by its provider's `CadenceRow`. No title, style, header, background, scale or alignment row: those are the
properties panel's ([S-6536]).

### 6.7 Preview states and fixtures

Kit views carry no preview code. `--widget-png <id> --state <name>` swaps the widget's source
for a `FixtureSource` (debug builds only, host code in `src/Themia.App/Shell/Gallery/Fixtures/`,
compiled under `#if DEBUG` like the rest of `Gallery/`) that replays the provider's recorded
answers through the real provider rules. Every kit widget gets the same state names for free: `items`, `many` (more than fit),
`loading`, `empty`, `nomatch`, `stale` (items with an offline problem), `signin`, `gone`,
`forbidden`, `ratelimited`, `problem`, `hovered`, `focused`, `accounts` (two accounts merged).
Recorded answers live once, in the provider project's `Fixtures/`, read by its tests and copied
into Debug builds only by the shared provider props (`Condition="'$(Configuration)' == 'Debug'"`),
so Release ships none. They are scrubbed: no tokens, no real e-mail addresses, no private
repository, project or task names (a check scans them, section 9). Every service records the same
set: a first page, a paged answer (Link header or `pageInfo`) or for a replica a full and a
partial sync, an empty answer, a `304` where its freshness allows one, a throttle as that service signals it, a 401, a 403 and an
error body in its own error format (a GraphQL `errors` array, a Todoist `error_tag`).

## 7. Building a service widget (the recipe)

Example: "Pull requests waiting on my review" for GitHub. The steps are the same for any service;
section 13 walks them for Linear and Todoist.

1. **Read** `SPEC.md` sections *Accounts and sign-in*, *Offline sync*, *Context menus*, *Responsive
   layout*, the kit's own section (S-7000 block) and section 4 of this file. Read the service's own
   developer docs (primary sources only) for: sign-in (device flow? PKCE? is a client secret
   required for the token exchange or the refresh? which loopback redirects are accepted, and is
   the port free?), read-only scopes, the transport, paging, conditional requests or a sync token,
   how a throttle is signalled (status, headers, body code, reset unit), the error format, and the
   web address of each item. Write the facts with their source URLs into the provider's `SPEC.md`.
2. **Provider** (once per service): `scripts/new-module.ps1 -Provider GitHub` creates
   `src/Providers/Themia.Provider.GitHub/` (section 8). Fill in its descriptor by picking a member
   of each strategy set of section 5.7 (sign-in members in the order the Accounts page should offer
   them; device flow or PKCE without a secret before a build-time secret, a pasted token always
   last). If a fact does not fit any member, stop: the SDK grows the member first (section 5.7's
   rule). Write the wire types, rules and queries (with the identity query) and record the
   fixtures of section 6.7. No App code: sign-in, client, cadence and storage are generic.
3. **Scaffold** the widget: `scripts/new-module.ps1 -Widget GitHubReviews -Provider GitHub -Shape List`
   creates `src/Widgets/Themia.Widget.GitHubReviews/` with every part of a module and failing tests.
4. **Spec the widget** in its project: the `KitSpec` (row looks, optional fields, overflow,
   columns), the item mapping (which wire fields become title, subtitle, lead, badges, when), the
   actions per item (Open; the "<verb> on <Service>" web actions; copy), the empty and no-match
   sentences, the card actions; the descriptor's add-menu shelf, keywords and default size.
5. **Test first** where it is logic, in the module's `Tests/`: the mapping on fixtures, the merge
   across accounts, the query key, the ladder plan at all seven tiers (`WidgetTiers`), the screen
   decisions. Names mirror tags: `S7104_a_draft_pull_request_carries_the_draft_badge`.
6. **View and settings rows**: the generated view is complete for a kit list; the rows are `KitRows`
   calls. Write the strings into the module's `Localization/Locales/en.json` (new keys in
   `$englishOnly` until translated, Q8).
7. **SPEC**: the module's `SPEC.md` with tagged lines for every behaviour (its own block), owning
   files and tests, and what needs a manual pass.
8. **Check**: `dotnet build` (Debug, warnings as errors), `dotnet test tests/Themia.Modules.Tests`
   (the module's tests and the conformance over the registry, section 9), the render checklist
   (section 10), `publish.ps1` once (AOT, source-generated JSON), and say what still needs a live
   pass (a real account, each sign-in member of the descriptor, rate limits on the wire).

## 8. The scaffolder

`scripts/new-module.ps1 (-Widget <Name> [-Provider <Service>] [-Shape List|Stat|Custom] [-Glyph <name>]
[-Shelf <AddShelf>] | -Provider <Service>)`. PowerShell, idempotent (refuses to run when the
folder or the id exists), and it writes **only inside the new folder**, plus one line in
`Themia.slnx` for IDEs (section 4.12). There are no anchors to edit in shared files any more: the
registration points of today (`WidgetKind`, `CreateDefault`, `JsonDerivedType`,
`AppStateJsonContext`, `WidgetCatalog.All` and `CreateView`, the settings dispatch, `Glyphs`,
`AddTree`, `WidgetKeywords`, the locale area, `WidgetSetup`, `SyncInterests`, `ItemKinds`,
`ContentAlignment`, `CanHaveHeader`, the SPEC section, the tests) are all members of the module.

A widget module:

| File | What it holds |
|---|---|
| `Themia.Widget.<Name>.csproj` | `<Project Sdk="Microsoft.NET.Sdk" />` and, with `-Provider`, the provider's project reference |
| `<Name>Module.cs` | `[assembly: ThemiaWidget("<id>", typeof(<Name>Module))]`, the `WidgetDescriptor` (label, description, glyph, default size, shelf, keywords, providers, traits), `CreateView`, `SettingsRows` |
| `<Name>Config.cs` | `sealed class … : WidgetConfig` (+ `IKitConfig` for a kit list, `IStyled` when asked), `Clone`, `Repair`; its JSON context |
| `<Name>Spec.cs` | the `KitSpec`, mapping and actions (a kit list), or the view's Core rules (`Stat`, `Custom`) |
| `<Name>View.cs` | the kit view of section 6.4, or a `Stat`/`Custom` skeleton implementing `IWidgetSizeAware` |
| `<Name>Page.cs` | `KitRows` calls, or nothing (no page, [S-1750]) |
| `Localization/Locales/en.json` | label, description, keywords, sentences, `$englishOnly` |
| `Preview/` | preview states when the widget is not a kit list (kit lists get theirs from fixtures) |
| `Tests/<Name>Tests.cs` | one failing test per skeleton tag |
| `SPEC.md` | the section skeleton, the next free tag block, "Needs a manual pass" |

A provider module: `Themia.Provider.<Service>.csproj`, `<Service>Provider.cs` (module entry), a
`<Service>Descriptor.cs` with every strategy field marked `TODO` so it does not compile until each
is chosen, empty `<Service>Wire.cs`, `<Service>Rules.cs`, `<Service>Queries.cs`, `Fixtures/`,
`Tests/`, `Localization/Locales/en.json` and `SPEC.md`; it stops there with a list of what the
agent must write by hand (the descriptor's choices, wire types, rules, queries). It never writes
App code for a service, because there is none.

## 9. Conformance checks

Two homes: checks over **source** (scans) in `tests/Themia.Core.Tests/KitConformanceTests.cs`, and
checks over the **registry** (every compiled-in module, section 4.9) in
`tests/Themia.Modules.Tests/ModuleConformanceTests.cs`. The scans share one `RepoFiles` helper that replaces the seven copies of
`FindRoot()` (a refactor commit of its own). Comments are not code (lines starting `//` are
skipped, as `ItemKindsTests` does). Each check starts with an explicit allow list of today's
offenders, named with the reason, and the list only shrinks.

| Check | Rule | Offenders today |
|---|---|---|
| Menus | no `new MenuFlyout`, `MenuFlyoutItem` or `CommandBarFlyout` under `App/Widgets/` except `ItemMenu.cs` ([S-1740]) | none in Widgets; Shell drop-downs (`EditBar`, `ViewBar`, `PropertiesStrip`, `AccountsPage`) are not context menus and are out of scope |
| One launcher | no `Process.Start`/`UseShellExecute` outside `Services/ShellLauncher.cs` | `RssView.cs`; allowed with reasons: `DataPurge`, `Files/GitStatus`, `Files/ShellFileOps`, `ConfigPages.About`, `ConfigPages.Uninstall` |
| Colours | under `App/Widgets/`, a `Colors.X` or `Color.FromArgb` only as the fallback argument of `ThemeBrush.Of(...)`; none at all in `Widgets/Kit/` | to be listed when the test is written (Contacts' initials palette, chart washes) |
| Type | no `FontSize =` literal and no `new FontFamily(` in views; sizes come from `FluentType`/`Fluent.Apply` | `HabitTrackerView.Rows.cs` |
| Text | no user-visible string literal (`Text = "`, `Content = "`, `PlaceholderText = "`, a literal tooltip) in `App/Widgets/` and `App/Shell/Config/` | example URLs in placeholders (allowed by pattern: a literal that parses as an https URL) |
| Kit views stay thin | a view whose config implements `IKitConfig` may not mention `ScrollViewer`, `RowFocus`, `ItemMenu`, `ProgressRing`, `Fluent.EmptyState`, `ItemCursor`, `ListKeys` or `Process` | none (no kit widget yet) |
| Kit actions | no `ItemMenuEntry` constructed in a kit view; every `KitActionKind` and `KitIcon` member has its mapping in `KitMenu`/`KitIcons` | |
| Registration | over the registry: every module has a label and description in English, a glyph, an add-menu shelf that exists, a default config of its `ConfigType` that round-trips through the state options with its id as discriminator, and its declared providers registered; the generator already rejects bad, duplicate and mismatched ids at build time (THEMIAMOD001..004) | none once the built-ins are declared as modules (section 11, step M2) |
| SPEC per module | every module folder has a `SPEC.md`; no tag is used twice across all SPEC files; every non-retired tag of a module's section has a test named `S<tag>_…` or is listed under "Needs a manual pass" | modules only; the root SPEC's host sections keep today's rules |
| Strings | per string table (the host's `Str` and every module's): every key in all 30 languages or `$englishOnly`, placeholders and plurals match ([S-1105], [S-1106], [S-1112]); a kit widget's table holds every key its spec uses | |
| Fixtures | every file under `Fixtures/` parses, and none contains a token shape (`ghp_`, `gho_`, `ghu_`, `ghr_`, `github_pat_`, `lin_api_`, `lin_oauth_`, a 40-hex run as Todoist tokens are), `Bearer `, `Authorization`, an `@` address outside `example.com`/`users.noreply.github.com` | |
| Generic kit | no file outside `src/Providers/` (the SDK, Core, the app, `Themia.Sdk.WinUI`) names a kit service or a registered provider id; no service-specific header name (anything but the standard `ETag`, `Last-Modified`, `Link`, `Retry-After`) and no error code literal outside a provider project | none (nothing built yet); `RateLimitRules`' GitHub header constants move into GitHub's descriptor when the check lands |
| Descriptors | over the registry: every provider with a `Service` has a non-empty `SignIn` list and https `WebHosts`; `BuildTime` implies a `BuildSecret` key `ClientSecretRules` accepts; every query names a bucket the scheme can block; every feature a widget config needs is offered by a registered provider | |
| Module boundaries | every module csproj references only `Themia.Sdk`, `Themia.Sdk.WinUI` and provider projects (never Core, the app or another widget); module JSON contexts use the host's options; no Core or app file names a widget id (allow list ends empty) | the in-app modules during the migration (section 11) |
| Polling | every `ServiceClient` request goes through the conditional helper (when the query's freshness allows), `RateLimitRules` and `ServiceFailures.Classify`; a test per service drives its recorded throttle, 401, 403, a `304` where it has one, and its error body through the Core rules | |

Behaviour the checks cannot see stays in the render checklist and the manual pass.

## 10. Render checklist and review rubric

For every kit widget, before calling it done (outputs outside `artifacts/`, e.g.
`TestResults/render/<kind>/`):

- `--widget-png <id> <out.png>` (all seven tiers on one page) × `--theme light` and `dark` × `--lang en`,
  `de` (longest words), `ar` (right to left, own digits), `ja` (no spaces to wrap at).
- The same at `--state` `items`, `many`, `loading`, `empty`, `stale`, `signin`, `ratelimited`, `hovered`, `focused`.
- Each style (`--style`) when the kind has styles; `--header left` and `--header hidden` once.
- `--settings-png Widget:<id>` in both themes and in `de`; with `--search` for one row.
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

## 11. Migration plan

Migrated widgets lose their old code in the same commit (one path). Each migration is a refactor
with **zero visible change**: before and after renders of the checklist in section 10 are taken and
compared pixel for pixel (a small `--widget-png` diff script under `scripts/`), and the widget's
existing tests stay green without edits. Anything that would change what the user sees is split out
as a behaviour commit with its SPEC edit and test first.

### 11.1 Modules first (M1..M4)

The module architecture comes before the kit, so the kit is built in the SDK and the pilots are
projects from their first commit. The ~80 widgets of today move in two moves: first their
*registration* becomes one path (every widget declared as a module, still compiled inside the
app), then their *code* moves out into projects, one widget project per commit. There is never a second
registration path: when a table becomes a descriptor member, the table is deleted in that commit.

- **M1. The SDK split (refactors, tests unedited).** `Themia.Sdk` gets the shared pure files from
  Core (section 4.2's list) and `Themia.Sdk.WinUI` the view contracts and UI parts from the app
  (`IWidget*`, `Ticker`, `Fluent`, `FluentLayout`, `Glyphs`, `ItemFill`, `ItemMenu`, `RowFocus`,
  `ScrollFade`, `EmptyStatePanel`, a `ShellLauncher` facade). Files move with their namespaces
  unchanged, so each move is a pure move and no test or caller changes; new SDK types use
  `Themia.Sdk.*`. Dependencies that point the wrong way (a UI part reading `AppServices`) are cut
  first, in their own commits, behind `IWidgetHost`. `publish.ps1` after each project is added.
  *M1 as built (2026-10-17).* `Themia.Sdk` holds `Localization/` (the runtime, `RegionFormat`
  and all locale files, so `Str` is still the one generated enum, now generated in the SDK),
  `Diagnostics/` (`Log` and its rules), `Links/ViewLinks`, `Time/TickSchedule`,
  `Office/LenientJson` (the lenient enum converter the persisted enums use), the language and
  region vocabulary (`AppLanguage`, `ClockHours`, `FirstWeekday`, `TemperatureScale`,
  `RegionPreferences`, `TemperatureUnit`, `WeekStartDay`, `ContentAlign`, split out of the Core
  files that declared them) and the shared widget rules `WidgetArea`, `Disclosure`, `WidgetTiers`,
  `ListKeys`, `WidgetKey`, `TypeAhead`, `FetchState`, `LaunchTarget` (with `FolderPath`),
  `FluentType`, `HeaderFit`, `HeroFit`, `ListFade`, `EmptyStateFit`. `CalendarRules.WeekStartOf`
  delegates to `RegionFormat.WeekStartOf`, which the region rules need below Core.
  `Themia.Sdk.WinUI` holds `IWidgetSurface`, `IWidgetSurfaceAware`, `IWidgetHeaderContent`,
  `IWidgetKeys`, `IWidgetSizeAware`, `IWidgetAligned`, `IWidgetContextActions`,
  `IWidgetPreviewState` (Debug), `Ticker`, `Glyphs`, `ScrollFade`, `EmptyStatePanel`. No type was
  made public for the move: the SDK grants `InternalsVisibleTo` to `Themia.Core` and the tests,
  `Themia.Sdk.WinUI` to the app (`LenientJson`, `LocaleTable`, `LocaleData`, `StringTables`,
  `WidgetAction`, `IWidgetContextActions`, `IWidgetPreviewState`, `Aligned` are internal). A module
  project will need these public (M3).

  *Left for M2: what M1 found tangled* (each cut is its own refactor commit before the move):
  - `WidgetConfig`, `IStyled`/`WidgetStyles`: the `[JsonDerivedType]` list names every config and
    `WidgetKind`; they move once the union is built by `StateJson`'s modifier. `IWidgetView`
    (`Attach(Ticker, WidgetConfig)`) waits for `WidgetConfig`.
  - `FluentLayout` and with it `Fluent` and `RowFocus`: `FluentLayout.Themed` reads
    `DesignApplier.Brush` (the design state in `AppState`); its rows also use `ColorChoice`
    (`Notices`, the screen colour picker's interop, `ColorPresets`), `RowReorder` (Win32 input,
    `ReorderKeys`, `BlockGeometry`), `SettingRow`/`SettingsIndex` (settings search), `FontPicker`,
    `DeskListOrder`, and the CommunityToolkit settings controls. Needs a theme-brush seam on
    `IWidgetHost` (or the design brushes as SDK state) first. `UiLanguage` is free to move with it.
  - `ItemFill`/`ItemPalette`, and Core's `ItemLook`, `DesignLook`, `FillChoice`: read
    `DesignState` and `WidgetInstance`.
  - `ItemMenu`: `FileIcons` (shell icons), `FolderEntry` (the Folder widget's rules), `MenuArtIcons`
    (`AppIcons`, the gallery and system-data sources) and Core's `ContextMenus`, which mixes the
    item menu's line layout (`MenuLine`, `MenuSplit`, `QuickCommand`, `ContextMenuLayout`) with the
    shell's own card menus (`ShellCommand`): split it first. `KeyChord` moves with `ItemMenu`.
  - `ShellLauncher`: Win32 interop plus Core's mail, calendar, contact, meeting, disk and
    notification link rules; the facade on `IWidgetHost` comes first.
  - `IWidgetDropTarget`: `WidgetDrag` wraps the OLE drag interop (`Services/Files`) and Core's
    `SurfaceDrops`.
  - `SyncFreshness` (`Sync/SyncSchedule.cs`): `SyncProblem` reads `AccountError`; it moves with the
    one failure vocabulary (section 11.2, step 9) or once account errors are SDK types.
  - The source-scanning tests (`ItemKindsTests`, `ConfirmFlyoutTests`, `ContentAlignmentTests`) scan
    `src/Themia.App/Widgets` only; none of the moved parts matched them, and the shared
    `RepoFiles` locator of section 11.2 step 1 should include `src/Themia.Sdk.WinUI` from then on.
- **M2. Ids and the registry, with every built-in widget declared in place.** The registry
  generator, the attributes and `WidgetDescriptor`; each of today's widgets gets a small module
  class *inside the app* (`[assembly: ThemiaWidget("clock", typeof(ClockModule))]`; the generator
  also reads the compilation's own attributes). During M2 the descriptor carries the old
  `WidgetKind` as its key: a defined interim, not a second path. Then one table per commit becomes
  a descriptor member and the table goes: `WidgetCatalog.All` and `CreateView`, `CreateDefault`,
  the `[JsonDerivedType]` list and the configs in `AppStateJsonContext` (to `StateJson`'s modifier),
  the settings dispatch, `AddTree`, `WidgetKeywords`, `ItemKinds`, `ContentAlignment`,
  `CanHaveHeader`, `WidgetSetup`/`WidgetLiveness` (to `ISetupRule`), `SyncInterests` (to
  `IInterests`). The last commit of M2 replaces `WidgetKind` with `WidgetId` and removes
  `WidgetInstance.Kind` (schema changed in place, no migration: the app is unreleased). The same
  for providers: `AccountProvider`, `AccountFeatures` and `UpdateSource` become ids, with Microsoft
  and Google declared as provider modules inside Core until step 11. Behaviour commit, SPEC line and
  test first: an unknown id is dropped on load with a logged notice (section 4.5, Q15).
  *M2 as built (2026-10-17).* Contracts in the SDK: `WidgetId`, `ProviderId`, `FeatureId`,
  `CadenceId` (camelCase, validated, JSON converters; [S-6900]), `ThemiaWidget`/`ThemiaProvider`,
  `WidgetDescriptor` (init-only members, the required ones enforced by the compiler),
  `ModuleCatalog` (installed once, refuses a duplicate id or config type; `Of`, `TryGet`, `TryFind`,
  `IdOf`), `WidgetTraits`, `AddPlacement` (with `AddFolderId` moved down), and `WidgetConfig` and
  `Glyphs` moved into `Themia.Sdk` (namespaces kept). `IWidgetModule` (`Descriptor`, `CreateView`,
  `SettingsRows`) and `WidgetSettingsRows` in `Themia.Sdk.WinUI`. `Themia.Modules.Generator`
  (THEMIAMOD001..003, reading the compilation's own attributes too; 004, the project-suffix rule,
  comes with the first project in M3) emits `Themia.App.Modules.ModuleRegistry`; `ModuleHost.Install`
  runs first in `App()` and checks each module's descriptor id against its attribute.
  The interim is one level finer than planned: each built-in widget's **descriptor** is declared on
  its config in Core (`ClockConfig.Descriptor`), because Core's rules and Core's tests read it and
  cannot see the app; its **module** (`Widgets/Modules/<Name>Module.cs`: view and settings rows) is
  in the app and returns that descriptor. The tests install the same descriptors (found by reflection
  over Core in a module initializer) and `WidgetRegistryTests` checks that the app declares exactly
  one module per descriptor. When a widget moves to its project (M3/M4) descriptor and module merge.
  Moved onto the descriptor, each table deleted in its commit: the catalogue (`LabelId`,
  `DescriptionId` with `DescriptionFacts`, `Glyph`, `DefaultWidth`/`Height`; `CreateView` on the
  module), `ConfigType` + `Json` (the `[JsonDerivedType]` list and the configs in
  `AppStateJsonContext` replaced by `StateJson`'s modifier; the built-in configs share one interim
  `WidgetConfigJsonContext`), `CreateDefault`, the settings dispatch (`SettingsRows`), `Keywords` and
  `ProviderNames` (`WidgetKeywords` gone), `Traits` (`HasItems` for `ItemKinds`, `CanHaveHeader` and
  `StartsHeaderless` for the header tables) and `Add` (the `AddTree`'s widget lists; its folder
  skeleton stays host data). Onto the config instead (section 5.6's rule): `ISetupRule`
  (`WidgetSetup.Needed`, `WidgetLiveness.SetUpInPlace`), `IInterests` over an `InterestsBuilder`
  (`SyncInterests.From`), `IContentAligned` (`ContentAlignment`). `WidgetKind` is gone: ids
  everywhere (host code that still names a widget does it as `ClockConfig.Descriptor.Id`, greppable
  for M4), `WidgetConfig.Kind` sealed in the base and answered by the catalogue from the config's
  type (no config can repeat the `kind` metadata property), `WidgetInstance.Kind` computed from the
  config and no longer stored (state files keep their shape otherwise; the old per-widget `kind` is
  ignored on read). Behaviour, test first: an unknown or malformed config id, or a widget without a
  config, is dropped element by element before the typed read with a log line naming the widget, the
  id and the view ([S-6908], Q15). Regression net: `Fixtures/state-every-kind.json` (generated at
  the start of M2, one widget of every kind with its default config) loads with no repair and writes
  back unchanged but for the dropped per-widget `kind`.
  *Left for M3* (first steps, in order):
  - Providers: `AccountProvider` → `ProviderId`, `AccountFeatures` → a `FeatureId` set, `UpdateSource`
    → `CadenceRow`s and `AppPreferences.Refresh` keyed by `CadenceId`, Microsoft and Google as
    provider modules inside Core. Not done in M2: it changes `accounts.json` and the preferences
    schema and runs through sign-in code that needs a live-account pass; `ProviderNames` on the
    descriptor are display strings until then.
  - `IWidgetView` (`Attach(Ticker, WidgetConfig)`) can move to `Themia.Sdk.WinUI` now that
    `WidgetConfig` is in the SDK; `LocalText`/`StringTable` replace the descriptor's `Str` members
    when the strings generator gets its module mode.
  - The remaining per-family host tables that name widgets: `QuickToggleRules.KindOf`,
    `PowerActionRules.KindOf`, `AppState.CreateDefault`'s seed widgets, `SyncInterests.For`'s weather
    check, the gallery renders (`EditBarRender`, `SwitchCheck`, `MotionRender`, `MenuRender`,
    `SettingsRender`); the `AddTree` folder titles that are widget names. They move with their
    widgets (M4) or into the SDK.
  - Each descriptor still points at the shared `WidgetConfigJsonContext`; a moved widget takes its
    config's line into its own context.
  - The showcase command lists widgets in registry (id) order now, not the old catalogue order.
- **M3. Module plumbing and the first move.** The glob references, `src/Widgets/Directory.Build.props`
  and `src/Providers/Directory.Build.props`, the strings generator's module mode over a public
  `StringTable`, `LocalText`, the module test host with the registry conformance tests, the SPEC
  fragment test, the notices fragments, `scripts/new-module.ps1`. Then the first move, a small
  widget with no services (Year progress): its files, strings (all 30 languages move into its own
  table), SPEC section (into its `SPEC.md`, tags unchanged), tests and preview move into
  `src/Widgets/Themia.Widget.YearProgress/`; pixel diff zero; `publish.ps1` and `package.ps1`
  (packaged build, a manual pass).
  *M3 as built (2026-10-17).* Providers first: `AccountProvider` became `ProviderId` and
  `AccountFeatures` a `FeatureSet` of `FeatureId`s (`HostFeatures.Mail` and the other three);
  `IProviderModule` (id, `LocalText` label, features, `AccountAuth`, its place in the Add account
  menu, an optional own `CadenceRow`) with the five account providers declared as modules inside
  Core (`HostProviders`, `MicrosoftProvider`..`CardDavProvider`, `[assembly: ThemiaProvider]`) and
  installed into `ProviderCatalog` beside the widgets. The ids are the wire names, so
  `accounts.json` keeps its shape: `AccountsFixtureTests` loads a file of every provider generated
  with the build from before (`f4d892e`) with no repair and writes it back byte for byte, on both
  builds. Sign-in code is unchanged but for the comparisons (`provider == HostProviders.Google`);
  a live pass is still owed. `UpdateSource` became `CadenceRow`s (`HostCadences`; a provider may
  add its own) and `AppPreferences.Refresh` a map keyed by `CadenceId`
  (`"refresh": { "weather": 30, ... }`): schema changed in place, so a profile's old
  `weatherMinutes`-style entries are dropped on load, logged, and those four cadences start at their
  defaults once. Host tables that named widgets went onto the configs and descriptors
  (`QuickToggleConfig.Toggle`, `PowerButtonConfig.Action`, `WeatherConfig.ReadsWeather` and
  `ForecastDays`, family folders titled by their first widget). Strings: one `StringTable` behind
  every table (the host's `Str` included), `LocalText` (a table and an id, or a fixed brand name),
  the generator's module mode (`ThemiaStringsEnum`, THEMIALOC010), descriptor and style labels as
  `LocalText`. `IWidgetView` into `Themia.Sdk.WinUI`. The SDK grew what Year progress needed, each
  move its own commit: `ThemeBrush.Of` (the design engine behind it; `FluentLayout.Themed` is gone,
  325 call sites), the `ItemFill` facade over the host's item-layer engine (`IItemFillHost`),
  `Fluent`, `WidgetLayout`, `RingPart`, `UiLanguage`, `IStyled`/`StyleOption`/the style registry,
  `Formatting`, `RingArc`, `DotGrid`, `CalendarDays` (week start, local midnight, out of
  `CalendarRules`) and the item roles, the WinUI parts made public. Module plumbing:
  `src/Widgets/` and `src/Providers/` `Directory.Build.props`/`.targets`, glob `ProjectReference`s
  in the app and in `tests/Themia.Modules.Tests`, THEMIAMOD004, the test host (installs Core's
  descriptors and the registry; compiles each module's `Tests/`; `ModuleConformanceTests`; the
  every-kind state fixture and the unknown-id tests moved there because only it sees every widget),
  `scripts/new-module.ps1` (dry-run on a throwaway widget and provider: both built, rendered in the
  picker and passed conformance; removed). Year progress moved whole into
  `src/Widgets/Themia.Widget.YearProgress/`: config, descriptor (on the module now), view, preview,
  its rules (`TimeProgress.cs`), its 12 keys in 30 languages as `YearProgressStr`, its glyph, its
  SPEC section ([S-4800]..[S-4805], [S-6040]..[S-6042]) and its tests; the app names it nowhere.
  Pixel diff: the M2 render set and 45 Year progress renders (three styles, every tier, dark, the
  settings page and the picker, in en, de, ar, ja, fa, zh) identical to the build before (one menu
  image differs on some runs, as in M2: a fade caught mid-way, not reproducible). Release AOT
  publish green with 0 warnings (49.3 MB); `package.ps1` builds the unsigned MSIX (59.6 MB).
  *Left for M4:*
  - Settings rows from a module: `FluentLayout` (and with it `ColorChoice`, `RowReorder`,
    `SettingRow`/search, `FontPicker`) is still the host's; the first module with a settings page
    moves it, or a `KitRows` seam, into `Themia.Sdk.WinUI` first. `ItemMenu`, `ShellLauncher`,
    `IWidgetDropTarget` and `IWidgetHost` (views still read `AppServices`) likewise.
  - The first-run seed (`AppState.CreateDefault`) still names the clock, shortcuts, weather, meters,
    toggles, calendar, tasks and notes by their config types: it is the host's product decision, not
    a per-widget table, but when one of those moves Core cannot see its config any more, so the seed
    becomes data (ids plus config fragments read through `StateJson`, an absent id skipped).
  - The Debug gallery scenes (`EditBarRender`, `SwitchCheck`, `MotionRender`, `MenuRender`,
    `SettingsRender`) name widgets the same way; they turn into ids and each module's `Preview/`
    states as their widgets move.
  - `ProviderNames` on descriptors are still display strings ("CalDAV"); they become `ProviderId`s
    with the provider-gone screen. `WeatherConfig`, `QuickToggleConfig`, `PowerButtonConfig` and the
    other shared config bases move into the SDK with the first of their widgets.
  - Notices fragments (`NOTICES.md` per module): no module bundles third-party files yet.
  - Microsoft and Google as their own provider projects come with sign-in by strategy (11.2, step 11).
- **M4. Moves, one widget project per commit** (code two widgets share goes into the SDK first,
  Q16), whenever convenient and always when a widget migrates onto
  the kit (RSS, Mail, Tasks and the agenda move in the same series as their kit migration). Each is
  a refactor with the pixel diff. When the app declares no widget any more, the generator stops
  reading the compilation's own attributes (one path) and a conformance test asserts that the
  app and Core declare no widget modules.
  *M4 part A as built (2026-10-18): the enabling moves, no widget moved.* Each its own commit,
  Debug build green throughout. Into `Themia.Sdk.WinUI` (public, namespaces kept): `ItemMenu` and
  `ItemMenuEntry` with `MenuArtIcons`; `FluentLayout` (every row kind, `ColorPickerRow`, the fill
  rows, `ListEditor`), `ColorChoice`, `RowReorder`, `SettingRow` and the settings-search metadata,
  `PlaceSearch`; `RowFocus`, `WidgetItems.Mark`, `ConfirmFlyout`, `Notices`; `IWidgetDropTarget`
  over a new `IWidgetDrag`; `LaunchOutcome`; and the new `IWidgetHost` and `IShellLauncher`
  ([S-6913]). Into `Themia.Sdk`: the item menu's line layout (`QuickCommand`, `MenuLine`,
  `MenuSplit`, `ContextMenuLayout`; Core's `ContextMenus.cs` keeps the shell's `ShellCommand`
  menus), `KeyChord`, `MenuArt`, `Notice`, `FillChoice` (with `Alpha`, out of `DesignLook`),
  `HexColor`, `ColorPresets`, `ScreenPick` and the pick result, `SettingsSearch`, `ReorderKeys`,
  `DeskListOrder`, `PlaceLookup`, `WebInput`, `DropEffect`/`DropContent`, `ContactLinkKind`, the
  config contracts `ISetupRule`, `IPlaced`, `IContentAligned`, `IShownSettings`, the shared config
  bases `QuickToggleConfig`, `PowerButtonConfig`, `WeatherConfig`, `SkyPlaceConfig`, `DetailConfig`,
  and `UpdateCadence` with `HostCadences` and `RefreshPreferences` (the shared cadence a widget
  asks). What needs Win32 stays host code behind a seam the host installs once in `App()`, as
  `ThemeBrush` and `ItemFill` already were: the eyedropper (`IScreenEyedropper`), a menu line's file
  icon (`ItemMenu.InstallFileIcons`), the geocoder (`PlaceSearch.InstallLookup`), the OLE drag
  (`WidgetDrag : IWidgetDrag`); the press locator stays as the shell's `WidgetPress`. `FontPicker`
  stayed: only the host's design rows use it.
  `IWidgetHost` (one per card, `Shell/CardWidgetHost.cs`, handed to `IWidgetModule.CreateView(host)`):
  `Ticker`, `Dispatcher`, `Launcher` (`IShellLauncher`: `Open`, `OpenContainingFolder`,
  `OpenMailLink`, `OpenCalendarLink`, `OpenMeetingLink`, `OpenContactLink`, `OpenWindowsSettings`,
  `OpenApp`, `Eject`, `OutlookInstalled`, all `ShellLauncher`'s), `IsPreview`, `ShowAccounts()`. The
  12 views that opened things and the three that opened Settings › Accounts (and `OfficeStatePanel`,
  `DiskMeter`) use it now; no view names `ShellLauncher` or `AppServices.ShowAccounts`. Notices stay
  the SDK's static `Notices` and the keyboard lease the card's `IWidgetSurface` (one path each).
  The first-run seed is data (`FirstRunDesktop`: ids, grid rects, bare or not, config fragments
  merged over the default through `StateJson`, then copied so nested ids are fresh); it names no
  config type, a seeded id the build lacks is left out and logged ([S-893], tests unchanged but for
  the places list written out). Descriptors name their providers by `ProviderId` (`Providers`);
  search matches the ids, which normalise to the old display words, so search is unchanged; a
  conformance test checks each is registered. Tests: one `tests/Shared/RepoFiles.cs` replaces the
  seven `FindRoot` copies; the source scans (`ItemKinds`, `ConfirmFlyout`, `ContentAlignment`,
  `WidgetPage`) read `RepoFiles.ViewSources()` (the app's widgets, the SDK's widget parts, every
  module); `KitConformanceTests` add no menu but `ItemMenu`'s ([S-1740]) and colours made from
  numbers only in today's list of 11 files, which may only shrink ([S-822]).
  Evidence: Debug build 0 warnings; Core 7766 and module tests 57 green; the M2 render set
  against a fresh "before" built at `873248d`: 228 images, 0 differ; Release AOT
  publish 0 warnings (49.3 MB); Debug AOT publish 0 warnings and its `--widget-png clock`,
  `--settings-png Widget:weatherNow`, `--picker-png --search google` finish in 2-3 s, identical to the
  JIT renders.
  *M4 part B, group 1 as built (2026-10-18): the widgets with no host service.* Seventeen widget
  projects, one per commit, each its config, descriptor (on its module now), view, preview, settings
  page (`<Name>Settings.Rows`, the module's `SettingsRows`), rules, strings, glyph where only it drew
  it, SPEC section and tests: Stopwatch, Quote, Moon, Sun, Scratchpad, Countdown, Recycle Bin,
  Bluetooth, Shut down, Restart, Sleep, Hibernate, Lock, Sign out, Turn off display, Pomodoro and
  Clock (its XAML view compiles in the module library, as the spike showed). Namespaces unchanged;
  Core and the app name none of them but in the first-run seed (data, by id) and the Debug gallery
  scenes (by id; the edit bar scene names styles by name). Into the SDK first, each its own commit:
  `FlipCard` with `SystemMotion`; the sky library (`Core/Astronomy`) and `SkyParts`; `RoundButtonPart`
  with `RoundButtonFace`; `ControlKey` (each widget's own key map moved with it: `StopwatchKeys`,
  `QuoteKeys`, `PomodoroKeys`); `Glyphs` public; `SkyPlaceRows` (the Sun's and Moon's place line, on their properties menus since 2026-10-19);
  `ChromeFrame` (the flyout chrome, out of `EditBar`); `ColorPreset` named by `LocalText`;
  `RecycleBinState` with the bin's target and tooltip; the power rules (`PowerButton`,
  `PowerActionRules`, `PowerFace`), `PowerButtonView` and `ButtonInset`; the ringtones
  (`PomodoroSounds`, `SoundPlan`, `AlarmSound`, `PomodoroPhase`); `LocaleFormat.Am`/`.Pm`; the strings
  generator's `Plural(locale, …)`. Contracts added: `IWidgetHost.RecycleBin` (`IRecycleBin`, the
  host's one answer and Windows' own empty) and `IWidgetHost.Power` (`IPowerBackend`: the Win32
  calls on a desktop card, inert on a preview), and the `SoundCue` seam the host installs in `App()`
  (its `winmm` player). `Glides`, `HugRow`, `RotatedBox` and `WidgetParts` stayed: no group-1 widget
  draws with them (the meters, the mixer, the gallery, the online readouts do; they move with those).
  Strings were carried over by a script that copies each value verbatim (plural objects included)
  from every one of the 30 host files into the module's file of the same language and then removes
  it from the host (a key another host part still reads, or two widgets share, was copied instead);
  every move printed the same count for all 30 languages. A behaviour fix the moves surfaced, with
  its regression test: a folder no widget of the build is at home in is left out of the add menu's
  tree (it threw once Core's catalogue held no power button). Tests: a host test that needs a real
  widget (the seed, placement, the add menu, styles, setup, alignment, search, the place rows, the
  power family) moved whole into `tests/Themia.Modules.Tests`, which Core now grants its internals;
  `ConfigJson`, `StateRoundTrip` and `ModuleSources` (which reads module projects' views too) are in
  `tests/Shared`. Tests that carried the original app's tag numbers (the pomodoro's S01..S04, which
  are the root's geometry tags here) or no SPEC line at all (Bluetooth's S240..S244) got their tags
  written down in their module's SPEC.md; each power button got one tag of its own ([S-7400] ..
  [S-8000]) and a test. Evidence: Debug build 0 warnings after every commit (three intermediate SDK
  commits also built in a scratch worktree); Core 6471 and module tests 1309 green; the pixel diff of
  every moved widget against a "before" rendered at `11b9d8c` (every tier per state, light, dark and
  German, each style, the settings page in light, dark and German, the picker, menus, edit bar,
  motion; 342 images) identical but for one motion frame that also differs between two runs of the
  "before" build (`Sides-60ms`, an animation caught mid-way). Release AOT publish 0 warnings
  (49.5 MB); Debug AOT publish 0 warnings and its renders of the clock (XAML, from its module), a
  power button armed, Bluetooth's devices and the pomodoro's settings page finish in 2-4 s,
  identical to the JIT renders.
  *Deferred from group 1:* **AudioVisualizer**: its bars come from the host's WASAPI loopback capture
  (`AudioLoopback`, COM), a host service with no SDK contract yet; it moves with the system group,
  which brings the audio contracts (`AudioFeed`/`CoreAudio`) anyway.
  *M4 part B, group 2 as built (2026-10-18): the thirteen quick-setting toggles.* Thirteen widget
  projects, one per commit (`Themia.Widget.WifiToggle`, `.BluetoothToggle`, `.AirplaneModeToggle`,
  `.EnergySaverToggle`, `.NightLightToggle`, `.RotationLockToggle`, `.DoNotDisturbToggle`,
  `.SoundToggle`, `.MicrophoneToggle`, `.SpatialSoundToggle`, `.MonoAudioToggle`,
  `.ColorFiltersToggle`, `.GameModeToggle`), each its config, descriptor (on its module), its picker
  label and description in 30 languages as `<Name>ToggleStr`, its SPEC tag of its own ([S-8200] ..
  [S-9400], one hundred each, in the order of `QuickToggleRules.All`) and its test; no settings page
  (the style is the only setting, [S-1750]), no preview of its own (the shared view's states), no
  glyph of its own (the shared view draws all thirteen from `Glyphs`). Into the SDK first, each its
  own commit: the shared rules `QuickToggleRules` (with the decoders of what Windows answers, which
  the host's sources call, so their tests stay Core's unchanged), `ToggleGate`, `ToggleFace`,
  `SwitchFace` and `PixelRect` (out of `GridGeometry`) into `Themia.Sdk`; `QuickToggleView` with its
  Switch face and preview states into `Themia.Sdk.WinUI`. Contract added: `IWidgetHost.Toggles`
  (`IQuickToggles.Acquire(QuickToggle, changed)` → `IToggleSource`: `Reading`, `SetAsync`, `Refresh`,
  dispose to let go), keyed by the closed `QuickToggle` enum, which names the host's drivers, not
  widgets; the host's `ToggleHub` answers it (`HostToggles`; `ToggleLease` is the source). The
  drivers (`Services/Toggles/*`), `ReadingOrder`/`KeyFollow` ([S-3920], [S-3921]) and
  `NightLightValue` ([S-3919]) stay host code: only the host's sources use them, and the focus
  session holds do not disturb through the hub directly. The name on the tile and the state and
  reason lines stay in the SDK's `toggles` table (the shared view reads them for every kind, as the
  power view reads the actions' names); `QuickToggleRules.PickerLabelId`/`DescriptionId` went, the
  picker label being each descriptor's. Strings were carried over by the group 1 script, 2 keys a
  widget, the same count in all 30 languages each time (26 keys a language in all), none copied.
  Tests: the four toggle tests that ask the catalogue (kinds and back, the round trip, no header,
  names and labels in every language) moved into the module test host, the label read off each
  descriptor; the rest of `QuickToggleTests` (the decoders, the gate, the look, the face) stay
  Core's, unedited; no test was retagged. Core's test count drops by 82 rows: those four, and the
  six registry theories over Core's descriptors (`WidgetRegistryTests`) for each of the thirteen,
  which the module host's conformance theories now run per module.
  Evidence: Debug build 0 warnings after every commit, both test projects built and green at the
  first move, after six and at the end; Core 6376 and module tests 1378 green; the pixel diff against a
  "before" built at `26b35fa` (every toggle at every tier in each of the seven preview states in the
  default, light and dark themes and German; each style, Switch included, on, off, unavailable and
  under the pointer in both themes and on in German; the settings page in light, dark and German; the
  picker plain, German and searched, menus, edit bar in both themes, motion, Settings › Widgets and a
  settings search: 834 images) identical but for two images that also differ between runs of the
  "before" build: `Sides-60ms` (an animation caught mid-way, as in group 1) and the menu scene's
  `card-desktop-no-settings` (its first icon caught fading in; two "before" runs and two "after" runs
  each gave both variants). Release AOT publish 0 warnings (49.6 MB); Debug AOT publish 0 warnings and
  its renders of the night light toggle's Switch style, the microphone toggle off and the picker
  searched for "toggle" finish in 2 s, identical to the JIT renders. Manual passes owed: each toggle
  switching on a real desktop (never in a render or a test), as before.
  *M4 part B, group 3 as built (2026-10-18): the online widgets but RSS.* Six widget projects, one per
  commit (`Themia.Widget.RainNextHour`, `.AirQuality`, `.ExchangeRates`, `.WeatherNow`,
  `.WeatherForecast`, `.Stocks`), each its config (styles and enums with it), descriptor (on its
  module), view, preview, settings page (`<Name>Settings.Rows`; the weather family's place row with its
  clear button, [S-2848], through the SDK's `WeatherPlaceRows`), its own ladder, words and rules, its
  strings as `<Name>Str`, its glyph where only it drew one (air quality, exchange rates, the forecast),
  its SPEC section and its tests. Into the SDK first, each its own commit: the answers and their
  parsers the host's services produce and the views draw (`WeatherData`, `WeatherReport`, the air
  reading and wire, `RainNowcast` with its rules, which the host's request reads, `ExchangeRates` with
  `ExchangeRules`, which the ECB parser checks codes by, `TickerQuote`, `MarketQuote`, `ExchangeTime`,
  `QuoteMarket`/`QuoteRange`) and the parts they draw with (`HeroLayout`, `PartMetrics`, `Sparkline`,
  `GaugeLayout`); `IInterests` and `InterestsBuilder` with the online interests (`WeatherInterest`,
  `QuoteInterest`, `AirInterest`, `RainInterest`, `RatesInterest`, `OnlineCadence`; the builder keeps one
  typed list per interest kind, `Of<T>()`, so it names no source and Core's `SyncInterests.From` reads
  back the kinds the host serves; the weather rule is `WeatherInterests.For`, the stock, air and rain
  rules their configs' `Interest()`); the view parts (`HeroReadout`/`StatPart`/`WidgetParts`,
  `OnlineParts` with `InlineAge`, `GaugeDial` with `MeterBrushes` and the glides: `Glides` into
  `Themia.Sdk.WinUI`, `Glide`, `OkLab`, `SystemCadence`, `MeterPressure`, `FactStyle` into `Themia.Sdk`,
  the user's Smooth switch staying the host's `GlideSwitch` behind `GlideMotion.InstallSmooth`),
  `WeatherPlaceRows`, `WeatherSample` (the weather previews' one fixture), and later `WeatherGlyph`
  (the condition's glyph both weather widgets draw), `ReadoutLayout` and `LocaleFormat.GroupSeparator`
  (public, for the holding's parser). The shared online files were split per widget first
  (`AirLayout`/`AirText`, `RainLayout`/`RainText`, `RatesLayout`/`RatesText`; the measures the three
  share stay `OnlineLayouts` in the SDK). Contracts added: `IWidgetHost.Weather` (`IWeatherAnswers`:
  the conditions, forecast, air and rain), `.Quotes` (`IQuoteAnswers`), `.Rates` (`IRateAnswers`),
  each a `Fetched<T>` (now the SDK's) per want, the key it is synced under, `Show`/Refresh and
  `Updated(key)`, shaped on what the views called on `WebSync`; the host's `HostWeatherAnswers`,
  `HostQuoteAnswers`, `HostRateAnswers` delegate to `AppServices.WebSync`, or where it is not running
  (a debug render) to the services' caches, as the views did; and the `SymbolLookup` seam (the Stocks
  settings' search, the host's `MarketService.SearchAsync` installed in `App()`). `WebSync`,
  `AnswerSync`, the services, their caches and the vault shelves stay host code. Strings were carried
  by the group 1 script, the same count in all 30 languages each time: rain 13, air quality 43 (39 and
  the four `$englishOnly` units), exchange rates 14, weather now 17, the forecast 13, stocks 35 (each
  style area emptied went: `styles-air`, `-rates`, `-weathernow`, `-forecast`, `-stocks`); the source
  line, the weather tooltips, the place header, the WMO conditions and the quote's phase, zone and
  asset words stay the SDK's (shared, or read by SDK code). Tests: each widget's own into its
  `Tests/`; the SDK rules' (wires, nowcast, cross rates, quotes) stay Core's under their root tags; the
  side-by-side ones and the host rules a weather widget is the example of into the module test host
  (`OnlineFamilyTests`, `OnlineStylesTests`, `OnlineHostTests`, `SyncHousekeepingTests`,
  `ConfigSnapshotTests` whole); shared fixtures `OnlineReplies` and `StyleTiers` in `tests/Shared`; a new
  fixture test (`OnlineStateFixtureTests`) reads a state of the six widgets configured (places, styles,
  the index, the pair, the instrument with holding and colours, Fahrenheit), generated with the build
  from before at `011bda01`, with no repair, the same values, and writes it back unchanged. One retag:
  `S1100_the_readings_are_labelled_in_the_language_asked_for` is the weather module's `S722_` (a module
  test may only name its own tags); other tests changed only the call shape (`WeatherInterests.For`,
  `config.Interest()`, `new WidgetId("…")` for the descriptors that left Core, `AirLayout.Air` and the
  like). The [S-1116] allow list lost `stocks.extendedHoursDesc` (no longer the host's): the module host
  does not run that rule over module tables yet (left open). Core's test count 6043 (from 6376), the
  module host's 1732 (from 1378).
  Evidence: Debug build 0 warnings after every commit, both test projects built and green at the
  interests commit, after the view parts and after every move, and at the end; each move's render set
  compared at once against a "before" rendered at `011bda01` (0 differ each); the final pixel diff of
  306 images (every tier in each preview state and the empty one, light, dark and German, the fill
  sizes 120x80..1400x360, each style in each state in both themes and German, `--align`
  left/center/right where offered, `--place-cleared` tiers, the settings page plain, `--place-chosen`
  and `--place-cleared` in light, dark and German; picker plain, German and searched, menus, edit bar
  in both themes, motion, Settings › Widgets and searches) against the "before" build, rendered back to
  back, identical but for `Sides-60ms` and `card-desktop-no-settings`, which also differ between the
  "before" build's own runs (as in groups 1 and 2). Release AOT publish 0 warnings (49.6 MB); Debug AOT
  publish 0 warnings and its renders of weather now's tiers, a stock quote and the air quality page
  with a place finish in 4 s, identical to the JIT renders. Manual passes owed: each widget against its
  live service (a fetch, Refresh, offline with a restored shelf, a place in and outside Europe for the
  air index, a rainy hour, an ECB publication day and a weekend, a listed symbol through a session and
  its extended hours, a coin, the symbol search), and the region read in the packaged build.
  *M4 part B, group 4 as built (2026-10-18): the system widgets.* Twenty-one widget projects, one per
  commit: the seven meters (`Themia.Widget.Cpu`, `.Memory`, `.Battery`, `.Network`, `.Disk`, `.Ping`,
  `.Gpu`), the seven detail widgets (`.CpuDetails`, `.MemoryDetails`, `.TopProcesses`, `.NetworkDetails`,
  `.BatteryDetails`, `.PingDetails`, `.GpuDetails`), `.Drive`, `.RemovableDrives`, `.Brightness`, `.Mixer`,
  `.Volume`, `.Music` and `.AudioVisualizer` (deferred from group 1), each its config (styles and enums
  with it), descriptor (on its module), its part of the shared view or its own view, preview where it
  has one, settings page (`<Name>Settings.Rows`; none for the CPU, memory and battery details, Removable
  drives and the visualizer, as before, [S-1750]), strings as `<Name>Str`, its SPEC section and tests.
  First, each its own commit: a fix the user asked for, test first (the module test host runs the
  [S-1116] native-digits rule over every module's table, with the modules' allow list by widget and key;
  `stocks.extendedHoursDesc` is back on it). Then the SDK contracts, each an `IWidgetHost` member shaped
  on what the views called, the COM, WASAPI, WinRT and Win32 staying host code behind them:
  `System` (`ISystemFeeds`: `Watch(SourceId<T>, onSample, target)` → `IFeedWatch<T>`, `WatchSeries` →
  `ISeriesWatch`; the host's `SystemHub` implements it; null only before the host has started it, as
  `AppServices.System` was), with the hub's data into `Themia.Sdk` first (`FeedKey`, `SeriesPoint` and the
  history, `CoreHistory`, `MeterReading`, `SourceId` with the `Sources` table and its snapshots, the
  rules those snapshots carry, `SystemInterests` with `IReadsSystem` on each system config, so the
  host's switch over config types went and Core keeps `SystemInterests.From(state)` as a C# 14 static
  extension, its callers unchanged), `WatchSlot` and `SystemDrive.Root`; `Drives` (`IDrives`, the one
  `DriveService`); `Audio` (`IAudioOutputs`: subscribe, level, mute, the default-output switch; the
  shared `AudioFeed` over it); `Brightness` (`IBrightness`; `DeviceOf` added with the brightness move);
  `Media` (`IMediaSessions.Open` → `IMediaSession`); `Loopback` (`IAudioCapture`, the visualizer's
  capture). What a settings page lists from the host is a seam installed in `App()`, as `SymbolLookup`
  is: `DriveList`, `AudioDevices`, `GpuAdapters`; so are a process's executable (`ProcessPaths`) and a
  row's shell icon (`AppIconSource`, `ThemeOf` with it). The shared parts into the SDK, public,
  namespaces kept: `MeterView` with its looks, faces and preview states, `MeterPart`/`MeterItemsWatch`,
  `BarList`/`TileGrid`, `CoreGraphGrid`, `SparkBand` (`SparkScroll` with the data), `MeterMenuLines` (was `MeterSettingsRows`),
  `GaugeConfig`/`MeterTiming`; `DetailView` and `DetailPart` (the sample state asks the part for its
  fixture sheet, `DetailPart.SampleSheet`, instead of naming the battery details), `DetailFace`,
  `FolderRealize`; the pairs' parts: `DriveIcon`/`DriveBar`, `AudioLook`, `AudioDeviceRow`,
  `GpuAdapterLine`, `PingHostLine` (were the page rows) with `HostAddress.DefaultPingHost`. Widget-only rules went with their
  widget (`PingWindow` to the ping details, `VisualizerLayout` and `SpectrumCanvas` to the visualizer,
  `HugRow` to the mixer); Core's `DetailConfigs.cs`, `DriveConfigs.cs`, `WidgetSettings.Details.cs` and
  the app's `Widgets/Meters`, `Widgets/Details` went with the last of their widgets. The recent
  behaviours are as they were (the renders below): the graphs' scroll one sample behind with the new
  point off-screen, the CPU's core graphs, the glides and the Smooth switch, the virtualized process
  list, the history caps, the mixer without a level meter, the disk with its header and no drive in
  its body. Strings were carried by the group 1 script, the same count in all 30 languages each time,
  163 keys a language in all: CPU 7, memory 7, battery 3, network 3, disk 8, ping 3, GPU 11, CPU
  details 2, memory details 2, top processes 11, network details 4, battery details 2, ping details
  12, GPU details 3, Drive 10, Removable drives 4, Brightness 9, Mixer 22, Volume 11, Music 20, the
  visualizer 9 (style areas emptied went: `styles-cpu`, `-memory`, `-gpu`); what two of them read (the
  meters' and details' shared words, `styles-io`'s Graph and Ring, the drive and device rows' words)
  stays the SDK's table. Tags: a tag whose tests test SDK rules stayed in the root under its tests,
  the config's tests under it moved into the module test host (`MeterHostTests`, `IoStylesHostTests`,
  `DiskHostTests`, `PingHostTests`, `DetailHostTests`, `DriveHostTests`, `WindowsPanelsHostTests`,
  `AudioHostTests`, `MusicHostTests`, `ItemKindsHostTests`), and the widget's own tags moved into its
  `SPEC.md`; a widget left with none of its own got one with a test ([S-9500] CPU details, [S-9600]
  memory details, [S-9700] top processes, [S-9800] battery details). No test was retagged; one changed
  its body: the GPU meter's `S433_a_new_meter_shows_the_first_adapter…` kept the meter's default, the
  details' default it also asserted moved to the GPU details' `S2431_` (a module test may not name
  another widget's config). Other tests changed only the call shape (`new WidgetId("…")` for the
  descriptors that left Core, `WidgetRegistryTests` over two descriptors still in Core). Found on the
  way and fixed in the moves' commits: the module host's items rule ([S-6570]) now follows SDK parts
  built of painting parts (the meters' shared view lays out the bars and tiles that paint); three Core
  tests that read two real widgets of the first-run desktop moved into the module host
  (`SettingsPageSyncHostTests`, `ItemTextHostTests`), the seed's meters being projects. A new fixture
  test (`SystemStateFixtureTests`) reads a state of all twenty-one configured, generated with the
  build from before at `d7dd3ae7`, with no repair, the same values, and writes it back unchanged.
  Core's test count 5780 (from 6043), the module host's 2024 (from 1770).
  Evidence: Debug build 0 warnings after every commit; both test projects
  built and green after every move and at the end; each move's render set compared at once against
  a "before" rendered at `d7dd3ae7`, and the final pixel diff of 698 images (every tier in each preview
  state, light, dark and German, the fill sizes 120x80..1400x360, each style in each state in both
  themes and German, the meters' `--tween 0.4`, `--progress 0.5` and `--span 30|300|3600` stills and a
  header-less tier, the settings page plain, dark, German and per style; picker plain, German and
  searched 17 ways, menus, edit bar in both themes, motion, Settings › Widgets and five searches)
  against the "before" build: 0 differ (the "before" rendered twice differed only in `Sides-60ms`, an
  animation caught mid-way, as in groups 1-3; it matched this time). Release AOT publish 0 warnings
  (49.9 MB); Debug AOT publish 0 warnings (82.8 MB, before 82.3 MB) and its renders of the CPU meter
  (sample, core graphs), the disk meter, the visualizer and the mixer's page finish in about 2 s,
  identical to the JIT renders. Memory: the Debug AOT `--switch-check --busy` desktop (the system
  widgets, galleries and visualizer on two stand-in displays) sampled at 30, 60 and 90 s, twice each:
  153/153/153 and 154/154/154 MB private before, 153/153/153 MB twice after. Manual passes owed: each
  module's own `SPEC.md` names it (the meters and details on a live desktop, a laptop's battery, two
  GPUs, USB drives and shares, a DDC/CI monitor and a laptop panel, the mixer and volume moving real
  levels, a media player, the visualizer with music), never in a render or a test, which are inert.

  *M4 part B, group 6 as built (2026-10-18): the local-store widgets.* Ten widget projects, one per
  commit: `Themia.Widget.Clipboard`, `.Alarm`, `.FocusSession`, `.Notifications` (Windows' list behind a
  host service no other group claimed), `.Notes`, `.HabitTracker`, the three screen-time widgets
  (`.ScreenTimeApps`, `.ScreenTimeDonut`, `.ScreenTimeChart`) and `.Gallery`, each its config (styles and
  enums with it), descriptor (on its module), view, preview, settings page (`<Name>Settings.Rows`; none
  for Notifications, as before, [S-1750]), its own ladder where it had one in a shared rules file
  (`FocusSessionLayout`, `NotificationsLayout`, `ScreenTimeAppsLayout`, `ScreenTimeDonutLayout`,
  `ScreenTimeChartLayout`; Notes, the habit tracker and the gallery took theirs whole: `NotesLayout`
  with the markdown editor and its rules, `HabitLayout`/`HabitDrag`/`HabitHeatmap`,
  `GalleryArrangement`/`GalleryPlan`/`StickerOutline`), its strings as `<Name>Str`, its glyph where only
  it drew it (clipboard, alarm, focus, notifications, habit tracker, gallery), its SPEC section and its
  tests. Contracts first, each its own commit, an `IWidgetHost` member shaped on what the view called,
  the store and its persistence staying host code behind it: `ClipboardHistory` (`IClipboardHistory`,
  with `CopyText` for putting a row back), `Alarms` (`IAlarms`; `IAlarm` on the config, so the host's
  book and slots name no widget, the slots' collection Core's as an extension), `Focus` (`IFocusSession`;
  do not disturb stays held host-side through the quick-setting hub), `Notifications` (`INotifications`),
  `Notes` (`INotes`), `Habits` (`IHabitLists`; the host pool lost its in-memory mode, the preview holds
  its own lists), `ScreenTime` (`IScreenTimeSource`, public) and `Gallery` (`IGalleries` with
  `IGalleryWatch` and `IGalleryPicture`). A settings page has no card, so what it reads of the host is
  `SettingsHost`, installed at start (the notes, the habit lists, the screen-time history, the launcher a
  page opens a folder through, and `OwnPicker`, the settings window a page's file picker belongs to), as
  `DriveList` is; `ProcessPaths` gained an executable's display name (the screen-time sample's packaged
  apps). Into the SDK, the data the host's stores and the views share and the parts two widgets share,
  each its own commit: `ClipboardHistory` (with its entry and formatting), `ColorFormats`, `AlarmRules`,
  `FocusSessionRules`, `NotificationRules`, `NoteMeta`/`NoteRead`, `HabitLists` with `HabitRules`,
  `HabitPeriods` and `LocalDay`, `AddFooterRow`/`AddFooter` (the habit tracker and the task list),
  `ScreenTimeDay`/`ScreenTimePreferences`/`ScreenTimeRules`/`ScreenTimeLayout` (the measures the three
  share) and the configs' shared settings, the three screen-time widgets' shared view parts
  (`ScreenTimeShared`), their preview fixture (`SampleScreenTime`) and settings rows
  (`ScreenTimeSettingsRows`), `PackagedApps`, `StickerRules`, `RotatedBox` with `StreamRotation` (the
  gallery and the video stream), `ControlKeys`, `ImageFiles`, `FolderFormatting` with `FolderEntry`,
  `FolderBrowse` (the folder's and the gallery's pages), `GallerySource`, `GalleryMemory` (the source key
  with the rescan interval), `GalleryStates`, `BingPhoto`; and `StateJson.Merged` (a config fragment over
  a default, out of the first-run desktop, so a debug scene names the gallery by id). The stores and
  files stay Core's and the host's, with their validation and tests: `NoteStore` (`notes/*.md`,
  `index.json`, the rename retry of [S-49]), `HabitListStore` (`habits.json`), `ScreenTimeStore`
  (`usage_stats.json`), the clipboard history in memory only, the alarms in the state. Recent behaviours
  are as they were (the renders below): the notes' focused text box with no fill or accent line, the
  habit tracker's rows, the clipboard history's caps. Strings were carried by the group 1 script, the
  same count in all 30 languages each time, 232 keys a language in all: clipboard 15, alarm 18, focus 15,
  notifications 14, notes 41, habit tracker 56, screen-time apps 11, donut 3, chart 8, gallery 51 (the
  style area `styles-habits` emptied went); what two of them read (the screen-time widgets' shared
  words, the alarm's snooze and dismiss the host's notice reads) stays the SDK's table. Tags: a tag
  whose tests test SDK or host rules stayed in the root under its tests, the config's tests under it
  moved into the module test host (`ClipboardHostTests`, `AlarmTests`, `WindowsPanelsHostTests`,
  `HabitHostTests`, `ScreenTimeHostTests`, `GalleryHostTests`, `GalleryPersistenceTests`,
  `ItemKindsHostTests`), the widget's own tags into its `SPEC.md`. No test was retagged; tests changed
  only their call shape (a ladder's new class, `GallerySourceKey.RescanInterval`, `new WidgetId("…")`,
  the registry test over two descriptors still in Core, RSS and Tasks). Found on the way and fixed in
  the moves' commits: the [S-1500] dialog scan read module settings pages as views (a page may ask in a
  dialog; `RepoFiles.IsSettingsPage`), and the notes editor's Latin-digit allowance moved to the module
  host's list. New fixture tests of the users' data, generated with the build from before at `d02d36bf`
  (made-up content): `LocalStoreFixtureTests` (a notes folder with mixed line endings and an empty note,
  `habits.json`, `usage_stats.json`: each loads with no repair and writes back byte for byte) and
  `LocalStateFixtureTests` (every local-store widget configured: no repair, the same values, written back
  unchanged).
  Core's test count 5377 (from 5780), the module host's 2423 (from 2024).
  Evidence: Debug build 0 warnings after every commit (the one SDK commit committed apart from its move,
  `71dde8c2`, also built alone in a scratch worktree); both test projects built and green after every
  move (scoped) and in full at the notifications move and at the end; each move's render set compared at
  once against a "before" rendered at `d02d36bf` (0 differ each but one clipboard popup, below), and the
  final pixel diff of 482 images (every tier in each preview state, light, dark and German, the fill
  sizes 120x80..1400x360, each style in each state in both themes and German, the `--keys` renders of
  the nine widgets that take keys, the settings page plain, dark and German; picker plain, German and
  searched nine ways, menus, edit bar in both themes, motion, Settings › Widgets and six searches)
  against the "before" build: 479 identical, 3 differ and each was proved flaky by rendering alone:
  the clipboard's `tiers-confirm-light-popup0` and the menu scene's `card-desktop-no-settings` (the
  "before" was rendered four at a time and caught a fade; three runs of the "before" build alone and the
  final renders are identical) and `Sides-60ms` (the "before" build's own runs give different frames,
  as in groups 1-4). Release AOT publish 0 warnings (50.0 MB, before 49.9 MB); Debug AOT publish 0
  warnings (83.0 MB, before 82.8 MB) and its renders of the habit tracker, notes being edited, the donut
  hovered, the alarm's page, the clipboard's question, a gallery grid, the notification list and the
  focus session holding do not disturb finish in 1-14 s, identical to the JIT renders. Found and left as
  it was: under Debug AOT only, a render of the gallery's `--state sticker` and the drawing of the
  clipboard's `--state confirm` popup never finish, in the build from before as in this one (JIT draws
  both); not a module question. Manual passes owed: each module's own `SPEC.md` names it (typing in two
  notes widgets, habits ticked in two trackers and the data folder, real clipboard traffic, an alarm
  ringing with its notification, a focus session with do not disturb, real toasts, a tracked week of
  screen time, a folder of photos and Bing's photo, the settings pages' pickers), never in a render or a
  test, which touch none of the user's stores.

  *M4 part B, group 7 as built (2026-10-18): files and launchers.* Four widget projects, one per commit:
  `Themia.Widget.RecentFiles`, `.VideoStream`, `.Shortcuts` and `.Folder`, each its config (styles and
  enums with it), descriptor (on its module), view, preview, settings page (`<Name>Settings.Rows`; none
  for Recent files, as before, [S-1750]), its own rules (`RecentFilesLayout`; `StreamChromeRules` with
  `FrameRateMeter`, and `ShownWatch`; `ShortcutRules`, `ShortcutDrop`, `ShortcutLayout`,
  `ShortcutSelection`; `FolderColumns`, `FolderFilter`, `FolderGesture`, `FolderHistory`, `FolderLasso`,
  `FolderLayout`, `FolderMenu`, `FolderRules`, `FolderSelection`), its strings as `<Name>Str`, its glyph
  where only it drew it (Recent files, the camera, Shortcuts), its SPEC section and its tests. The
  widget ids left in Core and the app are RSS and the account widgets (Email, Calendar, Contacts,
  NextMeeting, WeekPlanner, Tasks). First, its own commit and Debug tooling only: the Debug Native AOT
  build preinitialises static constructors as Release does (`--preinitstatics` in the app's csproj);
  without it the runtime's reference-tracking callback (`TrackerObjectManager.WalkExternalTrackerObjects`
  → `FindReferenceTargetsCallback`) ran its class constructor inside the first garbage collection that
  found a XAML object, the constructor runner allocated, the allocation waited for the collection in
  progress, and the process hung for good with every thread waiting (found with a native stack dump of
  the hung process; seen as the gallery's `--state sticker` and the clipboard question's popup never
  finishing, both allocating enough to collect while the page is alive). Then the SDK, each its own
  commit: the parts the widgets share with the host (`FolderBadges`, `FileNames`, `FileDrops`,
  `SurfaceDrops`, `ItemClickMode`, `InstalledApp`, `RecentFile` with `RecentFileRules`, `VideoStreamRules`
  with `StreamTransport`, `StreamFit`, `HlsVariants`, the app layouts `AppLayoutModel`, `LayoutTree` and
  `LayoutPlan`, `SiteIcons.FaviconOf`; the shared files split per widget first: `RecentFilesLayout`,
  `StreamChromeRules`, `LayoutWindows` and Core's `DropUrls` stayed apart), and the contracts, each an
  `IWidgetHost` member over the host's one service, the Win32, COM, OLE and sockets staying host code:
  `FileIcons` (`IFileIcons`), `FileClipboard` (`IFileClipboard`), `Folders` (`IFolders`: the listing, the
  watch, the repository's status, Explorer's New file types, a ZIP; `FolderWatch` is the `IFolderWatch`),
  `FileOps` (`IShellFileOps`: copy, move, recycle and rename with the shell's dialogs and undo, open, Open
  with, Properties, Show in Explorer, new folder and file, cloud pins, a shortcut's target, a drag out),
  `ExplorerMenus` (`IExplorerMenus`: the full shell menu, a verb, a file's Open with apps), `RecentFiles`
  (`IRecentItems`), `AppLayouts` (`IAppLayouts`, and `SettingsHost.AppLayouts`; the host's editor finds
  a layout item through `IHoldsAppLayouts` on the config, so it names no widget), `Streams`
  (`IVideoStreams` with `IStreamSink`), `InstalledApps`, `Favicons`, and `IWidgetDrag.ReadShellItems`;
  the records they carry went into the SDK with them. The Debug scenes name the four by id
  (`StateJson.Merged` fragments for the switch check's camera and the layout editor's demo layout), and
  `--menu-png` draws the folder's menus through the module's preview (`IWidgetPreviewMenus`, new).
  Recent behaviours are as they were (the renders below): the folder's lasso, Explorer's menu with its
  two-tone icons, light dismiss, the right column visible, edit mode inert, double-click and Enter
  opening. Strings were carried by the group 1 script, the same count in all 30 languages each time,
  184 keys a language in all: Recent files 5, the camera 45, Shortcuts 56, Folder 78 (the `folder-menu`
  and `styles-folder` areas emptied went); what two of them, the host's layout editor or its file
  services read (the folder's sizes and Select all, the shortcut kinds, Open file location) stays the
  SDK's table; the native-digit allow list's two MP4 keys are the camera module's now. Tags: a tag
  whose tests test SDK or host rules stayed in the root under its tests (the Recent Items source, the
  camera's title, backoff, rotation, refresh, transports and HLS cap, the app layouts, the folder's
  search, columns, icons, git, selection keys, clipboard, operations, drops and watch), the module
  code's tests under it moved into the module test host (`DeskInfoHostTests`, `VideoStreamHostTests`,
  `ShortcutsHostTests`, `FolderHostTests`, and whole `FolderKindsTests`, `LauncherPersistenceTests`,
  `FillChoiceTests`, `ItemTextTests`, two `SettingsPageSync` tests that read real widgets of the
  first-run desktop); the Recycle Bin item's Empty ([S-4864]) moved with Shortcuts. No test was
  retagged or renamed; the bin's own rule test left `ShortcutsTests` for a new Core
  `RecycleBinStateTests`; tests changed only their call shape (`DropUrls.InText`,
  `SiteIcons.FaviconOf`, `new WidgetId("…")`). Found on the way and fixed in the moves' commits: the
  module host's round-trip conformance now lets a clone mint fresh nested ids (a Shortcuts clone gives
  its items new ids, [S-893]); the Recent files view still opened its folder through the shell directly
  (now `IWidgetHost.FileOps`). A new fixture test (`FilesStateFixtureTests`) reads a state of the four
  configured (three folders in their styles with their columns, a Shortcuts list with every kind of item
  and an app layout, a table, a camera with every setting changed and a fresh one), generated with the
  build from before at `c11f637e` in a worktree, with no repair, the same values, and writes it back
  unchanged. Core's test count 5026 (from 5377), the module host's 2772 (from 2423).
  Evidence: Debug build 0 warnings after every commit; both test projects built and green after every
  move and at the end; each move's render set compared at once against a "before" rendered at
  `c11f637e` (0 differ each), and the final pixel diff of 273 images (every tier in each preview state,
  light, dark and German, the fill sizes 120x80..1400x360, each style in each state in both themes and
  German, the `--keys` renders of the four, the settings page plain, dark, German and per style; picker
  plain, German and searched eight ways, menus in both themes with the folder's file, Open with, folder
  and background menus, the layout editor plain and empty, edit bar in both themes, motion, Settings ›
  Widgets and four searches) against the "before" build: 271 identical, 2 differ
  (`menu-dark/card-desktop-no-settings`, `motion/Sides-60ms`), both proved flaky by rendering alone:
  two runs each of the "before" and the final build alone are identical to each other and across.
  Release AOT publish 0 warnings (50.1 MB, before 50.0 MB); Debug AOT publish 0 warnings (82.3 MB,
  before 83.0 MB: the preinitialised statics) and its renders of the folder (web and the lasso), its
  page, the menus, Recent files, Shortcuts and its page, the camera and its page, the layout editor,
  the gallery's sticker and the clipboard's question with its popup finish in 2-26 s, identical to the
  JIT renders. Manual passes owed: each module's own `SPEC.md` names it (a real folder with the lasso,
  Explorer's menu, rename, delete, drag in and out, the clipboard with Explorer, a ZIP, OneDrive pins;
  opening files to fill Recent files and Remove from list; Shortcuts' drops from Explorer, browsers and
  the Start menu, reorder, drag out, a layout launched and edited; a real MJPEG, snapshot, MP4 and HLS
  camera), never in a render or a test, which touch no real file.

  *M4 part B, group 8 as built (2026-10-18): the account widgets.* Six widget projects, one per
  commit: `Themia.Widget.NextMeeting`, `.Contacts`, `.Calendar`, `.WeekPlanner`, `.Email` and `.Tasks`,
  each its config, descriptor, view, preview, page, its strings as `<Name>Str` (the same count in all 30
  languages: Next meeting 5, Contacts 22, Calendar 30, Week planner 8, Email 36 after the accounts row
  below, Tasks 85; the `styles-calendar` and `styles-email` areas emptied went), its SPEC section and
  its tests. Before them, each its own refactor commit: the shared data into the SDK (the office model
  and rules, the mail model and links, the task model, due dates and rules, `SyncProblem`,
  `SyncFreshness`, the account interests, `HostProviders`' ids, `AccountError`, `AccountInfo`,
  `CalendarConfig` with its enums, later `TaskEdits`, `LocalTaskLists`, `AccountException`,
  `TaskLayout` and `TasksStyle`), and the contracts, each an `IWidgetHost` member over the host's one
  service, the sync services, IMAP, MIME, Graph and Google staying host code: `Office` (`IOfficeSource`:
  calendars, contacts, meetings, `Synced`), `Mail` (`IMailSource` with its feed, `MailScreens`, the
  snapshot; `MailLimits.PerFolder`), `Tasks` (`ITaskSource`, `ITaskFeed`, `ILocalTaskLists`), and on the
  settings side `SettingsHost.Office`, `SettingsHost.Tasks`, `SettingsHost.AccountsWith(feature)` and
  the SDK's `OfficeSettingsRows` (the accounts, each account's calendars and colours, birthdays). A
  fixture test (`AccountStateFixtureTests`) reads a state of every account widget configured,
  generated with the build from before (`e0d31a3f`), with no repair, and writes it back unchanged.
  With Tasks the last widget left the app and Core: `WidgetConfigJsonContext`, the app's
  `WidgetSettings` and `Widgets/Modules` went, Core's tests run over a registry of none (a host with
  no widget reads and writes its state, [S-6903]; Core's registry checks over every widget moved to
  the module host's `WidgetRegistryHostTests`), and the registry generator reads only the referenced
  assemblies (THEMIAMOD005 for a module the host declares itself, [S-6901]). One kit decision applied
  as a behaviour commit, test first: the mail page chooses its accounts with `KitRows.Accounts`
  ([S-7063]), which changed four translated sentences in es, ja, pl and zh to the calendar pages'
  words. Evidence: Debug build 0 warnings after every commit; both test projects green at each move
  (Core 5161, module host 3361 at the end); each move's renders interleaved before / after / before
  again against `e0d31a3f` (`pngdiff --noise`): Next meeting 0 of 18, Contacts 0 of 8, Calendar 17 of 78
  and Email 20 of 69 by text antialias only, Week planner 1 of 13 by antialias, Tasks 2 of 47 by a
  scroll fade that came and went under three parallel renders (both builds rendered alone twice:
  identical); Release AOT publish 0 warnings (50.8 MB), Debug AOT publish (83.7 MB) and its renders of
  the six widgets, the agenda style and the six pages finish.

  *Kit migration of the account widgets: Email, the agenda and Tasks done (see "account widgets on
  the kit" in 11.2).* They moved with their views as they were. What each needs, as found: Email the kit's row looks beyond `Rich` (a
  compact two-line row and the Table look with its column header and user-ordered columns), inline
  hover actions (mark read, delete) on a row, the compose button in `KitHeader`, and `MailScreens`
  folded into `KitScreens` (its sentences are mail's own: a behaviour commit, test first); the
  calendar's agenda `KitOverflow.Page` with day groups and two columns (`CalendarFit`'s agenda rules
  into a `KitSpec`), `KitStatePanel` for the office states; Tasks a toggle lead, the inline rename
  editor, drag reorder and the add footer in `KitList`, and `RowFocus` in place of XAML focus (likely
  visible: the focus rectangle). Contacts, Next meeting and Week planner stay off the kit (not lists of
  a source's items in the kit's sense). Each is a refactor with a zero pixel diff or a split behaviour
  commit, as RSS was.

  - **Online, left:** none (RSS moved with its kit migration, 11.2).
  - **Account widgets:** done (group 8, above); their kit migration is open.
  - **Local stores:** done (group 6, above); each became a typed `IWidgetHost` member, no
    `IModuleService` was needed (every store is the host's one already, created at start).
  - **Files and launchers:** done (group 7, above); every host service they used is a typed
    `IWidgetHost` member, and the app-layout editor finds its item through `IHoldsAppLayouts`.
  - **What remains:** no widget is left in the app or Core; the account widgets' kit migration.
  - **Everywhere:** the Debug gallery scenes that name widgets (`EditBarRender`, `SwitchCheck`,
    `MotionRender`, `MenuRender`, `SettingsRender`) turn into ids and module `Preview/` states; the
    descriptor/config interim ends per widget; `ItemPalette` (the host's item engine) is reached only
    through `ItemFill`; the generator reads no attributes of the app's own (done, group 8).

### 11.2 The kit

Order, smallest risk first (after M1..M3; M4 moves interleave):

1. **Groundwork (refactors).** `RepoFiles` test helper; `KitConformanceTests` with today's allow
   lists. The key maps are not merged here: they differ at one edge (section 5.3), so the merge
   rides with the kit's key map and RSS's migration, as its own behaviour commit.
2. **SDK kit.** `KitItem`, `KitAction`, `KitMenu.Lines`, `KitKeys`, `KitLadder`, `KitScreens`,
   `KitFreshness`, `IKitConfig` with the `WidgetSetup`/`SyncInterests` arms; tested; no consumer yet
   beyond tests (new code, not a second path).
3. **WinUI kit with its first consumer: RSS.** `KitList`, `KitHeader`, `KitStatePanel`, `KitMenu`,
   `KitBinding` land in `Themia.Sdk.WinUI` together with RSS on them (and RSS moves into
   `src/Widgets/Themia.Widget.Rss/`, M4); `FeedLayout` becomes RSS's `KitSpec`; `RssView.Rows`
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
   Weather/Stocks say staleness the same way: the problem glyph with one tooltip line per owner,
   the age on the Refresh menu line, no caution-coloured age in headers (section 5.5; decided, Q3).
8. **Behaviour: one open gesture** (decided, Q1/Q2): a click focuses, a double-click or Enter
   opens; Recent files and Notifications follow (and Recent files drops its own `EditMode` check,
   [S-1751] already covers it).
9. **Behaviour: the one failure vocabulary** (tests first): `FetchStatus` gains `SignInRequired`,
   `Forbidden`, `NotFound`; `AccountException.ToFetchStatus` stops folding them; their sentences.
   Mail, Tasks and the Office widgets say "sign in again" or "not allowed" where they said a generic
   problem: a visible change, with its SPEC lines.
10. **Service core, no service yet.** The strategy sets and the descriptor record, `ServicePager`
    (with `LinkPaging`), `ServicePolling`, `RateLimitScheme` with `RateLimitRules` reading it (its
    GitHub constants become the first scheme in a test; the existing rate-limit tests move to that
    scheme unchanged in what they assert), `ErrorScheme` with `ServiceFailures.Classify`, the
    GraphQL envelope, the `FormPost` transport, the replica contract (`IServiceReplica`), `KitMerge`.
    Tested on synthetic descriptors that mimic each of the three
    pilots' recorded replies, so a GitHub-shaped assumption fails a Linear-shaped test here, before
    any service exists.
11. **Sign-in by strategy (refactor for Microsoft and Google).** `ServiceSignIn` and a generic
    Accounts page add flow; Microsoft and Google get descriptors with `OAuthPkceLoopback`
    (`Localhost`/`Any`, no secret; `Loopback4`/`Any`, `BuildTime`); the ternaries go; the loopback
    listener takes a port list; `THEMIA_CLIENT_SECRETS` replaces `THEMIA_GOOGLE_CLIENT_SECRET`
    (`CLAUDE.md`, `docs/STORE.md`); `OAuthDeviceCode` (`DeviceCodeSession` restored from `e09beef^`
    with its tests), `PastedToken` and `AccountAuth.Token` land with tests and no provider yet. Zero
    visible change for the two existing providers; a live sign-in pass for both.
12. **`ServiceSync` and `ServiceClient`** with the `service` shelf kind (validation, purge, sweep)
    and replica entries.
13. **GitHub**, the first pilot (section 13), behind the user's answers to Q4, Q6 and Q7 (Q5 is
    decided): `src/Providers/Themia.Provider.GitHub/` (descriptor, wire, rules, queries, fixtures),
    then one widget project per commit. The GitHub App registration (Lometics) happens here.
14. **Linear**, the second pilot: its provider project, then its widget projects. Any line it needs
    outside its own folders is an SDK defect: fixed in the SDK first, in its own commit.
15. **Todoist**, the third pilot (Q11, Q12 decided): its provider project on the Sync API (replica,
    `FormPost`, sync token, local Today/Upcoming views), the console app registration with its
    build-time secret; the same rule.

Contacts (cards and search), Folder (Explorer semantics), the calendar grids and the meters stay
off the kit; they already have their own frameworks or needs no shared list serves.

*Kit as built (2026-10-18): steps 1..3, 7 and 8, with RSS.* Tags `[S-7000]..[S-7071]` in the root
`SPEC.md`, section "The widget kit (KIT)"; tests `tests/Themia.Core.Tests/KitTests.cs` and
`KitConformanceTests*.cs`.

- **Pure parts, `src/Themia.Sdk/Kit/` (`Themia.Sdk.Kit`).** `KitItem` (id, title, subtitle, preview,
  `KitLead`, `KitWhen`, `KitTone`, emphasis, `KitBadge`s, a `Uri` link, `KitGroup`), `KitSummary`,
  `KitIcon`; `KitItems.Clean` (no id or a repeated one dropped, `KitBounds`, links: http(s) for
  `webHosts: null`, https on the named hosts otherwise, never with user info; a summary's fraction
  clamped or dropped) and `KitText` (`Count` 999/1.2k/12k/3.4M never rounded up, `Age`, `When`).
  `KitAction` (`KitActionKind`: Open, OpenTo, CopyLink, CopyText, Toggle, Rename, Delete, Refresh;
  `Runnable`) and `KitMenu.Lines` to `KitMenuLine` (action, `QuickCommand`, `KeyChord`),
  `KitMenu.Primary`; `KitIcons` (every icon and kind to a `Glyphs` code point).
  `KitKeys.Decide(key, index, count)` to `KitKeyResult(KitKeyCommand, index)`. `KitSpec(Look,
  Optional, Untitled, Empty, Time, Overflow, MaxColumns)`, `KitShow`, `KitRowLook`, `KitOverflow`,
  `KitPlan`, `KitLadder.Plan(area, spec, shows)` with `RowEstimate` and `WholeRows`.
  `KitScreens.Decide(locale, accounts, chosen, outcomes, itemCount, empty, noMatch)` to
  `KitScreen(Kind, Sentence, OffersAccounts, HeaderProblems)`, `KitOutcome(Owner, Status, Detail,
  HeldAt, RetryAt)`, `KitAccount`, `KitAccountState`, `Targets`; `KitFreshness.Lines`,
  `RefreshLabel`, `When` (`SyncFreshness.When` delegates to it). `IKitConfig : ISetupRule` (Needs,
  Configured, AccountIds, Shows; its `NeedsSetup` is `KitSetup.Needed`). `IKitSource` (`Changed`,
  `Read()` to `KitRead`, `Busy`, `Show(refresh)`) and `KitRead` (items, outcomes, title, a refusal,
  web hosts, accounts, chosen): the seam the service layer's `ServiceSync` will implement.
- **WinUI parts, `src/Themia.Sdk.WinUI/Kit/` (`Themia.Sdk.WinUI.Kit`).** `KitList` (rich rows in
  columns, measured and shown whole under `ScrollFade`; `ItemFill`, `RowFocus` with the cursor kept by
  item id, `Fluent.Hover`; `ItemMenu.Attach` over `KitMenu`; a click focuses, a double-click or Enter
  runs the primary action; `KitKeys`; site icons through `IWidgetHost.Favicons`), `KitHeader`
  (problem glyph, ring, badge, one icon size), `KitStatePanel` (was `OfficeStatePanel`; Calendar,
  Contacts, Next meeting and Week planner use it), `KitBinding` (owns list, header and state panel;
  reads the source on attach and on `Changed`; decides, plans, draws; the language and age tick; the
  header title; the card's Refresh with its age; the action runner: opens through
  `IWidgetHost.Launcher`, copies through `IWidgetHost.CopyText`, Refresh through the source, the rest
  through the view's handler) and `KitRows` (`Accounts` over `KitAccountRow`/`KitAccountTexts`, which
  the Office pages' account rows now are; `InputBox`; `Count`; `Shows`).
- **RSS** is `src/Widgets/Themia.Widget.Rss/`: `RssSpec` (its `KitSpec`: rich, all four fields, three
  columns, ages; a feed entry as a `KitItem`; Open, Copy link, Refresh), `RssView` (a short kit view
  whose `FeedSource : IKitSource` reads `IWidgetHost.Feeds`), `RssSettings` (from `KitRows`),
  `FeedAggregate` (merge, outcomes, title). `FeedLayout` is gone (proved rung for rung equal to
  `KitLadder` before it went), so are `RssView.Rows`, the view's dead `RefreshSchedule` and its
  `Process.Start`. The SDK took `FeedParser`, `FeedInterest` and `SiteIcons.LooksLikeIcon`;
  `IWidgetHost.Feeds` (`IFeedAnswers`) is new.
- **Behaviour commits, each test first:** RSS opens through the launcher ([S-7062]); Open draws
  `Glyphs.Open`, not U+E8A7 whose Explorer art is "Show more options" ([S-7012]); Up and Left with
  nothing focused land on the first headline ([S-7020]); kit menus show Enter, Ctrl+C, F2 and Delete
  and the list takes Ctrl+C ([S-7011]); the one stale rule for RSS and the six online widgets
  ([S-7042], [S-7064]: no age in any header, the age on the Refresh line); one open gesture for
  Recent files and Notifications ([S-7065]). Two RSS fixes found on the way: the widget takes what the
  sync holds when it is shown (it said Loading until the next fetch, [S-1300]), and every feed
  answering with nothing is one "no articles" sentence.
- **Where it differs from the design above, and why.** One row look is built (`Rich`, the feed's):
  Line, TwoLine and Table come with their first widgets (Mail, the agenda, Tasks), so none is unused
  code. `KitPlan` carries no `Rows`: rich rows are measured, so the list fits whole rows itself.
  `KitKeys` reads the list in reading order (Left and Right step like Up and Down, as `ItemCursor` did
  for RSS) and has no `Page` overflow yet (the agenda brings it). `KitMenu` puts the web pages and the
  copies in the item's own group with Open (one separator less than the table in 5.2, so RSS's menu
  kept its shape); a line without what its kind needs is shown disabled rather than left out (RSS's
  Open and Copy link on an entry without a link were disabled before). `KitScreen` has a `NoAccounts`
  kind (the card's Set up) and a `Refused` screen (input the widget's own rules refuse); Loading keeps
  its sentence (RSS showed one); `OffersGrant` and a sentence for `RetryAt` wait for the service
  layer. `FetchStatus` did not gain `SignInRequired`, `Forbidden` or `NotFound` (step 9):
  `KitAccountState` carries the account's state until the service layer classifies replies.
  `IKitConfig` has no `Interest`: the config's `IInterests` already says what is synced. RSS's item ids
  are positions in the merged list, so its cursor stays on the place it was, as before. `KitBinding`
  takes the tick interval from the view (RSS redraws its ages on the feeds' cadence, as it did).
  The online widgets' header tooltip no longer names the data's source; their settings pages do.
- **Evidence.** Debug build 0 warnings after every commit; Core 5095 and module tests 2807 green at the
  end; the RSS pixel diff of 43 renders (six preview states added first, Debug only, on `.example`
  sites so no icon is ever fetched: tiers in default, light, dark and German, fill sizes, `--keys`,
  the settings page in both themes and German, Arabic and Japanese tiers, picker and settings
  searches) 0 differ after the refactor and after the move, against a "before" at `9b5098e9`. The
  M4 groups' `compare.py` checked only the alpha channel (`Image.getbbox` of an RGBA difference looks
  at alpha alone); `TestResults/kit1/compare.py` compares colour and alpha, and every diff here was
  run again with it. Behaviour renders before and after: `TestResults/kit1/behaviour/` (`stale/`,
  `stale2/`, `keys/`, `open/`). Release AOT publish 0 warnings (50.2 MB, before 50.1 MB);
  Debug AOT publish 0 warnings and its renders of RSS (sample and stale tiers, its page) and weather
  now stale finish in about 4 s, identical to the JIT renders.
- **Left then for the service-layer agent:** steps 9..12 and the account widgets (see below).

*Account widgets on the kit (2026-10-19): Email (all three styles) and the calendar's agenda.* Tags
`[S-7013]`, `[S-7032]..[S-7036]` (root `SPEC.md`, KIT), Email's `[S-6420]`, `[S-6421]`, Calendar's
`[S-3650]..[S-3652]`; tests `KitLinedTests`, `KitPagesTests`, `KitAccountWidgetTests` (Core),
`MailSpecTests`, `AgendaSpecTests`, `CalendarFreshnessTests` (module host).

- **Kit gaps built first, each its own commit with tests.** The lined looks: `KitRowLook.TwoLine`
  (stacked rows going one `Line` on a strip, with inline actions that swap in for the time on the
  hovered and focused row at the slot's one width, `KitLadder.LineBoxes`) and `Table` (the user's
  columns, `KitColumn`, column titles from the spec, `KitLadder.TableColumns`); `KitTime.Compact`
  and `Stamp` (Mail's times, `MailTime` delegates); `KitItem.Tooltip`; `KitShow.Badges`;
  `KitChoices` (fields, preview lines, columns) and `IKitConfig.Choices`; `KitActionKind.Change`
  and an Open/OpenTo the view carries out by id ([S-7013]: the account widgets keep their writes;
  kit services stay read-only); `KitHeaderButton` (a header view button with a choice menu: Mail's
  compose and sort), `KitRead.Count` and `Notes` (a change waiting or refused); the paged look
  (`KitOverflow.Page`, `KitPages`, `KitDays`, `KitGroup.Selected`) with the `Schedule` row
  (`KitSchedule`, the time column measured from `KitSpec.TimeLabels`, the stripe through
  `HexBrush`), `KitList.Decorate` (a view's own per-row hook), `Disclosure.SecondLines`,
  `SyncProblems.ToFetchStatus`. Mail's ladder (`MailLayout`) and the agenda's (`CalendarFit`'s
  agenda rules) were proved equal to the kit's, rung for rung, then went.
- **Refactors, zero pixel differences** against `c513c56b` (the start plus Debug preview states:
  Email `stale`/`offline`, Calendar `stale`/`empty`, Tasks `stale`), `pngdiff --noise`, before /
  after / before interleaved per render: Email onto `KitList` (94 renders: tiers, three styles,
  seven states, dark, light, German, fill sizes, keys, settings pages; 0 differ, 3 noise); the
  agenda onto the paged `KitList` (63 renders of the agenda and month; 0 differ). Its rows keep their
  layout with a focus ring laid over them (no border in the layout: a 1.5px ring and fractional
  padding rounded the stripe a pixel off). Renders under six parallel jobs showed sub-pixel
  differences that serial renders of both builds did not: four jobs is the safe load.
- **Behaviour commits.** Email's menu and keys are the kit's (KitMenu's order with Copy subject and
  Delete in the icon row, the keys shown; Left/Right step, Ctrl+C copies the subject; tested in
  `MailSpecTests`). Email is a kit view (`KitBinding` over `MailKitSource`): the kit's screens and
  state panel, the one stale rule, `MailScreens.Decide` and mail's own 24 failure sentences gone in
  30 languages; renders change only in the stale and offline states
  (`TestResults/kitacc/cmp-em4`). Calendar: `KitHeader` for the problem glyph and ring in every
  style, the kit's words and the age on Refresh (renders unchanged); the agenda's one open gesture
  and the kit's cursor keys (`TestResults/kitacc/cmp-cal7`, `style-agenda-keys`). The Up-with-no-
  cursor rule: Email and the agenda land on the first row (the agenda's on the page on screen).
- **Where it differs, and why.** The calendar is not an `IKitConfig` (its other four styles keep
  their own views, so its view is not a thin kit view, [S-7069]); only the agenda's rows are the
  kit's, and the header's glyph and ring. Mail's ring shows only for a refresh the user asked for
  (its source's `Busy`), as before. Header view buttons are subtle buttons, not items (the item
  layer is for items, [S-6570]). The problem glyph keeps no width gate of Mail's own (the kit's one
  header): at 120px it takes the folder name's room. Mail's lists still scroll under the fade over
  the whole slot (its look), the rich look still fits whole rows.
- **Tasks (2026-10-19), after the above.** Kit gaps first: `KitKeys` MoveUp/MoveDown; the primary
  action of an item with no Open is its Rename (a task opens for editing); a toggle without a state is
  a plain line (`KitAction.Checked` nullable); `RowFocus.Overlay` (the ring laid over a row, so it
  takes no room: the agenda's and Tasks' rows); the `Check` look (`KitChecks`: Tasks' ladder proved
  equal to `TaskLayout.Plan` rung for rung, then gone; `KitLead.Check`, `KitDue`, `KitRowsPanel`
  and `KitListHost` (Tasks' panel and whole-line host, now the kit's), the optimistic toggle, rename
  in place with Tab, the add row, drag reorder within a group, `KitList.Overlay`); `KitStatePanel`'s
  own way out (`[S-7039]`). Drag reorder is the kit's: it is a slot order of the rows panel the kit
  already draws, with nothing Tasks-specific in it. Behaviour commits, each with renders: the one
  stale rule (`KitHeader`, `TaskFreshness`; renders unchanged; the S-7042 allow list is now empty);
  the ring instead of XAML focus and the system focus rect (only the keys renders change:
  `TestResults/kitacc/cmp-t2`); the kit's menu and keys (Enter renames rather than ticks, Space
  ticks; `TaskLayout.Decide` gone); the kit's state panel (`TestResults/kitacc/cmp-t6`: the
  failed and nolocal states keep their link at every size). Refactor: the rows onto `KitList`
  (58 renders against `675ebdcd`, 0 differ in serial renders; the done fade differs between two
  runs of the before build itself under load). Tags `[S-7037]`..`[S-7039]`, Tasks' `[S-615]`..`[S-617]`
  (606..609 are retired). Where it differs: Tasks is not an `IKitConfig` and not a `KitBinding`
  view (its source resolves the default list asynchronously, has a list on this PC, and screens the
  kit's screens do not have: no lists, start a list, try again), so [S-7069]'s thin-view check does
  not cover it; its rows, header and state panel are the kit's.
- **Owed, live:** a signed-in Microsoft, Google and IMAP account for Mail (the three styles, quick
  actions landing on the server, compose from several accounts, a refusal as a header note); a
  signed-in calendar for the agenda (double-click opening Outlook or the provider's page, the links
  across widgets, paging by keys); a signed-in To Do and Google Tasks account and a list on this PC
  (ticking, renaming with Tab, adding several in a row, dragging in one and two columns, Alt+Shift
  moves, Today's list names), and the showcase views on the live desktop for all three.

*Service layer as built (2026-10-18): steps 9..12.* Tags `[S-9900]..[S-9931]`, root `SPEC.md`
section "The service layer (SERVICES)"; tests `ServiceLayerTests`, `ServiceSignInTests`,
`ServiceSyncTests`, `RateLimitRulesTests`, `AccountOAuthTests` (Core) and
`DescriptorConformanceTests` (module host). A fresh tag block (`9900`..) because the kit's own
(`7000`..`7099`) had 28 numbers left and `7400`..`9800` were the power buttons' and toggles'.

- **Step 9, the one failure vocabulary.** `FetchStatus` gained `SignInRequired`, `Forbidden`,
  `NotFound` (each a sentence in 30 languages, none retried on the cadence); `ToFetchStatus` folds
  only server failures and bad bodies into "unexpected", a gone account into "not set up". A
  throttle's `RetryAt` is used at last: `SyncSchedule.Failed(…, retryAt)` and
  `AccountFailures.Failed(schedule, ex, now)` in mail, office and tasks. Mail says its own "sign in
  again" for the new status (its sentence for the old folded one).
- **Step 10, the core, no service** (`src/Themia.Sdk/Services/`, namespace `Themia.Sdk.Services`):
  `ServiceDescriptor` (init-only data: provider, feature, cadence row, `TransportKind`,
  `ApiRoot`/`ApiRootRule`, `ProviderSignIn`, `Identity` (an `IServiceIdentity` query), pinned
  headers, `RateLimitScheme`, `ErrorScheme`, `FreshnessHints`, `AnonymousRule`, bounds, `WebHosts`,
  `IdleFloor`); the auth set (`OAuthPkceLoopback` with `LoopbackRule`, `ClientSecretRule`,
  `RefreshRule`, `AuthorizeExtras`, `ScopeOnToken`, `IdentityEndpoint`; `OAuthDeviceCode` with
  `GrantPage`; `PastedToken` with `TokenShape`; `AnonymousAuth`; `TokenScheme`, `ScopeSeparator`);
  `ServiceRequest`/`ServiceBody`/`ServiceReply`, `PageAt`, `KitChunk`, `KitPage`, `IServiceQuery`
  (+ `IServiceQuery<TWire>` parsing through the service's context, naming the first path that
  broke), `IServiceStep`; `PagingKind`, `LinkPaging`, `ServicePager` (count, bounds, empty page, end,
  loop), `ServicePolling.NextPoll`; `ServiceFailures.Classify` to `ServiceVerdict`; `GraphQLEnvelope`
  (request and answer, partial data by the list's path), `FormBody`; `ServiceInterest`;
  `IServiceReplicaQuery`/`ReplicaMerge`/`IReplicaView` with the typed base
  `ServiceReplica<TReplica, TWire>` (where it differs: the host holds a replica as the provider's own
  JSON, so the host's sync never needs the provider's types); `KitMerge`; `ClientSecretRules`;
  `OAuthRequests` (the members' requests from their data), `IdentityAnswers`, `DeviceGrant`,
  `SignInKinds`, `ServiceRoots`. `RateLimitRules` moved into the SDK (namespace kept) and reads a
  scheme: its GitHub constants became the test's GitHub-shaped scheme, its tests asserting what they
  did; the host's providers read `RateLimitScheme.RetryAfterOnly` (a 429, or a 403 with
  `Retry-After`: what they honoured, minus GitHub's budget headers). Tested on GitHub-, Linear- and
  Todoist-shaped descriptors and replies.
- **Step 11, sign-in by strategy.** Microsoft and Google are their browser members (data in
  `OAuthClients`); `SignInWithBrowserAsync` is one path with no provider named (no ternaries), the
  refresh request is built from the member that signed the account in, who signed in is the member's
  `IdentityEndpoint` (the Graph and userinfo wire types went), the loopback takes a port list, and
  `THEMIA_CLIENT_SECRETS` (`google=…;todoist=…`, validated whole by the build and parsed by
  `ClientSecretRules` in the app) replaced `THEMIA_GOOGLE_CLIENT_SECRET` (CLAUDE.md, README,
  docs/STORE.md; a build with a dummy value proved the semicolon reaches the constant verbatim). The
  device code (`DeviceCodeSession`, `DevicePoll`, `DeviceStep` restored from `e09beef^` with their
  tests and strings, 21 languages added) and the pasted token run in `AccountService`
  (`SignInWithDeviceCodeAsync`, `AddPastedTokenAsync`) with the descriptor's identity request; an
  account records the member kind that signed it in (`signIn` in `accounts.json`, absent for a
  provider's only flow, so the accounts fixture writes back byte for byte), a pasted token is
  `AccountAuth.Token` (never renewed), and its scheme (`Bearer` or `Raw`) comes from its member. The
  Accounts page's Add menu lists every provider with sign-in members, a provider's first member's
  panel shows with links to the others (the device code large with Copy and its page; the paste box
  with its token page), and an account whose service grants per resource has a grant-page row.
  Where it differs: there is no `ServiceSignIn` class of its own; its methods are the account
  service's, which owns the store, the secrets and the single-flight refresh. A rotating refresh
  token whose write fails is kept in memory and is what the next refresh sends (the store already did
  this; now stated, [S-9914]). Microsoft and Google stay declared in Core (`HostProviders`): their
  mail, calendar, contacts and tasks sources are host code that names them.
- **Step 12, `ServiceSync` and `ServiceClient`.** The engine is in Core
  (`src/Themia.Core/Services/`): `ServiceClient` (steps, conditional bodies, bucket blocks, the one
  classifier, a 304 from the held body, GraphQL unwrapped, the one paging loop, replica syncs with a
  refused token answered by a full read), `ServiceMemory` (bodies, blocks, steps; in memory only),
  `ServiceEntries` (the pure sync state: entries per provider, account and query from the
  interests, cadence and floors, Due/Complete/Force/Wake, `Read` to a `KitRead` with the accounts'
  states and the grant page, the shelf) and `FixtureReplay` (recorded answers as a transport). The
  app's `ServiceSync` (`src/Themia.App/Services/Kit/`) is the glue: interests, accounts, the ticker
  at a minute, network wake, the sealed `service` shelf per account (`SyncKind.Service`, purged and
  swept with the rest) and one `IServiceSource` per widget (`IWidgetHost.Services`); the transport is
  `AccountService.SendServiceAsync` (pinned headers, the member's token scheme, an account's own
  root, no throttle retries: `AccountHttp` gained `retryThrottles: false`, the typed `If-None-Match`).
  Where it differs: the client and the entries are Core, not App, so the "Polling" rules are unit
  tests over recorded replies rather than a manual pass; the network itself stays App code. A preview
  card's sources replay the provider's `Fixtures/<state>.json` (`FixtureServices`, Debug only; the
  provider props copy fixtures under `Fixtures/<Provider>/`), giving every kit widget the standard
  state names. `KitScreen` gained `Grant` (`OffersGrant`) and a throttle's line says when it asks
  again; `KitRead` gained the rules' empty sentence and the grant page; `KitStatePanel` draws the
  grant link beside the Accounts link.
- **Conformance added.** Descriptors over the registry ([S-9930]); Polling over source ([S-9931]:
  the service path, the transport and the classifier each named only where they may be); the
  generic-kit rule ([S-7071]) now covers the service layer in the SDK, Core and the app, and its
  allow list of service headers outside providers lost `RateLimitRules.cs`.
- **Evidence.** Debug build 0 warnings after every commit; Core 5227 and module tests 2823 green at
  the end; the Accounts page rendered with the browser card (Google) and the two new panels in both
  themes (`THEMIA_DEBUG_ACCOUNTS_FLOW=device|paste`); Release AOT publish 0 warnings (50.8 MB, before
  50.2 MB). No sign-in, request or account was used: a live pass is owed (below).
- **Owed, live:** Microsoft and Google sign-in and token renewal after the refactor (the requests
  are proved equal field for field, the flow is not run); an official build with
  `THEMIA_CLIENT_SECRETS` signing in to Google; each pilot's members once it exists (device flow
  and grant page, PKCE loopback, pasted keys), rate limits on the wire.


## 12. Design decisions and open questions

### Decisions

- **Shapes.** One shape is built now: **List** (`KitList`), with an optional summary line, groups,
  columns, scroll or page overflow, and four row looks (line, two-line, rich, table). It covers
  Mail, Tasks, the agenda, RSS, notifications, recent files and every list of section 13. **Stat** (one
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
  descriptor (section 5.7); a service writes no App code and no branch in shared code. Proven on
  paper against GitHub, Linear and Todoist (section 13) before anything is built.
- **No "updated since" deltas.** Incremental reads only through a service's own sync token; a
  high-water filter misses items that leave a filtered set.
- **Web links only.** Item links and web actions open https pages on the descriptor's `WebHosts`;
  app URL schemes are not offered.
- **Fixtures out of views.** Preview states come from recorded answers replayed through the real
  rules; `*.Preview.cs` partials disappear from widgets as they migrate.
- **Widgets and providers are projects** (section 4): glob project references plus a generated
  static registry; ids instead of closed enums, validated at build time and on every load; one JSON
  union built from every module's own context; strings, tests, fixtures and SPEC per module.
  Proven by the spike under Native AOT (section 4.11). Rejected: `[ModuleInitializer]`
  registration (runs lazily under CoreCLR, eagerly under AOT), runtime plugin loading, reflection.
- **Row click (Q1, Q2).** A click focuses; a double-click or Enter opens the item. Every kit
  widget, and Recent files and Notifications follow (section 11.2, step 8).
- **One stale rule (Q3).** The problem glyph, its tooltip one line per owner ("Work: No network ·
  updated 10:42"), the age on the Refresh menu line; no caution-coloured age text in headers.
- **GitHub (Q5).** A GitHub App with device flow: read-only repository permissions, expiring user
  tokens refreshed without a client secret, installed per organisation or repository; a pasted
  fine-grained token as the fallback. No notifications widget, so no OAuth app.
- **Unknown widget id (Q15).** Dropped on load with a logged notice; no placeholder card. Not a
  recoverable error in the sense of `CLAUDE.md`, so dropping it is not "deleting user data on a
  recoverable error" (section 4.5).
- **Project granularity (Q16).** One project per widget. Shared UI elements and any code several
  widgets share live in Themia's core (`Themia.Sdk` / `Themia.Sdk.WinUI`), never duplicated into
  widget projects and never in family projects (section 4.2).
- **Namespaces (Q17).** Files keep their `Themia.Core.*` / `Themia.App.*` namespaces when they move
  into the SDKs (pure moves, callers and tests unedited); only new types use `Themia.Sdk.*`.
  Renaming happens incrementally later, if at all.
- **Todoist's day (Q18).** "Today" is the day in the time zone set in the Todoist account
  (`user.tz_info`), as Todoist's own app shows it (section 13.3).
- **Todoist (Q11, Q12).** A console-registered app with a build-time client secret
  (`todoist=` in `THEMIA_CLIENT_SECRETS`), scope `data:read`; a pasted personal token last. Data
  through the **Sync API**: one replica per account, kept fresh by sync tokens, and Today and
  Upcoming computed from it by Todoist's own rules in its provider project (section 13.3).

### Open questions for the user

Resolved and folded in above: Q1, Q2, Q3, Q5, Q9, Q11, Q12, Q15, Q16, Q17, Q18.

- **Q4. Which GitHub widgets first?** Candidates (no notifications, Q5): review requests, assigned
  issues and pull requests, a repository's open pull requests, a repository's issues, releases,
  CI status (workflow runs). The paper test uses review requests and CI status.
- **Q6. GitHub Enterprise.** Support an Enterprise server address per account (the old widgets did,
  through `ApiBaseUrl`; the descriptor has `ApiRootRule.PerAccount` for it), or github.com only for
  now? With a GitHub App, an Enterprise server needs its own app registration on that server.
- **Q7. Without an account.** The old repository widget read public repositories anonymously (60
  requests an hour per IP, 15-minute floor). Keep anonymous public reads for repository widgets
  (`AnonymousRule.PublicReads`), or always require an account?
- **Q8. Languages.** Ship a new widget with all 30 languages translated in the same change, or
  English first (keys in `$englishOnly`) and translations in a follow-up?
- **Q10. Migration appetite.** Order: modules M1..M3 (section 11.1), then the kit with RSS, Mail,
  the agenda and Tasks as zero-change refactors, then GitHub. Or GitHub right after RSS (the kit
  proven on one old and one new widget), with Mail and Tasks later?
- **Q13. Linear's loopback redirect.** Linear's docs do not say whether a registered
  `http://127.0.0.1/...` redirect matches any port. If not, the descriptor uses
  `LoopbackPort.Fixed` with two or three registered ports. To be checked when the app is
  registered.
- **Q14. Todoist's console redirect.** Whether a console app's redirect may be `http://127.0.0.1`
  and whether its port must match is not documented; checked in the console at registration (the
  descriptor has `Fixed(ports)` if it must).

## 13. Paper test: GitHub, Linear, Todoist

Each service below is written as if it were being added today, as a provider project (section 4)
using only what section 5.7 allows a service to add. Facts come from the services' own developer docs (read 2026-10-17, sources in
section 14); a fact marked *unverified* was not stated in the docs and is checked against a
recorded reply or at registration. The "kit changes this service forced" lists are the changes the
first draft of the kit needed; each is folded into sections 5 and 6 above, so every list ends with
**open: none**. Updated after the user's decisions (Q5: GitHub App, no notifications; Q11/Q12:
Todoist console app, Sync API).

### 13.1 GitHub (REST v3)

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
only classic-style tokens (not GitHub App or fine-grained tokens) [G5], which is why the decided
GitHub App has no notifications widget. A GitHub App reads only what it is installed on and the
user can see; read-only repository permissions are chosen at registration [G8].

**Chosen strategies.**

| Field | GitHub |
|---|---|
| Project | `src/Providers/Themia.Provider.GitHub/`; widgets `Themia.Widget.GitHubReviews`, `Themia.Widget.GitHubChecks` (one project per widget, Q16) |
| Provider, feature, cadence | id `gitHub`, feature `gitHub`, cadence row 1, 2, 5, 15 min (default 5) |
| Transport | `RestJson` |
| ApiRoot, rule | `https://api.github.com/`, `PerAccount` (Enterprise `https://<host>/api/v3/`, Q6) |
| SignIn | 1. `OAuthDeviceCode(github.com/login/device/code, github.com/login/oauth/access_token, the GitHub App's client id, no scopes (a GitHub App's access is its permissions), Space, Refresh(rotates, 8 h tokens, refreshed without a secret), GrantPage https://github.com/apps/<slug>/installations/new)`, stored as `OAuthRefresh`; 2. `PastedToken(https://github.com/settings/personal-access-tokens/new, fine-grained shape, Bearer)`; 3. `Anonymous` if Q7 keeps it |
| Token header | `Bearer` |
| Pinned | `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2026-03-10` |
| Limits | Budgets `[(x-ratelimit-limit, x-ratelimit-remaining, x-ratelimit-reset)]`, `EpochSeconds`, bucket header `x-ratelimit-resource`, throttle statuses {403, 429}, Retry-After honoured on all |
| Errors | `JsonCode` none (`message` only); Messages `["secondary rate limit" -> RateLimited]` |
| Freshness | `ETag` |
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
No notifications widget (Q5): the notifications API does not accept GitHub App tokens.

A repository the app is not installed on answers 404 (`NotFound`) for a private repository; the
widget's screen offers the descriptor's `GrantPage` ("Choose repositories on GitHub") beside the
Accounts link, and review requests from repositories outside the installation simply do not
appear (search returns what the token may see; *unverified* in detail, checked with a recorded
reply).

Refresh: the cadence choice; 304s keep a 2-minute cadence cheap; search stays far under 30 a
minute even with several widgets.

**Kit changes this service forced** (folded in): rate-limit blocks per account **and bucket**
(search's budget is not core's); a secondary limit recognised by its message (`ErrorScheme.Messages`);
dependent requests
(`ServiceStep`) for the default branch; the summary line with a tone (`KitSummary`); a per-account
API root (`ApiRootRule`); device flow as an auth member with no secret and a rotating refresh;
a grant page for per-repository access (`GrantPage`, `KitScreen.OffersGrant`). **Open: none.**

**Registration (Lometics organisation, later, browser-assisted).** A GitHub App (Q5); no OAuth app.
- GitHub App: `https://github.com/organizations/<lometics-org>/settings/apps/new`; webhook off; tick
  *Enable Device Flow*; keep *Expire user authorization tokens* on; repository permissions read-only
  (Metadata, Pull requests, Issues, Actions, Checks); no organisation write permission; installable
  by any account. Device-flow refresh needs no secret, so none is stored.
- No client secret is unavoidable for GitHub.

### 13.2 Linear (GraphQL)

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
| Project | `src/Providers/Themia.Provider.Linear/`; widgets `Themia.Widget.LinearIssues`, `Themia.Widget.LinearCycle` |
| Provider, feature, cadence | id `linear`, feature `linear`, cadence row 1, 2, 5, 15 min (default 2) |
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

### 13.3 Todoist (API v1)

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
/api/v1/sync` as `application/x-www-form-urlencoded`, `sync_token` (`*` for all active data) and
`resource_types` (a JSON-encoded array of strings); the answer carries a new `sync_token`,
`full_sync` (whether it holds all data or only the changes), `full_sync_date_utc` (for big
accounts a full sync may lag; then an incremental sync with its token catches up), `items`,
`projects` and `user`; `user.tz_info` has `timezone`, `hours`, `minutes`, `is_dst` and
`gmt_string`; deleted objects come back as tombstones with `is_deleted: true` [T5][T8]. The item
fields the rules read (`content`, `project_id`, `priority`, `labels`, `checked`, `is_deleted`,
`responsible_uid`, `child_order`, `due {date, timezone, is_recurring, string}`, `deadline`) are
named as in the v1 item object; the parts of [T8] read did not show the item schema, so their
exact shape is *unverified* until a recorded reply.

**Chosen strategies.**

| Field | Todoist |
|---|---|
| Project | `src/Providers/Themia.Provider.Todoist/`; widgets `Themia.Widget.TodoistToday`, `Themia.Widget.TodoistUpcoming` (one project per widget, Q16) |
| Provider, feature, cadence | id `todoist`, feature `todoist`, cadence row 1, 5, 15, 30 min (default 5) |
| Transport | `FormPost` to `sync` |
| ApiRoot, rule | `https://api.todoist.com/api/v1/`, `Fixed` |
| SignIn | 1. `OAuthPkceLoopback(app.todoist.com/oauth/authorize, api.todoist.com/oauth/access_token, the console app's client id, "data:read", Comma, Loopback4 with `Any` or `Fixed(ports)` per Q14, ClientSecret BuildTime, Refresh(rotates, 60 s grace))`, build secret key `todoist`; 2. `PastedToken(Todoist Settings › Integrations › Developer, 40-hex shape, Bearer)` (full access: offered last, and the Accounts page says so) |
| Token header | `Bearer` |
| Pinned | none |
| Limits | Budgets none (no headers documented), `DeltaSeconds`, no bucket header; buckets `sync-partial` (1,000 per 15 min) and `sync-full` (100 per 15 min); throttle statuses {429}, Retry-After ignored on {401} |
| Errors | `JsonCode` with `CodeField = "error_tag"`; Codes `UNAUTHORIZED -> SignInRequired`, `INVALID_ARGUMENT_VALUE -> BadResponse` (documented), scope tags from recorded replies (*unverified*); `RetryAfterField = "error_extra.retry_after"`; a rejected sync token -> a full read |
| Freshness | `Incremental.SyncToken`, a full read at least daily and whenever the token is rejected |
| Replica | `TodoistReplica`: items and projects by id, the user's `tz_info`, the sync token, `full_sync_date_utc`; bounded (5,000 items; past the bound the furthest-due open items go first, logged) |
| Anonymous | `None` |
| Bounds | MaxPages 1 (sync has no pages), ItemBound 5,000 |
| WebHosts | `app.todoist.com` |
| Identity | the first full sync's `user` (`id`, `full_name`); a pasted token is checked with a sync of `resource_types=["user"]` before it is stored |

**The replica and its rules** (all in `TodoistRules`, pure, tested on recorded syncs):

- *Request*: `sync_token=<held or *>&resource_types=["items","projects","user"]`; bucket
  `sync-full` for `*`, else `sync-partial`.
- *Merge*: `full_sync: true` replaces the replica; otherwise every object upserts by id, and
  `is_deleted: true` removes it. An item with `checked: true` leaves the open set (kept out of
  every view, gone at the next full read). An item whose project was deleted or archived goes with
  it. When `full_sync_date_utc` lags the request by more than a minute, the next request is an
  incremental one at once (the documented advice), not a second full read.
- *Validate on load*: ids once, strings cut to bounds, `due.date` parses as a day or a date-time,
  links rebuilt from ids (never stored URLs).
- *Today view* (`TodoistList.Today`): open items due on or before today; overdue first, then
  today; sorted by due (timed before all-day on the same day), then priority, then `child_order`.
  An item assigned to someone else (`responsible_uid` set and not the account's user) is left out,
  as Todoist's Today shows the user's own tasks (*unverified*, checked against the Todoist app with
  a shared project). A recurring task shows its current occurrence only (its `due` is the next
  instance; Themia does not expand recurrences).
- *Upcoming view* (`TodoistList.Upcoming`, 3, 7 or 14 days): open items due after today up to the
  horizon, grouped by due day (`KitGroup(Day:)`, so the kit draws "Tomorrow", "Mon 20 Oct"); a
  recurring task appears once, on its next occurrence (Todoist's own Upcoming repeats it on each
  day; the difference is a SPEC line, not a bug).
- *Day boundary*: the day in the time zone set in the Todoist account (Q18, decided), as Todoist's
  own Today shows it. Under `InvariantGlobalization` the IANA name in `tz_info.timezone` cannot be
  mapped to rules, so the zone is the offset `tz_info` carries (`hours`, `minutes`, `is_dst`),
  refreshed on every sync (a DST change reaches the replica with the next sync's `user`). An
  all-day `due.date` is a `KitWhen.Day` and never moves with the zone; a floating date-time (no
  `timezone`) is read in the account's zone; a fixed one is converted. Views are recomputed on
  every merge and at midnight in the account's zone (a ticker deadline at that instant), without
  a request.
- *Map to items*: Id `id`; Title `content` with Markdown reduced to plain text; Subtitle the
  project's name from the replica; Lead `StatusDot` by `priority` (4 = p1 -> Critical, 3 -> Caution,
  2 -> Accent, 1 -> none); When `KitTime.Due`, overdue -> Critical tone; Badges the first two
  `labels`; Link `https://app.todoist.com/app/task/<id>`. Actions: Open; `OpenTo("Complete in
  Todoist", link)`; `OpenTo("Reschedule in Todoist", link)`; `CopyText(content)`. Empty: "Nothing
  due today" / "Nothing in the next 7 days".

Refresh: default 5 minutes, which is 3 partial syncs per 15 minutes per account against a budget
of 1,000, however many Todoist widgets there are (they share the replica); the daily full read is
one of 100. A 429 blocks the account's bucket for `retry_after`.

**Kit changes this service forced** (folded in): the `FormPost` transport; the replica contract
(`IServiceReplica`: merge, validate, views) with one entry per account shared by every widget of
the provider; sync-token freshness with a rejected-token and daily full read; the bucket chosen
per request (full or partial); views recomputed on a day tick without a request; Retry-After
ignored on a 401; a retry time read from the error body (`RetryAfterField`); error codes in a named
JSON field (`JsonCode`); a build-time client secret for any provider (`THEMIA_CLIENT_SECRETS`
keyed by the provider's `BuildSecret`); rotating refresh tokens persisted before use, with the
reuse grace as data; day groups drawn by the kit (`KitGroup.Day`); date-only and floating due
times (`KitWhen`); comma scopes; links built by the rules and checked against `WebHosts`; app URL
schemes deliberately not offered. **Open: none.**

**Registration (later).** Todoist App Management Console
(`https://app.todoist.com/app/settings/integrations/app-management`), signed in as the Lometics
account (Q11: decided). Create a console app with a loopback redirect URL
(`http://127.0.0.1:<port>/callback`, or the port list of Q14), scope `data:read`; ship its client
secret as `todoist=` in `THEMIA_CLIENT_SECRETS` (unavoidable on this path; a build secret, never
in source). Whether the console accepts a loopback redirect and whether the port must match is
checked there (Q14).

### 13.4 Result

| | GitHub | Linear | Todoist |
|---|---|---|---|
| Project | `Themia.Provider.GitHub` | `Themia.Provider.Linear` | `Themia.Provider.Todoist` |
| Transport | RestJson | GraphQL | FormPost (Sync) |
| Paging | LinkHeader | BodyCursor (`pageInfo`) | none (replica) |
| Auth | OAuthDeviceCode (GitHub App, rotating refresh, grant page); PastedToken; Anonymous (Q7) | OAuthPkceLoopback (no secret); PastedToken (Raw) | OAuthPkceLoopback (BuildTime secret); PastedToken |
| Freshness | ETag | None | SyncToken replica, local Today/Upcoming views |
| Rate limits | headers, epoch s, bucket header | two budgets, epoch ms, 400 + body code | 429 + body retry, Retry-After ignored on 401 |
| Errors | status + message rule | GraphQL codes | `error_tag` codes |
| Read-only scope | GitHub App read-only permissions | `read` | `data:read` (OAuth only) |

Every row is a member of a section 5.7 set with parameters from the table above; no service needs
a line outside its provider project (and Todoist's build secret in the build environment). The
three providers' `SPEC.md` files ([S-7100], [S-7200], [S-7300] blocks) copy these facts with their
sources.

## 14. Sources

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
- [T8] Todoist API v1, Sync (request and response fields, full sync delay) and the User object (`tz_info`): https://developer.todoist.com/api/v1/ (sections "Sync", "User")
- [N1] The module spike (section 4.11), 2026-10-17. The .NET behaviour it relied on was measured, not read: module initializer timing under CoreCLR and Native AOT, resolver combination and contract modifiers under AOT, XAML in a WinUI class library.

## 15. Porting a widget's settings to the properties menu

User decision 2026-10-19: a widget's options live on the selection's properties menu in edit mode
(SPEC "QUICKSET", [S-3716]..[S-3730]), not on a settings page. The widgets move one at a time;
Shortcuts (`src/Widgets/Themia.Widget.Shortcuts/ShortcutsMenu.cs`) and Clock
(`src/Widgets/Themia.Widget.Clock/ClockMenu.cs`) are the references, Weather now, Weather forecast
and Air quality are done too.

### 15.1 The vocabulary

The module implements `IWidgetModule.MenuLines(WidgetMenuLines menu, WidgetConfig config)` (usually
one line calling a static `<Name>Menu.Lines(menu, (<Name>Config)config, …)`). Every line is an
`ItemMenuEntry` built by `WidgetMenuLines` (`src/Themia.Sdk.WinUI/Modules/WidgetMenuLines.cs`); each
helper sets the value and calls the save itself, and the host builds the menu again after every
change, so a closure may keep the value it was built with.

| Setting | Helper | On the menu |
|---|---|---|
| bool | `menu.Toggle(glyph, text, on, set)` | the tick at the line's end; best without a glyph under a submenu |
| enum, any closed set | `menu.Choice(glyph, text, [(value, words)…], current, set)` | a submenu of dots; the line shows the option that holds |
| number | `menu.Number(glyph, text, value, min, max, step, format, set)` (any `INumber<T>`) | dots up to `MaxNumberSteps` (20) steps, else asked for as text, range-checked and rounded to a step; the value at the end |
| colour `#RRGGBB` or empty | `menu.Colour(text, current, set, options?)` (`ColorChoiceOptions`: `DefaultLabel` null when a colour is required, `Presets` `ColorPresets.Surfaces` by default or `ColorPresets.Windows`, `AllowCustom`) | the same colour submenu as Appearance › Item text; the swatch is the line's icon |
| `FillChoice` | `menu.Fill(text, current, set, options?)` (`FillChoiceOptions`) | the same submenu as Appearance › Background |
| text | `menu.Ask(new WidgetTextAsk(title, placeholder, accept, refuse, done, Initial:, AllowEmpty:))` from a line's action | a small flyout beside the menu; `refuse` is the trust boundary (validate as typed) |
| a place, a zone, anything searched | `WidgetMenuLines.Search(glyph, text, value, new MenuSearch(placeholder, find, fixedLines, settle))` | box on top, five answers and Show all, fixed lines under them |
| a folder | `menu.PickFolder(glyph, text, path, set)` | Windows' folder picker; the folder's name at the end |
| a list (feeds, symbols, hosts) | `menu.List(glyph, text, items, new MenuList<T>(name, emptySentence) { Rename, Extra, Add, AddLines, Max, Reorder, Glyph, IconFile })` | a submenu of items (Rename…, own lines, Move up/down, Remove), Add… |
| a destructive command | `menu.Confirm(glyph, text, question, confirmWords, action, danger: true, disabled:)` | asks beside the menu first; danger lines in the critical colour |
| a change, a command | `menu.Command(glyph, text, change, disabled:)` or a plain `new ItemMenuEntry(glyph, text, action)` | runs on a click (a plain command does not save) |
| words only | `WidgetMenuLines.Note(text)` | greyed, never clickable (a data source's credit) |
| a group | `WidgetMenuLines.Submenu(glyph, text, lines)`, `ItemMenuEntry.Separator` | |

Where lines go: `menu.StyleLines` (under Style, after the styles) for what only the current look
draws, `menu.Lines` for the rest, and `menu.PlaceCard()` once, where the card's Appearance submenu
stands (call it first unless something matters more than the look; without it Appearance stands
first anyway). Something the vocabulary lacks is an SDK change first, in its own commit, with tests
in `tests/Themia.Modules.Tests/WidgetMenuLinesTests.cs`, rendered in `--editbar-png`
(`props-*-vocabulary*.png`).

### 15.2 The rules

- **StyleLines for what the look draws.** An option only some styles draw goes under Style, guarded
  by the config's `Offers(setting)` ([S-6070], `IShownSettings<T>`), so a style that does not draw it
  never shows it (Clock: seconds and the date; Shortcuts: tile size, icons only, stretch).
- **PlaceCard early.** Appearance is the card's look; the widget's own data lines follow it.
- **No settings page left.** Every row of the page becomes a line; then delete `<Name>Settings.cs`,
  the module's `SettingsRows` override (so `HasSettings` is false and More settings goes, [S-1750]),
  and any page-only helpers. A row that only makes sense on a page (a long explanation) becomes a
  `Note` or goes.
- **Dead strings go.** Remove every locale key the page used that nothing reads any more, from all
  30 locale files of the module (and of the SDK area if it was the SDK's). New words go into `en.json`
  and its `$englishOnly` list.
- **Tests in the module's `Tests/`**, named after its own SPEC tags (`S1388_…`, see
  `ShortcutsMenuTests`, `ClockMenuTests`): what each style offers under Style, the first level's
  order, what a click sets and that it saves once, that picking the current value saves nothing, and
  the `refuse` checks of every typed value.
- **The module's `SPEC.md`** says its options are on the properties menu ([S-3719], with the date of
  the user's request) and lists them; retire the page's lines there.
- **Render it**: `--editbar-png` with the widget selected and its submenus open (add a case to
  `EditBarRender.PropsShots`), and look at the PNGs.

# Rss

The module `rss` ([S-6911]): headlines from one or several feeds, merged newest first; the first
widget built on the kit (root `SPEC.md`, The widget kit (KIT), [S-7000]..[S-7063]). Its tags are this
file's alone; the root `SPEC.md` holds what it reads of the host, with their root tags: the saved
feeds ([S-1300]), the network guard ([S-1301]), the site icons ([S-1304], `IWidgetHost.Favicons`,
the SDK's `SiteIcons`), the feed sync ([S-2661], `IWidgetHost.Feeds`) and the parser (the SDK's
`FeedParser`).

Owning files: everything in this folder: `RssModule.cs` (descriptor, view factory, JSON context),
`RssConfig.cs` (the feeds, the cap, the fields; an `IKitConfig`), `RssSpec.cs` (the kit spec, a feed
entry as a kit item, a headline's actions, `RssKeys`), `FeedAggregate.cs` (the merge, the outcomes,
the title), `RssView.cs` (a kit view over its `FeedSource` on `IWidgetHost.Feeds`, and the header's
age), `RssSettings.cs` (its page, from `KitRows`), `Preview/` (Debug renders: `--state
sample|single|stale|failed|empty|loading`, sample feeds on the reserved `.example` domain),
`Localization/Locales/` (its own `RssStr`, 30 languages). Tests: `Tests/RssTests.cs` (run by
`tests/Themia.Modules.Tests`); its configured state is the module host's `RssStateFixtureTests`.

- **[S-1302]** Rss reads several feeds (`RssConfig.Feeds`, up to 12, a list editor in settings, each
  address checked on commit), fetched in parallel through the shared cache and merged
  (`FeedAggregate.Merge`, classic S-30): newest first, undated last in arrival order, capped at
  `MaxItems` across all feeds. Repeated and blank addresses fold; refused ones are not fetched. A
  feed that fails while others answer is a partial refresh: "Name: reason" in the header tooltip and
  the caution colour (classic S-34/S-35); all failing shows every reason; every feed answering with
  no items is "This feed has no articles" once, not that reason per feed ([S-7040]). Header title (S-04): the
  user's, else the single feed's `<title>`, else the feeds' names (title or host) joined with ", ",
  else the catalogue name. Item age (S-06): "Just now"/"5m ago"/"3h ago"/"6d ago", then "Mar 4"
  ("Aug 30, 2025" in another year). `FeedUrl` is gone (no compatibility).
- **[S-1303]** Rss **Refresh** (card menu and item menu, classic S-15): the network now, past the
  cache's freshness and the back-off, through the feed sync ([S-2661]). While a fetch for one of its
  feeds is out, a small spinner sits beside the age in the header (classic S-33), so Refresh is never
  a button with no visible effect.
- **[S-1305]** Rss ladder (the kit's rich look, `KitLadder.Plan` over `RssSpec.Spec` ([S-7031]), rows shown whole, measured since rows are
  as tall as their headlines, the rest a scroll away with the shared fade [S-1281]): rung 0 the
  newest headline, two lines (one if even that does not fit); its age; its site icon from 180px of
  row width (inline at the start of the age line); the site's name on its own line from 240px; two
  columns from 400px wide (three from 720px on a short strip), read left to right; the summary where
  it costs no row, or where ≥3 rows still fit on a tile ≥280px high and not a tall column (1–2
  lines). By tier: XS one headline; S headline + icon + age; M two rows with names; Wide two
  columns; Tall 5+ headlines; L two columns with summaries; XL three columns with summaries.
- **[S-1306]** No reading pane at any size (retired: it read one item beside the list; user
  decision, the feed is its headlines). A click on a row focuses it; a double-click opens it
  everywhere, as every list on this desktop, through the host's launcher ([S-7062]; fixed: the
  widget started the browser itself). Item menu (`ItemMenu`): Open, Copy link, Refresh. Keys through the shell's
  keyboard lease (`IWidgetKeys`, [S-800]; a click in the widget takes it): the kit's one key map
  ([S-7020]): Up/Down/Left/Right step, Page Up/Down, Home/End, Enter opens, the Menu key opens the
  menu, Escape lets go; with nothing focused Up and Left land on the last headline (`RssKeys`).

Needs a manual pass: real feeds (RSS and Atom, one on the local network, one failing beside two that
answer), Refresh with the spinner while it is out, a headline opening in the browser by double-click
and by Enter, Copy link, the site icons of real sites, a restart with the feeds restored.

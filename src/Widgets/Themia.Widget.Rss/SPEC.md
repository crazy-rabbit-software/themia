# Rss

The module `rss` ([S-6911]): headlines from one or several feeds, merged newest first; the first
widget built on the kit (root `SPEC.md`, The widget kit (KIT), [S-7000]..[S-7063]). Its tags are this
file's alone; the root `SPEC.md` holds what it reads of the host, with their root tags: the saved
feeds ([S-1300]), the network guard ([S-1301]), the site icons ([S-1304], `IWidgetHost.Favicons`,
the SDK's `SiteIcons`), the feed sync ([S-2661], `IWidgetHost.Feeds`) and the parser (the SDK's
`FeedParser`).

Owning files: everything in this folder: `RssModule.cs` (descriptor, view factory, JSON context),
`RssConfig.cs` (the feeds, the cap, the fields; an `IKitConfig`), `RssSpec.cs` (the kit spec, a feed
entry as a kit item, a headline's actions), `FeedAggregate.cs` (the merge, the outcomes,
the title), `RssView.cs` (a kit view over its `FeedSource` on `IWidgetHost.Feeds`, and the header's
age), `RssMenu.cs` (its properties-menu lines, from `KitMenuLines`), `Preview/` (Debug renders: `--state
sample|single|stale|failed|empty|loading`, sample feeds on the reserved `.example` domain),
`Localization/Locales/` (its own `RssStr`, 30 languages). Tests: `Tests/RssTests.cs` and `Tests/RssMenuTests.cs` (run by
`tests/Themia.Modules.Tests`); its configured state is the module host's `RssStateFixtureTests`.

- **[S-1302]** Rss reads several feeds (`RssConfig.Feeds`, up to 12, the menu's list ([S-7510]), each
  address checked as it is typed), fetched in parallel through the shared cache and merged
  (`FeedAggregate.Merge`, classic S-30): newest first, undated last in arrival order, capped at
  `MaxItems` across all feeds. Repeated and blank addresses fold; refused ones are not fetched. A
  feed that fails while others answer is a partial refresh: the header's problem glyph, one tooltip
  line per failed feed ("Tech Daily: Couldn't connect · updated 10:42", [S-7042]; classic S-34/S-35); all failing shows every reason; every feed answering with
  no items is "This feed has no articles" once, not that reason per feed ([S-7040]). Header title (S-04): the
  user's, else the single feed's `<title>`, else the feeds' names (title or host) joined with ", ",
  else the catalogue name. Item age (S-06): "Just now"/"5m ago"/"3h ago"/"6d ago", then "Mar 4"
  ("Aug 30, 2025" in another year). `FeedUrl` is gone (no compatibility).
- **[S-1303]** Rss **Refresh** (card menu and item menu, classic S-15): the network now, past the
  cache's freshness and the back-off, through the feed sync ([S-2661]). While a fetch for one of its
  feeds is out, the kit header's ring turns (classic S-33), so Refresh is never a button with no
  visible effect; the card menu's line says how old the oldest list on screen is ("Refresh · updated
  4 min ago", [S-7042]; the age is no longer in the header).
- **[S-11403]** **Rss states its rows** ([S-2440]): `Rows` (1..20, default 5) headlines a column,
  `Lines` per headline (the headline with its age; the site with its icon; the summary), offered up to
  the lines its fields have (Source and Preview off: one), default 2, and **Columns** (1 or 2,
  [S-11400]), what its width used to decide: two double the width range (440..1040). One column is
  220..520, a new one 300 × 248. The height follows the rows; dragging the bottom edge sets Rows; more
  headlines scroll inside them (the wheel as before). Nothing appears or disappears with the size:
  [S-1305]'s ladder is gone. Tests `RssTests.S11403_`, `RssMenuTests`.
- **[S-1305]** *(Retired by [S-11403]: the lines, the columns and the fields are the config's.)* Rss ladder (the kit's rich look, `KitLadder.Plan` over `RssSpec.Spec` ([S-7031]) on the
  family's rows, [S-6711]): one row per headline, its title the name (one line, ending in an
  ellipsis), its age the value, the site's name on the second line and the summary on the third,
  the site's icon as the lead; the lines grow with the tile both ways, the rows that fit whole show
  and the rest is a scroll away with the shared fade ([S-1281]). By tier: XS and S the headline
  (S with its icon and age); Wide one line per headline with icon and age; M and Tall two lines
  (the site's name); L three (the summary); XL three in two columns, read left to right. With no
  feed the card's Set up row says "Add a feed" ([S-1862]).
- **[S-1306]** No reading pane at any size (retired: it read one item beside the list; user
  decision, the feed is its headlines). A click on a row focuses it; a double-click opens it
  everywhere, as every list on this desktop, through the host's launcher ([S-7062]; fixed: the
  widget started the browser itself). Item menu (`ItemMenu`, from `KitMenu`): Open (Enter), Copy link (Ctrl+C), Refresh. Keys through the shell's
  keyboard lease (`IWidgetKeys`, [S-800]; a click in the widget takes it): the kit's one key map
  ([S-7020]): Up/Down/Left/Right step, Page Up/Down, Home/End, Enter opens, Ctrl+C copies the link, the Menu key opens
  the menu, Escape lets go; with nothing focused every movement key but End lands on the newest
  headline (it was the last one for Up and Left, `ItemCursor`'s answer; now the one every list
  gives).

- **[S-7510]** Its options are on the properties menu ([S-3719], user request 2026-10-19; the
  settings page is gone): Appearance first, then **Feeds**, the menu's editable list ([S-3729]): a
  feed's line is its address without the scheme, dragged to reorder ([S-3732]), its submenu Change address… and
  Remove; Add… asks for an address; both are checked by `WebInput` as they are typed (the trust
  boundary: refused with the reason, never repaired, trimmed when given), and Add… is greyed at 12.
  Then the kit's switches (`KitMenuLines.Shows`): Show source, Show preview, Show date; and
  **Articles**, 5 to 50 in steps of 5 (`KitMenuLines.Count`, a choice of dots, [S-3726]); after a
  divider a link to **Update interval** (⚙ › Online updates › Feeds, [S-3749], [S-3590]).

Needs a manual pass: real feeds (RSS and Atom, one on the local network, one failing beside two that
answer), Refresh with the spinner while it is out, a headline opening in the browser by double-click
and by Enter, Copy link, the site icons of real sites, a restart with the feeds restored.

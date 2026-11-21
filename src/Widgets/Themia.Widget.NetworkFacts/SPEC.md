# Network facts (NETWORK-FACTS)

The family project `Themia.Widget.NetworkFacts` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the connection's facts, **one widget a fact**, each a
single metric on the one base (the root `SPEC.md`'s single metric, [S-14600]..[S-14607]: its one row, its default, its
trend, its sizing). Its tags are this file's; the root holds the sheet's rules, the SDK's `NetworkDetailRules` ([S-2415], [S-2418], [S-2419]) and the throughput's `NetworkRules` ([S-1190], [S-1192]).

Owning files: everything in this folder: `NetworkKinds.cs`, `NetworkMetric.cs` (module and config bases, the part over the
hub's addresses, public address and throughput, the JSON context), `Localization/Locales/` (its own `NetworkFactsStr`).
Tests: `Tests/NetworkMetricTests.cs`, `Tests/NetworkAdapterTests.cs` (the Adapter setting over every kind that holds it).

- **[S-11020]** *(Retired by [S-14640]: no card of picked facts.)*
- **[S-14640]** **A widget a fact** (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for all of them. you should also have the option to show trendlines as well or just the trend with arrows."): `networkLocalAddress` "Local address", `networkReceived` "Data received", `networkSent` "Data sent", `networkLinkSpeed` "Link speed", `networkPublicAddress` "Public address", `networkDownload` "Download speed", `networkUpload` "Upload speed", the family **Network facts** under System
  (`AddFolderId.NetworkFacts`). The addresses are read before the throughput on a tick ([S-2501]), so the throughput's read
  is a new reading of one tick. Trends ([S-14603]): download speed and upload speed an arrow and a line; local address, data received, data sent, link speed and public address none (a text, a constant or a time: no Trend line); a rate's line is the throughput's own
  series (download its first line, upload its second), read only while a line is drawn. 
- **[S-2417]** The public address asks `api.ipify.org` through the host's one lookup only for the public address's own card,
  a widget the user adds for it; its page says whom it asks and how often ([S-3719]): the note, then Appearance.
- **[S-2019]** A click on the card ([S-2000]) opens Windows' network status page (`ms-settings:network-status`); every kind declares it (`NetworkKinds`, `MetricKind.Opens`, [S-14706]), as do the card menu's first line and Enter ([S-14707]).

- **[S-14960]** **The Adapter setting** (owner, 2026-11-21: "local address you should be able to select the network adapter, for
  the similar widgets the same"): every network kind that reads one adapter's figures holds it, one setting over one config
  contract (`IHoldsAdapter`: the adapter's id, Windows' GUID, and its name as last seen) and one shared line
  (`SharedSettings.NetworkAdapter`, [S-11752]): the local address, data received, data sent, link speed, download speed and
  upload speed (its graph, which stands in for the removed Network history, [S-14713], the chosen adapter's own series),
  and the Network reading (`network`). Not the public address (the
  machine's address on the internet, whatever adapter it leaves by), not Network adapters (every adapter, a row each), not
  Ping (a host, and it binds to no adapter). **Automatic** (no id, the default) is what these kinds always read: the busiest
  adapter's address and link speed, every counted adapter's traffic together ([S-2415], [S-1190]).
- **[S-14961]** **Which adapters**: the machine's adapters as Windows' network connections name them ("Wi-Fi", "Ethernet 2"),
  up or not, without what the throughput never counts ([S-1192]: loopback, tunnels, capture mirrors, filter bindings), a
  Wi-Fi Direct virtual adapter or one not present (`NetworkAdapterRules.Offered`); connected first, then by name. Installed
  at start like the drive list (`AdapterList`, the host's walk, `NetworkAdapterWalk`), read from the machine the hub reads:
  this PC, or the sample PC's Wi-Fi and its dock's unplugged Ethernet under a render's `--machine sample`
  (`SamplePc.Adapters`).
- **[S-14962]** **The line** (`AdapterMenuLines.Adapter`, marked shared, [S-11753]): Adapter, a choice of Automatic, then each
  adapter by its name ("Ethernet (not connected)" while it is down); picking one stores its id and its name. A chosen adapter
  the list has not stays offered by the name it was chosen under, "(not connected)": the choice is never silently lost. While
  the page shows it the line follows Windows (an address change: an adapter came, went, connected or dropped) and is built
  again when the list it showed differs; the host's notice is held only while a page waits on it. Several network widgets
  selected set it once for all of them ([S-11751]). It is the first line of each holder's page (the history's before its span).
- **[S-14963]** **The hub reads the chosen adapter** from its one throughput feed (one read a tick for every network widget,
  [S-2501]): its facts are that adapter's own rates, session totals, address and link speed (`NetworkAdapterRules.Sheet` over
  `Narrow`); a reading's line and a rate's graph are the feed's series of that adapter (`SeriesInterest` instance: the adapter's
  id, empty for Automatic; `NetworkAdapterRules.Reading`). A chosen adapter the sample has not (unplugged, disabled, gone)
  never shows another adapter's figures: every fact is "--", dimmed, its reason the tooltip ("Ethernet is not connected";
  the reading and a rate's graph, which know the id alone: "The chosen adapter is not connected"), no point on the chart.
- **[S-14964]** **Repair at load**: an adapter id this PC has not is kept as it is (a dock away, an adapter unplugged), shown
  "--" with the reason until it is back; a blank id is Automatic and a name kept without an id is dropped, both logged.

Needs a manual pass: the cards on a live desktop (an adapter coming and going, a chosen adapter unplugged and back, the Adapter line following it while open, the public address card, offline).

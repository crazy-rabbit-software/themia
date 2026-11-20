# Network facts (NETWORK-FACTS)

The family project `Themia.Widget.NetworkFacts` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the connection's facts, **one widget a fact**, each a
single metric on the one base (the root `SPEC.md`'s single metric, [S-14600]..[S-14607]: its one row, its default, its
trend, its sizing). Its tags are this file's; the root holds the sheet's rules, the SDK's `NetworkDetailRules` ([S-2415], [S-2418], [S-2419]) and the throughput's `NetworkRules` ([S-1190], [S-1192]).

Owning files: everything in this folder: `NetworkKinds.cs`, `NetworkMetric.cs` (module and config bases, the part over the
hub's addresses, public address and throughput, the JSON context), `Localization/Locales/` (its own `NetworkFactsStr`).
Tests: `Tests/NetworkMetricTests.cs`.

- **[S-11020]** *(Retired by [S-14640]: no card of picked facts.)*
- **[S-14640]** **A widget a fact** (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for all of them. you should also have the option to show trendlines as well or just the trend with arrows."): `networkLocalAddress` "Local address", `networkReceived` "Data received", `networkSent` "Data sent", `networkLinkSpeed` "Link speed", `networkPublicAddress` "Public address", `networkDownload` "Download speed", `networkUpload` "Upload speed", the family **Network facts** under System
  (`AddFolderId.NetworkFacts`). The addresses are read before the throughput on a tick ([S-2501]), so the throughput's read
  is a new reading of one tick. Trends ([S-14603]): download speed and upload speed an arrow and a line; local address, data received, data sent, link speed and public address none (a text, a constant or a time: no Trend line); a rate's line is the throughput's own
  series (download its first line, upload its second), read only while a line is drawn. A placed `networkFacts` card is
  replaced in place by its rows ([S-14606]).
- **[S-2417]** The public address asks `api.ipify.org` through the host's one lookup only for the public address's own card,
  a widget the user adds for it; its page says whom it asks and how often ([S-3719]): the note, then Appearance.
- **[S-2019]** A click on the card ([S-2000]) opens Windows' network status page (`ms-settings:network-status`); the module decides it (`NetworkMetricModule.Opens`).

Needs a manual pass: the cards on a live desktop (an adapter coming and going, the public address card, offline).

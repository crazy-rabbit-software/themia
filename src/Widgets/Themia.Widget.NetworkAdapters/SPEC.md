# Network adapters (networkAdapters)

The module `networkAdapters` ([S-6911]): every counted network adapter, a row each. Its tags are this
file's alone; the root `SPEC.md` holds what the list atoms share ([S-11008], [S-2440]) and the rows'
rules, the SDK's `NetworkDetailRules` ([S-2416] the adapters' rows, [S-2419] the local address).

Owning files: everything in this folder: `NetworkAdaptersModule.cs` (descriptor, config, its half of
the shared `DetailView`, JSON context), `Localization/Locales/` (its own `NetworkAdaptersStr`). Tests:
`Tests/NetworkAdaptersTests.cs`.

- **[S-11021]** Network adapters (the adapter list of Network details as its own atom): the network
  reading's own feed, one row per counted adapter with an address or traffic, busiest first ([S-2416]):
  its kind's icon, its name and its download, its address and link speed under them; no adapter is
  "No connection". Its rows are its one setting (`Rows`, 1..20, four by default, two lines each,
  `KitSizing`: 36 + 40 a row + 12, 220..520 wide); more adapters scroll inside them. A click opens
  Windows' network status page.

Needs a manual pass: a machine with Wi-Fi, Ethernet and a VPN adapter.

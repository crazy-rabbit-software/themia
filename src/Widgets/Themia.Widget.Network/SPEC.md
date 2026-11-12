# Network (reading)

The module `network` ([S-6911]): the machine's network throughput, the reading alone (the id stays the
reading's; its history is `networkHistory`, its facts `networkFacts`, its adapters `networkAdapters`,
and the old cards' looks the ready-made groups "Network" and "Network details"). Its tags are this
file's alone; the root `SPEC.md` holds what the system atoms share ([S-11000]..[S-11009]) and the
throughput's rules, the SDK's `NetworkRules` ([S-1190] per adapter, [S-1192] the counted adapters).

Owning files: everything in this folder: `NetworkModule.cs` (descriptor, sizing, view factory, JSON
context), `NetworkConfig.cs` (what it asks the hub, `IReadsSystem`), `NetworkMeter.cs` (its half of
the shared `MeterView`), `Localization/Locales/` (its own `NetworkStr`). Tests: `Tests/NetworkTests.cs`.

- **[S-1191]** Download over upload, both at 28, each led by its arrow ([S-6264], [S-11001]): one height
  (128), 172..320 wide. It has no style: the old Graph style is the Network history atom, and the old
  Numbers style is this reading. Nothing appears with size; no tier ladder is left.
- **[S-6270]** *(Retired: Network has no styles; its reading is both numbers, its history an atom of its own.)*
- Its options are on the properties menu ([S-3719]): Appearance; nothing of its own.
- **[S-2018]** A click on the card ([S-2000]) opens Windows' network status page
  (`ms-settings:network-status`); the module decides it (`NetworkModule.Opens`, through its part's
  `Opens`).

Needs a manual pass: the reading on a live desktop (a download and an upload moving it).

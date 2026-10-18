# Contacts

The module `contacts` ([S-6911]): the people of the signed-in accounts, as a list, a compact list, a
details table or cards, searched and opened. Its tags are this file's alone; the root `SPEC.md`
holds what it shares, with their root tags: where the people come from and the config ([S-580]), the
layouts ([S-581], `ContactRules.Effective`), the details columns ([S-582]), the count ([S-583]), the
search ([S-585], `ContactSearch`), the avatars ([S-586]), the person's menu ([S-587]), the shared
source ([S-588]), the ladder and the keys ([S-1270], [S-1271], [S-1273], [S-1275], [S-1277],
`ContactRules`, in the SDK).

Owning files: everything in this folder: `ContactsModule.cs` (descriptor, view factory, JSON
context), `ContactsConfig.cs`, `ContactsView.cs` (the layouts, the search box, the selection and the
keys, the person's menu, over `IWidgetHost.Office`), `ContactsSettings.cs` (the SDK's account rows, the
layout, the columns, the count), `Localization/Locales/` (its own `ContactsStr`, 30 languages). Tests:
the rules' are Core's (`ContactsAndBirthdaysTests`, `ContactsFitTests`); the config's are the module
host's (`ContactsHostTests`, `OfficeConfigTests`).

- **[S-584]** A search box (from 150 px tall) asks for the keyboard while focused (Esc clears). It
  starts empty: the `defaultSearch` setting is gone ([S-3796]).
- **[S-1272]** Details: the Name cell carries a 20 px avatar when the table is ≥360 px wide.
- **[S-1276]** The People count is a number box (`FluentLayout.Stepper`): at least 5, steps of 5, no
  upper bound (S-583); text that is not a number is overwritten with the last good value.

Needs a live pass: a Microsoft, Google and CardDAV address book with a few hundred people (search,
the menu's email, call and message hand-offs), and the search box under the keyboard lease.

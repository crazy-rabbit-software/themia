# Microsoft Store listing: what to paste into Partner Center

The text and answers for Themia's first submission, in the order Partner Center asks for them. The
steps around it (reservation, identity, the Pro add-on, building the package) are in
[STORE.md](STORE.md). Facts here follow the app as built: 80 widget types, the free tier of
20 widgets and 2 views ([S-1065]), Pro as a one-time add-on, no telemetry.

## Pricing and availability

- **Price:** Free (the app). Pro is the add-on below.
- **Markets:** all.
- **Discoverability:** "Make this product available and discoverable in the Store". For a first
  dry run use "available but not discoverable" (hidden, reachable by link), which also lets a real
  Pro purchase be tested before launch.

## Properties

- **Category:** Personalization. (Secondary, if offered: Productivity.)
- **Privacy policy URL:** `https://lometics.com/en/legal/privacy/`
- **Website:** `https://themia.app`
- **Support contact:** `support@themia.app`
- **Product declarations:**
  - "This app has been tested to meet accessibility guidelines": **leave unticked** unless an
    accessibility pass has been done.
  - "Customers can install this app to alternate drives or removable storage": allowed.
  - "Windows can include this app's data in automatic backups to OneDrive": **no**. The data
    holds DPAPI-sealed account tokens that cannot be opened on another PC.
- **System requirements:** Windows 11, x64; keyboard and mouse.

## Age ratings (IARC questionnaire)

- **Category:** "Utility, Productivity, Communication or Other".
- **Violence, sex, language, controlled substances, gambling:** none.
- **Does the app share the user's location with other users?** No. With "Use my location" on, the
  position, rounded to about 1 km, goes only to Open-Meteo for the weather. It is never shown to or
  shared with other people.
- **Users interact or exchange content?** No. There is no chat and nothing is posted. Mail and
  calendar widgets only read the user's own accounts.
- **Unrestricted internet / web browsing?** No browser. RSS feeds and camera or video streams show
  addresses the user types in.
- **Digital purchases?** Yes: the Pro add-on.
- **Expected result:** 3+ / Everyone (PEGI 3, ESRB E).

## Store listing: the words and the pictures

Every field of the listing is in `store/listing/<lang>.json` (English is the source): the short
description, the description, the product features, the search terms, each screenshot's caption, and the
Pro add-on's title and description. Paste from there, or export the listings from Partner Center and let
`node store/fill-csv.mjs <exported.csv>` fill every language and lay out the folder to import
([store/README.md](../store/README.md)).

- **Product name:** Themia
- **What's new in this version:** First release of Themia on the Microsoft Store.
- **Copyright and trademark info:** © 2026 Lometics GmbH
- **Additional license terms:** the EULA, `https://lometics.com/en/legal/eula/` (German:
  `https://lometics.com/de/legal/eula/`). Partner Center wants the text itself: paste the EULA from
  that page.
- **Developed by:** Lometics GmbH
- **Screenshots:** the ten pages `node store/compose.mjs` makes in `store/out/images/<lang>/`
  (3840 × 2160 PNG), in their order, each with its caption from the listing. They are drawn by the app
  from `store/scenes.json`, so they follow the build: make them again before a submission.
- **Store logos:** `store/out/images/tile-300x300.png`, and `hero-3840x2160.png` as the 16:9 art. The
  package's own logos are used for the rest.

A language the app speaks but the listing has no file for needs none: the Store shows the English
listing and still lists the language as supported.

Google is not named anywhere in the listing while Themia does not offer Google accounts
(SPEC [S-3562]: Google has not verified the OAuth app yet); `store/listing.test.mjs` holds every
language to that. Put it back in the description, the features, the accounts page of the pictures
(`store/panorama.mjs`) and the certification notes when it is offered again.

## Pro add-on

- **Product type:** Durable. **Product lifetime:** Forever.
- **Product id:** `themia-pro`.
- **Title and description:** `addOn` in `store/listing/<lang>.json`; its icon is
  `store/out/images/pro-300x300.png`.
- **Price:** to decide. The earlier app sold Pro for USD 19, once.
- **Content type:** Electronic software download.
- After it is created, copy its Store ID into `StoreIds.ProAddOn` (see STORE.md, step 3).

## Submission options: notes for certification

Paste into **Notes for certification**:

> Themia is a full-trust Win32 desktop app (WinUI 3, Windows App SDK, Native AOT). It draws
> widgets on a transparent, click-through window on the desktop layer behind other windows. It has
> no main window and no tray icon. To see it after installing: start Themia from the Start menu.
> The desktop shows a starter layout of widgets. Press Ctrl+Alt+W or double-click a widget to enter
> edit mode, where widgets are added, moved and configured; a toolbar appears at the top of the
> screen. The app's settings are the gear (⚙) menu on that toolbar; there is no separate settings
> window. Starting Themia again from the Start menu while it runs enters edit mode with that menu
> open.
>
> Restricted capability runFullTrust: required for the transparent, click-through window kept at
> the desktop layer (one per display, clipped with SetWindowRgn), the global hotkey and the
> low-level mouse and keyboard hooks used only while editing, WASAPI audio meters and volume, and
> system readings (CPU, GPU, memory, disk, network) through Win32 APIs.
>
> Other capabilities: radios and bluetooth for the Bluetooth widget (paired devices and battery);
> location only while a weather widget has "Use my location" switched on (the position is rounded
> to about 1 km and sent only to Open-Meteo); userNotificationListener for the Notifications widget,
> after the user allows it.
>
> No account is needed to test. Account widgets (mail, calendar, tasks) are optional and sign in
> with the tester's own Microsoft account. The free tier allows 20 widgets and 2 views;
> the Pro add-on lifts the limits.

These notes follow SPEC [S-65] (Ctrl+Alt+W) and [S-66] (a second start opens edit mode). Check
them in the sideloaded package before pasting.

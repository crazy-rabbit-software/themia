# Microsoft Store listing: what to paste into Partner Center

The text and answers for Themia's first submission, in the order Partner Center asks for them. The
steps around it (reservation, identity, the Pro add-on, building the package) are in
[STORE.md](STORE.md). Facts here follow the app as built: 78 widget types, the free tier of
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

## Store listing: English (en-us)

**Product name:** Themia

**Short description** (shown first, keep it to one or two sentences):

> Transparent widgets that live on your Windows desktop: clock, weather, calendar, mail, tasks,
> notes, system meters and more, drawn behind your windows and arranged the way you want.

**Description:**

> Themia puts widgets on the desktop itself, behind your windows, where they are always a glance
> away and never in the way. No window, no tray icon: just your desktop, with what you want to see
> on it.
>
> Choose from 78 widget types, from a clock, the weather and your calendar to CPU, memory and network
> meters, mail, tasks, notes, music controls, shortcuts and folders. Every widget adapts to its size:
> make it bigger and it shows more, make it smaller and it keeps what matters most. Many come in
> several styles, so a clock can be digital, analogue, flip or written out in words.
>
> Arrange them freely, snap them to a grid and give each display its own layout. Views let you keep
> several desktops (one for work, one for the evening) and switch with a click. Colours, borders,
> transparency and fonts follow your theme or your own choice.
>
> Your data stays on your PC. Themia sends no analytics and has no account of its own. Mail,
> calendars and tasks connect straight from your computer to Microsoft, Google, iCloud or your own
> IMAP and CalDAV server. Nothing passes through us.
>
> Built natively for Windows 11 with WinUI 3 and compiled ahead of time, Themia starts fast, uses
> little memory and does no work when nothing has changed.
>
> Free: every widget type, up to 20 widgets and 2 views.
> Themia Pro: unlimited widgets and views, one purchase, no subscription.

**What's new in this version:** First release of Themia on the Microsoft Store.

**Product features** (one per line, up to 20):

1. 78 widget types: clock, weather, calendar, mail, tasks, notes, system meters and more
2. Lives on the desktop layer, behind your windows
3. Widgets that show more as they grow and stay clear when small
4. Several styles per widget, such as digital, analogue, flip and word clocks
5. Free placement with snapping, per display and per scale
6. Views: several desktop layouts, switched with a click
7. Mail, calendar and tasks from Microsoft, Google, iCloud, IMAP and CalDAV
8. Weather, forecast, air quality and rain from Open-Meteo, no account needed
9. CPU, GPU, memory, disk, network, battery and audio at a glance
10. Your data stays on your PC; no analytics, no telemetry
11. Native WinUI 3 app, compiled ahead of time: fast and light
12. Available in 30 languages
13. Free for up to 20 widgets and 2 views; Pro is a one-time purchase

**Search terms** (up to 7): `desktop widgets`, `widgets`, `clock widget`, `weather widget`,
`system monitor`, `rainmeter alternative`, `desktop customization`

**Copyright and trademark info:** © 2026 Lometics GmbH

**Additional license terms:** the EULA, `https://lometics.com/en/legal/eula/`. Partner Center wants
the text itself: paste the EULA from that page.

**Developed by:** Lometics GmbH

**Screenshots** (at least 1, up to 10; 1920×1080 or 3840×2160 PNG; desktop): still to be taken of
this build on a real desktop. The ones in `themia.app/marketing-materials` show the earlier app.
Suggested set:
1. A full desktop with a calm layout: clock, weather, calendar, tasks.
2. Edit mode with the properties menu open beside a widget.
3. The same widget at several sizes (the showcase views).
4. Clock styles side by side.
5. System meters (CPU, GPU, memory, network).
6. Light theme.

**Store logos:** optional. The package's own logos are used. A 1:1 box art of 1080×1080 helps the
listing look sharp.

## Store listing: German (de-de)

**Kurzbeschreibung:**

> Transparente Widgets direkt auf deinem Windows-Desktop: Uhr, Wetter, Kalender, Mail, Aufgaben,
> Notizen, Systemanzeigen und mehr, hinter deinen Fenstern und so angeordnet, wie du willst.

**Beschreibung:**

> Themia legt Widgets auf den Desktop selbst, hinter deine Fenster: immer einen Blick entfernt und
> nie im Weg. Kein Fenster, kein Tray-Symbol, nur dein Desktop mit dem, was du sehen willst.
>
> 78 Widget-Arten stehen zur Wahl, von Uhr, Wetter und Kalender bis zu CPU-, Arbeitsspeicher- und
> Netzwerkanzeigen, Mail, Aufgaben, Notizen, Musiksteuerung, Verknüpfungen und Ordnern. Jedes Widget
> passt sich seiner Grösse an: grösser zeigt es mehr, kleiner behält es das Wichtigste. Viele gibt es
> in mehreren Stilen, eine Uhr etwa digital, analog, als Klappuhr oder in Worten.
>
> Ordne sie frei an, lass sie am Raster einrasten und gib jedem Bildschirm sein eigenes Layout. Mit
> Ansichten hältst du mehrere Desktops bereit (einen für die Arbeit, einen für den Abend) und
> wechselst mit einem Klick. Farben, Rahmen, Transparenz und Schriften folgen deinem Design oder
> deiner eigenen Wahl.
>
> Deine Daten bleiben auf deinem PC. Themia sendet keine Analysedaten und braucht kein eigenes Konto.
> Mail, Kalender und Aufgaben verbinden sich direkt von deinem Rechner mit Microsoft, Google, iCloud
> oder deinem eigenen IMAP- und CalDAV-Server. Nichts läuft über uns.
>
> Nativ für Windows 11 gebaut, mit WinUI 3 und vorab kompiliert: Themia startet schnell, braucht wenig
> Arbeitsspeicher und arbeitet nicht, wenn sich nichts ändert.
>
> Gratis: alle Widget-Arten, bis zu 20 Widgets und 2 Ansichten.
> Themia Pro: unbegrenzt Widgets und Ansichten, ein Kauf, kein Abo.

**Neuerungen in dieser Version:** Erste Version von Themia im Microsoft Store.

**Produktmerkmale:**

1. 78 Widget-Arten: Uhr, Wetter, Kalender, Mail, Aufgaben, Notizen, Systemanzeigen und mehr
2. Lebt auf der Desktop-Ebene, hinter deinen Fenstern
3. Widgets, die mit der Grösse mehr zeigen und klein übersichtlich bleiben
4. Mehrere Stile pro Widget, etwa Digital-, Analog-, Klapp- und Wortuhr
5. Freie Anordnung mit Einrasten, pro Bildschirm und Skalierung
6. Ansichten: mehrere Desktop-Layouts, per Klick gewechselt
7. Mail, Kalender und Aufgaben von Microsoft, Google, iCloud, IMAP und CalDAV
8. Wetter, Vorhersage, Luftqualität und Regen von Open-Meteo, ohne Konto
9. CPU, GPU, Arbeitsspeicher, Laufwerke, Netzwerk, Akku und Audio auf einen Blick
10. Deine Daten bleiben auf deinem PC; keine Analyse, keine Telemetrie
11. Native WinUI-3-App, vorab kompiliert: schnell und schlank
12. In 30 Sprachen verfügbar
13. Gratis für bis zu 20 Widgets und 2 Ansichten; Pro ist ein einmaliger Kauf

**Suchbegriffe:** `Desktop Widgets`, `Widgets`, `Uhr Widget`, `Wetter Widget`, `Systemmonitor`,
`Rainmeter Alternative`, `Desktop anpassen`

**Weitere Lizenzbedingungen:** `https://lometics.com/de/legal/eula/` (der Text der Seite).

The other 28 languages the app speaks need no listing of their own. Without one, the Store shows
the English listing and still lists the language as supported.

## Pro add-on

- **Product type:** Durable. **Product lifetime:** Forever.
- **Product id:** `themia-pro`.
- **Title:** Themia Pro / Themia Pro (de).
- **Description:** "Unlimited widgets and views. One purchase, yours for good: no subscription." /
  "Unbegrenzt Widgets und Ansichten. Ein Kauf, für immer deiner: kein Abo."
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
> with the tester's own Microsoft or Google account. The free tier allows 20 widgets and 2 views;
> the Pro add-on lifts the limits.

These notes follow SPEC [S-65] (Ctrl+Alt+W) and [S-66] (a second start opens edit mode). Check
them in the sideloaded package before pasting.

# Google OAuth verification: the demo video

What Google's reviewers must see in the demo video for the Themia project (`themia-494012`), and how
to record it. The video goes into Google Auth Platform › Data Access › "Demo video", as an unlisted
YouTube link.

## What Google requires

From [Sensitive scope verification](https://developers.google.com/identity/protocols/oauth2/production-readiness/sensitive-scope-verification),
[Restricted scope verification](https://developers.google.com/identity/protocols/oauth2/production-readiness/restricted-scope-verification)
and the [verification requirements](https://support.google.com/cloud/answer/13464321):

- [ ] The video is in **English**: the app, the browser, Google's pages and every caption.
- [ ] It shows the **whole OAuth grant** a user goes through, started from inside the app.
- [ ] The consent screen shows the **app name** ("Themia") and the **same scopes** Data Access lists.
- [ ] The browser's **address bar shows the OAuth client ID** (`client_id=187748979097-url067h00d18pjbjgskddssdj0nrq04s...`).
- [ ] It covers **every OAuth client** in the project. There is one: "Themia Desktop" (Desktop).
- [ ] It shows **what each sensitive and restricted scope does** in the app:
      `gmail.readonly` (restricted), `calendar.events.readonly`, `contacts.readonly`, `tasks`.
      `calendar.calendarlist.readonly` is non-sensitive; show it anyway, it is cheap.
- [ ] It is uploaded to YouTube as **Unlisted** (not Private).

The first video was sent back on 23 April 2026 for two reasons: it did not show the requested
scopes in the consent workflow, and it did not show enough of the app working. Reply to that email
thread (from api-oauth-dev-verification-reply) with the new link once the video is in Data Access.

Other common reasons a video is sent back: the client ID is never readable in the address bar; the
consent screen is cut, scrolled past or zoomed out; a scope is granted but its feature is never
shown; a write scope (`tasks`) is shown only reading; the app or Google's pages are in another
language; the video shows a different app than the one under review.

Restricted scopes (Gmail) may also need a security assessment (CASA) for apps that reach the data
"from or through a third-party server". Themia reads Gmail only on the user's PC, so state that
plainly in the video and the justification.

## Before recording

1. **Build**: the WinUI app, which requests exactly the scopes Data Access lists. Google sign-in only
   works with the client secret built in. In your own terminal (the secret never goes into a file):
   ```powershell
   $env:THEMIA_CLIENT_SECRETS = "google=<the Themia Desktop client secret>"
   powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
   ```
2. **Account**: a Google account **with Gmail** (personal@nathaniel-walser.com has none: its mail is
   on Microsoft 365, so the Mail widget would have nothing to show) and demo data (a few emails, two calendars with events this week,
   contacts with phone numbers and a birthday, a task list with three tasks). The project is at
   96 of its lifetime 100 users: a new account uses one; your own account is already counted, but
   its real mail and contacts would be in the video. If the account has used Themia before, revoke
   it first at https://myaccount.google.com/permissions so the full consent screen appears.
3. **Screen**: quit every other copy of Themia, set Windows to English, Focus on (no
   notifications), close other windows, use a plain wallpaper. Brave (the default browser) in
   English, with no other tabs. The video records the primary screen only.

## The shot list

Each scene gets a caption in the edit; the words in quotes are those captions.

1. **Title card** (added in the edit): "Themia: Google OAuth verification demo. Windows desktop app.
   OAuth client: Themia Desktop (187748979097-url067h00d18pjbjgskddssdj0nrq04s.apps.googleusercontent.com).
   Scopes: gmail.readonly, calendar.events.readonly, calendar.calendarlist.readonly,
   contacts.readonly, tasks."
2. **The app** (10 s): the desktop with Themia's widgets. "Themia shows widgets on the Windows
   desktop. It has no server: Google data goes from Google straight to this PC."
3. **Start the sign-in** (15 s): Ctrl+Alt+W, the edit bar's settings, Accounts, Add an account,
   Google. "The user connects a Google account from Themia's settings."
4. **Google sign-in** (15 s): the browser opens; choose the demo account.
5. **Unverified app screen** (10 s, if shown): Advanced, Go to Themia. "Shown while verification is
   pending."
6. **The client ID** (15 s): click the address bar, press Home, then move along the URL until
   `client_id=187748979097-url067...` shows; zoom with Windows Magnifier (Win and +) and hold for
   five seconds, then Win and Esc. "The address bar carries Themia's OAuth client ID."
7. **The consent screen** (20 s): show "Themia wants to access your Google Account" and the
   "This will allow Themia to:" list with **every** permission on its own line (Gmail, calendar
   events, calendar list, contacts, tasks). If Google folds them behind a "# services" link, click
   it so each one shows. Scroll slowly so each line is readable, tick every box, then Continue or
   Allow. "The user grants each scope." This is what the April 2026 review sent back: "The demo
   video you submitted does not show the requested scopes in the OAuth consent workflow", with a
   sample of this list.
8. **Connected** (5 s): back in Themia, the account is listed under Accounts.
9. **Gmail, `gmail.readonly`** (30 s): add a Mail widget, pick the account: the inbox appears
   (sender, subject, preview, time, unread). Right-click a message: only Open, Reply and Copy; no
   delete, archive or send. "gmail.readonly: the Mail widget lists recent messages. Read only;
   nothing is sent, changed or deleted."
10. **Calendar, `calendar.calendarlist.readonly` and `calendar.events.readonly`** (30 s): add a
    Calendar widget (week view), pick the account; open its Calendars line: the account's calendars
    are listed; tick the second one and its events appear. "calendarlist.readonly lists the
    calendars to choose from; events.readonly shows their events. Read only."
11. **Contacts, `contacts.readonly`** (25 s): add a Contacts widget: names, emails, phone numbers,
    companies. On the calendar, turn on Show contact birthdays: the birthday appears. "contacts.readonly:
    the Contacts widget and birthdays on the calendar. Read only."
12. **Tasks, `tasks`** (40 s): add a Tasks widget on the account's list. Check off a task, add one
    with "Add a task", delete one. Then open https://tasks.google.com in the browser: the same
    changes are there. "tasks: the Tasks widget completes, adds and deletes tasks. tasks.readonly
    could not write."
13. **On this PC only** (15 s): Settings › Accounts, sign out of the account: the widgets go empty.
    "Signing out forgets the account and everything synced from it on this PC."
14. **End card** (added in the edit): "Themia, by Lometics GmbH. themia.app"

About four to five minutes. Move the pointer slowly, and pause a second on every result.

## Recording and the edit

`scripts/demo-video.ps1 start` records the primary screen to `Videos\ThemiaDemo\raw-<time>.mkv`
(30 fps, lossless enough to read small text); `scripts/demo-video.ps1 stop` ends it. The edit trims
waits, adds the title and end cards and the captions, and writes a 1080p MP4 for YouTube.

## Uploading

YouTube Studio, Create, Upload video: title "Themia: Google OAuth verification demo", Visibility
**Unlisted**, not made for kids. Paste the link into Data Access › Demo video and Save (Google asks
to confirm, as it updates the open review), then reply to Google's review email that the video is
updated.

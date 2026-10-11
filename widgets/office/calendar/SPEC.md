# Calendar Spec

Behavior tags are stable IDs. When adding behavior, append a new [S-NN]; never renumber or reuse existing tags. When behavior is removed, delete the bullet but don't reuse its tag.

## Display
- [S-01] Shows events from configured Microsoft, Google, and/or CalDAV calendars in one of four views.
- [S-02] Header has a calendar-days icon and "Calendar" title; hidden when `hideHeader` true.
- [S-03] Top bar (hidden in agenda view and when header hidden) shows prev/next navigation arrows, the current range title, and a "today" dot button.
- [S-04] Month view: 6-row × 7-column grid with Mo–Su labels, showing day number and an adaptive number of event/birthday chips per day (1–6, computed from the measured cell height via `monthChipCapacity`, so a taller widget shows more), plus a "+N" overflow chip for the remainder.
- [S-42] Responsive by widget size: larger widgets render more verbosely (bigger type, more month chips, always-on agenda meta) while smaller widgets fold detail (fewer/hidden chips, dropped labels, shrunk type) via CSS container queries + the adaptive month cap.
- [S-43] Month compact fallback: on a small widget — roughly default-size or smaller in either dimension (≤480px tall or ≤360px wide) — each day with events/birthdays shows up to three colored dots (per-account palette [S-08] for events, warning hue for birthdays, plus one muted overflow dot beyond three) instead of chips, so days-with-events stay legible without cramped/truncated chip text. Larger widgets show full event chips. Dot color comes from `eventDotColor` (`responsive.ts`).
- [S-44] Month large-detail: as the widget grows, chips gain padding/line-height and the day-view hour rows + agenda rows grow taller/roomier. The timed-event start-time prefix on month chips appears ONLY once columns are genuinely wide (widget ≥800px, ~115px+ per column) so the title — always the priority — is never crowded out; below that the title takes the whole chip.
- [S-45] Event chips never squish: month and week chips keep their natural height (`flex-shrink:0`) so a crammed/short cell clips whole chips (bounded by the adaptive cap + "+N") instead of compressing them into unreadable slivers.
- [S-05] Week view: 7 day columns with narrow weekday header and stacked event chips. Each timed-event chip stacks the start time on its own line above the title so the title keeps the full (narrow) column width; birthday chips stay single-line (cake + name).
- [S-06] Day view: all-day bar plus a 7:00–22:00 hour timeline where timed events are positioned by start/end times.
- [S-07] Agenda view: groups up to `agendaCount` events by date, labelling "Today" / "Tomorrow" / weekday-month-day; shows subject, location, and organizer per event. The time column stacks the start time at the top of the event and the end time at the bottom (no dash separator); all-day events show a single "All day".
- [S-08] Each event chip can get a colored left border from a fixed 6-color palette, auto-assigned by the calendar's fetch-target index (`_idx`). Shown when more than one calendar is displayed (see [S-47]/[S-51] for the customized-single-calendar case).
- [S-09] Today's date is visually highlighted in month and week views.
- [S-10] Birthday entries show with a cake emoji and the contact's first name (or full label in agenda/day-allday).
- [S-11] Empty-state message with config hint when no accounts selected.
- [S-12] Reauth UI ("Calendar access needed" with Sign-Out & Re-connect button) shown on `calendar_not_authorized`.
- [S-13] Error message shown for other backend errors.
- [S-14] "No upcoming events." shown in agenda view when there are no matches.
- [S-15] "No events today" shown in day view when the day has no events.

## Configuration
- [S-16] `accountIds`: multi-select Microsoft/Google/CalDAV accounts.
- [S-49] `calendars`: `Record<accountId, string[]>` of selected calendar ids per account. An account absent here (or with an empty list) shows its **default** calendar only — the pre-feature behavior, so existing configs are unchanged and a fresh widget starts primary-on. The Source tab renders a per-account calendar picker (via `calendar_list_calendars` [S-48]), shown only where an account exposes more than one calendar; the default calendar starts toggled on, the rest off. Deselecting every calendar for an account drops the key (back to the default). `resolveCalendarTargets` (`targets.ts`) turns account set + selection into the fetch targets, and is the single source of truth shared by the widget and the config color rows.
- [S-17] `defaultView`: `month` | `week` | `day` | `agenda` (selected via segmented control).
- [S-18] `showBirthdays`: toggle whether contact birthdays are fetched and displayed.
- [S-19] `agendaCount`: max number of events shown in agenda view (default 20).
- [S-20] `hideHeader`: toggle the widget title bar.
- [S-46] `weekStart`: closed set `'monday' | 'sunday'` (default `'monday'`). Controls the first day column in the month grid, the week view, and the weekday header row (Mo–Su vs Su–Sa). A legacy/absent/corrupt value narrows to `'monday'` at the trust boundary. Pure helpers (`weekStartOffset` / `startOfWeek` / `weekdayOrder` in `dates.ts`) drive all three so both modes align. (issue #57a)
- [S-47]/[S-51] `calendarColors`: `Record<colorKey, hex>` per-calendar color overrides, where the color key is a calendar id when a specific calendar is selected, else the account id (`calendarColorKey`). A valid `#rrggbb` override wins over the [S-08] index palette; an absent or corrupt override falls back to the palette (`resolveCalendarColor` in `colors.ts`). Colors render when more than one calendar is displayed (as in [S-08]) OR when the user has set an override for a displayed calendar — so a single-calendar user's custom color is still shown. Legacy `accountColors` (keyed by account id) is merged under `calendarColors` at read and migrated on the next config write, so an existing per-account color carries forward onto that account's default calendar. Configured per displayed calendar in the config's Source tab. (issue #57b)

## Context menu
- [S-21] Widget chrome right-click offers Refresh and view switcher (Month / Week / Day / Agenda).
- [S-40] Any `_refresh` config-token bump (context-menu Refresh, config-window close) re-fetches events and forces the birthday cache past its daily TTL (`refresh(true)`). The mount run never fires: a `_refresh` token persisted in config must not force contact enumeration on app start (see [S-38]).

## Data / backend
- [S-22] Events fetched via `calendar_get_events` with the view range's start/end ISO datetimes, one call per (account, calendar) in parallel.
- [S-48] `calendar_list_calendars(accountId)` enumerates every calendar the account exposes, returning `{ id, name, is_default, color }` per calendar. Microsoft: `GET /v1.0/me/calendars` (`isDefaultCalendar`, `hexColor`); Google: `GET /calendar/v3/users/me/calendarList` (`primary`, `backgroundColor`); CalDAV: walks the calendar-home-set and returns every VEVENT collection (`displayname`, Apple `calendar-color`), flagging the stored collection as default. Paginates (`@odata.nextLink` / `nextPageToken`); a 403 surfaces as `calendar_not_authorized`. CalDAV degrades to the single stored collection when the home-set can't be discovered.
- [S-50] `calendar_get_events` takes an optional `calendarId`. Absent → the account's default calendar (Microsoft `/me/calendarView`, Google `calendars/primary`, CalDAV the stored collection URL), i.e. the pre-feature behavior. Present → that specific calendar (`/me/calendars/{id}/calendarView`, `calendars/{id}/events`, or a REPORT against the resolved CalDAV collection URL). Ids are URL-encoded into the path.
- [S-51] Calendar colors are normalized at the trust boundary (`normalize_cal_color`): a well-formed `#rrggbb` is kept (lowercased), an Apple/CalDAV `#rrggbbaa` is truncated to `#rrggbb`, and Graph's `""`/`"auto"` or any garbage → `None` so the frontend falls back to its palette. Remote/persisted color strings are untrusted.
- [S-52] Google sign-in requests `calendar.calendarlist.readonly` alongside `calendar.events.readonly`. The events scope alone reads any calendar's events but not `calendarList`, which answered 403, so the picker ([S-49]) never appeared and only the primary calendar could be shown. An account signed in before the scope was added keeps its default-calendar-only view until it signs in again. Test: Rust `s52_google_scope_lists_calendars`.
- [S-23] Birthdays fetched via `calendar_get_birthdays` per account when `showBirthdays` is enabled, served through a persisted daily-TTL cache (`createCachedFetch`, namespace `calendar-birthdays`, one entry per account set, order-insensitive key).
- [S-24] `calendar_not_authorized` surfaces the reauth UI; reauth signs out accounts via `email_sign_out_account`.
- [S-25] Polls every 5 minutes (events only; poll ticks merely nudge the birthday cache, which refetches at most once per daily TTL).
- [S-26] Reloads events when account IDs, `showBirthdays`, view mode, or navigation date change; the birthday cache key follows account set + `showBirthdays`, and a key change refetches only when the cached entry is stale.
- [S-38] Mount, navigation (clicks/arrow keys) and poll ticks never re-enumerate contacts while the cached birthdays are fresh. Transient birthday-fetch failures are never cached: previously cached birthdays stay visible, the error is logged, and the next poll retries.
- [S-39] Per-account permission failures (`contacts_not_authorized` / `calendar_not_authorized`) are cached as denied accounts, surfaced once per denial set via an error toast, and retried hourly instead of on every poll.
- [S-27] Opening an event invokes `calendar_open_event` with web_link and event id.
- [S-41] Event datetimes are parsed by `gdt` (`dates.ts`): a string with an explicit RFC3339 zone (`Z` or a `±HH:MM`/`±HHMM` offset, including the negative offsets Google returns for the Americas) is parsed as-is; a naive zoneless datetime is treated as UTC. A stray `Z` is never appended to an already-offset string (issue #48: US-Eastern Google timed events rendered as "Invalid Date").

## Interactions
- [S-28] ◂ / ▸ buttons step by month / week / day depending on view mode.
- [S-29] Today dot button resets navigation to today and reloads.
- [S-30] Clicking an event chip (anywhere it has a web_link or id) opens the event.
- [S-31] Left/Right arrow keys step the period backward/forward.
- [S-32] Up/Down arrow keys in month or week view jump by one week.
- [S-33] Home key jumps to today.
- [S-34] Enter opens the first event on the current nav cursor day.
- [S-35] Keyboard input routed via the Rust low-level hook; mousedown activates the widget's keyboard router.
- [S-36] Account list also accepts CalDAV accounts; CalDAV events use the same `calendar_get_events` dispatch path (provider-dispatched to `fetch_caldav_events`).
- [S-37] The Source tab's account toggle list is scoped to calendar-relevant providers (Microsoft / Google / CalDAV): only those connections appear as toggles. The "Manage connections" button opens the full Connections page **unfiltered**: every connection is listed and the same **Add connection** button is offered ([S-40 email]), identical to reaching it from the sidebar. (Per-widget scoping lives only in the toggle list, never on the Connections page.)

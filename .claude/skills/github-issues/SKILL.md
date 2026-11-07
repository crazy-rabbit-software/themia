---
name: github-issues
description: Handle Themia's GitHub issues (crazy-rabbit-software/themia) - read the ones waiting on Nathaniel, reply in his voice, triage them (issue type, labels, milestone, assignee, board Status/Priority, sub-issues, duplicates) and fix what the winui code still gets wrong. Use when the user says "handle the issues", "answer the GitHub issues", "triage issues", or /github-issues.
---

# Handle Themia's GitHub issues

Repo `crazy-rabbit-software/themia` (public). Board: user project `nwalser/projects/4`
(owner `nwalser`, id `PVT_kwHOAfy6XM4BVSm_`). Maintainer login `nwalser`. The code to check
against is this checkout's `winui` branch: the WinUI rebuild, manifest version 1.0.0, which replies
call "the next major version". The released app (v0.15.x, Tauri) is on `main`
(`git show main:<path>`, never switch branches).

## 1. Find what waits on Nathaniel

```bash
gh issue list -R crazy-rabbit-software/themia --state open --limit 200 --json number,title,comments,author --jq '.[] | select(.author.login != "nwalser") | select((.comments | length) == 0 or .comments[-1].author.login != "nwalser") | "\(.number)\t\(.title)"'
```

Then read each one in full (`gh issue view N -R ... --json title,body,comments`). A last comment that is
only thanks ("looking forward to it") needs no reply; a new detail, a "still broken", a question or
another user adding to it does. Also read Nathaniel's earlier replies on the issue: never contradict
a promise without saying so.

Untriaged issues (no `area:*` label, no issue type) get the full triage in step 4 even when they
need no reply.

## 2. Check the code before answering

For every bug or request, find out what winui does today: grep the widget's
`src/Widgets/Themia.Widget.<Name>/SPEC.md`, root `SPEC.md`, then the code. Several issues at once:
one Explore agent with a numbered list ("DONE | PARTIAL | NO, evidence, file/SPEC tag"). Never
claim "the next major version has this" without evidence in the code.

Outcomes:
- **Done in winui** -> say so, plainly, in a sentence or two of what the user will see.
- **Still broken in winui and fixable** -> fix it here, following CLAUDE.md (SPEC line, regression
  test named `S<tag>_issue_<n>_...`, build + scoped tests, SDK change in its own commit, commit
  message "Area: what (issue N). Why. [S-NN]; test X." with no AI attribution). Then reply "I've
  fixed this for the next major version". A module's tests may only name tags its own `SPEC.md`
  defines (conformance test S6912).
- **Not possible / not planned** -> say why in one honest sentence (Windows limitation, cost,
  out of scope by decision: tray icon, always-on-top, pop-out windows).
- **Unclear** -> ask one concrete question.

## 3. Write the reply in Nathaniel's voice

Shape, always:

```
Dear @<login>, thank you for <the specific thing: the report, the clear steps, the idea>!
<1-4 sentences: what is true, what changes in the next major version, or one question.>

Have an awesome day!
Nathaniel
```

- Plain, warm, short. First person ("I've fixed", "I'll look into it"). "sorry for the
  inconvenience / late reply" when it was slow or broken.
- Say what the user will see, never internals (no class names, no SPEC tags). Product words:
  "the next major version", "the current version", widget names as the app shows them.
- No dates, no release promises. No em dashes, no marketing words, no bullet lists unless
  answering a numbered list of theirs. Code formatting only for things they typed.
- Several people on one issue: "Dear @a and @b". Pinging someone else only to thank them; name
  them without @ otherwise.
- Write in English even when the issue is not (that is what earlier replies do).
- Closing comments (duplicate, answered, stale) are one or two friendly sentences; the
  "Have an awesome day!" sign-off is optional on a bare "closing since X covers it".

## 4. Triage system (apply to every issue touched)

| Field | Rule |
|---|---|
| Issue type | `Bug` (something broken), `Feature` (request, idea, new widget), `Task` (internal chore) |
| Template label | keep `bug` / `enhancement` / `widget-request` as filed; add `enhancement` if none |
| Area labels | one to three `area:*`: desktop, displays, app, appearance, accounts, calendar, email, tasks, files, system, media, weather, finance, productivity, integrations |
| Flag labels | `needs-info` (waiting on reporter), `upstream` (Windows or a service limits it), `wontfix`, `duplicate`, `invalid` (spam, empty) |
| Milestone | `v1.0.0` = done or fixed in winui (the next major version). No milestone = backlog. `v0.16.0` is retired: move its issues out |
| Assignee | always `nwalser`, on every issue touched, open or closed (his rule, 2026-11-07); `apply.py` adds him unless an entry says `"assign": false` |
| Board Status | `Todo`; `In progress`; `Blocked` (waiting on outside: Google verification); `Done not Tested` (fixed in code, needs a live pass: desktop layering, sign-in, displays, shell); `Done not Released` (done in winui, verified); `Done` (released) |
| Board Priority | `High` crash, data loss, app or a core widget unusable; `Medium` broken feature with a workaround, popular request; `Low` nice to have |
| Size | only when it helps planning a Todo |
| Relatives | exact duplicate -> close `--duplicate-of` the older/better issue after a comment pointing there; parts of a bigger request -> sub-issue of the umbrella (`parent`) |
| Closing | duplicate; "completed" when an existing widget already does it; "not planned" for declined, `invalid`, or `needs-info` silent for 30+ days. Issues done in winui stay open until 1.0 ships |

When 1.0 ships: close every `v1.0.0` issue as completed with a short "This is in v1.0, available
in the Microsoft Store" comment, set board Status `Done`.

## 5. Apply

Write a plan (list of entries, see the docstring of `apply.py` next to this file), dry-run it, show
the user the replies (number, recipient, text) and the closes, then run it:

```bash
python -X utf8 .claude/skills/github-issues/apply.py <plan.json> --dry-run
python -X utf8 .claude/skills/github-issues/apply.py <plan.json>
```

Keep the plan and the log in `runs/<date>.json` / `.log` next to this file (git-ignored). An HTTP 410 means
GitHub deleted the issue (spam): skip it. A "Something went wrong" GraphQL error is transient:
rerun that one edit. Comments are public: post them only when the user asked to respond in this request. Labels must
exist first (`gh label list`); create a missing `area:*` with colour `c5def5`. `gh project` needs
the `project` scope (`gh auth refresh -s project`).

## 6. Report

Per issue: replied / triaged only / fixed (commit) / closed (why). List fixes that still need a
live pass (CLAUDE.md "Definition of done" 4), and anything left for Nathaniel to decide.

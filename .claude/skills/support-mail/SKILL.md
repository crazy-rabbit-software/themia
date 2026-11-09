---
name: support-mail
description: Answer the emails in Lometics' shared mailboxes support@lometics.com and info@lometics.com (Themia users, uninstall-survey feedback, licence and download questions) - find the open threads, check the facts against the code, and write reply drafts in Nathaniel's voice, in the sender's language, into the shared mailbox's Drafts for him to send. Use when the user says "answer the support mails", "handle support@ / info@", "reply to the emails in the shared mailbox", or /support-mail.
---

# Answer the shared mailboxes

Two shared mailboxes, both Themia's: `support@lometics.com` (bug reports, the uninstall survey's
"Why I uninstalled Themia" mails, downloads, licences) and `info@lometics.com` (general, press,
partners, personal notes to Nathaniel). Nathaniel signs every reply himself.

**The skill drafts, it never sends.** Drafts land in the shared mailbox's own Drafts folder, threaded,
from the shared address; Nathaniel reads and sends them in Outlook. Never use the Microsoft 365
connector's `outlook_create_reply_draft` / `outlook_send_*`: those draft in his personal mailbox and
send from his own address. Sending, forwarding, deleting or moving mail only when he asks for
that exact mail in this request.

## Tool

`drafts.py` next to this file (Microsoft Graph, delegated, `Mail.ReadWrite.Shared`; his account has
Full Access to both mailboxes). Run from this folder:

```bash
python -X utf8 drafts.py login                         # once; device code in the browser; cache is DPAPI-sealed in %LOCALAPPDATA%\lometics-support
python -X utf8 drafts.py -m support list --days 30     # inbox: date, answered/OPEN, sender, subject, then the id
python -X utf8 drafts.py -m info list --days 30
python -X utf8 drafts.py -m support show <id>          # one message as text
python -X utf8 drafts.py -m support draft <id> <body.html>
python -X utf8 drafts.py batch runs/<date>/drafts.json # [{"mailbox": "support"|"info", "id": "...", "body": "x.html"}, ...]
```

"not signed in" means: ask Nathaniel to run `login` himself (it needs his browser sign-in). The
Microsoft 365 connector's `outlook_email_search` with `mailboxOwnerEmail` is a fine read-only
alternative for searching older mail.

## 1. Find what waits on Nathaniel

`list` both mailboxes. `OPEN` = no reply from the mailbox in that conversation since the message
came in (it checks Sent Items of the same window, so use the same `--days`). For each open thread,
answer the **newest** message and read the whole conversation first (`show` each message, plus the
mailbox's earlier reply in Sent Items): never contradict what Nathaniel promised before. Skip:

- spam, newsletters, notifications, automated mail, out-of-office replies;
- a last message that is only thanks ("no problem, looking forward to it"): no reply needed, list it;
- a sender already answered from the other mailbox or on GitHub (same person, same question).

Several mails from one person about one thing: one reply, to the newest.

## 2. Check the facts before answering

- **Themia today** (the released app, v0.15.x, Tauri): `git show main:<path>` in this repo, never
  switch branches. **The next version** (the WinUI rebuild, `winui`, 1.0.0): this checkout, its
  `SPEC.md` and widget `SPEC.md`s. Never say the next version does something without evidence there.
- **Website, pricing, Pro, discounts, download page**: the website repo
  `C:\Users\Nathaniel Walser\Repositories\themia.app`.
- **Known topics** (check they are still true): the current version uses too many resources and
  the rebuild is much lighter; Google sign-in is off until Google finishes reviewing the app; the
  source is public at github.com/crazy-rabbit-software/themia (Elastic License 2.0); a bug that is
  also a GitHub issue can link it.
- A real bug that is still broken in winui: say so to Nathaniel, and offer to fix it (the
  github-issues skill's rules) or file an issue; do not promise a fix in the mail before it exists.

**Only Nathaniel decides** money and licences: discount or coupon codes, refunds, free licences,
licence keys, sending installers directly, invoices, partnerships, press, legal or privacy
(GDPR) requests. Draft the reply with a visible `[[NATHANIEL: what to fill in or decide]]` marker
in place of the decision, and list it in the report. Never invent a code, price, date or promise.

## 3. Write the reply in Nathaniel's voice

```
Dear <first name>,

Thank you for <the specific thing>! <"sorry for the late reply" when it waited 2+ days>
<1-4 sentences: what is true, what the next version changes, or one concrete question.>

Have an awesome day!
Nathaniel
```

- **In the sender's language** (the whole mail, greeting and closing too): Spanish
  "Estimado/Estimada <name>," ... "¡Que tenga un día estupendo!"; German "Hallo <name>," ...
  "Einen schönen Tag noch!"; formal "usted/Sie" unless they wrote informally. English when unsure.
- First name only, as they signed (not the display name in caps). No name: "Dear Themia user" /
  equivalent.
- Plain, warm, short, first person. What the user will see, never internals (no class names, branches,
  SPEC tags, "WinUI"): "the current version", "the new version, rebuilt natively for Windows".
- No release dates or promises of timing ("soon" at most). No em dashes, no marketing words, no
  bullet lists unless answering their numbered list. "I'll let you know when it's out" is fine.
- HTML body: each paragraph a `<p>`, the closing as `<p>Have an awesome day!<br>Nathaniel</p>`, links
  as `<a href>`. Only `p, br, a, b, i, ul, ol, li`. The script adds the quoted original below.

## 4. Draft

Write each body to `runs/<date>/<n>-<sender>.html` (git-ignored) and a `runs/<date>/drafts.json`.
Show Nathaniel every reply first (mailbox, recipient, subject, the text, any `[[NATHANIEL]]`
markers) and wait for his OK or edits. Then `list` again right before `batch`: Nathaniel works the
same mailbox, so a mail may have been answered, moved or archived meanwhile (a `404 ErrorItemNotFound`
on `createReply` means exactly that). Drop the drafts whose mail is answered or gone and say so;
never draft into a thread he answered while you worked. A draft for a thread that already has one
in Drafts: say so instead of adding a second.

## 5. Report

Per mail: drafted (mailbox, recipient, subject) / needs Nathaniel (what to decide) / no reply needed
(why). Then: "the drafts are in the Drafts folder of support@ / info@; nothing was sent". Mention
any bug worth a GitHub issue or a fix.

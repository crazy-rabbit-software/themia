"""Reply drafts in Lometics' shared mailboxes (support@, info@), for a person to read and send.

    python drafts.py login                                 sign in once (device code, in your browser)
    python drafts.py [-m support|info] list [--days 30]    the inbox: date, answered?, sender, subject, id
    python drafts.py [-m support|info] show <id>           one message as text: from, date, subject, body
    python drafts.py [-m support|info] draft <id> <body.html>
    python drafts.py [-m support|info] reply-latest <sender address> <body.html>   (their newest message)
    python drafts.py [-m support|info] batch <drafts.json> [{"id" or "from": "...", "body": "body.html"}, ...]

-m picks the mailbox (default support); a batch item may carry its own "mailbox".

A draft is Graph's createReply on the message in the shared mailbox, so it lands in that
mailbox's Drafts folder, threaded, from the shared address, with the original quoted under the
given HTML. There is deliberately no send command: a person sends.

Sign-in is the device-code flow of Microsoft Graph PowerShell's public client (no secret, no
app registration of our own); the account must have Full Access to the mailbox. The token cache
is sealed with DPAPI for the current Windows user and kept outside the repository.
"""

import argparse
import ctypes
import ctypes.wintypes as wt
import html
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path

import msal

MAILBOXES = {"support": "support@lometics.com", "info": "info@lometics.com"}
MAILBOX = MAILBOXES["support"]
CLIENT_ID = "14d82eec-204b-4c2f-b7e8-296a70dab67e"  # Microsoft Graph PowerShell (public client)
AUTHORITY = "https://login.microsoftonline.com/organizations"
SCOPES = ["Mail.ReadWrite.Shared"]
GRAPH = "https://graph.microsoft.com/v1.0"
CACHE = Path(os.environ["LOCALAPPDATA"]) / "lometics-support" / "token.bin"


# ---- DPAPI: the token cache is readable by this Windows user only ---------------------------

class _Blob(ctypes.Structure):
    _fields_ = [("cbData", wt.DWORD), ("pbData", ctypes.POINTER(ctypes.c_char))]


def _dpapi(data: bytes, protect: bool) -> bytes:
    crypt = ctypes.windll.crypt32
    kernel = ctypes.windll.kernel32
    buf = ctypes.create_string_buffer(data, len(data))
    inp = _Blob(len(data), ctypes.cast(buf, ctypes.POINTER(ctypes.c_char)))
    out = _Blob()
    fn = crypt.CryptProtectData if protect else crypt.CryptUnprotectData
    if not fn(ctypes.byref(inp), None, None, None, None, 0, ctypes.byref(out)):
        raise OSError(f"DPAPI {'protect' if protect else 'unprotect'} failed ({ctypes.GetLastError()})")
    try:
        return ctypes.string_at(out.pbData, out.cbData)
    finally:
        kernel.LocalFree(out.pbData)


def _load_cache() -> msal.SerializableTokenCache:
    cache = msal.SerializableTokenCache()
    if CACHE.exists():
        try:
            cache.deserialize(_dpapi(CACHE.read_bytes(), protect=False).decode("utf-8"))
        except (OSError, ValueError) as e:
            print(f"the token cache could not be read ({e}); sign in again with: login", file=sys.stderr)
    return cache


def _save_cache(cache: msal.SerializableTokenCache) -> None:
    if cache.has_state_changed:
        CACHE.parent.mkdir(parents=True, exist_ok=True)
        tmp = CACHE.with_suffix(".tmp")
        tmp.write_bytes(_dpapi(cache.serialize().encode("utf-8"), protect=True))
        tmp.replace(CACHE)


def _app(cache):
    return msal.PublicClientApplication(CLIENT_ID, authority=AUTHORITY, token_cache=cache)


def _token() -> str:
    cache = _load_cache()
    app = _app(cache)
    accounts = app.get_accounts()
    result = app.acquire_token_silent(SCOPES, account=accounts[0]) if accounts else None
    _save_cache(cache)
    if not result or "access_token" not in result:
        sys.exit("not signed in (or the sign-in expired): run  python scripts/support-drafts.py login")
    return result["access_token"]


# ---- Graph ---------------------------------------------------------------------------------

def _graph(method: str, path: str, body=None, token=None):
    url = path if path.startswith("https://") else GRAPH + urllib.parse.quote(path, safe="/?&=$,'@():")
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Authorization", f"Bearer {token or _token()}")
    req.add_header("Content-Type", "application/json")
    req.add_header("Prefer", 'outlook.body-content-type="html"')
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            raw = r.read()
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", "replace")[:600]
        sys.exit(f"Graph {method} {path.split('?')[0]} failed: HTTP {e.code} {detail}")


def _box(mailbox: str | None = None) -> str:
    return f"/users/{mailbox or MAILBOX}"


# ---- commands ------------------------------------------------------------------------------

def cmd_login(_):
    cache = _load_cache()
    app = _app(cache)
    flow = app.initiate_device_flow(scopes=SCOPES)
    if "user_code" not in flow:
        sys.exit(f"could not start the sign-in: {flow.get('error_description', flow)}")
    print(flow["message"], flush=True)
    result = app.acquire_token_by_device_flow(flow)
    _save_cache(cache)
    if "access_token" not in result:
        sys.exit(f"sign-in failed: {result.get('error_description', result.get('error'))}")
    print(f"signed in as {result.get('id_token_claims', {}).get('preferred_username', '?')}")


def cmd_list(args):
    token = _token()
    since = (datetime.now(timezone.utc) - timedelta(days=args.days)).strftime("%Y-%m-%dT%H:%M:%SZ")
    sent = _graph("GET", f"{_box()}/mailFolders/sentitems/messages?$top=200&$select=conversationId,sentDateTime"
                  f"&$filter=sentDateTime ge {since}", token=token)["value"]
    answered = {}
    for m in sent:
        answered[m["conversationId"]] = max(answered.get(m["conversationId"], ""), m["sentDateTime"])
    q = (f"{_box()}/mailFolders/inbox/messages?$top=100&$orderby=receivedDateTime desc"
         f"&$select=id,receivedDateTime,from,subject,conversationId&$filter=receivedDateTime ge {since}")
    for m in _graph("GET", q, token=token)["value"]:
        who = (m.get("from") or {}).get("emailAddress", {}).get("address", "?")
        done = "answered" if answered.get(m["conversationId"], "") > m["receivedDateTime"] else "OPEN    "
        print(f"{m['receivedDateTime'][:10]}  {done}  {who:<40.40}  {m.get('subject') or '(no subject)'}\n    {m['id']}")


def cmd_show(args):
    m = _graph("GET", f"{_box()}/messages/{args.id}?$select=from,toRecipients,receivedDateTime,subject,body,conversationId",
               token=_token())
    who = (m.get("from") or {}).get("emailAddress", {})
    print(f"From:    {who.get('name', '')} <{who.get('address', '?')}>")
    print(f"Date:    {m['receivedDateTime']}")
    print(f"Subject: {m.get('subject') or '(no subject)'}\n")
    print(_text(m["body"]))


def _text(body: dict) -> str:
    """The body as plain text: Graph gives HTML (the Prefer header), so strip tags crudely."""
    content = body.get("content", "")
    if body.get("contentType") != "html":
        return content
    content = re.sub(r"(?is)<(script|style|head)\b.*?</\1>", "", content)
    content = re.sub(r"(?i)<br\s*/?>|</p>|</div>|</tr>|</li>", "\n", content)
    content = html.unescape(re.sub(r"<[^>]+>", "", content))
    return re.sub(r"\n\s*\n\s*\n+", "\n\n", content).strip()


def _draft(message_id: str, html: str, token: str, mailbox: str | None = None) -> str:
    draft = _graph("POST", f"{_box(mailbox)}/messages/{message_id}/createReply", body={}, token=token)
    quoted = (draft.get("body") or {}).get("content", "")
    _graph("PATCH", f"{_box(mailbox)}/messages/{draft['id']}",
           body={"body": {"contentType": "html", "content": html + quoted}}, token=token)
    return f"{draft.get('subject')} -> {', '.join(r['emailAddress']['address'] for r in draft.get('toRecipients', []))}"


def cmd_draft(args):
    print(_draft(args.id, Path(args.body).read_text(encoding="utf-8"), _token()))


def _latest_from(sender: str, token: str, mailbox: str | None = None) -> str:
    """The id of the newest message from sender (an address), in any folder, or exit when there is none."""
    # No $orderby: Graph refuses it beside a filter on another property ("too complex").
    q = (f"{_box(mailbox)}/messages?$top=50&$select=id,from,receivedDateTime"
         f"&$filter=from/emailAddress/address eq '{sender.replace(chr(39), '')}'")
    hits = _graph("GET", q, token=token)["value"]
    if not hits:
        sys.exit(f"no inbox message from {sender}")
    return max(hits, key=lambda m: m["receivedDateTime"])["id"]


def cmd_reply_latest(args):
    token = _token()
    print(_draft(_latest_from(args.sender, token), Path(args.body).read_text(encoding="utf-8"), token))


def cmd_batch(args):
    """Each item is {"id": ..., "body": ...} or {"from": <address>, "body": ...} (its newest message)."""
    token = _token()
    base = Path(args.file).parent
    for item in json.loads(Path(args.file).read_text(encoding="utf-8")):
        box = MAILBOXES.get(item.get("mailbox", ""), item.get("mailbox")) if item.get("mailbox") else None
        target = item["id"] if "id" in item else _latest_from(item["from"], token, box)
        print("drafted:", _draft(target, (base / item["body"]).read_text(encoding="utf-8"), token, box))


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("-m", "--mailbox", choices=sorted(MAILBOXES), default="support")
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("login").set_defaults(fn=cmd_login)
    ls = sub.add_parser("list")
    ls.add_argument("--days", type=int, default=30)
    ls.set_defaults(fn=cmd_list)
    sh = sub.add_parser("show")
    sh.add_argument("id")
    sh.set_defaults(fn=cmd_show)
    d = sub.add_parser("draft")
    d.add_argument("id")
    d.add_argument("body")
    d.set_defaults(fn=cmd_draft)
    r = sub.add_parser("reply-latest")
    r.add_argument("sender")
    r.add_argument("body")
    r.set_defaults(fn=cmd_reply_latest)
    b = sub.add_parser("batch")
    b.add_argument("file")
    b.set_defaults(fn=cmd_batch)
    args = p.parse_args()
    global MAILBOX
    MAILBOX = MAILBOXES[args.mailbox]
    args.fn(args)


if __name__ == "__main__":
    main()

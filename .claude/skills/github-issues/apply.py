"""Apply a triage plan to crazy-rabbit-software/themia issues and the nwalser/projects/4 board.

    python -X utf8 .claude/skills/github-issues/apply.py plan.json [--dry-run]

plan.json is a list of entries, one per issue; every key but "n" is optional:

    {"n": 189,
     "type": "Bug" | "Feature" | "Task",
     "add": ["area:files"], "remove": ["widget-request"],
     "milestone": "v1.0.0" | null            (null removes it; omit to leave it)
     "assign": false                         (default: nwalser is added to every issue)
     "status": "Todo" | "In progress" | "Blocked" | "Done not Tested" | "Done not Released" | "Done",
     "priority": "Low" | "Medium" | "High",
     "size": "XS" | "S" | "M" | "L" | "XL",
     "parent": 76,                           (becomes a sub-issue of #76)
     "comment": "path/to/reply.md" or the text itself,
     "close": "completed" | "not planned" | {"duplicate": 132}}

Order per issue: comment, then fields, then close, so the reply is the last thing the reporter
reads before the issue closes. Every gh call is printed; a failing call is reported and the run
goes on with the next field (the summary at the end lists failures). Nothing is deleted.
"""

import json
import subprocess
import sys
from pathlib import Path

REPO = "crazy-rabbit-software/themia"
OWNER = "nwalser"
PROJECT = 4
PROJECT_ID = "PVT_kwHOAfy6XM4BVSm_"
ME = "nwalser"

DRY = "--dry-run" in sys.argv
failures = []


def gh(*args, capture=False):
    print("  gh " + " ".join(a if " " not in a else repr(a) for a in args))
    if DRY:
        return ""
    r = subprocess.run(["gh", *args], capture_output=True, text=True, encoding="utf-8")
    if r.returncode != 0:
        failures.append(f"gh {' '.join(args[:4])}: {r.stderr.strip()[:200]}")
        print("    FAILED: " + r.stderr.strip()[:200])
        return None
    return r.stdout


def board():
    """Field ids, option ids, and the board item of every issue number."""
    fields = json.loads(subprocess.run(
        ["gh", "project", "field-list", str(PROJECT), "--owner", OWNER, "--format", "json"],
        capture_output=True, text=True, encoding="utf-8", check=True).stdout)["fields"]
    options = {f["name"]: (f["id"], {o["name"]: o["id"] for o in f.get("options", [])}) for f in fields}
    items = json.loads(subprocess.run(
        ["gh", "project", "item-list", str(PROJECT), "--owner", OWNER, "--limit", "1000", "--format", "json"],
        capture_output=True, text=True, encoding="utf-8", check=True).stdout)["items"]
    by_number = {i["content"]["number"]: i["id"] for i in items
                 if i.get("content", {}).get("repository") == REPO and "number" in i.get("content", {})}
    return options, by_number


def item_for(n, items):
    if n in items:
        return items[n]
    out = gh("project", "item-add", str(PROJECT), "--owner", OWNER,
             "--url", f"https://github.com/{REPO}/issues/{n}", "--format", "json")
    if out:
        items[n] = json.loads(out)["id"]
        return items[n]
    return None


def main():
    plan = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    options, items = board()

    for e in plan:
        n = e["n"]
        print(f"#{n}")

        if "comment" in e:
            text = e["comment"]
            path = Path(text)
            if len(text) < 260 and path.suffix == ".md" and path.exists():
                text = path.read_text(encoding="utf-8")
            gh("issue", "comment", str(n), "-R", REPO, "--body", text.strip())

        if "type" in e:
            gh("api", "-X", "PATCH", f"repos/{REPO}/issues/{n}", "-f", f"type={e['type']}", "--silent")

        edit = []
        for label in e.get("add", []):
            edit += ["--add-label", label]
        for label in e.get("remove", []):
            edit += ["--remove-label", label]
        if "milestone" in e:
            edit += ["--milestone", e["milestone"]] if e["milestone"] else ["--remove-milestone"]
        # Nathaniel is the assignee of every issue (his rule); only an explicit false removes him.
        if e.get("assign", True):
            edit += ["--add-assignee", ME]
        else:
            edit += ["--remove-assignee", ME]
        if edit:
            gh("issue", "edit", str(n), "-R", REPO, *edit)

        for field, key in (("Status", "status"), ("Priority", "priority"), ("Size", "size")):
            if key in e:
                field_id, opts = options[field]
                item = item_for(n, items)
                if item and e[key] in opts:
                    gh("project", "item-edit", "--id", item, "--project-id", PROJECT_ID,
                       "--field-id", field_id, "--single-select-option-id", opts[e[key]])
                elif e[key] not in opts:
                    failures.append(f"#{n}: no {field} option '{e[key]}'")

        if "parent" in e:
            child = gh("api", f"repos/{REPO}/issues/{n}", "--jq", ".id")
            if child:
                gh("api", "-X", "POST", f"repos/{REPO}/issues/{e['parent']}/sub_issues",
                   "-F", f"sub_issue_id={child.strip()}", "--silent")

        close = e.get("close")
        if isinstance(close, dict) and "duplicate" in close:
            gh("issue", "close", str(n), "-R", REPO, "--duplicate-of", str(close["duplicate"]))
        elif close:
            gh("issue", "close", str(n), "-R", REPO, "--reason", close)

    print("\n" + ("dry run, nothing changed" if DRY else f"done, {len(failures)} failure(s)"))
    for f in failures:
        print("  " + f)


if __name__ == "__main__":
    main()

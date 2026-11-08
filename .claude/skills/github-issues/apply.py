"""Apply a triage plan to crazy-rabbit-software/themia issues (labels, type, milestone, issue fields).

    python -X utf8 .claude/skills/github-issues/apply.py plan.json [--dry-run]

plan.json is a list of entries, one per issue; every key but "n" is optional:

    {"n": 189,
     "type": "Bug" | "Feature" | "Task",
     "add": ["area:files"], "remove": ["widget-request"],
     "milestone": "v1.0.0" | null            (null removes it; omit to leave it)
     "assign": false                         (default: nwalser is added to every issue)
     "status": "Todo" | "In progress" | "Blocked" | "Done not Tested" | "Done not Released" | "Done",
     "priority": "Low" | "Medium" | "High" | "Urgent",
     "effort": "Low" | "Medium" | "High",
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
ORG = "crazy-rabbit-software"
ME = "nwalser"

# Plan key -> the organization's issue field (Status, Priority, Effort are single selects).
FIELDS = (("Status", "status"), ("Priority", "priority"), ("Effort", "effort"))

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


def issue_fields():
    """{field name: (field id, {option name: option id})} of the organization's issue fields."""
    q = ('{organization(login:"%s"){issueFields(first:50){nodes{... on IssueFieldSingleSelect'
         '{id name options{id name}}}}}}' % ORG)
    out = subprocess.run(["gh", "api", "graphql", "-f", f"query={q}"],
                         capture_output=True, text=True, encoding="utf-8", check=True).stdout
    nodes = json.loads(out)["data"]["organization"]["issueFields"]["nodes"]
    return {f["name"]: (f["id"], {o["name"]: o["id"] for o in f["options"]}) for f in nodes if f}


def main():
    plan = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    options = issue_fields()

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

        values = []
        for field, key in FIELDS:
            if key not in e:
                continue
            if field not in options:
                failures.append(f"#{n}: no issue field '{field}' in {ORG}")
            elif e[key] not in options[field][1]:
                failures.append(f"#{n}: no {field} option '{e[key]}'")
            else:
                field_id, opts = options[field]
                values.append(f'{{fieldId:"{field_id}",singleSelectOptionId:"{opts[e[key]]}"}}')
        if values:
            node = gh("api", f"repos/{REPO}/issues/{n}", "--jq", ".node_id") if not DRY else "<node>"
            if node:
                gh("api", "graphql", "-f", "query=mutation{setIssueFieldValue(input:{issueId:\"%s\","
                   "issueFields:[%s]}){issue{number}}}" % (node.strip(), ",".join(values)), "--silent")

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

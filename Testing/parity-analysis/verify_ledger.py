"""Check the curation ledgers against the pipeline's own output.

`ledger-resharper.json` and `ledger-sonar.json` record why each of ReSharper's and Sonar's
rules did or did not become a Skala rule proposal. The claim they make is *completeness*:
every rule is accounted for exactly once. That claim is worth nothing unless something
checks it, because the failure mode is silent -- a rule dropped from both the concept list
and the exclusion list simply disappears, and the next audit re-derives it from scratch.

Structural checks always run. The cross-checks against the pipeline need its generated
inputs and are skipped, loudly, when they are absent:

    python3 universe.py && python3 classify.py   # -> classified.json
    python3 fetch_sonar.py                       # -> sonar.json

Exit code is 1 if any check fails. Reconcile items are *not* failures -- see below.
"""
import json, os, sys

W = os.path.dirname(os.path.abspath(__file__))
fail, warn = [], []

RANGES = {"SK0001-SK0999", "SK1000-SK1999", "SK2000-SK2999", "SK3000-SK3499", "SK3500-SK3999",
          "SK4000-SK4999", "SK5000-SK5999", "SK6000-SK6999", "SK7000-SK7999", "SK8000-SK8999"}
SEVERITIES = {"error", "warning", "suggestion", "hint", "none"}
SCOPES = {"Syntax", "Semantic", "Compilation"}
FIXES = {"safe", "unsafe", "none"}


def structural(ledger, name, member):
    """Checks that need nothing but the ledger itself."""
    slugs, issues = {}, {}
    for c in ledger["concepts"]:
        if c["slug"] in slugs:
            fail.append(f"{name}: duplicate concept slug {c['slug']!r}")
        slugs[c["slug"]] = c
        if c["issue"] in issues:
            fail.append(f"{name}: issue #{c['issue']} claimed by both "
                        f"{issues[c['issue']]!r} and {c['slug']!r}")
        issues[c["issue"]] = c["slug"]
        if not c[member]:
            fail.append(f"{name}: concept {c['slug']!r} names no rule")
        for field, allowed in (("proposedRange", RANGES), ("proposedSeverity", SEVERITIES),
                               ("proposedScope", SCOPES), ("proposedFix", FIXES)):
            if c[field] not in allowed:
                fail.append(f"{name}: concept {c['slug']!r} has {field}={c[field]!r}, "
                            f"which is not one of {sorted(allowed)}")
    seen = {}
    for c in ledger["concepts"]:
        for r in c[member]:
            if r in seen:
                fail.append(f"{name}: {r} is claimed by both {seen[r]!r} and {c['slug']!r}")
            seen[r] = c["slug"]
    return seen, slugs


# --------------------------------------------------------------- ReSharper
rs = json.load(open(f"{W}/ledger-resharper.json"))
assigned, rs_slugs = structural(rs, "ledger-resharper", "inspections")
excluded = {e["inspection"]: e["reason"] for e in rs["excluded"]}

for e in rs["excluded"]:
    if not e["reason"].strip():
        fail.append(f"ledger-resharper: {e['inspection']} is excluded with an empty reason")
both = sorted(set(assigned) & set(excluded))
for i in both:
    fail.append(f"ledger-resharper: {i} is both assigned to {assigned[i]!r} and excluded")

if os.path.exists(f"{W}/classified.json"):
    rows = json.load(open(f"{W}/classified.json"))
    bucket = {(r["id"] or r["key"]): r["bucket"] for r in rows}
    uncovered = {k for k, b in bucket.items() if b == "Uncovered"}

    for k in sorted(uncovered - set(assigned) - set(excluded)):
        fail.append(f"ledger-resharper: {k} is Uncovered and appears in neither the concepts "
                    f"nor the exclusions -- it would be lost")
    for k in sorted(set(excluded) - uncovered):
        warn.append(f"reconcile: {k} is excluded by the ledger but the pipeline now buckets it "
                    f"{bucket.get(k, '<absent>')!r}")
    # An assigned inspection may legitimately be `Catalogued`: a concept can name a rule that
    # docs/plan/08 already allocates an id for but has never shipped (SK1002, SK1004).
    for k in sorted(set(assigned) - uncovered):
        b = bucket.get(k, "<absent>")
        if b != "Catalogued":
            warn.append(f"reconcile: {k} is assigned to {assigned[k]!r} but the pipeline now "
                        f"buckets it {b!r} -- the concept may need narrowing or closing")
    print(f"resharper: {len(rs['concepts'])} concepts covering {len(assigned)} inspections, "
          f"{len(excluded)} excluded, against {len(uncovered)} Uncovered rows")
else:
    warn.append("classified.json is absent -- ReSharper completeness NOT checked. "
                "Run: python3 universe.py && python3 classify.py")

# --------------------------------------------------------------- SonarQube
sn = json.load(open(f"{W}/ledger-sonar.json"))
claimed, sn_slugs = structural(sn, "ledger-sonar", "rules")
resolved = {e["rule"]: e for e in sn["resolved"]}

for e in sn["resolved"]:
    if not (e.get("detail") or e.get("concept")):
        fail.append(f"ledger-sonar: {e['rule']} is resolved with no detail and no concept")
    if e["resolution"] == "tracked-by-resharper-issue" and e.get("concept") not in rs_slugs:
        fail.append(f"ledger-sonar: {e['rule']} is tracked by concept {e.get('concept')!r}, "
                    f"which is not in ledger-resharper")
for k in sorted(set(claimed) & set(resolved)):
    fail.append(f"ledger-sonar: {k} is both claimed by {claimed[k]!r} and resolved")

if os.path.exists(f"{W}/sonar.json"):
    rules = set(json.load(open(f"{W}/sonar.json")))
    for k in sorted(rules - set(claimed) - set(resolved)):
        fail.append(f"ledger-sonar: {k} is published by Sonar and appears in neither the "
                    f"concepts nor the resolutions -- it would be lost")
    for k in sorted((set(claimed) | set(resolved)) - rules):
        warn.append(f"reconcile: {k} is in the ledger but Sonar no longer publishes it")
    print(f"sonar:     {len(sn['concepts'])} concepts covering {len(claimed)} rules, "
          f"{len(resolved)} resolved, against {len(rules)} published rules")
else:
    warn.append("sonar.json is absent -- SonarQube completeness NOT checked. "
                "Run: python3 fetch_sonar.py")

# --------------------------------------------------------------- verdict
for w in warn:
    print("WARN  " + w)
for f in fail:
    print("FAIL  " + f)
print(f"\n{len(fail)} failures, {len(warn)} warnings")
sys.exit(1 if fail else 0)

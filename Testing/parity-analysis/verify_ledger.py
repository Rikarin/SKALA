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

A second completeness claim is checked here, about the *shipped* catalogue rather than the
proposal queue -- see "the shipped catalogue" at the bottom of this file.
"""
import json, os, sys

W = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(W))
fail, warn = [], []

COVERAGE = {"complete", "partial"}

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

# ------------------------------------------- SonarQube: open, unimplemented rule ideas
# These are upstream GitHub issues rather than published rules, so there is no file to
# cross-check against -- `gh issue list` is the only source and it moves. The checks are
# therefore internal: an idea must be claimed by exactly one concept or resolved once.
ideas = sn.get("ideas")
if ideas:
    seen, claimed = {}, {}
    for c in ideas["concepts"]:
        if not c["upstreamIssues"]:
            fail.append(f"ledger-sonar.ideas: concept {c['slug']!r} names no upstream issue")
        if c["slug"] in sn_slugs or c["slug"] in rs_slugs:
            fail.append(f"ledger-sonar.ideas: slug {c['slug']!r} collides with an existing concept")
        for n in c["upstreamIssues"]:
            if n in claimed:
                fail.append(f"ledger-sonar.ideas: upstream #{n} is claimed by both "
                            f"{claimed[n]!r} and {c['slug']!r}")
            claimed[n] = c["slug"]
    for e in ideas["resolved"]:
        n = e["upstreamIssue"]
        if n in claimed:
            fail.append(f"ledger-sonar.ideas: upstream #{n} is both claimed by {claimed[n]!r} "
                        f"and resolved")
        if n in seen:
            fail.append(f"ledger-sonar.ideas: upstream #{n} is resolved twice")
        seen[n] = e
        if e["resolution"] == "already-tracked" and e.get("concept") not in set(rs_slugs) | set(sn_slugs):
            fail.append(f"ledger-sonar.ideas: #{n} is tracked by concept {e.get('concept')!r}, "
                        f"which is in neither ledger")
    total = len(claimed) + len(seen)
    if total != ideas["auditedAgainst"]["count"]:
        fail.append(f"ledger-sonar.ideas: {total} ideas accounted for, but the audit recorded "
                    f"{ideas['auditedAgainst']['count']} open -- one was dropped")
    print(f"sonar ideas: {len(ideas['concepts'])} concepts covering {len(claimed)} upstream "
          f"issues, {len(seen)} resolved, of {ideas['auditedAgainst']['count']} audited")
    warn.append("the open-idea audit is a snapshot of "
                f"{ideas['auditedAgainst']['date']}; upstream issues move and nothing here "
                "re-checks them. Re-run the `gh issue list` in `auditedAgainst.query` to refresh.")

# --------------------------------------------------- the shipped catalogue
# ⚠ Everything above this line checks the *proposal* queue: that no ReSharper inspection or
# Sonar rule can be dropped from the ledger without a word. Nothing checked the other
# direction -- that a rule which has actually **shipped** is recorded as having shipped --
# and the cost of that was measured: 84 rules landed in `rules.json` while `catalogued.json`
# went almost entirely un-updated, so every inspection those rules cover kept being counted
# `Uncovered`. The published parity gap was inflated by work that was already done, and
# doc 17's residue -- which is the work queue -- was counting it as still owed.
#
# A zero from a disabled instrument and a zero from clean code are the same zero. These two
# assertions are what stop the map from going quiet again.
#
# ⚠ The first of them is *also* asserted in C#, by
# `RuleCatalogTests.TheParityMap_CreditsEveryShippedReSharperMappingToItsOwnRule`. The
# duplication is deliberate and not an oversight: this pipeline is outside the solution by
# design, it is edited and re-run by people who never invoke `dotnet test`, and an assertion
# that lives only in the test project does not protect the file being hand-edited here.
RULES = f"{REPO}/Rules/Rikarin.Skala.Rules.Metadata/rules.json"
shipped = {r["id"]: r for r in json.load(open(RULES))["rules"]}
catalogued = json.load(open(f"{W}/catalogued.json"))

# Anti-vacuity. Both loops below pass happily against an empty catalogue or an empty map, and
# an empty file is the exact shape of the failure they exist to report.
if len(shipped) < 50:
    fail.append(f"rules.json holds {len(shipped)} rules; that is not the catalogue, and every "
                f"check below would pass vacuously against it")
if len(catalogued) < 100:
    fail.append(f"catalogued.json holds {len(catalogued)} entries; that is not the map, and the "
                f"parity-map check below would pass vacuously against it")

# (1) rules.json subset of catalogued.json, matched on the SK id. An inspection that a shipped
#     rule declares as its `resharperId` and the map does not credit to that rule is measured
#     as an uncovered gap by classify.py -- work already done, back on the queue.
declaring = [r for r in shipped.values() if r.get("resharperId")]
if len(declaring) < 10:
    fail.append(f"only {len(declaring)} shipped rules declare a resharperId; the parity-map "
                f"check has nothing to assert against")
for r in declaring:
    credited = catalogued.get(r["resharperId"])
    if credited is None:
        fail.append(f"catalogued.json: {r['id']} ships {r['resharperId']!r} and the parity map "
                    f"does not mention it -- classify.py will count that inspection Uncovered")
    elif credited != r["id"]:
        fail.append(f"catalogued.json: {r['id']} ships {r['resharperId']!r} but the map credits "
                    f"it to {credited} -- one of the two is wrong and the gap hides either way")

# (2) A concept that claims coverage must name a rule that exists. `coveredBy` is the shipped
#     SK ids now covering the concept; `coverage` is "complete" (every member id listed on the
#     concept is covered) or "partial". ⚠ `complete` is the load-bearing one: it is the claim
#     that closes a GitHub issue, so a typo'd or aspirational id in it retires a tracked piece
#     of work against a rule that does not exist.
def covered(ledger, name):
    complete = partial = 0
    for c in ledger["concepts"]:
        cov, by = c.get("coverage"), c.get("coveredBy") or []
        if cov is None and not by:
            continue
        if cov not in COVERAGE:
            fail.append(f"{name}: concept {c['slug']!r} has coverage={cov!r}, "
                        f"which is not one of {sorted(COVERAGE)}")
        for sk in by:
            if sk not in shipped:
                fail.append(f"{name}: concept {c['slug']!r} is covered by {sk}, which is not a "
                            f"rule in rules.json -- the coverage claim names nothing that ships")
        if cov == "complete":
            if not by:
                fail.append(f"{name}: concept {c['slug']!r} is marked complete and names no rule")
            elif not any(sk in shipped for sk in by):
                fail.append(f"{name}: concept {c['slug']!r} is marked complete but not one of "
                            f"{by} ships -- a complete claim must rest on a shipped rule")
            complete += 1
        elif cov == "partial":
            partial += 1
    return complete, partial


rs_complete, rs_partial = covered(rs, "ledger-resharper")
sn_complete, sn_partial = covered(sn, "ledger-sonar")
print(f"shipped:   {len(shipped)} rules, {len(declaring)} declaring a resharperId, "
      f"{len(catalogued)} parity-map entries")
print(f"coverage:  resharper {rs_complete} complete / {rs_partial} partial, "
      f"sonar {sn_complete} complete / {sn_partial} partial")

# --------------------------------------------------------------- verdict
for w in warn:
    print("WARN  " + w)
for f in fail:
    print("FAIL  " + f)
print(f"\n{len(fail)} failures, {len(warn)} warnings")
sys.exit(1 if fail else 0)

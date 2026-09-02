"""Check the curation ledgers against the pipeline's own output.

`ledger-resharper.json` and `ledger-sonar.json` record why each of ReSharper's and Sonar's
rules did or did not become a Skala rule proposal. The claim they make is *completeness*:
every rule is accounted for exactly once. That claim is worth nothing unless something
checks it, because the failure mode is silent -- a rule dropped from both the concept list
and the exclusion list simply disappears, and the next audit re-derives it from scratch.

Structural checks always run. The cross-checks against the pipeline need its generated
inputs:

    python3 classify.py       # -> classified.json
    python3 fetch_sonar.py    # -> sonar.json

⚠ **A skipped check is not a pass, and this file used to say it was.** Every cross-check
below is registered by name; a missing input marks it SKIPPED, the verdict names it, and the
run exits **2**. The verdict line never reads a bare `0 failures` unless every registered
check actually executed. That is #311, and it was not theoretical: `universe.json` and
`types-2026.xml` were both gitignored, so in a fresh clone or agent worktree the parity-map
key-validity check -- the strongest assertion in the file, and the one that catches an
invented inspection id -- did not run, and roughly thirty agents read the resulting
`0 failures` as verification. A zero from a disabled check and a zero from a clean map are
the same zero; this file exists to say so and was the thing saying it wrongly.

⚠ The parity-map key check no longer has a skip path at all. `types-2026.xml` is committed
now and `universe.py` exposes `build()`, so the universe is derived in-process from two
committed inputs and the check runs everywhere, unconditionally. Deleting `universe.json` no
longer disables it -- verify that when you change this, because that was the bug.

`--allow-skips` downgrades a skipped check to a warning and restores exit 0/1. It exists for
the genuinely degraded case (no network for `fetch_sonar.py`); it is not the default,
because "I could not verify the thing you asked me to verify" is not a pass.

Exit codes: 0 all checks ran and passed, 1 a check failed, 2 a check could not run.
Reconcile items are *not* failures -- see below.

A second completeness claim is checked here, about the *shipped* catalogue rather than the
proposal queue -- see "the shipped catalogue" at the bottom of this file.
"""
import json, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import universe as universe_mod

W = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(W))
fail, warn = [], []

ALLOW_SKIPS = "--allow-skips" in sys.argv

# ⚠ The register of cross-checks. A check that needs a generated input registers here so the
# verdict can say it did not run. The failure this prevents is structural: an `if
# os.path.exists(...)` with an `else: warn` reads as diligence and prints the same final line
# as a clean run, because a warning is not counted anywhere the exit code can see it.
CHECKS = {}


def ran(name, detail=""):
    CHECKS[name] = ("ran", detail)


def skipped(name, how):
    CHECKS[name] = ("skipped", how)

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
    ran("resharper completeness", f"{len(uncovered)} Uncovered rows")
else:
    skipped("resharper completeness",
            "classified.json is absent -- run: python3 universe.py && python3 classify.py")

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
    ran("sonar completeness", f"{len(rules)} published rules")
else:
    skipped("sonar completeness", "sonar.json is absent -- run: python3 fetch_sonar.py")

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
# ⚠ `ideas` carries concepts too, and leaving it out would have made every coverage claim written
# there unasserted -- the precise failure this section exists to prevent, reintroduced one nesting
# level down.
id_complete, id_partial = covered(sn["ideas"], "ledger-sonar.ideas") if sn.get("ideas") else (0, 0)

# (3) ⚠ Every key in the parity map must be an inspection that exists. This was found the
#     hard way: `MemberCanBeInternal.Global` is not a ReSharper id at all -- the real one is
#     `MemberCanBeInternal`, with no `.Global` suffix -- so the entry matched no universe row,
#     credited nothing, and had sat there being counted as one of the map's 142 entries.
#     Sixteen more were in the same state. An invented key is worse than a missing one: a
#     missing entry inflates the gap visibly, an invented one inflates the *map* and makes the
#     coverage look larger than it is, in a file whose whole purpose is to be trusted.
#
#     The C# test guards the values (every id is one doc 08 knows) and never guarded the keys.
#
#     ⚠ The known-inert entries are named here rather than tolerated silently, each with what
#     is actually wrong with it. This is the same "excluded with a written reason" discipline
#     the ledgers use: an entry that simply never appears is indistinguishable from one nobody
#     looked at. Re-point or drop one and delete its line; do not add to this list to make a
#     new bad entry pass.
INERT = {
    # id does not exist in ReSharper; the real inspection is named on the right.
    # `MemberCanBeInternal.Global` was here and has been dropped from the map: the id was invented
    # (ReSharper has no `.Global` suffix on it) and it pointed at SK6002, which doc 08 allocates to
    # a different concept. #114 closed out of scope, so nothing will ever ship for it.
    "AbstractTypeWithPublicConstructor": "invented; the real id is PublicConstructorInAbstractClass, "
                                         "which is now mapped to SK6003 -- drop this one",
    "CyclomaticComplexity": "invented; ReSharper has no complexity-threshold inspection. "
                            "FunctionComplexityOverflow is 'body too complex to analyse', a "
                            "different thing, so this is a drop and not a re-point",
    "CognitiveComplexity": "invented; same as CyclomaticComplexity",
    "ClosureAllocation": "invented; not in the 2026 dump under any name",
    "ImplicitlyCapturedClosure": "real in older ReSharper, absent from this dump and the export",
    "AsyncApostrophe": "invented",
    "CommentTypo": "invented",
    "CommentedOutCode": "invented",
    "SelfAssignment": "invented; no C# inspection of this name in the dump",
    "SimplifyLinqExpressionUseMinByMaxBy": "invented; the dump has UseAll and UseAny only",
    "ThrowingSystemException": "invented",
    "UnusedPragmaWarningRestore": "invented; RedundantDisableWarningComment is the nearest real "
                                  "id and is a different concept (ReSharper's own disable comment)",
    "UseSearchValues": "invented; no SearchValues inspection in the dump",
    "UseStringComparison": "invented; the real id is SpecifyStringComparison, which classify.py "
                           "already buckets Hosted (CA1307/CA1310), so this credits nothing",
    "XunitTestWithConsoleOutput": "invented",
    # real id, but outside the measured universe, so it credits nothing either.
    "UseUtf8StringLiteral": "real in types-2026.xml, absent from editor_config_template, so no "
                            "universe row carries it",
}
# ⚠ The universe is built in-process from `editor_config_template` and `types-2026.xml`, both
#    committed, rather than read from the gitignored `universe.json`. There is deliberately no
#    `if os.path.exists(...)` here: this check has no skip path, because its skip path is what
#    #311 was. Deleting `universe.json` must not silence it -- test that, do not assume it.
#
#    ⚠ `editor_config_template` is the universe and `types-2026.xml` is only metadata joined
#    onto it. #318 measured the map's keys against the XML alone and called 26 of them
#    fabrications; 14 of those are real inspections that merely post-date the dump and carry a
#    live `resharper_*_highlighting` key in the export. Checking against the XML would fail on
#    correct entries, which is a worse instrument than the one it replaced.
uni = universe_mod.build()
if len(uni) < 500:
    fail.append(f"the inspection universe built to {len(uni)} rows; that is not the universe, "
                f"and the parity-map key check below would pass vacuously against it")
uni_ids = {v["id"] for v in uni.values() if v["id"]}
_snake = universe_mod.snake
live = 0
for iid in catalogued:
    b = _snake(iid)
    if iid in uni_ids or any(f"resharper_{b}_highlighting{s}" in uni
                             for s in ("", "_highlighting")):
        live += 1
    elif iid not in INERT:
        fail.append(f"catalogued.json: {iid!r} matches no row in the inspection universe, so "
                    f"it credits {catalogued[iid]} with nothing. Verify the id against "
                    f"editor_config_template -- an invented id is silently inert")
for iid in sorted(set(INERT) - set(catalogued)):
    warn.append(f"reconcile: {iid!r} is on the known-inert list and is no longer in "
                f"catalogued.json -- delete its line from INERT")
# ⚠ Report inert and excused separately. Printing one number for both let a newly added
# bad entry read as one more of the ones already known about.
inert_now = len(catalogued) - live
print(f"parity map: {live} of {len(catalogued)} entries match a universe row "
      f"({inert_now} inert, {len(set(INERT) & set(catalogued))} of them on the known list)")
ran("parity-map key validity", f"{live} of {len(catalogued)} live, over {len(uni)} universe rows")
print(f"shipped:   {len(shipped)} rules, {len(declaring)} declaring a resharperId, "
      f"{len(catalogued)} parity-map entries")
print(f"coverage:  resharper {rs_complete} complete / {rs_partial} partial, "
      f"sonar {sn_complete} complete / {sn_partial} partial, "
      f"sonar-ideas {id_complete} complete / {id_partial} partial")

# --------------------------------------------------------------- verdict
# ⚠ Skips are printed after the failures, not before, and the verdict line names them. The old
# shape put a skip in the same `warn` list as a reconcile note, printed it 26 items up from the
# bottom, and ended with `0 failures` -- which is what everybody read and quoted.
for w in warn:
    print("WARN  " + w)
for f in fail:
    print("FAIL  " + f)

missing = sorted(n for n, (state, _) in CHECKS.items() if state == "skipped")
for n in missing:
    print(f"SKIP  {n} DID NOT RUN -- {CHECKS[n][1]}")

print(f"\nchecks: {len(CHECKS) - len(missing)} run, {len(missing)} skipped"
      + (f" ({', '.join(missing)})" if missing else ""))

if missing and not ALLOW_SKIPS:
    # ⚠ Deliberately NOT phrased as "N failures". That string is what a reader greps for and
    # what an agent quotes, and printing it next to a check that did not run is the whole bug:
    # the old code ended `0 failures, 26 warnings` whether the check passed or never executed.
    print(f"verdict: INCOMPLETE -- {len(missing)} check(s) could not run, so this is NOT a pass. "
          f"Among the checks that did run: {len(fail)} failed, {len(warn)} warning(s). "
          f"Generate the inputs above, or pass --allow-skips to accept the gap deliberately.")
    sys.exit(2)
if missing:
    warn.append("running with --allow-skips; the skipped checks above were not performed")
print(f"{len(fail)} failures, {len(warn)} warnings"
      + (f" -- ⚠ {len(missing)} check(s) skipped by --allow-skips" if missing else ""))
sys.exit(1 if fail else 0)

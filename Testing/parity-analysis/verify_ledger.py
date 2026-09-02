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
import json, os, re, sys

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
ALLOCATED = f"{REPO}/Rules/Rikarin.Skala.Rules.Metadata/allocated-ids.txt"
# ⚠ `retired` is filtered out, and the variable name is the reason. A rule retired AFTER shipping
# keeps its rules.json entry -- that is how its descriptor stays resolvable for an `.editorconfig`
# key and its docs page stays a tombstone -- so `rules.json` is a list of rules that *exist*, not a
# list of rules that ship. Reading it unfiltered made this file demand that `catalogued.json` credit
# an inspection to a withdrawn rule, which is the opposite of what the map should say: a retired
# rule covers nothing and the inspection belongs to the `Hosted` bucket. Same fix as `classify.py`'s
# `shipped_ids`, for the same reason, in the file whose comment above says the duplication is
# deliberate.
shipped = {r["id"]: r for r in json.load(open(RULES))["rules"] if not r.get("retired", False)}
catalogued = json.load(open(f"{W}/catalogued.json"))
allocated = {ln.split(None, 1)[0] for ln in open(ALLOCATED)
             if ln.strip() and not ln.startswith("#")}

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

# (1b) ⚠ Every value must name an id the register has actually allocated. Nothing here checked
#      the values at all -- a sabotage run added `"UseNameofExpression": "SK9999"` to the map and
#      the whole file still exited 0. The C# test does check them, but with
#      `register.Contains(id)`, a substring match against the whole of doc 08 -- which passes for
#      an id doc 08 mentions *only in order to say it will never be built*. That is how the map
#      came to credit five inspections to `SK2006`, `SK8001`, `SK8003` and `SK8004`, every one of
#      them in doc 08's cut tables (`CS0177`, xUnit1001, xUnit1049, "no mechanical fix"). The
#      bucket said "Skala covers this" for rules that were measured and declined.
#
#      `allocated-ids.txt` is the register (ADR-012, append-only), so it is what this asserts
#      against. `PLANNED` is the written-reason escape hatch for a concept doc 08 specifies and
#      has deliberately not allocated an id for yet -- CLAUDE.md forbids allocating ahead of a
#      specification, so this list is expected to be short and non-empty.
PLANNED = {
    "SK1002": "doc 08 § 'SK1000 — modernization' table and § M5: primary constructors are a "
              "declaration-shape rewrite with no safe fix, deferred rather than declined",
    "SK6004": "doc 08 § 'SK6000 — API and design': 'the other two remain outstanding' -- "
              "interface with one implementation is specified and not yet allocated",
}
for iid, sk in sorted(catalogued.items()):
    if not re.fullmatch(r"SK[0-9]{4}", sk):
        fail.append(f"catalogued.json: {iid!r} -> {sk!r} is not a well-formed rule id")
    elif sk not in shipped and sk not in allocated and sk not in PLANNED:
        fail.append(f"catalogued.json: {iid!r} is credited to {sk}, which neither ships nor is "
                    f"allocated in allocated-ids.txt. The map is claiming coverage from a rule "
                    f"that does not exist; either the id is wrong or the entry should be dropped "
                    f"so the inspection returns to the measured gap")
for sk in sorted(set(PLANNED) - set(catalogued.values())):
    warn.append(f"reconcile: {sk} is on the PLANNED list and nothing in catalogued.json credits "
                f"it any more -- delete its line from PLANNED")

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


# (2b) ⚠ Every concept carries a `state`, and a declined one carries its evidence. #301: the
#      vocabulary was `{complete, partial}` and both required `coveredBy` to name a shipped rule,
#      so there was no way to write down "we assessed this and decided not to build it". A
#      refutation had to be filed as an exclusion with a prose reason or not at all, which made a
#      concept that was *measured and declined* indistinguishable from one nobody had opened.
#
#      ⚠ The scale of that was not ~20, which is what #301 estimated. 196 of the 270 concepts had
#      a CLOSED issue and no coverage recorded -- every one of them reading as unexamined. The
#      losses are specific and expensive: #146 was refuted because PATH resolution is a property
#      of the environment and not of the call site, #153 because `BindingFlags.NonPublic` scored
#      0 true positives against 26 false ones, #169 because the null half is hosted by `CA1508`
#      and the residue needs a value lattice this codebase does not have. None of that was
#      anywhere the ledger could see it, so the same proposal returns in six months.
#
#      The four declining states are kept apart because **they have different futures**:
#      `out-of-reach` reopens when the machinery lands, `refuted` never does.
STATES = {
    "unexamined",    # nobody has assessed it; no issue, or an issue with no outcome recorded
    "proposed",      # an open GitHub issue tracks it
    "shipped",       # one or more SK rules cover it            -> requires coveredBy
    "hosted",        # a CA*/IDE*/compiler diagnostic covers it -> requires hostedBy + evidence
    "refuted",       # the premise is false, or the shape does not compile -> requires evidence
    "out-of-reach",  # real, but needs machinery Skala does not have      -> requires evidence
    "declined",      # real and reachable, but the false-positive cost is too high -> evidence
}
NEEDS_EVIDENCE = {"hosted", "refuted", "out-of-reach", "declined"}

# ⚠ A **ratchet, not a target**: the number of concepts carrying a decided state must never fall.
# Raise each figure as the migration proceeds; never lower one to make a run pass, because the
# only thing that makes this number drop is a migration being undone or a ledger being blanked.
#
# ⚠ It is per group and not a single total, and the sabotage run is why: with one global floor,
# blanking every ReSharper concept left the run green, because the Sonar ledger's decided rows
# carried the total past the floor on their own. A vacuity check another file's data can satisfy
# is not a vacuity check.
#
# `ledger-sonar.ideas` sits at 2 because it is genuinely un-migrated, not because the check is
# weak there. That gap is real and the `unexamined` warning below counts it.
DECIDED_FLOOR = {
    "ledger-resharper": 29,
    "ledger-sonar": 31,
    "ledger-sonar.ideas": 2,
}


def stated(ledger, name):
    counts = {}
    for c in ledger["concepts"]:
        st = c.get("state")
        counts[st] = counts.get(st, 0) + 1
        if st not in STATES:
            fail.append(f"{name}: concept {c['slug']!r} has state={st!r}, which is not one of "
                        f"{sorted(STATES)}. Every concept needs one -- a row with no state is the "
                        f"#301 gap reopening")
            continue
        ev = (c.get("evidence") or "").strip()
        # ⚠ The evidence requirement is the whole point of the field. Without it `state` becomes a
        # place to write "refuted" without having measured anything, which is worse than the gap
        # it replaced: it looks like a decision and carries none of the reasoning that made one.
        if st in NEEDS_EVIDENCE and not ev:
            fail.append(f"{name}: concept {c['slug']!r} is {st!r} and carries no evidence. "
                        f"Record the probe, the diagnostic id or the measurement that decided it")
        if st == "hosted" and not (c.get("hostedBy") or []):
            fail.append(f"{name}: concept {c['slug']!r} is 'hosted' and does not say what hosts "
                        f"it -- ADR-008 needs the diagnostic id, not just the verdict")
        if st == "shipped" and not (c.get("coveredBy") or []):
            fail.append(f"{name}: concept {c['slug']!r} is 'shipped' and names no rule")
        # ⚠ The two vocabularies must not drift apart. `coverage: complete` is what closes an
        # issue, so it cannot sit on a concept whose state says nothing shipped.
        if c.get("coverage") == "complete" and st != "shipped":
            fail.append(f"{name}: concept {c['slug']!r} has coverage='complete' but state={st!r} "
                        f"-- a complete coverage claim only makes sense on a shipped concept")
    return counts


state_counts = {}
for _led, _name in ((rs, "ledger-resharper"), (sn, "ledger-sonar"),
                    *(((sn["ideas"], "ledger-sonar.ideas"),) if sn.get("ideas") else ())):
    _counts = stated(_led, _name)
    for k, v in _counts.items():
        state_counts[k] = state_counts.get(k, 0) + v
    # Anti-vacuity, ⚠ **per ledger and not in total**. A migration that set every row to
    # `unexamined` satisfies every assertion above while recording exactly what the old schema
    # did, which is nothing. The first spelling of this counted across all three groups, and the
    # sabotage run caught it: blanking every ReSharper concept left the run green because the
    # Sonar ledger's 33 decided rows carried the total past the floor on their own. A vacuity
    # check that another file's data can satisfy is not a vacuity check.
    _tot = sum(_counts.values())
    _dec = sum(v for k, v in _counts.items() if k in NEEDS_EVIDENCE or k == "shipped")
    if _dec < DECIDED_FLOOR[_name]:
        fail.append(f"{_name}: {_dec} concepts carry a decided state (shipped, hosted, refuted, "
                    f"out-of-reach or declined) of {_tot}, and the recorded floor is "
                    f"{DECIDED_FLOOR[_name]}. Either a migration was undone or rows were blanked; "
                    f"every #301 assertion above passes vacuously on an all-`unexamined` ledger")
print("states:    " + ", ".join(f"{k} {v}" for k, v in sorted(state_counts.items(),
                                                              key=lambda kv: -kv[1])))
if state_counts.get("unexamined"):
    warn.append(f"{state_counts['unexamined']} concepts are still 'unexamined'. Where the issue "
                f"is closed, the outcome exists on the issue and has not been migrated here yet "
                f"(#301) -- this number is the remaining debt and is meant to fall")

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
    #
    # ⚠ Eleven entries that used to be excused here have been **dropped from the map** by #318's
    # adjudication, and two of the reasons written above them were wrong. See `OUTSIDE_BOTH`
    # below for what replaced them and how each was decided.
    "CyclomaticComplexity": "real ReSharper C# inspection, absent from jb 2025.2.6's dump and "
                            "from the export -- see OUTSIDE_BOTH",
    "CognitiveComplexity": "real ReSharper C# inspection, absent from jb 2025.2.6's dump and "
                           "from the export -- see OUTSIDE_BOTH",
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
#
# ⚠ **The inputs are checked for presence, and their absence is a SKIP and not a pass.** That is
#    not a reintroduction of the #311 hole, it is the repair of the last corner of it. #311 was a
#    check that read `0 failures` when its input was gone. What stood here instead was a check
#    that *crashed* on a missing `editor_config_template` (an unhandled FileNotFoundError, exit 1,
#    no SKIP line, nothing in the register saying which check never ran) and, on a missing
#    `types-2026.xml`, quietly carried on export-only and reported failures it had no grounds to
#    report. Neither is a pass and neither said so in the register's own vocabulary. Both inputs
#    are committed, so this path fires only if somebody deletes one -- and then it says which.
TYPES_XML, EXPORT_FILE = universe_mod.TYPES_XML, universe_mod.EXPORT
_missing_inputs = [p for p in (TYPES_XML, EXPORT_FILE) if not os.path.exists(p)]
_how_missing = ("absent: " + ", ".join(os.path.relpath(p, REPO) for p in _missing_inputs)
                + " -- both are committed; restore from git rather than regenerating")

uni = universe_mod.build() if not _missing_inputs else {}
if _missing_inputs:
    skipped("parity-map key validity", _how_missing)
else:
    if len(uni) < 500:
        fail.append(f"the inspection universe built to {len(uni)} rows; that is not the universe, "
                    f"and the parity-map key check below would pass vacuously against it")
    uni_ids = {v["id"] for v in uni.values() if v["id"]}
    live = 0
    for iid in catalogued:
        if iid in uni_ids or any(k in uni for k in universe_mod.key_for(iid)):
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
    ran("parity-map key validity",
        f"{live} of {len(catalogued)} live, over {len(uni)} universe rows")

# (3b) ⚠ **Existence, against the UNION of the two inputs.** Check (3) above asks whether a key
#      *credits* anything -- whether it joins to a row of the measured universe, which is the
#      export alone. It cannot tell an id ReSharper has never had from one ReSharper has and the
#      author's export happens not to carry. Those are different defects with different repairs:
#      the first is a fabrication and the row must go, the second is a real inspection the
#      measurement cannot see and the row is evidence worth keeping.
#
#      ⚠ **`types-2026.xml` alone is the wrong reference, and #318 was filed measuring against
#      it.** That reported 29 of the map's keys as fabrications (26 at first, on a substring test
#      that also miscounted -- see `universe.issue_type_ids`). Ten of the 29 are real, live,
#      correctly mapped inspections that simply post-date the dump and carry a
#      `resharper_*_highlighting` key in the export: `ConvertToExtensionBlock` and
#      `MoveToExtensionBlock` are C# 14 extension blocks, and `ShortLivedHttpClient`,
#      `EscapedKeyword`, `TemplateIsNotCompileTimeConstantProblem` and the rest are ordinary
#      current inspections. **An assert-against-the-XML check would have failed on correct
#      entries** -- a worse instrument than none, because it trains the reader to ignore it.
#
#      So: a key is defensible if EITHER input knows it. A key **neither** knows is the defect.
#
#      This check subsumes nothing and replaces nothing. It runs beside (3) because the two ask
#      different questions and a row can fail either independently.
OUTSIDE_BOTH = {
    # ⚠ Real ReSharper C# inspections that neither committed input carries. Both were on the
    # known-inert list marked "invented", and **both of those notes were wrong** -- refuted by
    # JetBrains' own settings file, `JetBrains/resharper-ultimate-whatsnew/Inspections.DotSettings`,
    # which sets `InspectionSeverities/=CyclomaticComplexity` to WARNING and, three lines up,
    # `CodeInspection/CyclomaticComplexityAnalysis/Thresholds/=CSHARP` to 7. A threshold keyed on
    # CSHARP is not something a C++-only inspection has.
    #
    # Neither id occurs in a single byte of the 935 MB `jetbrains.resharper.globaltools 2025.2.6`
    # payload (`grep -rla`, with 28 of 30 sampled known-live ids found by the same grep; the two
    # misses were `.`-suffixed variants like `UseMethodAny.1`, whose base id *is* found). So they
    # are real, and they are not in the command-line tool this dump came from -- the IDE surface
    # is wider than `jb inspectcode --dumpIssuesTypes`, which is the same reason `CommentTypo` is
    # missing from the dump while the bundled ReSpeller assembly declares it.
    #
    # ⚠ **Where the check should look to retire these two lines:** a `.DotSettings` or
    # `.editorconfig` exported from **Rider**, not a `jb inspectcode --dumpIssuesTypes`. Refresh
    # `editor_config_template` from a Rider export and both should appear, at which point they
    # join the universe, `INERT` loses them too, and this list should be empty.
    "CyclomaticComplexity": "real; JetBrains' own Inspections.DotSettings sets it WARNING beside "
                            "a CyclomaticComplexityAnalysis threshold keyed on CSHARP. Absent "
                            "from jb 2025.2.6 (dump and binaries) and from the export",
    "CognitiveComplexity": "real; set in current third-party C# .DotSettings including "
                           "dotnet/ResXResourceManager and a Rider global-settings export. "
                           "Absent from jb 2025.2.6 (dump and binaries) and from the export",
}
if _missing_inputs:
    skipped("parity-map key existence (union)", _how_missing)
else:
    _xml_ids = universe_mod.issue_type_ids()
    _ec_keys = universe_mod.export_keys()
    # Anti-vacuity, and deliberately separate from the missing-input skip above: a *present but
    # truncated* input is the failure a presence test cannot see. An empty XML makes every key
    # fall through to the export, and an empty export makes the union the XML alone -- which is
    # precisely the wrong instrument this check exists to avoid being.
    if len(_xml_ids) < 1000 or len(_ec_keys) < 500:
        # ⚠ Both a failure AND a skip. The failure says the data is wrong; the skip is what keeps
        # this check's name out of the "ran" column, because it did not. Recording only the
        # failure would have printed `N failures` for a check that never executed a comparison --
        # the #311 shape wearing the opposite sign.
        fail.append(f"the union inputs built to {len(_xml_ids)} issue types and {len(_ec_keys)} "
                    f"export keys; that is not both inputs, and the existence check below would "
                    f"pass vacuously or fail wholesale against them")
        skipped("parity-map key existence (union)",
                f"inputs present but truncated: {len(_xml_ids)} issue types, "
                f"{len(_ec_keys)} export keys")
    else:
        known = 0
        for iid in sorted(catalogued):
            if iid in _xml_ids or any(k in _ec_keys for k in universe_mod.key_for(iid)):
                known += 1
            elif iid not in OUTSIDE_BOTH:
                fail.append(
                    f"catalogued.json: {iid!r} is in neither types-2026.xml nor "
                    f"editor_config_template, so no ReSharper version this repository can see "
                    f"has ever had it. It credits {catalogued[iid]} with covering an inspection "
                    f"that does not exist. Establish the real id and re-point the key, or drop "
                    f"the row so the inspection returns to the measured gap -- do not add it to "
                    f"OUTSIDE_BOTH without evidence that ReSharper really ships it")
        for iid in sorted(set(OUTSIDE_BOTH) - set(catalogued)):
            warn.append(f"reconcile: {iid!r} is excused as real-but-unlisted and is no longer in "
                        f"catalogued.json -- delete its line from OUTSIDE_BOTH")
        _residue = len(catalogued) - known
        print(f"key union:  {known} of {len(catalogued)} entries are known to types-2026.xml "
              f"({len(_xml_ids)} ids) or editor_config_template ({len(_ec_keys)} keys); "
              f"{_residue} to neither, {len(set(OUTSIDE_BOTH) & set(catalogued))} excused")
        ran("parity-map key existence (union)",
            f"{known} of {len(catalogued)} known to one of the two inputs")
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

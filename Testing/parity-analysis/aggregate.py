"""Aggregate the per-project SARIF reports into a fire count per ReSharper inspection,
and join it onto the classification.

A file analysed by two projects (a shared `Compile` item, a multi-targeted project) would
be counted twice, so findings are deduplicated on (ruleId, file, startLine, startColumn)
before counting.
"""
import json, os, glob, collections

W = os.path.dirname(os.path.abspath(__file__))
seen = set()
fires = collections.Counter()
projects = []

for p in sorted(glob.glob(f"{W}/reports/*.sarif")):
    try:
        d = json.load(open(p))
    except Exception as e:  # noqa: BLE001
        print(f"  skip {os.path.basename(p)}: {e}")
        continue
    n = 0
    for run in d.get("runs", []):
        for r in run.get("results", []):
            rid = r.get("ruleId")
            loc = (r.get("locations") or [{}])[0]
            pl = (loc.get("physicalLocation") or {})
            uri = (pl.get("artifactLocation") or {}).get("uri", "")
            reg = pl.get("region") or {}
            k = (rid, uri, reg.get("startLine"), reg.get("startColumn"))
            if k in seen:
                continue
            seen.add(k)
            fires[rid] += 1
            n += 1
    projects.append((os.path.basename(p)[:-6], n))

print(f"projects aggregated: {len(projects)}")
for name, n in projects:
    print(f"  {name:34} {n:7}")
print(f"distinct findings after dedup: {sum(fires.values())}")
print(f"distinct inspections that fired: {len(fires)}")

json.dump(dict(fires), open(f"{W}/fires.json", "w"), indent=1)

# Join onto the classification. SARIF ruleId is ReSharper's issue-type Id.
rows = json.load(open(f"{W}/classified.json"))
byid = {}
for r in rows:
    if r["id"]:
        byid.setdefault(r["id"], r)

matched = sum(1 for k in fires if k in byid)
print(f"fired inspections that are in the C#-relevant universe: {matched}")
print()
for r in rows:
    r["fires"] = fires.get(r["id"] or "", 0)
json.dump(rows, open(f"{W}/classified.json", "w"), indent=1)

cs = [r for r in rows if not r["unityOnly"]]
print("fired at least once, by bucket:")
for b in ("Uncovered", "Catalogued", "Option", "Hosted", "Compiler", "Out of scope"):
    sub = [r for r in cs if r["bucket"] == b]
    fired = [r for r in sub if r["fires"] > 0]
    tot = sum(r["fires"] for r in sub)
    print(f"  {b:14} {len(fired):4}/{len(sub):4} fired   {tot:8} findings")

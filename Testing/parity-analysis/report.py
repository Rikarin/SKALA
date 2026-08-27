"""Emit the markdown for doc 17's fire-count sections."""
import json, os, collections

W = os.path.dirname(os.path.abspath(__file__))
rows = json.load(open(f"{W}/classified.json"))
cs = [r for r in rows if not r["unityOnly"]]
unc = [r for r in cs if r["bucket"] == "Uncovered"]

fired = [r for r in unc if r["fires"] > 0]
silent = [r for r in unc if r["fires"] == 0]
print(f"uncovered: {len(unc)}   fired: {len(fired)}   silent: {len(silent)}")
print(f"total findings from uncovered inspections: {sum(r['fires'] for r in unc)}")
print()

print("### top 40 uncovered by fire count\n")
print("| Fires | Inspection | Export severity | Category | What it reports |")
print("|---:|---|---|---|---|")
for r in sorted(unc, key=lambda x: -x["fires"])[:40]:
    d = (r["desc"] or "").replace("|", "\\|")
    print(f"| {r['fires']} | `{r['id'] or r['key']}` | {r['exportSeverity']} | {r['category'] or '-'} | {d} |")

print()
print("### uncovered that fired, by category\n")
byc = collections.Counter(r["category"] or "-" for r in fired)
tot = collections.Counter()
for r in fired:
    tot[r["category"] or "-"] += r["fires"]
print("| Category | Inspections that fired | Findings |")
print("|---|---:|---:|")
for c, n in byc.most_common():
    print(f"| {c} | {n} | {tot[c]} |")

print()
print("### the 12 uncovered at `error`\n")
print("| Fires | Inspection | What it reports |")
print("|---:|---|---|")
for r in sorted([x for x in unc if x["exportSeverity"] == "error"], key=lambda x: -x["fires"]):
    d = (r["desc"] or "").replace("|", "\\|")
    print(f"| {r['fires']} | `{r['id'] or r['key']}` | {d} |")

print()
print("### other buckets, for comparison\n")
for b in ("Catalogued", "Option", "Hosted"):
    sub = [r for r in cs if r["bucket"] == b]
    f = [r for r in sub if r["fires"] > 0]
    print(f"{b}: {len(f)}/{len(sub)} fired, {sum(r['fires'] for r in sub)} findings")

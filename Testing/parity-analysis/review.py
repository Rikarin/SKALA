import json, os, collections, sys

W = os.path.dirname(os.path.abspath(__file__))
rows = json.load(open(W + "/classified.json"))
unc = [r for r in rows if r["bucket"] == "Uncovered"]
print(f"Uncovered: {len(unc)}")
print("by category:", dict(collections.Counter(r["category"] or "-" for r in unc)))
print("by export severity:", dict(collections.Counter(r["exportSeverity"] for r in unc)))
print()
cat = sys.argv[1] if len(sys.argv) > 1 else None
for r in sorted(unc, key=lambda x: (x["category"] or "", x["id"] or "")):
    if cat and (r["category"] or "-") != cat:
        continue
    print(f"{r['exportSeverity']:10} {r['category'] or '-':22} {r['id'] or r['key']:52} {(r['desc'] or '')[:62]}")

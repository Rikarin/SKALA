"""How many distinct *concepts* does the Uncovered set represent?

ReSharper splits one idea across many inspection ids: `ReplaceWithOfType*` is ~20 ids,
`StringCompareIsCultureSpecific.1..6` is six, `.Global`/`.Local` doubles a further set.
A rule catalogue is sized in concepts, not in inspection ids, so the two counts have to be
reported separately or the target is inflated before anyone argues about it.

Collapsing rule: strip a trailing `.Global`/`.Local`, a trailing digit group, and the
`.N` disambiguator, then group by the remaining stem.
"""
import json, os, re, collections

W = os.path.dirname(os.path.abspath(__file__))
rows = json.load(open(f"{W}/classified.json"))
unc = [r for r in rows if r["bucket"] == "Uncovered" and not r["unityOnly"]]


def stem(iid, key):
    s = iid or key
    s = re.sub(r"\.(Global|Local)$", "", s)
    s = re.sub(r"[._]?\d+$", "", s)
    return s


groups = collections.defaultdict(list)
for r in unc:
    groups[stem(r["id"], r["key"])].append(r)

# A second, coarser pass: families that share a long prefix are one concept.
stems = sorted(groups)
merged = {}
for s in stems:
    hit = None
    for m in merged:
        if (s.startswith(m) and len(m) >= 12) or (m.startswith(s) and len(s) >= 12):
            hit = m
            break
    merged.setdefault(hit or s, []).extend(groups[s])

print(f"Uncovered inspection ids:        {len(unc)}")
print(f"after collapsing numbered/.Global variants: {len(groups)}")
print(f"after also merging long shared prefixes:    {len(merged)}")
print()
print("largest families (one concept, many ids):")
for s, rs in sorted(merged.items(), key=lambda kv: -len(kv[1]))[:18]:
    if len(rs) > 1:
        print(f"  {len(rs):3}  {s}")
json.dump({k: [r["id"] or r["key"] for r in v] for k, v in merged.items()},
          open(f"{W}/uncovered_concepts.json", "w"), indent=1)

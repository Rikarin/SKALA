"""Classify SonarSource's 480 published C# rules into the same buckets, and measure the
overlap with the ReSharper set.

Method for the overlap: a Sonar rule and a ReSharper inspection are candidate duplicates
when their titles/descriptions share enough distinctive content words. The join is a
*screen*, not the answer -- every pair it proposes is listed so it can be read, and the
counts below are reported as an estimate with the sample verified by hand.
"""
import json, os, re, collections

W = os.path.dirname(os.path.abspath(__file__))
sonar = json.load(open(f"{W}/sonar.json"))
rows = json.load(open(f"{W}/classified.json"))
rs = [r for r in rows if not r["unityOnly"]]

STOP = set("""a an the should not be used is are of in to for with and or on by this that it its as
if then else when where which who whom whose can could may might must shall will would do does did
have has had been being was were am no nor only own same so than too very just".split() them their
there here what how why all any both each few more most other some such only own use using used
instead prefer preferred rather always never must not""".split())


def toks(s):
    return {w for w in re.findall(r"[a-z0-9]+", (s or "").lower()) if w not in STOP and len(w) > 2}


rsidx = [(r, toks(f"{r['id']} {r['desc']}")) for r in rs]

pairs = {}
for sid, sv in sonar.items():
    st = toks(sv["title"])
    if not st:
        continue
    best = []
    for r, rt in rsidx:
        if not rt:
            continue
        inter = st & rt
        j = len(inter) / len(st | rt)
        if len(inter) >= 2 and j >= 0.34:
            best.append((j, r["id"] or r["key"], r["bucket"]))
    best.sort(reverse=True)
    if best:
        pairs[sid] = best[:3]

print(f"Sonar C# rules: {len(sonar)}")
print(f"  with a candidate ReSharper counterpart (title join): {len(pairs)}")
print(f"  with no ReSharper counterpart proposed:              {len(sonar) - len(pairs)}")
print()
# Where the matched ReSharper inspection already sits tells us the Sonar rule's bucket too.
bk = collections.Counter()
for sid, best in pairs.items():
    bk[best[0][2]] += 1
print("bucket of the best-matching ReSharper inspection:")
for b, n in bk.most_common():
    print(f"  {b:14} {n}")
print()
print("sample of proposed pairs (for hand verification):")
for sid in sorted(pairs)[:25]:
    j, rid, b = pairs[sid][0]
    print(f"  {sid:7} {sonar[sid]['title'][:52]:54} ~ {rid:38} [{b}] {j:.2f}")

json.dump({k: v for k, v in pairs.items()}, open(f"{W}/sonar_pairs.json", "w"), indent=1)

unmatched = {k: v for k, v in sonar.items() if k not in pairs}
print()
print("Sonar rules with NO ReSharper counterpart, by type:",
      dict(collections.Counter(v["type"] for v in unmatched.values())))
print("  ...by quickfix:", dict(collections.Counter(str(v["quickfix"]) for v in unmatched.values())))
json.dump(unmatched, open(f"{W}/sonar_unmatched.json", "w"), indent=1)

"""Sensitivity of the Sonar<->ReSharper title join.

Sonar states rules prescriptively ("X should not be Y"); ReSharper states them
diagnostically ("Redundant X"). A token join across those two vocabularies is a weak
instrument, and the honest way to use it is to show how the answer moves with the
threshold rather than to quote one number from it.
"""
import json, os, re

W = os.path.dirname(os.path.abspath(__file__))
sonar = json.load(open(f"{W}/sonar.json"))
rows = [r for r in json.load(open(f"{W}/classified.json")) if not r["unityOnly"]]

STOP = set("""a an the should not be used is are of in to for with and or on by this that it its as
if then else when where which who whom whose can could may might must shall will would do does did
have has had been being was were only other some such use using used instead prefer preferred
rather always never all any both each few more most there here what how why""".split())


def toks(s):
    return {w for w in re.findall(r"[a-z0-9]+", (s or "").lower()) if w not in STOP and len(w) > 2}


rsidx = [(r, toks(f"{r['id']} {r['desc']}")) for r in rows]
sidx = {k: toks(v["title"]) for k, v in sonar.items()}

print(f"{'min|inter|':>10} {'jaccard':>8} {'sonar matched':>14} {'implied distinct':>17}")
for mininter, jac in ((2, 0.34), (2, 0.25), (2, 0.20), (1, 0.20), (2, 0.15), (1, 0.15), (1, 0.10)):
    n = 0
    for k, st in sidx.items():
        if not st:
            continue
        for r, rt in rsidx:
            if rt and len(st & rt) >= mininter and len(st & rt) / len(st | rt) >= jac:
                n += 1
                break
    print(f"{mininter:>10} {jac:>8.2f} {n:>14} {len(sonar) - n + len(rows):>17}")

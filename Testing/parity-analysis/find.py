import json, os, sys, re

W = os.path.dirname(os.path.abspath(__file__))
u = json.load(open(W + "/universe.json"))
pat = re.compile("|".join(sys.argv[1:]), re.I)
for v in sorted(u.values(), key=lambda x: x["key"]):
    blob = f"{v['id']} {v['desc']}"
    if pat.search(blob):
        flag = "UNITY " if v["unityOnly"] else ""
        print(f"{flag}{v['exportSeverity']:10} {v['category'] or '-':22} {v['id'] or '?':46} {(v['desc'] or '')[:70]}")

"""Fetch the SonarSource C# rule *metadata* list (ids, titles, types, severities, tags).

LICENCE BOUNDARY (see docs/plan/17): sonar-dotnet is under the SONAR Source-Available
License v1.0 -- source-available, NOT open source. This script touches only
`analyzers/rspec/cs/S####.json`, the published rule metadata (the same facts served at
rules.sonarsource.com). It never fetches `analyzers/src/**` (their implementation) and
never stores the `.html` rule descriptions (their copyrighted prose).
"""
import json, os, sys, urllib.request, time

W = os.path.dirname(os.path.abspath(__file__))
UA = {"User-Agent": "skala-parity-analysis"}


def get(url):
    req = urllib.request.Request(url, headers=UA)
    for attempt in range(4):
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                return r.read()
        except Exception as e:  # noqa: BLE001
            if attempt == 3:
                raise
            print(f"  retry {attempt+1} {url}: {e}", file=sys.stderr)
            time.sleep(3 * (attempt + 1))


tree = json.loads(get("https://api.github.com/repos/SonarSource/sonar-dotnet/git/trees/master?recursive=1"))
paths = [n["path"] for n in tree["tree"]
         if n["path"].startswith("analyzers/rspec/cs/") and n["path"].endswith(".json")]
# Guard the boundary in code, not just in prose.
assert not any(p.startswith("analyzers/src/") for p in paths), "must never fetch implementation source"
print(f"rspec/cs json files: {len(paths)}")

base = "https://raw.githubusercontent.com/SonarSource/sonar-dotnet/master/"
rules = {}
for n, p in enumerate(paths):
    rid = os.path.basename(p)[:-5]
    if not (rid.startswith("S") and rid[1:].isdigit()):
        continue
    try:
        d = json.loads(get(base + p))
    except Exception as e:  # noqa: BLE001
        print(f"  SKIP {rid}: {e}", file=sys.stderr)
        continue
    # Metadata only. `description`/html prose is deliberately not retained.
    rules[rid] = {k: d.get(k) for k in
                  ("title", "type", "defaultSeverity", "tags", "sqKey", "scope",
                   "quickfix", "status", "remediation", "code")}
    if n % 50 == 0:
        print(f"  {n}/{len(paths)}")

json.dump(rules, open(W + "/sonar.json", "w"), indent=1)
print(f"wrote {len(rules)} rules -> sonar.json")

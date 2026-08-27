"""Draw a reproducible random sample of Sonar C# rules for hand classification.

The sample is chosen by SHA-256 of the rule id under a fixed salt, sorted ascending --
a hash of the id rather than a seeded RNG, so the draw depends on nothing but the ids
(the same reasoning `Testing/Rikarin.Skala.Testing/CorpusSample.cs` uses).
"""
import json, os, hashlib, sys

W = os.path.dirname(os.path.abspath(__file__))
sonar = json.load(open(f"{W}/sonar.json"))
N = int(sys.argv[1]) if len(sys.argv) > 1 else 60
SALT = "skala-parity-20260827\n"

ranked = sorted(sonar, key=lambda k: hashlib.sha256((SALT + k).encode()).hexdigest())
sample = ranked[:N]
json.dump(sample, open(f"{W}/sonar_sample_ids.json", "w"), indent=1)
for k in sample:
    v = sonar[k]
    tags = ",".join(v.get("tags") or [])[:38]
    print(f"{k:7} {v['type'][:11]:12} {str(v['quickfix']):10} {v['title'][:66]:68} {tags}")

"""Append every ReSharper inspection key from the Skala export to a target
.editorconfig, all raised to `warning`.

Rationale: a zero from an inspection left at `none` is indistinguishable from a
zero on clean code. Raising every inspection for the measurement run makes a zero
mean "did not fire", full stop. Vixen's own formatting/arrangement preferences are
left untouched (only `*_highlighting` keys are appended), so formatting inspections
are still judged against the style Vixen is actually written in.
"""
import sys

template, target = sys.argv[1], sys.argv[2]
keys = []
for line in open(template, encoding="utf-8-sig"):
    s = line.strip()
    if "=" not in s or not s.startswith("resharper_"):
        continue
    k = s.split("=", 1)[0].strip()
    if k.endswith("_highlighting"):
        keys.append(k)

with open(target, "a", encoding="utf-8") as f:
    f.write("\n\n# ---- Skala inspection-parity measurement run: every inspection raised ----\n")
    f.write("[*.cs]\n")
    for k in sorted(set(keys)):
        f.write(f"{k} = warning\n")
print(f"appended {len(set(keys))} raised inspection keys to {target}")

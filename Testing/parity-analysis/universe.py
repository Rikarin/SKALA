"""Build the C#-relevant ReSharper inspection universe from the author's own export,
enriched with the tool's own issue-type metadata."""
import json, re, os, collections

W = os.path.dirname(os.path.abspath(__file__))
# The repository root, derived from this script's own location. It was previously a
# hardcoded worktree path, which made the pipeline unrunnable everywhere but that
# one machine-and-moment; doc 17's README asks for a re-run, so it has to resolve.
REPO = os.path.dirname(os.path.dirname(W))

# --- the export: the author's configured severities, and the universe of keys ---
ec = {}
for line in open(f"{REPO}/editor_config_template", encoding="utf-8-sig"):
    s = line.strip()
    if "=" not in s or not s.startswith("resharper_"):
        continue
    k, v = s.split("=", 1)
    k, v = k.strip(), v.strip()
    if k.endswith("_highlighting"):
        ec[k] = v

# --- issue-type metadata, keyed back onto export keys ---
def snake(s):
    s = s.replace(".", "_")
    s = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", s)
    s = re.sub(r"(?<=[A-Z])(?=[A-Z][a-z])", "_", s)
    return s.lower()

import xml.etree.ElementTree as ET

# `types.json` is an optional cached dump from an older jb; the pipeline runs without
# it, on `types-2026.xml` alone. It was never committed, so requiring it was a second
# reason the committed pipeline could not be re-run.
sources = []
if os.path.exists(f"{W}/types.json"):
    sources.append(json.load(open(f"{W}/types.json")))
# The measuring version's own catalogue: covers plugin + newer-C# inspections that the
# 2025.2.6 base dump omits (NUnit, EF, logging templates, `ConvertToExtensionBlock`, ...).
if os.path.exists(f"{W}/types-2026.xml"):
    sources.append([e.attrib for e in ET.parse(f"{W}/types-2026.xml").getroot().iter("IssueType")])

meta = {}
for src in sources:
    for i in src:
        for cand in (f"resharper_{snake(i['Id'])}_highlighting",
                     f"resharper_{snake(i['Id'])}_highlighting_highlighting"):
            if cand in ec and cand not in meta:
                meta[cand] = i

# --- language partition. Anything whose key names another language is out of scope. ---
NONCS = ("cpp_", "vb_", "xaml_", "html_", "asp_", "web_", "razor_", "js_", "ts_", "css_",
         "angular_", "f_", "protobuf_", "json_", "yaml_", "xml_", "resx_", "ini_", "ue4_",
         "unreal_", "blueprint_", "sql_", "regexp_", "asmdef_", "asxx_", "blockshaders_")
GAMEENGINE = ("unity_", "burst_")

universe = {}
for k, sev in ec.items():
    body = k[len("resharper_"):]
    if any(body.startswith(p) for p in NONCS):
        continue
    lang_oos = any(body.startswith(p) for p in GAMEENGINE)
    m = meta.get(k, {})
    universe[k] = {
        "key": k,
        "id": m.get("Id"),
        "category": m.get("CategoryId"),
        "categoryName": m.get("Category"),
        "desc": m.get("Description"),
        "toolSeverity": m.get("Severity"),
        "exportSeverity": sev,
        "unityOnly": lang_oos,
    }

print(f"C#-relevant universe (unity kept): {len(universe)}")
print(f"  of which Unity/Burst-only:      {sum(1 for v in universe.values() if v['unityOnly'])}")
print(f"  C# proper:                      {sum(1 for v in universe.values() if not v['unityOnly'])}")
print(f"  with tool metadata:             {sum(1 for v in universe.values() if v['id'])}")
print()
print("export severity (C# proper):",
      dict(collections.Counter(v["exportSeverity"] for v in universe.values() if not v["unityOnly"])))
print()
cats = collections.Counter(v["category"] or "(no metadata)"
                           for v in universe.values() if not v["unityOnly"])
print("by ReSharper category:")
for c, n in cats.most_common():
    print(f"  {c:26} {n}")
json.dump(universe, open(f"{W}/universe.json", "w"), indent=1)

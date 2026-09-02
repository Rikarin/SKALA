"""Build the C#-relevant ReSharper inspection universe from the author's own export,
enriched with the tool's own issue-type metadata.

⚠ **`editor_config_template` is the universe; `types-2026.xml` is only metadata joined onto
it.** Getting that backwards produces a specific wrong answer, and it produced one: #318
measured `catalogued.json`'s keys against `types-2026.xml` and reported 26 (really 29, see
`verify_ledger.py`) of them as fabrications. Fourteen of those are real, live, correctly
mapped inspections that simply post-date the dump — `ConvertToExtensionBlock`,
`MoveToExtensionBlock`, the three `NUnit*`, `ShortLivedHttpClient` and so on all carry a
`resharper_*_highlighting` key in the export. The dump is *known-incomplete by construction*:
81 of the 888 C#-proper rows carry `id: null` for exactly this reason, which is the fact the
README has always recorded and #318 measured against anyway.

⚠ **The claim that used to stand at the `types-2026.xml` load below was false.** It read
"covers plugin + newer-C# inspections that the 2025.2.6 base dump omits (NUnit, EF, logging
templates, `ConvertToExtensionBlock`, ...)". The file contains **no** `NUnit*` id and **no**
id containing `ExtensionBlock` — `grep -c 'Id="NUnit'` is 0 — so every example the comment
gave was one the file does not have. Whatever was dumped, it was not the measuring version's
catalogue.

Importable: `build()` returns the universe without printing or writing, so `verify_ledger.py`
can check parity-map keys against it even when `universe.json` has never been generated.
"""
import json, re, os, collections
import xml.etree.ElementTree as ET

W = os.path.dirname(os.path.abspath(__file__))
# The repository root, derived from this script's own location. It was previously a
# hardcoded worktree path, which made the pipeline unrunnable everywhere but that
# one machine-and-moment; doc 17's README asks for a re-run, so it has to resolve.
REPO = os.path.dirname(os.path.dirname(W))

EXPORT = f"{REPO}/editor_config_template"
TYPES_XML = f"{W}/types-2026.xml"

# --- language partition. Anything whose key names another language is out of scope. ---
NONCS = ("cpp_", "vb_", "xaml_", "html_", "asp_", "web_", "razor_", "js_", "ts_", "css_",
         "angular_", "f_", "protobuf_", "json_", "yaml_", "xml_", "resx_", "ini_", "ue4_",
         "unreal_", "blueprint_", "sql_", "regexp_", "asmdef_", "asxx_", "blockshaders_")
GAMEENGINE = ("unity_", "burst_")


def snake(s):
    s = s.replace(".", "_")
    s = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", s)
    s = re.sub(r"(?<=[A-Z])(?=[A-Z][a-z])", "_", s)
    return s.lower()


def export_keys():
    """`resharper_*_highlighting` key -> the author's configured severity.

    The author's own ReSharper export. This is the **universe**: the set of inspections the
    parity measurement is taken over.
    """
    ec = {}
    for line in open(EXPORT, encoding="utf-8-sig"):
        s = line.strip()
        if "=" not in s or not s.startswith("resharper_"):
            continue
        k, v = s.split("=", 1)
        k, v = k.strip(), v.strip()
        if k.endswith("_highlighting"):
            ec[k] = v
    return ec


def issue_type_ids():
    """Every `IssueType/@Id` in `types-2026.xml`, exactly as written.

    ⚠ **Exactly** as written, and the word is load-bearing. #318's first measurement used a
    substring test and reported `CognitiveComplexity`, `SelfAssignment` and `ReplaceWithOfType`
    as present, because the file carries `CppClangTidyReadabilityFunctionCognitiveComplexity`,
    `cplusplus.SelfAssignment` and `ReplaceWithOfType.1`. A substring of an id is not the id:
    the first two are C++ inspections and the third is a numbered variant. That single
    instrument error moved the answer by three and sent the issue's central inference the
    wrong way.

    This is *metadata*, not the universe — it is `jb inspectcode 2025.2.6 --dumpIssuesTypes`,
    and it is joined onto `export_keys()` by `build()`. It is known-incomplete relative to what
    ReSharper actually ships: `CommentTypo` is declared by the bundled ReSpeller assembly and
    is not in here, and the IDE surface carries more still.
    """
    if not os.path.exists(TYPES_XML):
        return set()
    return {e.attrib["Id"] for e in ET.parse(TYPES_XML).getroot().iter("IssueType")}


def key_for(iid):
    """The `resharper_*_highlighting` spellings an inspection id could take in the export."""
    b = snake(iid)
    return (f"resharper_{b}_highlighting", f"resharper_{b}_highlighting_highlighting")


def build():
    """The universe, from the two committed inputs. No printing, no writing."""
    # --- the export: the author's configured severities, and the universe of keys ---
    ec = export_keys()

    # --- issue-type metadata, keyed back onto export keys ---
    # `types.json` is an optional cached dump from an older jb; the pipeline runs without
    # it, on `types-2026.xml` alone. It was never committed, so requiring it was a second
    # reason the committed pipeline could not be re-run.
    sources = []
    if os.path.exists(f"{W}/types.json"):
        sources.append(json.load(open(f"{W}/types.json")))
    if os.path.exists(TYPES_XML):
        sources.append([e.attrib for e in ET.parse(TYPES_XML).getroot().iter("IssueType")])

    meta = {}
    for src in sources:
        for i in src:
            for cand in key_for(i["Id"]):
                if cand in ec and cand not in meta:
                    meta[cand] = i

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
    return universe


def main():
    universe = build()
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


if __name__ == "__main__":
    main()

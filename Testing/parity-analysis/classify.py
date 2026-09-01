"""Classify every C#-relevant ReSharper inspection in the author's export into exactly
one bucket. Every assignment records the reason that produced it.

Bucket precedence (first match wins), chosen so that the cheapest-to-verify claim is
tested first and the residue -- `Uncovered` -- is what nothing else could explain:

  OutOfScope  -> another language/engine, or ReSharper's own annotation machinery
  Compiler    -> the C# compiler already reports it (CS####)
  Hosted      -> a Roslyn CA*/IDE* analyzer covers it (ADR-008: host, never rebuild)
  Option      -> arrangement/formatting, governed by an option in the registry
  Catalogued  -> an SK id in doc 08 already names the concept
  Uncovered   -> the residue; the only bucket that is a work queue
"""
import json, os, re, collections

W = os.path.dirname(os.path.abspath(__file__))
# The repository root, derived from this script's own location. It was previously a
# hardcoded worktree path, which made the pipeline unrunnable everywhere but that
# one machine-and-moment; doc 17's README asks for a re-run, so it has to resolve.
REPO = os.path.dirname(os.path.dirname(W))

universe = json.load(open(f"{W}/universe.json"))


# ⚠ 81 of the 888 C#-proper rows carry `id: null`, because `universe.py` can only attach
# an inspection id by joining the export key against the tool's own issue-type dump, and
# that join misses every inspection newer than the dumped release. Keying the hand-written
# maps below on `id` alone therefore made those 81 rows bypass both maps in silence and
# land in `Uncovered` -- an inflated residue that looked exactly like a real gap. Both
# maps are read through a key-indexed view as well, built with the same id -> export-key
# transform `universe.py` uses for the join. Keep the two copies of `snake()` in step.
def snake(s):
    s = s.replace(".", "_")
    s = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", s)
    s = re.sub(r"(?<=[A-Z])(?=[A-Z][a-z])", "_", s)
    return s.lower()


def bykey(m):
    """Re-index an id-keyed map onto the export keys those ids produce. First id wins,
    so an id that is already matched directly is never shadowed by a colliding one."""
    out = {}
    for iid, val in m.items():
        b = snake(iid)
        for cand in (f"resharper_{b}_highlighting", f"resharper_{b}_highlighting_highlighting"):
            out.setdefault(cand, val)
    return out


opts = json.load(open(f"{REPO}/Core/Rikarin.Skala.Options/options.json"))["options"]
optbykey = {}
for o in opts:
    optbykey[o["key"]] = o
    for a in o.get("aliases") or []:
        optbykey.setdefault(a, o)

# ---------------------------------------------------------------- out of scope
OOS_ID_PREFIX = (
    "ShaderLab", "Godot", "AsmDef", "Unreal", "UE4", "Cpp", "Vb", "Xaml", "Html", "Asp",
    "Razor", "Angular", "Web", "Js", "Ts", "Css", "Protobuf", "Yaml", "Json", "RegExp",
)
# ReSharper's own annotation/solution-wide-analysis machinery. These configure *ReSharper*,
# they are not findings about the code, and several are DO_NOT_SHOW by construction.
OOS_EXACT = {
    "NoSupportForVb", "InvalidXmlDocComment", "InheritdocInvalidUsage",
}
OOS_PATTERNS = [
    (re.compile(r"^Annotate(CanBeNull|NotNull)"), "ReSharper nullability-inference machinery (DO_NOT_SHOW)"),
    (re.compile(r"^(Assign|Suggest)?.*NullableWarningSuppressionIsUsed$"), "ReSharper annotation machinery"),
]


# The export's key names do not carry a language, and the tool's own dump has no language
# attribute, so a handful of VB-only inspections survive the prefix filter. They are
# identified by VB-only syntax named in their description.
VB_ONLY = re.compile(
    r"'(Me|MyBase|MyClass|Then|Yield|Dim|Imports|End If|Case Else|ByVal|ByRef|ReDim|"
    r"GoTo|Narrowing|Widening|Option Strict|Handles|WithEvents)[.']|"
    r"\bCase Else\b|\bVisual Basic\b|'Me\.'|'MyBase\.'|'MyClass\.'")


def out_of_scope(v):
    key, iid, desc = v["key"], v["id"] or "", v["desc"] or ""
    if v["unityOnly"]:
        return "Unity/Burst engine inspection; Skala targets general C#"
    if VB_ONLY.search(desc) or iid in (
            "RedundantMeQualifier", "RedundantMyBaseQualifier", "RedundantMyClassQualifier",
            "RedundantEmptyCaseElse", "RedundantIfStatementThenKeyword",
            "ConvertToVbAutoProperty", "ConvertToVbAutoPropertyWhenPossible",
            "ConvertToVbAutoPropertyWithPrivateSetter"):
        return "VB.NET-only inspection (key carries no language prefix)"
    b = key[len("resharper_"):]
    if b.startswith(("godot_", "shader_lab_", "asm_def_", "azure_functions_")):
        return "engine/framework-specific plugin inspection"
    # ASP.NET Core route-template analysis: a framework front end, not general C#.
    if iid.startswith("RouteTemplates.") or b.startswith("route_"):
        return "ASP.NET Core route templates; framework-specific"
    if b.startswith("entity_framework_") or iid.startswith("EntityFramework"):
        return "Entity Framework Core-specific; framework front end"
    if iid in ("NonParsableElement", "InactivePreprocessorBranch", "InvocationIsSkipped",
               "IgnoredDirective", "UnexpectedDirective", "UnexpectedAttribute",
               "RedundantInclude", "EscapedKeyword"):
        return "editor/tooling information, not a finding about the code"
    # Markup and web inspections whose keys carry no language prefix. `<script>` tag
    # diagnostics and MVC model binding are HTML/Razor, not C#.
    if "Script" in iid and "script" in desc.lower():
        return "HTML <script> markup, not C#"
    if iid.startswith("Mvc.") or iid.startswith("Asp."):
        return "ASP.NET MVC view binding; framework front end"
    if iid in ("ResourceItemNotResolved", "ResourceNotResolved", "PropertyNotResolved",
               "ResourceItemNotResolvedInSelectedCulture"):
        return ".resx / markup resource resolution, not C# source"
    if iid in OOS_EXACT:
        return "not a finding about C# source"
    for pat, why in OOS_PATTERNS:
        if pat.match(iid):
            return why
    if v["category"] in ("UNITY", "Unreal Build System", "UNITY_BURST", "UNITY_PERFORMANCE"):
        return "game-engine category"
    if v["toolSeverity"] == "DO_NOT_SHOW" and iid.startswith("Annotate"):
        return "ReSharper annotation inference, never shown"
    return None


# ---------------------------------------------------------------- compiler
def compiler(v):
    iid = v["id"] or ""
    if v["category"] == "CompilerWarnings":
        return "ReSharper's own category for compiler diagnostics"
    if iid.startswith("CSharpWarnings::CS") or iid.startswith("CSharpErrors"):
        return "surfaces a CS#### diagnostic"
    return None


# ---------------------------------------------------------------- hosted
# Concepts a shipped Roslyn analyzer (CA*/IDE*) already reports. ADR-008: Skala hosts these.
HOSTED = {
    "CSharpWarnings::CA2252": "CA2252", "CSharpWarnings::CA2254": "CA2254",
    "SpecifyACultureInStringConversionExplicitly": "CA1304/CA1305",
    "SpecifyStringComparison": "CA1307/CA1310",
    "UseStringComparison": "CA1307",
    # `NonReadonlyMemberInGetHashCode` was here as "CA1065-adjacent". CA1065 is "do not
    # raise exceptions in unexpected locations" and says nothing about a hash code that
    # depends on mutable state, so the entry credited a Roslyn analyzer that does not
    # exist. Removed; see doc 17 § "Two corrections to how this document measured".
    # `ReferenceEqualsWithValueType` was on its way into catalogued.json as part of SK2040
    # when a probe compiled against a real project showed CA2013 already reporting it, with
    # the same advice and enabled by default. Measured, not assumed: the shape was compiled
    # and the warning read off the build. ADR-008 hosts it.
    "ReferenceEqualsWithValueType": "CA2013",
    "VirtualMemberCallInConstructor": "CA2214",
    "StaticMemberInGenericType": "CA1000",
    "EmptyGeneralCatchClause": "CA1031",
    "UnusedMember.Global": "IDE0051", "UnusedMember.Local": "IDE0051",
    "UnusedParameter.Global": "IDE0060", "UnusedParameter.Local": "IDE0060",
    "UnusedVariable": "IDE0059",
    "UnusedAutoPropertyAccessor.Global": "IDE0052", "UnusedAutoPropertyAccessor.Local": "IDE0052",
    "UnusedType.Global": "IDE0051", "UnusedType.Local": "IDE0051",
    "RedundantUsingDirective": "IDE0005",
    "ConvertToAutoProperty": "IDE0032",
    "ConvertToAutoPropertyWithPrivateSetter": "IDE0032",
    "ConvertToAutoPropertyWhenPossible": "IDE0032",
    "InvertIf": "IDE0046-adjacent",
    "MergeSequentialChecks": "IDE0020/IDE0038",
    "UseNameofExpression": "IDE0280",
    "UseNullPropagation": "IDE0031",
    "ConvertToNullCoalescingCompoundAssignment": "IDE0074",
    "ConvertIfStatementToNullCoalescingAssignment": "IDE0074",
    "ConvertIfStatementToNullCoalescingExpression": "IDE0029",
    "ConvertSwitchStatementToSwitchExpression": "IDE0066",
    "ConvertToUsingDeclaration": "IDE0063",
    "ConvertToLambdaExpression": "IDE0053",
    "ArrangeObjectCreationWhenTypeEvident": "IDE0090",
    "SuggestVarOrType_SimpleTypes": "IDE0007/IDE0008",
    "InconsistentNaming": "IDE1006",
    "RedundantNameQualifier": "IDE0001",
    "SimplifyLinqExpressionUseAll": "CA1868-adjacent",
    "UseCollectionCountProperty": "CA1860",
    "ReplaceWithSingleCallToCount": "CA1829",
    "ReplaceWithStringIsNullOrEmpty": "CA1806-adjacent",
    "UseArrayEmptyMethod": "CA1825",
    "UseStringInterpolation": "CA1863-adjacent",
    "LoopCanBeConvertedToQuery": "IDE0270-adjacent",
    "UseThrowIfNullMethod": "CA1510",
    "UseArgumentExceptionThrowIfMethod": "CA1511",
    "UseIsOperator.1": "IDE0038", "UseIsOperator.2": "IDE0038",
    "MergeIntoPattern": "IDE0078",
    "UseSymbolAlias": "IDE0001",
}
HOSTED_BYKEY = bykey(HOSTED)


def hosted(v):
    iid = v["id"] or ""
    b = v["key"][len("resharper_"):]
    # ReSharper's NUnit and xUnit inspections mirror analyzer packages that ship with the
    # test frameworks themselves (NUnit.Analyzers `NUnit####`, `xunit.analyzers` `xUnit####`).
    # ADR-008 hosts those rather than rebuilding them -- which is exactly the reasoning
    # doc 08 already used to cut SK8003 and SK8004 in favour of xUnit1001/xUnit1049.
    if b.startswith("n_unit_") or iid.startswith("NUnit"):
        return "NUnit.Analyzers ships the equivalent NUnit#### diagnostic"
    if b.startswith("xunit_") or iid.startswith("Xunit"):
        return "xunit.analyzers ships the equivalent xUnit#### diagnostic"
    return HOSTED.get(iid) or HOSTED_BYKEY.get(v["key"])


# ---------------------------------------------------------------- option
# An inspection earns `Option` only when a concrete key in Skala's registry governs it.
# The tier is recorded, because Tier A ("implemented, pinned by an oracle fixture") and
# Tier D ("known to the registry, not implemented") are opposite answers to
# "does Skala cover this today?".
GOV = json.load(open(f"{W}/gov.json"))
# ⚠ `gov.json` is id-keyed like the other two maps, and 81 rows have no id. Re-index it onto
# the export keys for the same reason -- an inspection the registry governs must not fall
# through to `Uncovered` merely because the joining dump does not know its id.
GOV_BYKEY = bykey(GOV)


def option(v):
    iid = v["id"] or ""
    keys = GOV.get(iid) or GOV_BYKEY.get(v["key"])
    if keys is None and v["category"] in ("FormattingIssues", "CodeStyleIssues"):
        # Whitespace/line-break/indent families are the formatter's output as a whole;
        # SK0001 reports "file is not formatted" rather than one rule per whitespace shape.
        return ("formatter", "A", "whole-file formatting, reported by SK0001")
    if keys is None:
        return None
    found = [(k, optbykey[k].get("tier")) for k in keys if k in optbykey]
    if not found:
        return (None, None, "no key in the registry governs it")
    # Best tier available wins.
    found.sort(key=lambda kt: {"A": 0, "B": 1, "C": 2, "D": 3}.get(kt[1], 4))
    k, t = found[0]
    return (k, t, "")


# ---------------------------------------------------------------- catalogued
CATALOGUED = json.load(open(f"{W}/catalogued.json"))
CATALOGUED_BYKEY = bykey(CATALOGUED)


def catalogued(v):
    return CATALOGUED.get(v["id"] or "") or CATALOGUED_BYKEY.get(v["key"])


# ---------------------------------------------------------------- run
rows = []
for v in universe.values():
    r = dict(v)
    for name, fn in (("Out of scope", out_of_scope), ("Compiler", compiler), ("Hosted", hosted)):
        why = fn(v)
        if why:
            r["bucket"], r["reason"] = name, why
            break
    else:
        o = option(v)
        c = catalogued(v)
        if c:
            r["bucket"], r["reason"] = "Catalogued", c
        elif o:
            k, t, why = o
            if k is None:
                r["bucket"], r["reason"] = "Uncovered", why
            else:
                r["bucket"] = "Option"
                r["optionKey"], r["optionTier"] = k, t
                r["reason"] = why or f"governed by {k} (Tier {t})"
        else:
            r["bucket"], r["reason"] = "Uncovered", ""
    rows.append(r)

json.dump(rows, open(f"{W}/classified.json", "w"), indent=1)
c = collections.Counter(r["bucket"] for r in rows)
print(f"{'bucket':14} {'all':>5} {'C# proper':>10}")
for b, n in c.most_common():
    cs = sum(1 for r in rows if r["bucket"] == b and not r["unityOnly"])
    print(f"{b:14} {n:5} {cs:10}")
print(f"{'TOTAL':14} {len(rows):5} {sum(1 for r in rows if not r['unityOnly']):10}")
print()
opt = [r for r in rows if r["bucket"] == "Option"]
print("Option bucket by tier:",
      dict(collections.Counter(r.get("optionTier") for r in opt)))

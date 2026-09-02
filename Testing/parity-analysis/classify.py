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
    # ⚠ These two said `IDE0074` and `IDE0074` is a **phantom**: it is in the tool's supported-rule
    # list, with the identical title "Use compound assignment", and it is never emitted. Measured
    # against six canonical `x = x ?? y` shapes -- local, instance field, static field, property,
    # `this.`-qualified, string-with-literal -- with `dotnet_diagnostic.IDE0074.severity = warning`
    # *and* `dotnet_style_prefer_compound_assignment = true:warning` *and* `AnalysisMode=All` *and*
    # `EnforceCodeStyleInBuild=true`: all six reported **IDE0054**, none reported IDE0074. So the
    # concept is hosted and the id crediting it reported nothing, which is the worst of both -- the
    # row bucketed `Hosted` on a diagnostic that does not exist in practice. See #281.
    "ConvertToNullCoalescingCompoundAssignment": "IDE0054",
    "ConvertIfStatementToNullCoalescingAssignment": "IDE0054",
    "ConvertIfStatementToNullCoalescingExpression": "IDE0029",
    "ConvertSwitchStatementToSwitchExpression": "IDE0066",
    "ConvertToUsingDeclaration": "IDE0063",
    "ConvertToLambdaExpression": "IDE0053",
    "ArrangeObjectCreationWhenTypeEvident": "IDE0090",
    "SuggestVarOrType_SimpleTypes": "IDE0007/IDE0008",
    "InconsistentNaming": "IDE1006",
    "RedundantNameQualifier": "IDE0001",
    # ⚠ `CA1868-adjacent` was a hedge and the measurement replaces it with the real host. `CA1868`
    # is the `Contains` guard on a set and fires on its own shape in the same compilation; it
    # reports **0 of 4** of `SK4010`'s positives. `IDE0120` "Simplify LINQ expression" reports
    # **4 of 4** -- and only with `EnforceCodeStyleInBuild=true` plus an explicit severity, so it
    # is `code-style` and says nothing in a default build. Same correction for
    # `LoopCanBeConvertedToQuery`, whose `IDE0270-adjacent` hedge names a null-check rule.
    "SimplifyLinqExpressionUseAll": "IDE0120",
    # ⚠ Measured, not assumed, and it removed a branch from a rule that was being written.
    # Both shapes were compiled on SDK 10.0.400 with `AnalysisMode=All` and the warning read
    # off the build: `xs.Count() > 0` and `xs.LongCount() > 0` report CA1827, and
    # `if (!set.Contains(x)) set.Add(x)` reports CA1868. The first is five of the thirty-eight
    # inspections issue #100 collects and the second is one of issue #101's six, so both
    # issues ship one branch narrower than they were written. ADR-008 hosts rather than
    # rebuilds. Before this, all six fell through to `Uncovered` and were counted as a gap.
    "UseMethodAny.0": "CA1827", "UseMethodAny.1": "CA1827", "UseMethodAny.2": "CA1827",
    "UseMethodAny.3": "CA1827", "UseMethodAny.4": "CA1827", "UseMethodAny.5": "CA1827",
    "CanSimplifySetAddingWithSingleCall": "CA1868",
    "UseCollectionCountProperty": "CA1860",
    # ⚠ This said `CA1829` and `CA1829` is the wrong rule for it -- a near-miss that mattered,
    # because `CA1829` is `on` at stock and the entry therefore filed `SK4010` as duplicating
    # something every consumer already has. Measured: `CA1829` ("use the `Length`/`Count`
    # property, not the `Count()` method") reports **0 of 4** of `SK4010`'s positives and is
    # provably live in the same compilation, firing on what was then `SK1034`'s `count-call.cs`
    # fixture -- `items.Count()` on a `List<int>`; that rule is retired (#281) and `CA1829` is
    # exactly what took the shape over, which is the same measurement read twice. It declines
    # correctly -- `values.Where(p)` returns an iterator, which has no `Count` property to prefer.
    # The inspection is `xs.Where(p).Count()` -> `xs.Count(p)`, and its host is `IDE0120`.
    "ReplaceWithSingleCallToCount": "IDE0120",
    "ReplaceWithStringIsNullOrEmpty": "CA1806-adjacent",
    "UseArrayEmptyMethod": "CA1825",
    "UseStringInterpolation": "CA1863-adjacent",
    "LoopCanBeConvertedToQuery": "IDE0120",
    "UseThrowIfNullMethod": "CA1510",
    "UseArgumentExceptionThrowIfMethod": "CA1511",
    "UseIsOperator.1": "IDE0038", "UseIsOperator.2": "IDE0038",
    "MergeIntoPattern": "IDE0078",
    "UseSymbolAlias": "IDE0001",
    # `PartialMethodParameterNameMismatch` is hosted by the *compiler*, not by an analyzer, and it
    # lands here rather than in `compiler()` because ReSharper files it under Potential Code Quality
    # Issues rather than under CompilerWarnings -- so nothing above catches it and it was falling
    # through to the Uncovered residue, inflating the gap by one. Measured, not assumed: a probe
    # with mismatched parameter names across partial declarations builds with CS8826 for classic
    # and extended partial methods alike, including a difference of case alone, and with CS9256 for
    # partial indexers and partial constructors. Both are on at default warning levels. See
    # docs/plan/08 for the write-up and for the second thing that probe refuted.
    "PartialMethodParameterNameMismatch": "CS8826/CS9256",
    # ⚠ `RemoveConstructorInvocation` is `CA1806` at *stock* settings, which is stronger than the
    # usual "hosted at AnalysisMode=All". Measured on a probe built outside this repository with
    # empty Directory.Build.props/.targets above it, SDK 10.0.400, no AnalysisMode and no
    # .editorconfig: `isEnabledByDefault: true`, `defaultLevel: note`, and it reports `new Foo();`,
    # `new Foo(3);`, `new InvalidOperationException(…);` and `new Timer(…);` alike -- the
    # side-effecting-constructor exemption issue #50 asks for does not exist in CA1806 either.
    # `_ = new Widget();` is correctly silent. IDE0058 covers the same four lines. See docs/plan/08.
    "RemoveConstructorInvocation": "CA1806",
    # ⚠ `IDE0059` covers the local-assignment shapes and is the *middle* state, not "on": the
    # descriptor says isEnabledByDefault true / defaultLevel note, tagged
    # EnforceOnBuild_HighlyRecommended, and yet `EnforceCodeStyleInBuild=true` on its own produced
    # no IDE0059 at all on the probe -- it appeared only once dotnet_diagnostic.IDE0059.severity was
    # raised in an .editorconfig. Hosted all the same: ADR-008 is about who owns the concept.
    # `MemberInitializerValueIgnored`, the fourth inspection of the same issue, is *not* hosted by
    # anything and ships as SK2200; it is in catalogued.json rather than here.
    "RedundantAssignment": "IDE0059",
    "AssignmentIsFullyDiscarded": "IDE0059",
    # ⚠ Two more the *compiler* owns, and like `PartialMethodParameterNameMismatch` they land here
    # rather than in `compiler()` because ReSharper files them under its own categories rather than
    # under CompilerWarnings, so nothing above catches them and both were falling through to the
    # Uncovered residue. Measured on SDK 10.0.400 while SK2170 and SK2171 were being written, and
    # the first of the two reaches further than doc 17 supposed.
    #
    # `MisleadingBodyLikeStatement` -- an empty statement standing in for a body -- is `CS0642`,
    # "possible mistaken empty statement", on by default. A nine-case probe read the warnings off
    # the build: it fires for `if`, `else`, `lock`, `do`, `using` and `fixed` outright, and for
    # `while`, `for` and `foreach` exactly when a block follows the `;`. That last clause is what
    # matters -- `while (Step()) ;` alone is the idiomatic spin loop and is silent, and the same
    # line followed by `{ … }` warns. So the compiler covers precisely the shape that misleads, and
    # nothing was left for a rule to add. SK2170 ships the *indentation* half of the concept
    # instead, which no compiler can see and which this inspection does not describe.
    "MisleadingBodyLikeStatement": "CS0642",
    # `LongLiteralEndingLowerL` is `CS0078`, "the 'l' suffix is easily confused with the digit '1'",
    # on by default. The same probe confirms it fires on `1l` and on `1lu` and stays silent on
    # `1ul`. SK2171 ships the sibling inspection the compiler does *not* cover, the `\x` escape
    # whose length the next character decides.
    "LongLiteralEndingLowerL": "CS0078",
    # ⚠ The `if`-to-`?:` pair is hosted by `IDE0045` and `IDE0046`, which is why issue #76 closes
    # without an SK id. Measured behaviourally on a probe built outside this repository with empty
    # Directory.Build.props/.targets and a `root = true` .editorconfig above it, SDK 10.0.400,
    # net10.0. Both sit in the same middle state `IDE0059` does and it was established the same way:
    # a plain build does not even *load* the code-style analyzers -- with `EnforceCodeStyleInBuild`
    # unset the csc `/analyzer:` list holds only the NetAnalyzers and the source generators -- and
    # with it set but no severity in .editorconfig the SARIF holds `CA1822` and no IDE result of any
    # kind. Raising `dotnet_diagnostic.IDE0045/IDE0046.severity` to warning produced
    # `IDE0045: 'if' statement can be simplified` on `if (c) { x = 1; } else { x = 2; }` and
    # `IDE0046` three times, on the if/else `return` form *and* on the `if (c) { return 1; } return
    # 2;` fall-through form. ⚠ `AnalysisMode=All` plus `AnalysisLevel=latest-all` produced zero IDE
    # diagnostics, so those two properties do not reach code-style severities at all.
    #
    # ⚠ Only these two of issue #76's thirteen inspections are recorded here, because only these two
    # were measured. The `ReplaceWith*Assignment`, `RemoveRedundantOrStatement`, `ConvertIfToOr`,
    # `ConvertIfDoToWhile` and `SimplifyConditional*` rows describe different rewrites and stay in
    # the residue until somebody probes them.
    "ConvertIfStatementToConditionalTernaryExpression": "IDE0045",
    "ConvertIfStatementToReturnStatement": "IDE0046",
    # ⚠ `StreamReadReturnValueIgnored` is `CA2022` at *stock* settings -- the strongest of the three
    # states, not "enabled but Hidden" and not "on at AnalysisMode=All". Measured behaviourally on a
    # probe built outside this repository with empty Directory.Build.props/.targets above it, SDK
    # 10.0.400, no AnalysisMode, no EnforceCodeStyleInBuild and no .editorconfig: it reports as a
    # plain `warning` and closes issue #21 without a rule.
    #
    # The coverage was read off the build rather than off the descriptor, and it is wider than the
    # inspection: `Read(byte[], int, int)`, `Read(Span<byte>)`, `ReadAsync(byte[], int, int)` and
    # `ReadAsync(Memory<byte>)` all fire when the count is dropped, on `Stream` and on a derived
    # `FileStream` alike, and it is correctly silent when the result is used and on `ReadExactly`.
    # Two measured gaps, neither of which is the concept: `_ = s.Read(...)` is treated as a
    # deliberate discard and is not reported, and `BinaryReader.Read` / `TextReader.Read` are not
    # covered at all -- CA2022 is `Stream`-only, which is exactly the inspection's own scope.
    "StreamReadReturnValueIgnored": "CA2022",
}
HOSTED_BYKEY = bykey(HOSTED)

# ---------------------------------------------------------------- what "hosted" is worth
# ⚠ The map above recorded only that a `CA*`/`IDE*` **exists**. That is the wrong question, and
# recording it made 18 rows claim two incompatible things at once: `hosted()` runs before
# `catalogued()` and `break`s, so an inspection in both maps bucketed `Hosted` and the shipped
# Skala rule crediting it was silently shadowed. Nine shipped rules were in that state (#281).
# `RuleCatalogTests.TheParityMap_CreditsEveryShippedReSharperMappingToItsOwnRule` could not see
# it: it asserts the *entry exists* in catalogued.json, never that this pipeline reaches it. The
# map was correct and inert.
#
# ADR-008 is "host, never rebuild", and its corollary is that Skala must be *worth using with
# nothing hosted*. Those two sentences give opposite answers for a diagnostic nobody has turned
# on, so the state is what decides a row and existence never could. Measured on SDK 10.0.400,
# outside this repository, with empty Directory.Build.props/.targets above the probe:
#
#   on          the diagnostic is in a stock build's error log with nothing configured --
#               `IsEnabledByDefault` and a `DefaultSeverity` above `Hidden`. ADR-008 hosts it and
#               a Skala rule for the same concept is a duplicate.
#   opt-in      nothing at stock; **one `dotnet_diagnostic.<id>.severity` line** makes it visible.
#               ⚠ This is one state and not two: `enabled + Hidden` and `IsEnabledByDefault=False`
#               are indistinguishable to a consumer -- both produce nothing at stock and both are
#               lifted by the same single line -- so recording them apart would credit the ledger
#               with a distinction the shipped product does not have. #299 proposed three states
#               on the descriptor split; the measurement refuted the split, not the idea.
#   code-style  an `IDE*`: nothing at stock, and a severity line is **not enough** -- it also needs
#               `EnforceCodeStyleInBuild=true`, an MSBuild property change. ⚠ `AnalysisMode=All`
#               reaches **no** `IDE*` diagnostic at all, measured: 96 results, zero of them IDE.
#   compiler    a CS#### the compiler emits unconditionally.
#   package     the test framework's own analyzer package, present exactly when the consumer
#               already references the framework.
#
# ⚠ **43 of the 65 entries above name a diagnostic that produces nothing in a default build**
# (7 `opt-in` + 36 `code-style`). That is the size of what "exists" was hiding.
#
# ⚠ **Two of the three enabled-and-visible states are `Info`**, which is `note` in SARIF and
# produces **zero console lines** at `-v n`. `Info` still counts as `on` here because it is in the
# error log, an IDE shows it, and Skala's own equivalents ship at `suggestion` -- the same
# visibility. What it is not is a warning anybody sees scroll past.
ON, OPT_IN, CODE_STYLE, COMPILER, PACKAGE = "on", "opt-in", "code-style", "compiler", "package"

# Bare Roslyn id -> state. `CA*` from reflecting `IsEnabledByDefault`/`DefaultSeverity` out of
# SDK 10.0.400's `Microsoft.CodeAnalysis.NetAnalyzers.dll` and
# `Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll`; `IDE*` are uniformly `code-style` by the
# behavioural probe above. ⚠ Reflection needs `Roslyn/bincore/Microsoft.CodeAnalysis.dll` loaded
# first and an `AssemblyResolve` handler, or `GetTypes()` throws and every id reads absent --
# which is indistinguishable from the ids not existing.
HOST_STATE = {
    "CA1000": OPT_IN,   # on/Hidden
    "CA1031": OPT_IN,   # off/Warning
    "CA1304": OPT_IN,   # on/Hidden
    "CA1305": OPT_IN,   # on/Hidden
    "CA1307": OPT_IN,   # off/Warning -- ⚠ off even at AnalysisMode=Recommended
    "CA1310": OPT_IN,   # on/Hidden
    "CA1510": ON,       # on/Info
    "CA1511": ON,       # on/Info
    "CA1806": ON,       # on/Info
    "CA1825": ON,       # on/Info
    "CA1827": ON,       # on/Info
    "CA1829": ON,       # on/Info
    "CA1860": ON,       # on/Info
    "CA1863": OPT_IN,   # on/Hidden
    "CA1868": ON,       # on/Info
    "CA2013": ON,       # on/Warning
    "CA2022": ON,       # on/Warning
    "CA2214": OPT_IN,   # off/Warning
    "CA2252": ON,       # on/Error
    "CA2254": ON,       # on/Info
}


def host_state(label):
    """The weakest configuration a consumer needs before `label`'s diagnostic says anything.

    A label naming several ids takes the most visible of them: `CA1307/CA1310` is reachable at
    all only through `CA1310`, and reporting the pair as `opt-in` is the honest reading either
    way. An unknown `CA` is `opt-in` rather than `on`, because the failure that matters is
    crediting a host nobody has switched on.
    """
    ids = re.findall(r"\b(?:CA|IDE|CS)\d{4}\b", label or "")
    if not ids:
        return PACKAGE
    if any(i.startswith("IDE") for i in ids):
        return CODE_STYLE
    if all(i.startswith("CS") for i in ids):
        return COMPILER
    return ON if any(HOST_STATE.get(i) == ON for i in ids) else OPT_IN


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
shadowed = []
for v in universe.values():
    r = dict(v)
    for name, fn in (("Out of scope", out_of_scope), ("Compiler", compiler), ("Hosted", hosted)):
        why = fn(v)
        if not why:
            continue
        if name == "Hosted":
            state = host_state(why)
            r["hostState"] = state
            # ⚠ A host nobody has turned on does not shadow a rule that ships. `Hosted` and
            # `Catalogued` are different claims about who covers a concept, and for an `opt-in`
            # or `code-style` diagnostic the honest answer is *both*: Roslyn owns the concept and
            # Skala is the one reporting it in a build with nothing configured. ADR-008's
            # corollary -- Skala must be worth using with nothing hosted -- is the tie-break, and
            # it lived only in doc 08's prose until this branch existed, which is why nine
            # shipped rules were being counted as duplicates of a diagnostic that says nothing.
            #
            # ⚠ `package` stays on the `Hosted` side deliberately: a consumer running NUnit has
            # NUnit.Analyzers, which is the reasoning doc 08 already used to cut SK8003/SK8004.
            if state in (OPT_IN, CODE_STYLE) and catalogued(v):
                shadowed.append((v["key"], why, state, catalogued(v)))
                # ⚠ `continue`, not `break`, and the difference is the whole fix. `Hosted` is the
                # last test in this tuple, so falling out of the loop normally is what runs the
                # `else` branch below and lets `catalogued()` be reached at all.
                continue
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

print()
print("Hosted bucket by state:",
      dict(collections.Counter(r.get("hostState") for r in rows if r["bucket"] == "Hosted")))
# ⚠ Printed rather than silently applied. These are the rows where both maps make a claim and the
# host's is the weaker one; #281 is the record of them being adjudicated, and a row appearing here
# for the first time is a new adjudication somebody owes rather than a number that just moved.
print(f"Yielded to Catalogued -- host is opt-in/code-style ({len(shadowed)}):")
for key, why, state, sk in sorted(shadowed, key=lambda t: (str(t[3]), t[0])):
    print(f"  {str(sk):8} {key:66} {why:18} {state}")

# ⚠ The residue of #281, and the only part of it that is still a defect. A row the *hosted* map
# claims with a diagnostic that is `on` in a stock build, which `catalogued.json` also credits to a
# rule that ships, is a rule duplicating something every consumer already has -- ADR-008's "host,
# never rebuild" with nothing to weigh against it, because the corollary about being worth using
# with nothing hosted has no purchase on a diagnostic that is switched on. Printed rather than
# silently bucketed, because the fix is to retire the rule and that is a decision with a baseline
# consequence in every repository holding one, not a number for this script to move.
#
# ⚠ `retired` is filtered out, and this is load-bearing rather than tidy. A rule retired AFTER
# shipping keeps its rules.json entry -- that is how the descriptor stays resolvable and the docs
# page stays a tombstone -- so reading every id in the file would count a withdrawn rule as shipped
# and this alert could never be cleared by acting on it. It would go on naming rules that had
# already been retired, which is the failure mode where an instrument reports the same defect for
# ever and everyone learns to scroll past it.
shipped_ids = {r["id"] for r in json.load(
    open(f"{REPO}/Rules/Rikarin.Skala.Rules.Metadata/rules.json"))["rules"]
    if not r.get("retired", False)}
duplicating = []
for v in universe.values():
    if out_of_scope(v) or compiler(v):
        continue
    why = hosted(v)
    if not why or host_state(why) not in (ON, COMPILER):
        continue
    sk = catalogued(v)
    if sk in shipped_ids:
        duplicating.append((sk, v["key"], why))

if duplicating:
    print()
    print(f"⚠ ALERT: {len(duplicating)} shipped rule(s) duplicate a diagnostic that is ON at stock:")
    for sk, key, why in sorted(duplicating):
        print(f"  {sk:8} {key:66} {why}")
    print("  ADR-008 hosts these. Retire the rule (`retired` in allocated-ids.txt, never deleted --")
    print("  ADR-012 makes the id permanent) or refute the hosting with a measurement. See #281.")

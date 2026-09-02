# 17 — Inspection Parity

What ReSharper and SonarQube catch that Skala does not, measured rather than asserted.

## Why this document exists

[08](08-rule-catalogue.md) names a catalogue of rules and ships a fraction of it. That catalogue was
written in a single pass at the start of the project and **had never been checked against what the
tools it replaces actually cover**. The question "why is it not hundreds?" had no answer, because
nobody had counted.

⚠ **This stopped being a curiosity when ReSharper became a tool to be *retired*.**
[01](01-technology-decisions.md) § ADR-001 makes replacement the goal once results are identical.
**A tool cannot be retired without knowing what it catches that its replacement does not.** Today a
ReSharper inspection Skala lacks is still shown by Rider in the editor, so the gap costs nothing.
After replacement it is caught by nobody. That asymmetry is the whole argument for measuring this,
and it does not weaken as severity drops.

**This document is a map, not a plan.** It does not allocate `SK` ids, change
`rules.json`, or decide what ships. [08](08-rule-catalogue.md) remains the allocation register; the
next revision of it should be built from § "The uncovered set" below.

## ⚠ Severity is metadata here, never a filter

An earlier framing of this work scoped it to the 438 inspections at `warning` or `error` — the ones
"visible rather than dormant". **That was wrong in both directions and the whole document is
measured against all 888.**

1. **The quiet inspections are where this project's premise lives.**
   `convert_to_primary_constructor` is `suggestion` in the export. `convert_to_extension_block` — the
   C# 14 rewrite — is `hint`. `lambda_expression_can_be_made_static` is `none`. The entire `SK1xxx`
   modernization range exists because an AI writes an older dialect of C#, and the inspections that
   catch exactly that are the quiet ones. A `warning`-and-above filter would have excluded the most
   valuable part of the gap.
2. **The 92 at `none` are dormant, not rejected.** `none` is overwhelmingly ReSharper's shipped
   default rather than a decision the author took, and the expectation is that they get enabled.
3. **After replacement, a `hint` nobody provides is a `hint` nobody sees.**

⚠ **One distinction that is easy to conflate, and both halves are right.**
[03](03-configuration-model.md) maps `hint` to `DiagnosticSeverity.Hidden` and shows it only under
`--include-hints`, so that a hundred hints do not bury a dozen errors in a terminal. **That display
decision stands and this document does not touch it.** Whether a rule should *exist* is a separate
question from how loudly it is printed, and for hints the answer is yes. Nothing below should be read
as endorsing a severity filter on the catalogue.

Every inspection's configured severity is carried in the tables anyway, because it is the author's
own statement of how much they want the finding and is therefore useful for ordering work.

## Method

| Source | What it gave |
|---|---|
| `editor_config_template` | The universe and the author's own configured severity. 4 226 keys, of which **3 021** are `resharper_*_highlighting` inspection severities |
| `jb inspectcode --dumpIssuesTypes` | ReSharper's own catalogue: id, category, description, shipped default severity. 3 086 types at 2025.2.6, 3 208 at 2026.2.1 |
| `Core/Rikarin.Skala.Options/options.json` | The option registry and its tiers — what settles the `Option` bucket |
| `Rules/…/rules.json`, `allocated-ids.txt` | What is shipped and what ids are allocated |
| [08](08-rule-catalogue.md) | The catalogue of concepts, shipped and outstanding |
| `SonarSource/sonar-dotnet` `analyzers/rspec/cs/` | 480 published C# rule metadata files |
| `jb inspectcode` over a `git archive` of Vixen | Which inspections actually fire on real code |

### ⚠ The measurement run raises every severity first, and this is not a detail

`none` means an inspection produces nothing. **A zero from a disabled inspection and a zero from a
clean codebase are the same zero**, and reading the first as the second is how a real gap disappears
into a table. Milestones 7 and 8 each lost time to that shape of error.

So the scratch copy's `.editorconfig` has **every one of the 3 021 inspection keys rewritten to
`warning`**, appended as a trailing `[*.cs]` section so that Vixen's own formatting and arrangement
preferences are left intact — a formatting inspection is then still judged against the style Vixen is
actually written in. The run reports down to `INFO`. A zero in the fire-count column therefore means
*did not fire*, full stop.

### Two version numbers, and why they differ

The export was written by Rider 2025.2.x and the globally installed `jb` matches it. That version
**cannot load this repository's projects**: .NET 10.0.400 ships MSBuild 18.9, and 2025.2.6 fails
every project with `Unknown tools version: 18.9`, producing an empty report that looks exactly like a
clean tree. The firing measurement therefore uses **2026.2.1**, installed to a scratch `--tool-path`
so the user's own global tool is untouched. Inspections that exist only in 2026.2.1 are outside the
export's universe and are not counted; the version is used to *run*, not to define the surface.

### What the classification is, and where it is soft

Every C#-relevant inspection lands in exactly one bucket, first match wins:

| Bucket | Meaning |
|---|---|
| **Out of scope** | Another language or engine, or ReSharper's own annotation machinery. Says why |
| **Compiler** | The C# compiler already reports it (`CS####`). Skala surfaces these; it never reimplements them |
| **Hosted** | A Roslyn `CA*`/`IDE*` analyzer covers it. ADR-008: Skala hosts rather than rebuilds |
| **Option** | Arrangement or formatting, governed by a key in the option registry — not by a rule |
| **Catalogued** | An `SK` id in [08](08-rule-catalogue.md) already names the concept |
| **Uncovered** | None of the above. The real gap, and the only bucket that is a work queue |

⚠ **`Hosted` and `Catalogued` are hand-built maps and are therefore lower bounds.** 129 inspections
are mapped to 52 distinct `SK` ids, and 75 land in `Hosted` — Roslyn `CA*`/`IDE*` or a test
framework's own analyzer package; both maps were written by reading, not generated, so each will be
missing entries. **Every entry missing from them inflates `Uncovered`.** The uncovered count below is
an *upper* bound on the gap, and the honest reading of it is "at most this many", not "exactly this
many".

⚠ **One half of that judgement is now pinned by a test, and the other half cannot be.**
`RuleCatalogTests.TheParityMap_CreditsEveryShippedReSharperMappingToItsOwnRule` asserts
rules.json ⊆ `catalogued.json`: an inspection a *shipped* rule declares must be credited to that
rule's own id. That is the direction with a mechanical answer, and it caught four shipped rules the
map had omitted. The other direction — is every concept doc 08 names actually mapped from every
inspection that expresses it? — is a reading, and no test can hold it. The bound stays a bound.

⚠ Only **49 of doc 08's 109 rules have a ReSharper counterpart at all**. The other 60 — the taint
rules, the metrics, the duplication detector, the `SK9xxx` tool diagnostics — are ground ReSharper
does not cover, and they are the reason this document is a parity map rather than a to-do list.
Parity with ReSharper is not the whole of what Skala is for.

## The classification

⚠ **The universe is 888, not 853.** An earlier count of 853 C#-relevant inspections is close but was
not reproducible; excluding keys whose prefix names another language (`cpp_`, `vb_`, `xaml_`, `js_`,
`asp_`, …) and the Unity/Burst set leaves **888**, and the `none` count of **92** matches the earlier
figure exactly, which is the strongest evidence the two are the same universe. The 65 Unity/Burst
inspections are counted separately: they are C#, and they are not code Skala is aimed at.

| Bucket | Count | of 888 | `error` | `warning` | `suggestion` | `hint` | `none` |
|---|---:|---:|---:|---:|---:|---:|---:|
| **Uncovered** | **578** | **65.1 %** | 3 | 320 | 171 | 57 | 27 |
| Catalogued | 92 | 10.4 % | 0 | 41 | 31 | 12 | 8 |
| Hosted | 75 | 8.4 % | 0 | 44 | 18 | 10 | 3 |
| Option | 67 | 7.5 % | 0 | 1 | 3 | 15 | 48 |
| Out of scope | 74 | 8.3 % | 10 | 43 | 5 | 10 | 6 |
| Compiler | 2 | 0.2 % | 1 | 1 | 0 | 0 | 0 |
| **Total** | **888** | | 14 | 450 | 228 | 104 | 92 |

A further 65 Unity/Burst inspections are out of scope for the engine rather than for the language.

⚠ **This table read `Uncovered` 580 / `Catalogued` 89 / `Hosted` 76, and both of those numbers were
produced by an instrument with a defect in it. The measured figures are 578 / 92 / 75.** The
correction is small and the failure mode it exposes is not, so it is worth stating in full.

`classify.py` looked its two hand-built maps up by **inspection id**. `universe.py` can only attach
an id to an export key by joining against `jb inspectcode --dumpIssuesTypes`, and that join finds
nothing for any inspection newer than the dumped release — **81 of the 888 rows carry no id at
all.** Every one of those 81 therefore missed both maps in silence and fell through to `Uncovered`,
which is the residue bucket and so the work queue. An omission in a map does not look like an
omission; it looks like a gap in the product. Reading the maps through a key-indexed view as well,
built with the same id → export-key transform the join already uses, recovers **six** rows:
`ConvertToExtensionBlock` and `MoveToExtensionBlock` (`SK1004`), `AsyncVoidMethodWithoutAwait`
(`SK3001`), `ReplaceWithOfType` (`SK4010`), `TemplateIsNotCompileTimeConstantProblem` (`SK2016`),
and `UseArgumentExceptionThrowIfMethod`, which is `Hosted` on `CA1511`.

⚠ **Those six are also why the published 580 was not reproducible.** The run that produced it had
`types.json` — an uncommitted cache from an older `jb` — as a second metadata source, which supplied
ids for exactly those six. Re-running the committed pipeline on the 2025.2.6 dump alone gave **586**,
not 580, and nothing in the document said why. The fix makes the six land in their buckets from the
committed inputs alone, so the number no longer depends on a file that was never in the repository.

Four more left `Uncovered` because the map was missing rules that **already ship**:
`MergeCastWithTypeCheck` (`SK1015`), `PossibleInvalidOperationExceptionCollectionWasModified`
(`SK2007`), `ReturnOfTaskProducedByUsingVariable` (`SK3007`) and `UseAwaitUsing` (`SK3503`). Each is
named as the `resharperId` of a shipped rule in `rules.json` and each was being counted as work
still to do. This is the direction the new test now asserts.

Two entries were wrong rather than missing, and removing them does not move the total:

- **`ShortLivedHttpClient` was credited to `SK4008`.** [08](08-rule-catalogue.md) defines `SK4008` as
  the async state machine built for a method that always completes synchronously. `HttpClient` socket
  exhaustion is a different problem with a different fix. The mapping is deleted and the inspection
  is genuinely uncovered — it is issue #63. It would have
  become `Catalogued` for free once the lookup was fixed, which is the reason to state it: the fix
  makes wrong entries *count*, where before they could hide behind a missing id.
- **`NonReadonlyMemberInGetHashCode` was `Hosted` as "CA1065-adjacent".** `CA1065` is "do not raise
  exceptions in unexpected locations" and says nothing about a hash code computed over mutable state.
  The entry credited a Roslyn analyzer that does not exist. Removing it exposed a second wrong entry
  underneath: the map also read the inspection as `SK2004`, which is *typed equality without
  `Equals(object)`* and says nothing about a hash code either. Both are deleted and the row is
  genuinely uncovered — it is issue #161. ⚠ **Two wrong entries stacked on one inspection is the
  argument for the test rather than for a third careful read**: the hosted map hid the catalogued
  one, and only fixing the first made the second reachable.

⚠ **A third over-claiming entry, and the reason the count keeps moving up.** `catalogued.json` also
credited `PossibleMultipleEnumeration` to `SK4006`. `SK4006` is *Review a materialization used only by
`foreach`* — a `ToList()` that should be **removed**. Multiple enumeration is the opposite: no
materialization where one is needed. The entry is deleted and the row is uncovered; the concept is
issue #267, which reached the queue from the SonarQube rule-idea pass rather than from here, because
this map was hiding it. **Three wrong entries have now been found in one hand-written map**, two of
them only reachable after the one above them was fixed.

⚠ **`gov.json` was id-keyed too, and fixing it changed no bucket — which is the finding.**
`option()` now re-indexes `gov.json` onto the export keys like the other two maps, so
`ArrangeAccessorsOrder` and `ArrangeEmptyString` are found rather than silently missed. Both still
land in `Uncovered`, because the keys they name — `resharper_accessors_order` and
`resharper_empty_string_style` — **are not in the option registry at all**. Before the fix they read
as "nothing governs this"; now they read as "something governs this and Skala does not know the key",
which is the distinction § "The `Option` bucket covers less than its size suggests" is entirely about.
⚠ **They are a formatter and registry gap, not a rule gap, and they inflate `578` by two.** The
bucket vocabulary has no name for that, and inventing one here would be a worse error than the
overcount — [`06`](06-arrangement-and-syntax-styles.md) owns them. Both are excluded in
`ledger-resharper.json` with that reason.

⚠ **Two surviving entries over-claim, and are left alone deliberately.**
`UseArgumentExceptionThrowIfMethod → SK1020` and `ReplaceWithOfType → SK4010` both credit a shipped
rule with more than it does: `SK1020` covers `ArgumentNullException.ThrowIfNull` only, and `SK4010`
covers a `Where` fused into the operator that follows it, not `OfType`. The inspections are broader
than the rules. Narrowing the map would move both back to `Uncovered` and is a decision about the
*rules*, not the instrument — issue #105 and
issue #100 carry it.

⚠ **The sections below still quote 580, and are not silently rebased onto 576.** The ranking, the
fire counts and the concept collapse were measured against the 580-row residue over a SARIF run that
this correction did not repeat; changing the number without re-running the measurement would be the
same kind of claim this document exists to replace. None of the ten rows that left the residue
appears in the ranked queue, so the ordering is unaffected — but `ReplaceWithOfType` is used below as
the worked example of ReSharper splitting one idea across many ids, and it is now `Catalogued`.

⚠ **Three corrections during the pass moved 78 inspections out of `Uncovered`, and all three are
recorded because each was initially got wrong the same way — by classifying on the key's name.** The
first pass put the uncovered count at 658; reading the entries rather than their names took it to
580. **That 12 % error, all of it in one direction, is the measure of how much this kind of table
should be trusted before somebody has read it.**

- **30 `n_unit_*`/`xunit_*` inspections are `Hosted`, not uncovered.** NUnit.Analyzers ships the
  equivalent `NUnit####` diagnostic with the test framework, and `xunit.analyzers` does the same.
  **This is doc 08's own reasoning**: `SK8003` and `SK8004` were cut precisely because `xUnit1001`
  and `xUnit1049` already exist. Applying it consistently to NUnit removes 30 entries.
- **17 `RouteTemplates.*` (ASP.NET Core routing) and 8 `entity_framework_*` are framework front
  ends**, in the same class as the Godot and MEF inspections — a plugin's audience, not the core's.
- ⚠ **8 markup inspections were counted as C# because their keys carry no language prefix** —
  `UnclosedScript`, `OtherTagsInsideScript1/2`, `Mvc.InvalidModelType`, `ResourceNotResolved` and
  kin. They were 8 of what this document first reported as "12 uncovered inspections at `error`".
  **The real number is 3**, and the alarming version of that row was an artefact of the filter.

The uncovered set is by category: `CodeSmell` 193, `BestPractice` 143, `CodeRedundancy` 101,
`LanguageUsage` 54, `DeclarationRedundancy` 38, and a long tail — plus **31 with no category at all**,
which is the same id-join failure showing through in the one place the fix above does not reach. A
row whose id the dump does not carry has no category either, and no key-indexed view can invent one.
The earlier reading of this line (`CodeSmell` 200, `BestPractice` 144, `CodeRedundancy` 103,
`LanguageUsage` 57) was taken from the run that had the uncommitted `types.json` and so had those 31
categorised; it is not comparable to the row above it.

### ⚠ The `Option` bucket covers less than its size suggests, and this is the finding most likely to be argued with

The instruction that produced this work warned that `arrange_this_qualifier` and
`redundant_parentheses` belong in `Option`, and that counting them as missing rules double-counts
work already done. **The first half is right and the second half is not**, and checking the registry
rather than the names is what shows it:

| Inspection | Governing option | Tier |
|---|---|---|
| `ArrangeThisQualifier` | `resharper_instance_members_qualify_declared_in` | ⚠ **D** |
| `ArrangeRedundantParentheses` | `resharper_parentheses_redundancy_style` | ⚠ **D** |
| `ArrangeTrailingCommaInMultilineLists` | `resharper_trailing_comma_in_multiline_lists` | ⚠ **D** |
| `ArrangeStaticMemberQualifier` | `resharper_static_members_qualify_members` | ⚠ **D** |
| `ArrangeAttributes` | `resharper_place_attribute_on_same_line` | ⚠ **D** |
| `UnnecessaryWhitespace` | `trim_trailing_whitespace` | ⚠ **D** |
| `ArrangeAccessorOwnerBody` | `resharper_accessor_owner_body` | A |
| `SuggestVarOrType_BuiltInTypes` | `csharp_style_var_for_built_in_types` | A |
| `WrongIndentSize` | `indent_size` | A |

[03](03-configuration-model.md) is explicit that **Tier D means "known to the registry and not
implemented"** — parsed, reported by `skala config check`, and then *ignored*. So of the 67
inspections in the `Option` bucket:

- **52 are Tier A** — genuinely covered. The formatter reads the option and an oracle fixture pins
  it. Counting these as missing rules would double-count real work, exactly as warned.
- ⚠ **15 are Tier D or have no registry key at all** — the arrangement is *declared* and not
  performed. **These are a real gap.** They are not a gap in the rule catalogue and must not inflate
  a rule target; they are a gap in the formatter, and they belong to
  [06](06-arrangement-and-syntax-styles.md) rather than to [08](08-rule-catalogue.md). But a
  ReSharper user who loses ReSharper today loses `this.` qualification, redundant-parenthesis
  removal and trailing-comma arrangement, and no rule count anywhere records that.

⚠ **This is the double-counting trap in the direction nobody was watching for.** The warning was
against inflating the gap by miscounting options as rules. The measurement finds the opposite error
available too: crediting Skala with arrangement it declares and does not do. Both are avoided only by
reading the tier, and the tier is in `options.json`, not in the key's name.

The remaining `Option` members are the whitespace, indent, line-break and blank-line families —
`BadIndent`, `MissingSpace`, `RedundantLinebreak` and their kin. These are not one rule each; they
are the formatter's output as a whole, and `SK0001` "the file is not formatted" is the single
diagnostic that reports them. Treating those 40-odd inspections as 40 missing rules would be the
clearest possible case of the double-count.

## ⚠ Hosted and Catalogued were making the same claim about 18 rows, and only one could be right

`hosted()` runs before `catalogued()` and `break`s, so an inspection in both maps bucketed `Hosted`
and the Skala rule crediting it was shadowed — its `catalogued.json` entry never took effect.
**18 rows were in that state, naming 11 distinct `SK` ids, 9 of which ship.**
([#281](https://github.com/Rikarin/SKALA/issues/281))

⚠ **The count was found by importing `classify.py` and asking `catalogued()` about every row the run
bucketed `Hosted`, not by intersecting the two maps by hand.** `hosted()` and `catalogued()` both
fall back to `*_BYKEY`, so three of the eighteen match on the `resharper_*_highlighting` export key
rather than on the inspection id, and a set intersection on ids alone misses them — which is how the
first reading of this said six shipped rules rather than nine. `SK2010`, `SK2014` and `SK4010` are
the three. (`SK8003` and `SK8004` are the other two ids and do not ship.)

⚠ **Why no test caught it.** `RuleCatalogTests.TheParityMap_CreditsEveryShippedReSharperMappingToItsOwnRule`
asserts the *entry exists* in `catalogued.json`. It never asks whether this pipeline reaches that
entry. The map was correct and inert — a mapping nothing can set, in a different place from the one
this document already names.

### The adjudication, per rule

ADR-008 is *host, never rebuild*; its corollary is that Skala must be **worth using with nothing
hosted**. Those two sentences give opposite answers for a diagnostic nobody has turned on, so the
question is not "does a `CA*`/`IDE*` exist" but "what does a consumer see with nothing configured".
Each Roslyn diagnostic the hosted map names was run against the Skala rule's **own positive
fixtures**, in a probe built outside this repository with empty `Directory.Build.props`/`.targets`
above it, SDK 10.0.400, results read from SARIF; every configuration carried a canary proved to fire
in it, so a zero is a decline rather than a dead run.

| Rule | Hosted map said | Measured on the rule's own positives | Verdict |
|---|---|---|---|
| `SK1006` | `IDE0063` | 3/3 — but only with `EnforceCodeStyleInBuild` **and** a severity line | rule stands, host is `code-style` |
| `SK1010` | `IDE0078` | **0/5**, and `IDE0078` fired twice on its own shapes in the same build | rule stands; ⚠ Roslyn has no `x != null` → `x is not null` rule |
| `SK1012` | `IDE0066` | **0/3**; `IDE0066` converts an existing `switch` *statement* and fired on one | rule stands; `SK1012`'s input is an `if`/`else if` chain |
| `SK1020` | `CA1510` | **3/3 at stock**, `CA1510` is enabled/`Info` | ⚠ **duplicate — retire `SK1020`** |
| `SK1030` | `IDE0074`, `IDE0029` | **0/4** both. ⚠ `IDE0074` is a **phantom** | rule stands; ⚠ map corrected to `IDE0054`, which is 4/4 |
| `SK1034` | `CA1860` | **3/4 at stock**, and `CA1829` takes the fourth, both enabled/`Info` | ⚠ **duplicate — retire `SK1034`** |
| `SK2010` | `CA1304/CA1305`, `CA1307/CA1310` | `Hidden` or disabled at stock; nothing until `Recommended` | rule stands, host is `opt-in` — the decision § "SK2150" already took |
| `SK2014` | `CA1031` | disabled by default; under `All` it fires on bare `catch {}` only | rule stands; `CA1031` is about the breadth of the caught type, `SK2014` about the catch being empty |
| `SK4010` | `CA1829`, `CA1868`, `IDE0270` | **0/4** all three | rule stands; ⚠ all three ids were wrong, see below |

⚠ **`IDE0074` does not exist in practice, and it was crediting two rows.** It is in the tool's
supported-rule descriptor list with the title "Use compound assignment", so it is loaded rather than
missing, and it is **never emitted**. Six canonical `x = x ?? y` shapes — local, instance field,
static field, property, `this.`-qualified, string-with-literal — under
`dotnet_diagnostic.IDE0074.severity = warning` *and* `dotnet_style_prefer_compound_assignment =
true:warning` *and* `AnalysisMode=All` *and* `EnforceCodeStyleInBuild=true` all reported **`IDE0054`**
and none reported `IDE0074`. So two rows bucketed `Hosted` on a diagnostic that reports nothing,
which is the worst of the two failure modes: the concept really is hosted, and the id recording it
was not the one doing the hosting.

⚠ **`CA1829` was a near-miss that mattered more than a plain error would have.** `CA1829` is `on` at
stock, so `"ReplaceWithSingleCallToCount": "CA1829"` filed `SK4010` as duplicating something every
consumer already has. It reports **0 of 4** of `SK4010`'s positives while firing on `SK1034`'s
`count-call.cs` in the same compilation — a correct decline, because `values.Where(p)` returns an
iterator and an iterator has no `Count` property to prefer. `CA1868` and `IDE0270` were hedged as
"-adjacent" and are simply different rules (a `Contains` guard on a set, and null-check
simplification). The real host of all three inspections is **`IDE0120` "Simplify LINQ expression"**,
4/4 on the fixtures and `code-style`, so it says nothing in a default build either.

### What changed in the pipeline

A hosted entry now carries the state defined in [08](08-rule-catalogue.md) § "What the hosted map
records now", and **a host that is `opt-in` or `code-style` no longer shadows a rule that ships**.
`package` and `on` still do — a consumer running NUnit has NUnit.Analyzers, which is the reasoning
that cut `SK8003`/`SK8004`. **12 rows moved from `Hosted` to `Catalogued`**, covering the seven rules
that stand; `Hosted` 91 → 80 and `Catalogued` 254 → 265 on the committed inputs.

⚠ **And the residue is printed rather than bucketed.** `classify.py` now ends with an alert naming
every shipped rule that duplicates a diagnostic which is `on` at stock. It currently names `SK1020`
and `SK1034`, which is the list this section decided; a row appearing there in future is a new
adjudication somebody owes, not a number for the script to move on its own.

⚠ **The bucket table at the head of this section is older than all of this** — it reads `Uncovered`
578 / `Catalogued` 92 / `Hosted` 75 against today's 401 / 265 / 80 — and is left as the record of
what was measured then rather than restated. Read `classify.py`'s own output for current figures.

## The uncovered set, ranked by what fires on real code

**This is the work queue**, and it is the section [08](08-rule-catalogue.md)'s next revision should
be built from. A list of inspections is a wish; a list ordered by how often each one has something to
say about code somebody actually wrote is a plan.

### What was measured

A `git archive` of Vixen at `44b88648`, ten projects, **49 757 findings** deduplicated on
(rule, file, line, column):

| Project | Findings |
|---|---:|
| `Vixen.Rendering.Tests` | 16 286 |
| `Vixen.Rendering` | 10 227 |
| `Vixen.Editor.AssetEditors` | 6 160 |
| `Vixen.Raven` | 3 892 |
| `Vixen.Ui` | 3 086 |
| `Vixen.Engine` | 2 712 |
| `Vixen.Animation` | 2 651 |
| `Vixen.Audio` | 1 869 |
| `Vixen.Ai` | 1 784 |
| `Vixen.Net` | 1 090 |

⚠ **Ten projects, not the whole tree, and the reason is a failure worth recording.** Loading Vixen's
412-project solution did not converge in 45 minutes of wall time. A project at a time loads only that
project's `ProjectReference` closure and completes in one to three minutes each. The ten were chosen
as the largest non-generated projects plus one test project, ≈ 900 files of the 4 717 in the tree.
**A fire count here is therefore a lower bound**, and the ranking rather than the magnitude is what
should be read.

### ⚠ Two ways a zero can lie, and both are present

1. **Disabled, not clean** — handled. Every inspection was raised to `warning` first. The proof that
   this mattered: **44 inspections sitting at `none` in the export fired anyway**, led by
   `InternalOrPrivateMemberNotDocumented` at 3 408 and `ArrangeRedundantParentheses` at 1 231. Under
   the export's own severities every one of those would have read as a clean zero.
2. ⚠ **Solution-wide, not clean** — *not* handled, and it must be stated. The runs used `--no-swea`,
   so ReSharper's solution-wide analysis was off. The `.Local` variants fired (`UnusedMember.Local`
   112, `UnusedParameter.Local` 61) and **every `.Global` variant scored zero for that reason rather
   than for any fact about Vixen**. Roughly 55 inspections — the whole "unused across the solution",
   "never instantiated", "can be sealed" family — are unmeasured here, not measured at zero.

**499 of the 580 uncovered inspections did not fire at all.** Read with the two caveats above, that
is mostly a statement about a 900-file sample and a disabled analysis mode, not evidence that four
fifths of the gap is theoretical.

### The top of the queue

| Fires | Inspection | Export severity | Category | What it reports |
|---:|---|---|---|---|
| 3 408 | `InternalOrPrivateMemberNotDocumented` | `none` | BestPractice | Missing XML comment for a private or internal member |
| 951 | `InheritdocConsiderUsage` | `none` | CodeSmell | `<inheritdoc />` would inherit the base's documentation |
| 397 | `LambdaExpressionCanBeMadeStatic` | `none` | LanguageUsage | Lambda can be `static` — an allocation, not a style |
| 331 | `ArrangeEmptyString` | `none` | CodeStyleIssues | Empty string style |
| 220 | `ArrangeTypeModifiers` | `hint` | CodeStyleIssues | Explicit vs implicit modifier on types |
| 172 | `PrimaryConstructorParameterCaptureDisallowed` | `none` | CodeSmell | Primary constructor parameter capture |
| 139 | `SuggestBaseTypeForParameter` | `none` | BestPractice | Parameter could take the base type |
| 135 | `CheckNamespace` | `warning` | ConstraintViolation | Namespace does not match file location |
| 83 | `LoopCanBePartlyConvertedToQuery` | `none` | LanguageUsage | Part of a loop body is a LINQ expression |
| 63 | `ForeachCanBePartlyConvertedToQuery…` | `hint` | LanguageUsage | As above, via another `GetEnumerator` |
| 58 | `UsingStatementResourceInitialization` | `warning` | CodeSmell | Object initializer on a `using` variable |
| 53 | `InlineTemporaryVariable` | `hint` | LanguageUsage | Temporary used once |
| 39 | `PartialTypeWithSinglePart` | `warning` | DeclarationRedundancy | Redundant `partial` |
| 25 | `ForCanBeConvertedToForeach` | `suggestion` | LanguageUsage | `for` over an indexable is a `foreach` |
| 24 | `TailRecursiveCall` | `hint` | CodeSmell | Tail recursion could be a loop |
| 23 | `RedundantEmptySwitchSection` | `warning` | CodeRedundancy | Empty `switch` section |
| 21 | `RedundantCast` | `warning` | CodeRedundancy | Redundant cast |
| 21 | `MoveLocalFunctionAfterJumpStatement` | `hint` | BestPractice | Local function before `return`/`continue` |
| 20 | `LocalVariableHidesMember` | `warning` | CodeSmell | Local hides a member |
| 20 | `ParameterHidesMember` | `warning` | CodeSmell | Parameter hides a member |

By category, among the 81 uncovered inspections that fired:

| Category | Fired | Findings |
|---|---:|---:|
| BestPractice | 17 | 3 677 |
| CodeSmell | 23 | 1 328 |
| LanguageUsage | 17 | 707 |
| CodeStyleIssues | 3 | 554 |
| ConstraintViolation | 1 | 135 |
| CodeRedundancy | 15 | 111 |
| DeclarationRedundancy | 5 | 51 |

⚠ **The top of this table is documentation and naming, and that is a finding rather than a
disappointment.** `InternalOrPrivateMemberNotDocumented` and `InheritdocConsiderUsage` are 4 359 of
the 6 563 findings — two thirds — and doc 08 has one rule in that space (`SK7010`, public API only,
shipped at `none`). ⚠ **They are also the clearest case in this document for not letting a fire count
drive a decision on its own.** `SK7010` at `warning` already produces 1 868 findings on
`Testing/corpus` alone, and [08](08-rule-catalogue.md) lists that threshold as suspect precisely
because calibrating against the tree the rule runs on is how a metric comes to certify the present.
A high count here means the rule *has something to say*; it does not mean the rule should be loud.

⚠ **The genuinely valuable ones are further down and are quiet by configuration.**
`LambdaExpressionCanBeMadeStatic` fires 397 times at `none`: each is a closure allocation in a game
engine's hot path, it is exactly `SK4002`'s subject, and today nothing reports it. `RedundantCast`
(21) and `PartialTypeWithSinglePart` (39) are the mechanical cleanups a Rider user does not notice
they rely on until they are gone.

### ⚠ The Tier D arrangement gap has a number, and it is the largest single one in this measurement

`ArrangeRedundantParentheses` — governed by `resharper_parentheses_redundancy_style`, **Tier D, not
implemented** — fires **1 231 times** on 900 files. `ArrangeObjectCreationWhenTypeNotEvident` fires
950. Together with the rest of the `Option` bucket, arrangement accounts for **3 718 findings**,
against 6 563 for the entire uncovered rule set.

**The thing standing between Skala and retiring ReSharper is not mostly a rule catalogue.** It is
fifteen unimplemented arrangement options, and they outweigh most of the rule gap by volume on real
code. That is the practical conclusion of this document and it was not the expected one.

### ⚠ Re-measured after the fifteen were implemented: 1 928 findings become 195

The conclusion above was acted on. [06](06-arrangement-and-syntax-styles.md) § "The fifteen Tier D
arrangement options, settled" has the per-option verdict; this is what it did to the numbers, on the
same ten projects, with `skala arrange` run over a `git archive` scratch copy between the two
`jb inspectcode` runs.

| | before | after | |
|---|---:|---:|---:|
| **The fifteen** | **1 928** | **195** | **−1 733** |
| The whole `Option` bucket | 7 756 | 5 757 | −1 999 |

Per inspection, the ones that moved:

| Inspection | before | after |
|---|---:|---:|
| `ArrangeRedundantParentheses` | 1 226 | **1** |
| `ArgumentsStyleLiteral` | 471 | **1** |
| `ArgumentsStyleOther` | 20 | **0** |
| `ArgumentsStyleStringLiteral` | 13 | **0** |
| `ArrangeThisQualifier` | 5 | **0** |
| `SeparateControlTransferStatement` | 11 | 11 — stays Tier D, and [06](06-arrangement-and-syntax-styles.md) says why |
| `SuggestVarOrType_DeconstructionDeclarations` | 182 | 182 — the option moved, the inspection did not; see below |

⚠ **`ArrangeRedundantParentheses` — this document's headline, 1 231 findings — is now 1**, and that
one is `a * (x * y)`, which Skala keeps on purpose: equal precedence is not associativity and on
`float` the grouping is the author's arithmetic.

### ⚠ Two corrections to how this document measured, both of which flattered the gap

1. **The `Option` bucket totals here and above are not comparable, and the reason is the tree's build
   state.** The original run was `--no-build` on an unrestored `git archive`, where a great many
   types do not resolve and the inspections that depend on them stay quiet. Re-run with the same ten
   projects *built*, the same unarranged sources report 7 756 `Option` findings rather than 3 718.
   The before/after pair above is measured on two identically-built trees, so the −1 999 is a
   controlled difference; **3 718 → 5 757 is not a regression and must not be read as one.**
   ⚠ The per-inspection counts are robust either way: `ArrangeRedundantParentheses` reproduces at
   **1 231 on the unbuilt tree — this document's exact figure** — and 1 226 built.

2. ⚠ **Five of the fifteen were never measurable.** `ArrangeNamespaces` and `ArrangeArgumentsStyle`
   are real `jb cleanupcode` tasks that the M4 profile sweep never probed, so the oracle was running
   without them and declining five of the export's own settings. Two more —
   `dotnet_style_predefined_type_for_member_access` and `resharper_place_attribute_on_same_line` —
   were already implemented and credited to a neighbouring key. **Seven of the fifteen were artefacts
   of the measurement rather than missing work**, which is the finding this re-run is really for:
   `gov.json` and `catalogued.json` are named above as "judgement, not measurement", and the oracle
   profile belongs on that list beside them.

⚠ **`SuggestVarOrType_DeconstructionDeclarations` is the double-count this document warned about,
caught in the act.** Its option — `resharper_prefer_explicit_discard_declaration` — is now Tier A,
implemented and pinned. Its *inspection* reports something else entirely, governed by
`resharper_for_deconstruction_declarations`, which the author's export does not set and the registry
does not know; `gov.json` names that key first and correctly, and `classify.py` fell through to the
second because the first had no registry entry. It still fires 182 times and Skala still does not
address it. **Twelve option keys moved D → A; eleven of the fifteen inspections are retired.**

## SonarQube

### ⚠ The licence, and doc 01 currently states it wrongly

[01](01-technology-decisions.md) line 82 records `SonarAnalyzer.CSharp` as **LGPL-3.0-only**.
**It is not.** `SonarSource/sonar-dotnet` carries `LICENSE.txt` = **SONAR Source-Available License
v1.0** (GitHub reports the SPDX id as `NOASSERTION`), verified directly against the repository rather
than taken second-hand. Source-available is not open source.

⚠ **ADR-008's conclusion — never bundle it — is unchanged and strengthened**, and the reason is
sharper than a licence-compatibility argument. That licence defines "Competing" in terms of marketing
a product as a substitute for SonarQube's functionality, and [`README`](README.md) states in its first
paragraph that Skala replaces SonarQube. **Skala is squarely the thing that definition is about.**

**Where the line falls, and it needs to be stated because someone will implement from this map:**

| | |
|---|---|
| ✅ | The rule **list** — ids, titles, types, severities, tags. Facts and short labels, published openly as documentation at `rules.sonarsource.com`. Used here as a checklist |
| ✅ | Implementing a rule that detects the same problem. "`GC.Collect` should not be called" is not ownable, and these problems are long-standing common knowledge |
| ❌ | Copying their `.html` rule descriptions into `docs/rules/`, `rules.json` or `skala explain`. That is their copyrighted prose. **Every summary and rationale Skala carries is written from scratch** |
| ⚠ ❌ | Reading `analyzers/src/**`, their implementation, **at all**. A source-available licence typically forbids using the source to build a competing product, and it would make a Skala rule derivative regardless. **Work from the problem, never from their solution** |

The fetch script that produced the inventory asserts the boundary rather than merely documenting it:
it enumerates only `analyzers/rspec/cs/*.json` and stores only the metadata fields, discarding the
prose.

### The inventory

480 C# rules, all metadata, no prose:

| Type | | Default severity | | Quick fix | |
|---|---:|---|---:|---|---:|
| `CODE_SMELL` | 345 | Blocker | 38 | `unknown` | 251 |
| `BUG` | 94 | Critical | 89 | `infeasible` | 95 |
| `VULNERABILITY` | 41 | Major | 199 | `targeted` | 70 |
| | | Minor | 151 | `covered` | 57 |
| | | Info | 3 | `partial` | 7 |

⚠ `quickfix: infeasible` is worth carrying forward. [08](08-rule-catalogue.md)'s shipping bar
requires a fix, so **the 95 rules Sonar itself marks as having no feasible fix are candidates for
being genuinely hard rather than merely unbuilt** — and for Skala, candidates for the fixless form
that doc 08 repeatedly declines to rule out.

### ⚠ The overlap with ReSharper cannot be measured mechanically, so it was sampled

Sonar states rules prescriptively ("X should not be Y"); ReSharper states them diagnostically
("Redundant X"). A token join across those two vocabularies is a weak instrument, and it shows:

| Join threshold | Sonar rules matched |
|---|---:|
| 2 shared tokens, Jaccard ≥ 0.34 | 15 |
| 2 shared tokens, Jaccard ≥ 0.20 | 122 |
| 1 shared token, Jaccard ≥ 0.15 | 228 |
| 1 shared token, Jaccard ≥ 0.10 | 416 |

**An instrument whose answer moves from 15 to 416 across reasonable thresholds is not an
instrument.** Quoting any one of those numbers would be false precision, so the overlap was instead
measured by **hand-classifying a random sample of 60 rules**, drawn by
`SHA-256("skala-parity-20260827\n" + id)` sorted ascending — a hash of the id rather than a seeded
draw, so the sample depends on nothing but the ids.

| Bucket | Sample | Share | Projected to 480 | 95 % CI |
|---|---:|---:|---:|---|
| **Uncovered** | 32 | 53.3 % | **256** | 195–317 |
| Catalogued | 13 | 21.7 % | 104 | 54–154 |
| Hosted | 11 | 18.3 % | 88 | 41–135 |
| Out of scope | 4 | 6.7 % | 32 | 2–62 |

⚠ **`Hosted` at ~18 % is the finding that keeps this honest.** Roslyn's own analyzers already cover a
large share of Sonar's bug and vulnerability rules — `CA1065`, `CA1303`, `CA1010`, `CA1032`,
`CA1724`, `CA1044` all appeared in a 60-rule sample. Classifying those as uncovered would inflate the
gap badly, and ADR-008's position is that Skala hosts them rather than rebuilding them.

### ⚠ The union is not the sum

**481 Sonar + 888 ReSharper is not 1 369 problems.** The two tools overlap heavily and neither
overlap can be measured precisely. What can be said:

- ReSharper uncovered: **≤ 580** inspection ids, **≤ 510** distinct concepts after collapsing
  variants (below).
- Sonar uncovered: **≈ 256**, 95 % CI 195–317.
- Of the 60-rule Sonar sample, 13 were already catalogued by an `SK` id and several more had a
  ReSharper counterpart in the uncovered set — so a substantial share of Sonar's 256 is *the same
  problem* as one of ReSharper's 580, not an additional one.

A defensible union of **distinct uncovered problems is 600–800**, and the honest thing to say about
that range is that its width is the finding. It is not 1 369, and it is not 111.

## ⚠ Inspection ids are not concepts, and a catalogue is sized in concepts

ReSharper splits one idea across many ids. `ReplaceWithOfType*` is roughly twenty ids;
`StringCompareIsCultureSpecific.1` through `.6` is six; a `.Global`/`.Local` pair doubles a further
set for nothing but accessibility. A rule catalogue that allocated one `SK` id per ReSharper id would
be inflated before anyone argued about its contents.

Collapsing numbered variants, `.Global`/`.Local` pairs and long shared prefixes:

| | |
|---|---:|
| Uncovered inspection ids | 580 |
| after collapsing numbered and `.Global`/`.Local` variants | 525 |
| after also merging long shared prefixes | **510** |

⚠ **The collapse is real but modest — 12 %.** It was worth measuring precisely because it is the
obvious way to argue the gap down, and it does not carry that argument. The `ReplaceWithOfType`
family is vivid and it is not representative; most of the 580 are genuinely distinct ideas.

## The recommended catalogue target

⚠ **The honest answer is "far more than 111", and the reasoning matters more than the number.**

[08](08-rule-catalogue.md) names 109 rules. The measurement says the reachable surface, after every
deduction this document could justify — the compiler's, Roslyn's, the option registry's, other
languages', other engines', the test frameworks' own analyzers' — is **510 uncovered ReSharper
concepts plus roughly 256 Sonar rules, of which a large but unmeasured share are the same
problems**. Even at the pessimistic end of the overlap, the catalogue is naming **under a fifth of
what it would take to replace the two tools.**

**A target of 400–500 concepts** is what the evidence supports for full parity. But the number that
should govern planning is not that one, and this is the part [08](08-rule-catalogue.md)'s next
revision should take:

1. ⚠ **The catalogue's size was never the constraint; the shipping bar is.** Doc 08 ships 29 of 109
   at 26.9 % because a rule ships only with a fix, zero false positives on two reference trees, and a
   negative fixture set at least as large as the positive one. **Multiplying the catalogue by five
   without changing that bar produces a longer list of outstanding work, not more coverage.**
   [16](16-risks-and-open-questions.md) § R3 already names "a hundred that are usually right" as the
   failure mode.
2. **So the target is a statement about scope, not a commitment about dates.** The value of knowing
   it is 510 rather than 111 is that it converts "we are 27 % done" into "we are about 5 % done", and
   the second number is the one that should inform whether ReSharper can be retired and when.
3. ⚠ **A large share of the 580 is cheap.** The `CodeRedundancy` (103) and `DeclarationRedundancy`
   (38) families are syntactic, mechanically fixable, and have near-zero false-positive surface —
   `RedundantCast`, `RedundantJumpStatement`, `RedundantBoolCompare`, `RedundantCatchClause`. These
   are the rules the shipping bar is *easiest* on, and they are 141 of the gap. **The catalogue's
   composition should change, not only its length**: doc 08 is weighted towards semantically hard
   rules, and the cheapest third of the real gap is barely represented in it.

### What this does not recommend

**Do not allocate 500 `SK` ids.** ADR-012 makes an id permanent, and an id allocated against an
inspection nobody has read is exactly the "declared cut with no recorded reason" problem
[08](08-rule-catalogue.md) had to unpick. The uncovered set below is the queue; ids are allocated as
concepts are actually specified.

## What replacing ReSharper requires

### Must be covered first — the inspections whose absence is silent and costly

Ordered by what a user loses on the day Rider is switched off, not by fire count:

1. ⚠ **The 15 Tier D arrangement options.** These are not rules and they are the sharpest regression:
   `this.` qualification, redundant parentheses, trailing commas, attribute placement, trailing
   whitespace. `skala format` is the tool's headline feature and it silently does not perform these.
   **This is a formatter gap and it blocks replacement more directly than any rule does.**
2. **The 3 `error`-severity uncovered inspections.** The author configured these as errors; after
   replacement nothing reports them.
3. **The 322 `warning`-severity uncovered inspections**, ranked by fire count below.
4. **The `CodeRedundancy`/`DeclarationRedundancy` families (141)** — cheap, mechanical, high volume,
   and the bulk of what "code cleanup" means to a Rider user.

### What Skala should deliberately never cover

| | Why |
|---|---|
| The 65 Unity/Burst inspections | Engine-specific. Skala targets general C# |
| Godot, ShaderLab, MEF, WinForms `STAThread`, WPF `ConstructorArgument` | Framework-specific; the audience is a plugin, not the core |
| ReSharper's nullability-inference machinery (`Annotate*`) | It configures ReSharper. It is not a finding about the code, and it is `DO_NOT_SHOW` by construction |
| The 2 `CompilerWarnings` inspections and all `CS####` | ADR: surfaced, never reimplemented |
| The 44 `Hosted` concepts | ADR-008. Hosting `CA*`/`IDE*` is the design, and rebuilding them is the anti-goal |
| Sonar's `S6640` "unsafe code blocks should not be used" and kin | Policy choices dressed as defects. Vixen is a game engine and `unsafe` is correct there |
| Inter-procedural taint | [08](08-rule-catalogue.md) already scopes this out for v1, and the sample confirms it is where Sonar's real advantage is |

⚠ **"It fires zero times on Vixen" is not on that list**, and
[16](16-risks-and-open-questions.md) § "The reference trees are a test subject" is why. A zero is a
fact about Vixen. Where Vixen does not follow a rule, Vixen changes.

## Reproducing this

Every number above comes from a script in `Testing/parity-analysis/` rather than from reading. The inputs
are the committed `editor_config_template`, `options.json`, `rules.json` and this repository's own
`docs/plan/08`; the two external inputs are `jb inspectcode`'s issue-type dump and Sonar's published
rule metadata, both fetched by script.

⚠ **The classification's soft edges, stated so that a re-run is compared fairly:** the `Hosted` and
`Catalogued` maps are hand-built and incomplete, which biases `Uncovered` upwards; the VB.NET
inspections whose keys carry no language prefix are excluded by matching VB-only syntax in their
descriptions, which caught 8 and may have missed one or two; and the firing measurement is scoped to
part of Vixen rather than all of it, for the reason given above.

⚠ **A re-run of the committed pipeline does not reproduce the numbers this document first
published, and the reason is worth carrying forward.** The published `Uncovered` 580 came from a run
with `types.json` present — an uncommitted metadata cache from an older `jb`. Without it the same
scripts returned **586**, because `classify.py` looked its maps up by inspection id and 81 of the 888
rows have none. The lookup now matches on the export key as well, so the maps no longer depend on
whether the joining dump happens to know the inspection, and the measured figure is **578**. See
§ "The classification" for the full accounting.

**A re-run is therefore expected to print `Uncovered 578` and nothing else.** If it prints anything
else, the difference is a real change in the inputs — a newer export, a newer dump, an edit to the
maps — and not the instrument drifting. That is what the fix bought: the number is now a function of
the committed inputs alone.

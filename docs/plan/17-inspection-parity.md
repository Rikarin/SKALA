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

⚠ **`Hosted` and `Catalogued` are hand-built maps and are therefore lower bounds.** 126 inspections
are mapped to 49 distinct `SK` ids and 44 to Roslyn analyzers; both maps were written by reading, not
generated, so each will be missing entries. **Every entry missing from them inflates `Uncovered`.**
The uncovered count below is an *upper* bound on the gap, and the honest reading of it is "at most
this many", not "exactly this many".

## The classification

⚠ **The universe is 888, not 853.** An earlier count of 853 C#-relevant inspections is close but was
not reproducible; excluding keys whose prefix names another language (`cpp_`, `vb_`, `xaml_`, `js_`,
`asp_`, …) and the Unity/Burst set leaves **888**, and the `none` count of **92** matches the earlier
figure exactly, which is the strongest evidence the two are the same universe. The 65 Unity/Burst
inspections are counted separately: they are C#, and they are not code Skala is aimed at.

| Bucket | Count | of 888 | `error` | `warning` | `suggestion` | `hint` | `none` |
|---|---:|---:|---:|---:|---:|---:|---:|
| **Uncovered** | **658** | **74.1 %** | 12 | 380 | 174 | 64 | 28 |
| Catalogued | 89 | 10.0 % | 0 | 38 | 31 | 12 | 8 |
| Option | 67 | 7.5 % | 0 | 1 | 3 | 15 | 48 |
| Hosted | 44 | 5.0 % | 0 | 14 | 18 | 10 | 2 |
| Out of scope | 28 | 3.2 % | 1 | 16 | 2 | 3 | 6 |
| Compiler | 2 | 0.2 % | 1 | 1 | 0 | 0 | 0 |
| **Total** | **888** | | 14 | 450 | 228 | 104 | 92 |

A further 65 Unity/Burst inspections are out of scope for the engine rather than for the language.

### ⚠ The `Option` bucket is the one this document most expects to be argued with, and it is smaller than it looks

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

<!-- FIRE COUNTS -->

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

- ReSharper uncovered: **≤ 658** inspection ids, **≤ 587** distinct concepts after collapsing
  variants (below).
- Sonar uncovered: **≈ 256**, 95 % CI 195–317.
- Of the 60-rule Sonar sample, 13 were already catalogued by an `SK` id and several more had a
  ReSharper counterpart in the uncovered set — so a substantial share of Sonar's 256 is *the same
  problem* as one of ReSharper's 658, not an additional one.

A defensible union of **distinct uncovered problems is 700–900**, and the honest thing to say about
that range is that its width is the finding. It is not 1 369, and it is not 111.

## ⚠ Inspection ids are not concepts, and a catalogue is sized in concepts

ReSharper splits one idea across many ids. `ReplaceWithOfType*` is roughly twenty ids;
`StringCompareIsCultureSpecific.1` through `.6` is six; a `.Global`/`.Local` pair doubles a further
set for nothing but accessibility. A rule catalogue that allocated one `SK` id per ReSharper id would
be inflated before anyone argued about its contents.

Collapsing numbered variants, `.Global`/`.Local` pairs and long shared prefixes:

| | |
|---|---:|
| Uncovered inspection ids | 658 |
| after collapsing numbered and `.Global`/`.Local` variants | 602 |
| after also merging long shared prefixes | **587** |

⚠ **The collapse is real but modest — 11 %.** It was worth measuring precisely because it is the
obvious way to argue the gap down, and it does not carry that argument. The `ReplaceWithOfType`
family is vivid and it is not representative; most of the 658 are genuinely distinct ideas.

## The recommended catalogue target

⚠ **The honest answer is "far more than 111", and the reasoning matters more than the number.**

[08](08-rule-catalogue.md) names 109 rules. The measurement says the reachable surface, after every
deduction this document could justify — the compiler's, Roslyn's, the option registry's, other
languages', other engines' — is **587 uncovered ReSharper concepts plus roughly 256 Sonar rules, of
which a large but unmeasured share are the same problems**. Even at the pessimistic end of the
overlap, the catalogue is naming **under a fifth of what it would take to replace the two tools.**

**A target of 450–550 concepts** is what the evidence supports for full parity. But the number that
should govern planning is not that one, and this is the part [08](08-rule-catalogue.md)'s next
revision should take:

1. ⚠ **The catalogue's size was never the constraint; the shipping bar is.** Doc 08 ships 29 of 109
   at 26.9 % because a rule ships only with a fix, zero false positives on two reference trees, and a
   negative fixture set at least as large as the positive one. **Multiplying the catalogue by five
   without changing that bar produces a longer list of outstanding work, not more coverage.**
   [16](16-risks-and-open-questions.md) § R3 already names "a hundred that are usually right" as the
   failure mode.
2. **So the target is a statement about scope, not a commitment about dates.** The value of knowing
   it is 587 rather than 111 is that it converts "we are 27 % done" into "we are about 5 % done", and
   the second number is the one that should inform whether ReSharper can be retired and when.
3. ⚠ **A large share of the 658 is cheap.** The `CodeRedundancy` (103) and `DeclarationRedundancy`
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
2. **The 12 `error`-severity uncovered inspections.** The author configured these as errors; after
   replacement nothing reports them.
3. **The 380 `warning`-severity uncovered inspections**, ranked by fire count below.
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

Every number above comes from a script in the analysis harness rather than from reading. The inputs
are the committed `editor_config_template`, `options.json`, `rules.json` and this repository's own
`docs/plan/08`; the two external inputs are `jb inspectcode`'s issue-type dump and Sonar's published
rule metadata, both fetched by script.

⚠ **The classification's soft edges, stated so that a re-run is compared fairly:** the `Hosted` and
`Catalogued` maps are hand-built and incomplete, which biases `Uncovered` upwards; the VB.NET
inspections whose keys carry no language prefix are excluded by matching VB-only syntax in their
descriptions, which caught 8 and may have missed one or two; and the firing measurement is scoped to
part of Vixen rather than all of it, for the reason given above.

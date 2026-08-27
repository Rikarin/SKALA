# The oracle's cleanup profile, swept

M4's first act is a second `jb cleanupcode` profile. There is no published list of the cleanup task
names a `.DotSettings` profile may contain, and an unknown one is **silently ignored** rather than
rejected — so a profile that looks like it enables ten rewrites can be enabling three, and nothing
says so. This is the sweep that established the profile in `Testing/Rikarin.Skala.Testing/OracleProfile.cs`,
kept for the same reason `docs/sk-div-0005-margin-sweep.md` is kept: the next person to doubt the
profile should be able to re-run it rather than re-derive it.

Tool: `jb cleanupcode` 2025.2.6, macOS ARM64, against a scratch project carrying a copy of the
repository's `.editorconfig`.

## Method

A profile is a `.DotSettings` document whose single string value is itself an XML document:

```xml
<s:String x:Key="/Default/CodeStyle/CodeCleanup/Profiles/=Probe/@EntryIndexedValue">
  &lt;Profile name="Probe"&gt;…tasks…&lt;/Profile&gt;
</s:String>
```

Each task is enabled one at a time over a probe file that is already in the oracle's *format-only*
output, so that every observed change is arrangement and none of it is whitespace. "no-change" means
either "the task did nothing here" or "that is not a task name" — the two are indistinguishable from
the outside, which is the whole hazard.

## What is a task, and what is an attribute

Two shapes, and they are not interchangeable:

| Shape | Example |
|---|---|
| Element, C#-specific | `<CSOptimizeUsings><OptimizeUsings>True</OptimizeUsings>…</CSOptimizeUsings>` |
| Attribute of `CSCodeStyleAttributes` | `<CSCodeStyleAttributes ArrangeVarStyle="True" />` |

`<CSCodeStyleAttributes />` with no attributes changes nothing, and an element name that does not
exist (`<ZZNotARealDescriptor ArrangeVarStyle="True" />`) changes nothing either — so the container
is real, opt-in, and the per-attribute probe below is meaningful.

The authoritative name list is not documented, but it is recoverable: the tool's resource strings are
named `CodeCleanupTask_<Name>`, and

```
strings -a $JB/*.dll | grep -oE "CodeCleanupTask_[A-Za-z]+" | sort -u
```

enumerates every task the installed build knows. ⚠ Appearing in that list is **necessary and not
sufficient** — see `ArrangeNullCheckingPattern` below.

## Results

Probed as elements:

| Task | Effect |
|---|---|
| `CSReformatCode` | whitespace (the format-only profile) |
| `CSUpdateFileHeader` | off, deliberately — the corpus has no file headers to update |
| `CSOptimizeUsings` | ✅ sorts and removes usings; needs its nested `<OptimizeUsings>True</OptimizeUsings>`, **not** `True` as element content |
| `CSArrangeQualifiers` | ✅ removes `this.`, adds the configured static qualifier |
| `CSFixBuiltinTypeReferences` | ✅ `String` → `string`, `String.Empty` → `string.Empty` |
| `CSReorderTypeMembers` | ✅ works — excluded on purpose, see below |
| `CSUseAutoProperty`, `CSMakeFieldReadonly` | ✅ work — out of doc 06's catalogue |
| `CSArrangeThisQualifier`, `CSShortenReferences`, `CSRemoveCodeRedundancies`, `CSSortModifiers`, `CSArrangeTrailingCommas`, `CSArrangeCodeBodyStyle`, `CSUseVar` | ✗ not task names in this build (silently ignored) |

Probed as `CSCodeStyleAttributes` attributes:

| Attribute | Effect |
|---|---|
| `ArrangeVarStyle` | ✅ `List<int> a = new List<int>()` → `var a = new List<int>()` |
| `ArrangeCodeBodyStyle` | ✅ block body → expression body |
| `ArrangeObjectCreation` | ✅ `Plain GetPlain() { return new Plain(); }` → `=> new()` |
| `ArrangeDefaultValue` | ✅ (subject to `var` winning first — see below) |
| `SortModifiers` | ✅ modifier order, and drops a redundant `private` |
| `ArrangeTypeAccessModifier`, `ArrangeTypeMemberAccessModifier` | ✅ |
| `RemoveRedundantParentheses` | ✅ `a + (b * c)` → `a + b * c` |
| `ArrangeTrailingCommas`, `ArrangeAttributes` | ✅ (no-ops on the probe, real tasks per the resource list) |
| `ArrangeNullCheckingPattern` | ✗ **no effect, in any shape** |
| `ArrangeStringLiteral`, `ArrangeEmptyString` | ✗ not attribute names |
| `ArrangeBraces`, `ArrangeRedundantBraces` | ✗ no effect |

## ⚠ Two tasks the first sweep missed

⚠ **The sweep above is a list of what was probed, and its silence is not evidence.** Two real
`CSCodeStyleAttributes` attributes were never tried, so the profile ran without them and the oracle
looked like a tool that declines two of the export's settings:

| Attribute | Effect |
|---|---|
| `ArrangeNamespaces` | ✅ `namespace N { … }` ⇒ `namespace N;`, under `csharp_style_namespace_declarations = file_scoped` |
| `ArrangeArgumentsStyle` | ✅ strips a redundant argument name, under the four `resharper_arguments_*` keys at `positional` — and adds one when a key says `named` |

Both are in the `CodeCleanupTask_` resource list that this document already says is recoverable, and
neither is exotic. They were missed because the probe set was built from doc 06's catalogue rather
than from the tool's own name list, so a setting doc 06 did not discuss had no probe written for it.

⚠ **The consequence was a measurement that could not come out any other way.** With those two absent,
`ArrangeNamespaceBody` and the four `ArgumentsStyle*` inspections read as "the oracle does not do
this" — and [17](plan/17-inspection-parity.md) recorded all five among fifteen arrangement options
that Skala declares and does not perform. Five of that fifteen were unmeasurable rather than
unimplemented, which is the same failure mode as an unknown task name being silently ignored, one
level up.

**The lesson the profile now carries:** probe from `strings -a $JB/*.dll | grep -oE
"CodeCleanupTask_[A-Za-z]+"`, not from the catalogue you expect to find.

## ⚠ Three rewrites doc 06 asks for that the oracle will not perform

This is the finding, and it is the M4 analogue of SK-DIV-0005: swept, not found, recorded.

1. **`!= null` → `is not null`.** `resharper_null_checking_pattern_style = not_null_pattern` is set in
   the export. `ArrangeNullCheckingPattern` is a real `CodeCleanupTask_` name. Neither as an element
   nor as a `CSCodeStyleAttributes` attribute does it rewrite `if (p != null)`, on nullable or
   non-nullable operands, with `resharper_arrange_null_checking_pattern_highlighting` left at its
   exported `hint` **or raised to `warning`**.
2. **`string.Empty` → `""`.** `resharper_empty_string = empty_literal` is set. Cleanup produces
   `string.Empty` (via `CSFixBuiltinTypeReferences` normalising `String.Empty`) and stops there. The
   export's own `resharper_arrange_empty_string_highlighting = none` is consistent with this.
3. **Redundant nested braces `{ { x; } }`.** `resharper_braces_redundant = true` is set; no task
   removes them.

The reading these three support: `null_checking_pattern_style` and `empty_string` govern the pattern
ReSharper **generates** — in a quick-fix, a generated `Equals`, a "check parameter for null" action —
rather than a cleanup rewrite of code that already exists. They are code-*generation* settings that
happen to live in the same file as the cleanup settings.

Consequence for M4: Skala implements all three (doc 06 requires the first, explicitly including its
`operator ==` divergence), and they are pinned by **hand-written fixtures** rather than by the
oracle, because on these three the oracle has no opinion to disagree with. They are excluded from the
changed-span agreement number for the same reason — measuring Skala against an oracle that never
moves would score every correct rewrite as a divergence. See `SK-DIV-0013` in `docs/divergences.md`.

## ⚠ Why `CSReorderTypeMembers` is not in the profile

It works, and it is the one task the roadmap named that the export does not configure. Member
ordering is driven by a *file layout* XML (`/Default/CodeStyle/CSharpFileLayoutPatterns/…`) which the
author's Rider export does not ship, so enabling the task measures ReSharper's built-in default
layout rather than this repository's configuration — and it rewrites nearly every file wholesale,
which would swamp every other class in the differential.

Its cost is measured rather than asserted: `dotnet run --project Testing/Rikarin.Skala.Testing --
arrangement --reorder` reports the same corpus under a profile that adds it. See the M4 numbers in
`docs/plan/15-roadmap.md`.

## Reproducing

The probe scripts are not committed (they are three lines of `bash` around the settings document
above). The profile itself is committed, in `OracleProfile.Cleanup`, and

```
./build.sh Oracle          # regenerates both profiles' fixtures
dotnet run --project Testing/Rikarin.Skala.Testing -- arrangement
```

is the loop that re-checks it.

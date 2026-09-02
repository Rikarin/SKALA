# 03 — Configuration Model

This is the first thing to build, because everything else is a consumer of it, and because the
input is a 4 238-line machine-generated file whose contents nobody has ever read end to end.

## What the Rider export actually contains

`editor_config_template`, exported from Rider's **Settings → Editor → Code Style → Export to
.editorconfig**, measured:

| Group | Count | Notes |
|---|---|---|
| Total assignments | 4 226 | across 3 sections |
| `resharper_*` non-severity (formatting + arrangement) | 648 | ≈ 380 apply to C#. ⚠ A count of the *export*; the registry has since grown past it to 520 — see below |
| `resharper_*_highlighting` (inspection severities) | 3 021 | 853 apply to C# |
| `dotnet_diagnostic.*.severity` | 253 | Roslyn/compiler IDs, incl. `bc*` (VB) and `cs*` |
| `dotnet_naming_*` | 215 | 20 rules, 6 styles, 20 symbol groups |
| `csharp_*` / `dotnet_style_*` (Microsoft) | 40 | overlapping the `resharper_*` set |
| Sections | 3 | `[*]`, `[*.csv]`, and one glob of 47 extensions |

Roughly 90 % of it is Rider writing out its own defaults. Of the 853 C#-relevant inspection
severities, 424 are `warning`, 225 `suggestion`, 98 `hint`, 92 `none` and 14 `error` — and almost
all of those are ReSharper's shipped default for that inspection, not a choice.

⚠ M5 recounted the last row by stripping the language-prefixed families (`resharper_cpp_` 1 864,
`resharper_vb_` 76, `resharper_unity_` 65, `resharper_xaml_` 56, and seven smaller ones) and gets
**912** rather than 853, at 462 `warning` / 232 `suggestion` / 110 `hint` / 93 `none` / 15 `error`.
The shape is the same and the difference is where the JS/TS and general-purpose inspections are
counted; nothing downstream depends on which number is used, and the reason both are recorded is
that § "Severities" below now turns on what those values actually are.

### Four things about that file that will bite

1. ⚠ **There is no `root = true`.** An `.editorconfig` in a parent directory — a home directory, a
   mono-repo root — still applies, and Rider's export silently inherits it. Skala emits `SK9002`
   (info) when the effective config for a file draws from a file above the repository root, naming
   which keys came from where.
2. ⚠ **The standard `max_line_length` key is absent**; the width lives only in
   `resharper_csharp_max_line_length = 120`. Every other tool in the ecosystem (CSharpier, most
   editors, `git diff --stat` heuristics) therefore does not know the width. `skala config fix`
   offers to add `max_line_length = 120` alongside it, and Skala reads the ReSharper key as
   authoritative when both exist and disagree — with `SK9005` telling you they disagree.
3. ⚠ **`[*]` sets `insert_final_newline = false` and `trim_trailing_whitespace = false`, while
   `resharper_csharp_insert_final_newline = true` and `resharper_remove_spaces_on_blank_lines = true`.**
   These are direct contradictions between the generic keys and the C# ones. ReSharper resolves them
   by language specificity — the C# key wins for `.cs`. Skala must do the same, and must say so
   (`SK9005`, one report per run, not per file), because a reader of that file will otherwise
   reasonably conclude Skala is ignoring `insert_final_newline`.
4. **The 47-extension glob at the bottom is Rider's "these are code files" list**, and it sets only
   indentation. It includes `.cs`, and it is *last*, so under editorconfig precedence it overrides
   `[*]` for indentation. Skala uses Roslyn's `AnalyzerConfigSet` so this is handled by the same code
   that handles it for the compiler, rather than by a hand-rolled matcher that gets `{a,b}` groups
   subtly wrong.

### The style this config actually describes

Worth stating plainly, because it is the target of every conformance test:

```
120 columns · 4 spaces · LF · braces on the same line (`csharp_new_line_before_open_brace = none`)
`else`/`catch`/`finally` on the closing brace line · file-scoped namespaces · usings outside the
namespace, sorted, System not first · `var` everywhere · target-typed `new` everywhere ·
`default` literal · expression bodies for methods, accessors and local functions; block bodies for
constructors · `is not null` over `!= null` · no trailing commas · no alignment of anything
(`int_align = false`, every `align_multiline_* = false` that matters) · continuous indent, single ·
at most 2 consecutive blank lines · exactly 1 blank line around types, fields, properties and
methods, 0 around single-line members · wrapping is `chop_if_long` for arguments, parameters, base
lists, chained calls, ternaries; `chop_always` for switch expressions; `wrap_if_long` for array
initializers and constructor initializers · at most 4 initializer elements on a line · xmldoc wraps
at 120
```

The absence of alignment is a gift: `int_align_*` and the `align_multiline_*` family being off means
the fitting engine never needs to measure a *previous* line to lay out the current one, which is the
feature that makes ReSharper's formatter quadratic in the worst case. ✅ Verified against the export.

## The option registry

⚠ **520 options** cannot be maintained as hand-written C# properties, hand-written parsers,
hand-written docs and hand-written tests. (This said 380, which was the M0 estimate of the C#
formatting keys in the export. M3 extended the registry past them — it is 462 `csharp`, 32 `xmldoc`
and 26 language-agnostic today — and the estimate was never revised.) There is exactly one source of
truth:

`Core/Rikarin.Skala.Options/options.json` — one entry per option:

```jsonc
{
  "key": "resharper_csharp_wrap_arguments_style",
  "aliases": ["resharper_wrap_arguments_style"],   // ReSharper's language-generic spelling
  "language": "csharp",
  "type": "enum:WrapStyle",                        // wrap_if_long | chop_if_long | chop_always
  "default": "wrap_if_long",                       // ReSharper's default, not ours — and since M3
                                                   // `defaultSource` says whether that is a claim
                                                   // or a measurement
  "tier": "A",
  "construct": "ArgumentList",
  "summary": "How an argument list that does not fit on one line is broken.",
  "since": "0.1",
  "oracle": "constructs/wrapping/arguments/*.cs"   // which corpus files pin this option
}
```

⚠ **`type` is the validation, and for 83 of the 520 entries it was not validating anything.** Until
M9, 27 entries were `"type": "string"` — which means *every* string is a legal value — and 56 were
`"type": "int"` with nothing behind them but `int.TryParse`. `resharper_align_ternary = sideways`,
`csharp_using_directive_placement = nowhere`, `resharper_csharp_max_line_length = -1` and
`resharper_csharp_indent_size = 0` were all accepted, discarded, and replaced by a default in
silence. Four fields close that, and each is enforced at build time by the generator (`SKG004`,
`SKG005`) rather than by review:

| Field | On | Meaning |
|---|---|---|
| `min` / `max` | `int` | The inclusive bounds. ⚠ Read with the consumer's clamping — `max_line_length = 0` and `max_*_on_line = 0` are values the formatter deliberately supports, so their floor is 0 and not 1. |
| `boundsBecause` | any bounded entry | Why that bound is knowable. Required whenever `min` or `max` is set; a bound with no reason is the same guess the missing bound was. |
| `freeFormBecause` | `string` | Why the option has no closed domain. Required on every `string` entry and forbidden elsewhere. **This is the field that makes "it's a string" reviewable**, and the ten entries that keep it name what JetBrains would have to publish to close them. |
| `tabMeans` | `indent_size` only | The key whose value the literal `tab` stands for. The EditorConfig specification defines the alias there and nowhere else. |

`OptionValueValidationTests` then sweeps the registry itself: every value in every declared domain is
accepted, every option with a closed domain refuses a value outside it with exactly one `SK9017`, and
the free-form set is asserted against a hard-coded list — so a new `string` entry fails the suite
until somebody writes down why.

From it, `Rikarin.Skala.Options.Generator` (an incremental source generator, so it participates in
the build rather than being a checked-in codegen step) emits:

- `FormattingOptions` — a readonly struct-of-arrays keyed by a generated `OptionId` enum, so that
  reading an option in the fitting hot loop is an array index, not a dictionary lookup. ✅ This
  matters: the fitting pass reads options millions of times over the corpus.
- The parser: key → `OptionId`, value → typed value, including ReSharper's value aliases
  (`true`/`always`, `false`/`never`).
- `OptionId`, `OptionEnums` (the thirty value enums and their `TryParse`), and `OptionRegistry` —
  the table, the alias index and `TryResolve`.

⚠ **This list named five outputs and the generator emits four.** The three below were described as
built and are not; they are kept here as the record of what was intended, because deleting them
would hide a gap rather than close it.

- ❌ `docs/options/*.md`, one page per option, and the tier matrix. **No such directory exists and
  the generator emits no Markdown.** What exists is `docs/site/options/` — 108 HTML pages written at
  *runtime* by `skala docs site`, not at build time by the generator. The tier matrix is published
  there.
- ❌ The `.editorconfig` completion list used by the LSP. **Nothing consumes `OptionRegistry.Spellings`**;
  there is no completion handler anywhere in the LSP.
- ❌ A test stub per option asserting that the option is exercised by at least one corpus file.
  **Nothing is generated.** The role is played by two hand-written suites — `OptionCoverageTests`
  (Tier A ⇔ what the formatter actually reads, both directions) and `OptionRegistryTests.Tiers_AreHonest`
  (a Tier A/B claim must carry an oracle fixture glob). ⚠ They are scoped to the options the
  formatter reads, not to all 520, so "an option with no test is a build failure" is false for the
  293 Tier D entries — which is most of the registry. M9 narrowed that to *observability*: every one
  of the 520 is now swept for **value validation** by `OptionValueValidationTests`, which is cheap
  because it resolves and never formats. What a Tier D option still has no test for is whether
  anything reads it.

⚠ Adding support for an option is therefore: add the JSON entry, add the corpus file, implement it,
and add it to `PhaseOneOptions.Implemented` or `ArrangementOptions.Implemented`. **Forgetting any of
them fails a test; none of them fails to compile.** There is no `[MustHandle]` attribute anywhere in
the tree — the sentence that used to stand here described an exhaustiveness check that was never
written. The generator's real build-failure diagnostics are `SKG001`–`SKG003` and `SK9004`, and they
are about the registry file rather than about implementations.

## Option tiers

Every option in `options.json` has a tier, and `skala config explain` prints them for the current
file. This is non-negotiable #4 from [00](00-vision-and-principles.md) made concrete.

| Tier | Meaning | Members | Behaviour |
|---|---|---:|---|
| **A — implemented** | Skala reproduces Rider's behaviour, pinned by at least one oracle fixture | 221 | Applied |
| **B — approximated** | Implemented, with a documented divergence in stated edge cases | ⚠ **0** | Applied; `skala config explain` shows the divergence text |
| **C — accepted, ignored** | Parsed, validated, and deliberately not implemented | 6 | Ignored |
| **D — not implemented** | ⚠ **Known to the registry and not implemented yet** | 293 | Ignored; reported by `skala config check` |

⚠ **Tier D was defined backwards here for four milestones.** It said "not in `options.json` at all",
which is the exact opposite of what the code means by it: a Tier D option *is* in the registry — 293
of the 520 entries — and is simply unimplemented. A key genuinely absent from the registry has **no
tier**; it is `SK9001`, with a "did you mean" over the registry, and that diagnostic is what the old
row's behaviour column was describing. The two are not the same thing and conflating them made the
tier the document's own progress bar was counted against meaningless.

⚠ `SK9001` must be *info*, not warning, by default. A real export carries thousands of keys Skala
will never own, and a tool that emits thousands of warnings on first run gets uninstalled on first
run. (`skala config check` reports 263 such keys against this repository's own export, alongside
3 021 inspection severities, 253 diagnostic severities and 215 naming rules, which belong to other
engines entirely.)

⚠ **Tier B is live machinery with no members, and that is the outcome rather than a gap.** Every
option that would have been B is Tier D plus an `SK-DIV` entry in `docs/divergences.md`: recording a
divergence in a document beats recording it in a tier, because the document can say what the
divergence *is*. Two tests hold the tier empty and honest. The four-tier model is a three-tier model
in practice and the row stays so that the reasoning is not lost.

⚠ **Tier C is six keys, and none of the families this document used to name is among them.** It
listed "C++ keys, VB keys, XAML keys" — there is not one `resharper_cpp_*`, `*_vb_*` or `xaml` entry
in the registry, so those are not "parsed, validated and deliberately not implemented"; they are
`SK9001` unknown keys, which is Tier-none. The six are `resharper_csharp_old_engine` and
`resharper_use_old_engine`, the two autodetect toggles, plus `resharper_use_indent_from_vs` and
`resharper_show_autodetect_configure_formatting_tip` — the last two never named here, and a third of
the tier.

⚠ **All six now carry a measurement, and it is recorded in `unsweptBecause` rather than in `inert`.**
The distinction is the point and the registry enforces it: `inert` is Tier D by construction —
`OptionRegistryTests.Inert_OptionsCarryAReasonAndAreNotClaimedAsImplemented` asserts the tier — so a
Tier C key that was *also* measured unobservable had nowhere to put that fact and sat with an empty
entry instead. Recording it in a second field keeps the refusal and the measurement apart, which
matters because they disagree about one of the six: five of them are flat in `jb cleanupcode` 2025.2.6
at every value, and **`resharper_csharp_old_engine` is not** — at `true` the oracle rewrites the whole
probe, outdenting a file-scoped namespace's members and moving the wrap points. Tier C was still the
right answer for it. **C means Skala declines; it has never meant nothing would happen.** Collapsing
the two would have turned a deliberate refusal into a claim that the option does not exist, which is
the one reading Tier C exists to rule out. See `docs/tier-d-split.md` § "Measured, 2026-08-31".

**The tier matrix is published** — into `docs/site/options/` by `skala docs site`, not into
`docs/options/` by the generator. The headline number is **Tier A: 259 of 520**.

⚠ This paragraph said **221** until it was checked against the artefact it describes; the generated
`docs/site/index.html` said 246 at the same moment, and 259 after the documentation-comment family
was measured. A number in prose beside a number in a generated file is a number that will drift, and
this one had. The site is the one to read.

⚠ **The number that matters is not that one.** 259 of 520 is a fact about the registry; what a user
needs is *of the keys I set, how many are honoured*, and on this repository's own export that is
**205 applied of 458 set**, with 243 not implemented and 10 inert. `skala config check` reports the
per-configuration split first and the registry-wide totals after, for that reason.

⚠ **And option coverage is a precondition for replacing ReSharper, not polish.** Today an ignored
key is still honoured by Rider in the editor, so its cost is invisible — which is why a 99.7 %
fidelity number and 243 ignored keys coexist without contradiction. After replacement
([01](01-technology-decisions.md) § ADR-001) nobody honours it, and changing that setting in Rider
does nothing, silently, for ever.

## Precedence

Resolution order for a given file, first match wins within a key:

1. Command line (`--option key=value`, repeatable) — for debugging and for the conformance harness.
   Never used in normal operation, and `skala check` records in the SARIF that an override was
   active, so a passing run with overrides can't be mistaken for a clean one.
2. `.editorconfig` chain, resolved by Roslyn's `AnalyzerConfigSet`: nearest file first, later
   sections within a file winning over earlier ones, `root = true` stopping the walk.
3. **Language specialisation within a level**: `resharper_csharp_x` beats `resharper_x` beats the
   Microsoft equivalent (`csharp_x`) beats the generic editorconfig key. This is ReSharper's own
   order and the reason for hazard 3 above.
4. `options.json` default — which is **ReSharper's default**, not a Skala opinion. A key absent from
   the config must produce what Rider produces with that key absent. ⚠ M3 makes that true for **123**
   keys by deriving the value from the oracle rather than taking the export's; see "Deriving
   ReSharper's defaults" below for what the other 397 still record and why. (123 + 397 = 520. This
   line said 126 and § "Deriving ReSharper's defaults" implied 127; both were wrong, and neither
   reconciled with the other or with the registry.)

`skala.jsonc` never participates. It cannot set a style option; attempting to is `SK9003` (error).

## Severities

Three namespaces arrive in the same file and all three must work:

| Form | Example | Applies to |
|---|---|---|
| `dotnet_diagnostic.<id>.severity` | `dotnet_diagnostic.CA2252.severity = error` | Any Roslyn analyzer, incl. Skala's `SK####` and hosted third-party rules |
| `resharper_<inspection>_highlighting` | `resharper_convert_to_primary_constructor_highlighting = suggestion` | ⚠ **Nothing.** Parsed, classified as an inspection severity so `config check` stays quiet, and then ignored |
| `<style_key>:<severity>` | `csharp_style_namespace_declarations = file_scoped:suggestion` | Microsoft style keys with an inline severity |

The middle row was written as a headline feature: **the 853 C#-relevant `resharper_*_highlighting`
keys already in the export configure Skala's rules**, via a mapping table in `rules.json`
(`resharperId: "ConvertToPrimaryConstructor"`), so that the author's existing Rider severity applies
with no new configuration and adoption is a copy rather than a project.

⚠ **Milestone 5 measured it against the real export and it did not hold as stated; the reduced
version that shipped has since been removed outright.** The section below is kept because the four
measurements are still the argument, and because someone will propose the feature again.

⚠ **Only the severity axis went.** Reading a Rider export for formatting and arrangement **options**
is Skala's core premise and is untouched: `options.json`, `config check`/`explain`/`distill`/`diff`/
`fix`/`sync`/`canonical`, and every `resharper_*` key that is not `_highlighting` are exactly as they
were. `OptionResolver.Classify` still buckets any `_highlighting` key as `InspectionSeverity`
precisely so that an export's three thousand inspection severities do not produce three thousand
`SK9001`s.

### Q5, resolved twice: recorded as a choice, then removed

⚠ **The mapping and the bridge are both gone.** `resharperId` is no longer a field in `rules.json`,
`--resharper-severities` and `"analysis": { "resharperSeverities": true }` no longer exist, and
`dotnet_diagnostic.SK….severity` is the only way to set a Skala rule's severity.

**Why, beyond the four measurements below: one field could not describe the relationship.** A rule
declared exactly one `resharperId`, while `Testing/parity-analysis/catalogued.json` maps **295
inspections onto 162 rules, 49 of which cover more than one** — `SK4010` covers eleven. So
`resharper_<x>_highlighting = none` either switched off a rule covering ten other concepts, or was
inert for the other ten. It could not mean what a reader would expect it to mean, and measurement 1
below is that same fact discovered from the other end. Skala is meant to **replace** ReSharper rather
than keep speaking its configuration vocabulary, and nothing consumes Skala yet, so no migration path
was built and none is wanted.

The four measurements, kept because they are the standing argument:

All four are on the export in this repository:

**1. The correspondence is many-to-many in both directions, so there is no derivation.** For
`SK1010` — `x != null` becoming `x is not null` — the export carries at least six inspections over
the same ground, at four different severities:

```ini
resharper_merge_into_pattern_highlighting                    = suggestion
resharper_join_null_check_with_usage_highlighting            = suggestion
resharper_convert_type_check_to_null_check_highlighting      = warning
resharper_convert_type_check_pattern_to_null_check_highlighting = warning
resharper_arrange_null_checking_pattern_highlighting         = hint
resharper_use_null_propagation_highlighting                  = hint
```

Nothing computes which of those governs `SK1010`. In the other direction, `SK1034` covered what
`use_collection_count_property`, `replace_with_single_call_to_any` and `replace_with_single_call_to_count`
split into three (⚠ `SK1034` is retired — #281 — but the many-to-one shape it illustrates is
unchanged and several live rules have it). The mapping was therefore **recorded in `rules.json` as a
choice**, one key per rule, with a `resharperNote` saying which alternatives were passed over and
why. ⚠ **That "function from Skala rule to at most one key" is exactly what was wrong with it**: a
one-key field cannot describe a rule that answers eleven inspections, so the recorded choice silently
spoke for ten it did not name. `resharperNote` survives the removal and still says which alternatives
were passed over; the machine-readable half does not.

**2. It is partial, and the gap is not exotic.** `SK1005` (file-scoped namespace) has **no**
ReSharper inspection id. Rider drives that conversion from the *Microsoft* key
`csharp_style_namespace_declarations = file_scoped:suggestion` and reports the result under
`resharper_arrange_namespace_body_highlighting = hint`. One concept, two mechanisms, two severities,
and neither of them is an inspection Skala can name. `resharperId` for that rule was `null`; the
field no longer exists at all.

**3. ⚠ A derived key that looks right and does not exist is worse than no mapping.**
`ConvertToFileScopedNamespace` and `ConvertToThrowIfNull` both snake-case into plausible keys and
JetBrains emits neither. A mapping to a key nothing sets never applies, looks like a feature and
behaves like a comment. `RuleCatalogTests.EveryDeclaredReSharperKey_ExistsInTheExport` read the real
export and failed the build for it; it went with the field, because with no declared keys left there
is nothing for it to check.

**4. ⚠ Reading the keys as authoritative would switch a rule off in the repository the tool was
built for.** The export sets

```ini
resharper_use_throw_if_null_method_highlighting = none
```

so `SK1020` — the `ArgumentNullException.ThrowIfNull` rule — would have been silently disabled by a
value nobody chose for it. ⚠ `SK1020` is retired (#281), so the one worked example no longer fires
and nobody has re-measured which *live* rule this export would silence; the argument does not rest
on the example. That is the decisive measurement: the 912 `resharper_*_highlighting` values in
an export (462 `warning`, 232 `suggestion`, 110 `hint`, 93 `none`, 15 `error`) were chosen for
ReSharper's inspections, and a value that has never been looked at is not consent.

**The first resolution** was to ship the mechanism opt-in, behind `skala check
--resharper-severities` or `"analysis": { "resharperSeverities": true }`, with
`dotnet_diagnostic.SK….severity` winning over it.

**The resolution that stands is that there is no mechanism.** Severity precedence is now two rows,
not three:

| | wins over | because |
|---|---|---|
| `dotnet_diagnostic.SK1010.severity` | everything | it names the Skala rule, so it cannot mean anything else |
| `rules.json` `defaultSeverity` | — | the fallback |

⚠ **The headline claim does not survive in any form**, and that is the point rather than a shortfall:
an export's severities were chosen for ReSharper's inspections, and Skala's rules are not ReSharper's
inspections. `skala explain <id>` still prints the `resharperNote` — the prose about how the concept
lines up and what was passed over — and no longer prints a key, because there is no key.

Severity ladder, and how the five ReSharper levels map:

| ReSharper | Roslyn `DiagnosticSeverity` | Fails a gate? | Shown by default? |
|---|---|---|---|
| `error` | Error | yes | yes |
| `warning` | Warning | per gate | yes |
| `suggestion` | Info | no | yes, dimmed |
| `hint` | Hidden | no | only `--include-hints` |
| `none` | suppressed | no | no |

⚠ `hint` maps to Hidden, not Info. There are 98 C#-relevant hints in the export; surfacing them in a
terminal by default would bury the 14 errors.

## `skala config` — the commands that make this reviewable

```bash
skala config explain [<path>]     # effective options for a file, with source file:line and tier
skala config diff <a> <b>         # what changes between two .editorconfig files, semantically
skala config diff --canonical     # …or between this repository and the canonical. Exit 3 on drift
skala config distill              # ← the important one
skala config fix                  # add root/max_line_length, resolve contradictions, in place
skala config check                # tier report + contradictions, exit non-zero under --strict
skala config sync [--apply]       # write the canonical block, preserve the local block below it
skala config canonical --out <d>  # maintainer: recompose the payload from a Rider export
```

**`skala config distill`** takes the 4 238-line export and writes back the subset that differs from
ReSharper's defaults, measured against `options.json`. A distilled file a human can read, review in
a diff, and reason about, that produces byte-identical formatting.

⚠ **The problem this used to have, and what it cost.** The original text said `options.json` "stores
those defaults precisely for this purpose", and M0 established that it cannot: JetBrains' EditorConfig
property tables publish each property's name, language and possible values, and **never its shipped
default** — all 22 schema pages and the 5 200-row index were checked. So every registry entry
recorded the export's own value as its default, marked `defaultSource: "template"` or `"unknown"`,
and `distill` — which may only drop a key whose default was *checked* — dropped 0 of 4 226 and said
so, at length, on stdout.

⚠ **It cost more than `distill`, and M2 measured how much.** A registry entry whose `default` is the
export's value is not merely unusable for distilling — it is what Skala *applies* to any repository
whose `.editorconfig` leaves that key unset, which is most repositories. Rider applies its own
default to the same file, and the two disagree. Over Vixen — 4 708 files, whose `.editorconfig` sets
157 keys and no `wrap_*`, `keep_*` or `place_*` key at all — that was the difference between 2 374
files changing and 1 301: **45 % of the diff on a real repository was this one gap**, and it is
invisible on `corpus/real/`, which carries the export.

## Deriving ReSharper's defaults

The document used to say the only reliable source was a one-off human action — export an
`.editorconfig` from a pristine Rider profile and diff it against the author's — and that until it
existed `distill` was honest and useless. ⚠ **There is a second source, and it is the oracle.** A
`jb cleanupcode` run under a configuration carrying nothing but `root = true` *is*
ReSharper-with-defaults, by construction. M3 derives the table from it.

The method, and the two things that have to be got right:

1. Run the oracle over the fixture corpus with `root = true` and nothing else. That is the baseline.
2. Run it again, a handful of times, with every option set to its 1st legal value, then its 2nd, and
   so on. Batching by value index is the only affordable shape: `cleanupcode`'s startup dominates and
   one run per option per value is thousands of runs.
3. For each option, compare only *its own* fixture — the one `options.json`'s `oracle` field names.
   The value whose run reproduces the baseline there is the default.

⚠ **The isolation is by directory, not by round.** The first attempt gave every fixture the same
configuration and answered nothing at all: 197 options and *zero* fixtures unchanged in round one,
because every fixture was moved by something else in the batch. One subdirectory per fixture, each
with its own `root = true` plus one key, gives the batching for free and the isolation with it — 144,
110, 17 and 2 fixtures unchanged over four rounds, and the whole probe runs in three minutes.
⚠ The command is `dotnet run --project Testing/Rikarin.Skala.Testing -- defaults`. This line said
"`fidelity defaults`", which is wrong twice over: `fidelity` and `defaults` are *sibling* verbs of
the internal harness, not a verb and its subcommand, and neither is on the `skala` CLI at all. There
is deliberately no build target for it — it takes tens of minutes and requires `jb` on the PATH, so
it is a reviewed developer action rather than anything CI runs.

| Verdict | Count | Meaning |
|---|---:|---|
| `Derived` | 131 | exactly one value reproduced the baseline |
| `Insensitive` | 54 | every value did; the fixture cannot see the option |
| `Ambiguous` | 10 | several did, but not all |
| `Contradicted` | 2 | none did; something else moved the fixture |

⚠ Only `Derived` is written, and it is written as `defaultSource: "oracle-probe"` and never
`"resharper-docs"`, because it is derived and JetBrains still documents nothing. ⚠ **The registry
holds 123 `oracle-probe` entries, not 131 and not the 127 the paragraph below implies.** The probe's
`Derived` verdict count and the number actually adopted into the registry are two different figures
and this section conflated them; 123 is what `options.json` says, and it is the one the arithmetic
in § "Precedence" has to agree with. 110 of the 131
agree with the export, which is itself a result: those keys are Rider's defaults and the export is
redundant in them. Fourteen genuinely differ, and they are recognisably ReSharper out of the box —
Allman braces, `new_line_before_else = true`, `empty_block_style = multiline`,
`wrap_chained_method_calls = wrap_if_long`, `keep_existing_invocation_parens_arrangement = true`.

⚠ **Options interact, so this is a strong signal and not proof, and four of the fourteen are recorded
`unknown` rather than adopted.** Formatting 60 Vixen files with Vixen's own `.editorconfig` and
comparing against the oracle under the same configuration is an independent check, and the four chain
keys — `wrap_after_dot_in_method_calls`, `wrap_after_property_in_chained_method_calls`,
`wrap_before_first_method_call`, `wrap_primary_constructor_parameters_style` — make it worse rather
than better. They come back `Derived` because they are unobservable under ReSharper's own defaults:
nothing chops while `wrap_chained_method_calls` is `wrap_if_long`, so the probe saw no change and
read it as agreement.

Measured, on those 60 files against the oracle under Vixen's configuration:

| Fallback table | line | file |
|---|---:|---:|
| the export's values (M0–M2) | 97.00 % | 38.33 % |
| the ten corroborated derived values | **97.84 %** | **51.67 %** |
| all fourteen | 96.30 % | 30.00 % |

Over the whole Vixen tree, `format --check` goes from 2 700 files to 2 506 at the commit this was
measured at (2 552 in the shipped build, which has four more formatting changes in it). And
`distill` now drops **108 keys** of the export's 4 239 lines, where it dropped none.

A pristine-profile export is still worth having: it would settle the 54 insensitive keys, the 10
ambiguous ones and the 4 contradicted ones without another probe. It is no longer the only way
forward.

It is offered, never imposed. The export must keep working forever (ADR-001), because the workflow
that produced it — change a setting in Rider, re-export — must keep working. `distill` is for the
repository that wants its style to be a document rather than a dump; the round trip
`distill(export) ≡ export` in *behaviour* is a conformance test.

## Naming rules

The 215 `dotnet_naming_*` keys form 20 rules using Microsoft's three-part scheme (symbols → style →
rule with severity). This is standard editorconfig and Roslyn already parses and enforces it through
the IDE code-style diagnostic `IDE1006`; it is not a C# compiler diagnostic.

Skala does **not** reimplement naming. `Microsoft.CodeAnalysis.CSharp.CodeStyle` is pinned to the
same version as the rest of Roslyn, its four implementation assemblies ship under
`RoslynCodeStyle/`, and the analysis host selects only the analyzer that owns `IDE1006`. It runs in
`binlog` and `workspace` modes and is listed as skipped in `loose`, where there is no semantic model.
Consequently `dotnet_naming_rule.private_instance_fields_rule.*` means exactly what it means in the
Roslyn IDE. The analyzer's own specificity ordering resolves overlapping rules; Skala neither uses
file order nor invents a second precedence model.

There is no `SK0110`. The earlier plan promised one for conflicting overlaps, based on the obsolete
premise that modern Roslyn silently used the first rule in file order. A configuration diagnostic
must not disagree with the engine it claims to validate. The two exported groups with an empty
`applicable_kinds` value (`enum_member_symbols` and `unity_serialized_field_symbols`) remain inert;
the standard symbol vocabulary cannot express those Rider-specific concepts as written.

The export's effective rules, for the record: interfaces `IPascal`, type parameters `TPascal`, everything
public `PascalCase`, and locals/parameters `camelCase`. The requested enum-member and Unity
serialized-field rules have empty standard symbol groups and therefore never fire.

## Canonical distribution across repositories

Resolves [16](16-risks-and-open-questions.md) § Q4. The stated goal is that every repository under
`~/Projects` formats identically; today that means copying `.editorconfig` into each one, which
drifts silently, and a drifted config is precisely the failure Skala exists to prevent.

### ⚠ The restore-time drop does not exist

Q4 leaned toward "an SDK package that carries the canonical `.editorconfig` and drops it at restore
time". The carrying half works. **The dropping half cannot be built**, and this was measured rather
than assumed — a probe package with the file in `content/`, in `contentFiles/any/any/`, and a
`build/*.targets` with a target hooked `BeforeTargets="Restore"`, restored and built by a consumer:

| Mechanism | `dotnet restore` | `dotnet build` |
|---|---|---|
| `content/` | not copied | not copied |
| `contentFiles/any/any/` | not copied to the project directory | not copied to the project directory |
| `build/*.targets`, `BeforeTargets="Restore"` | **did not run** | — |
| `build/*.targets`, `BeforeTargets="Build"` | — | ran; wrote into the project directory |

`content/` is the `packages.config` era and does nothing under `PackageReference`.
`contentFiles/` links files into the *compilation*, not into the working tree. And a package's
targets cannot run during restore for the reason that decides it: they are imported through
`obj/*.nuget.g.targets`, which restore is in the middle of generating.

Dropping it from a **build** target does run — and is worse. Measured on a probe repository whose
canonical would make a block-scoped namespace an `IDE0161` error:

| | outcome |
|---|---|
| build 1, the build that installs the file | **succeeded** — the compiler had already been handed the configuration that existed at evaluation time |
| build 2, incremental, nothing changed | **succeeded** — the arriving `.editorconfig` did not invalidate `CoreCompile` |
| build 3, `--no-incremental` | failed, `error IDE0161` |

**A gate whose first two runs pass is not a gate.** Add to that: it writes into the source tree from
projects MSBuild runs in parallel, and it changes a file Rider has open.

### The deeper constraint: Rider reads one file, by name

Every scheme that puts the canonical somewhere other than the repository's own `.editorconfig` —
`EditorConfigFiles` from a package, a `.globalconfig`, a second file beside it — is invisible to the
IDE, which walks directories looking for files literally called `.editorconfig`. And the compiler
would not help either: **an `.editorconfig`'s section globs resolve relative to the directory
containing the file**, so a canonical sitting in `~/.nuget/packages/…/content/` has a `[*]` that
matches only files under the NuGet cache. That is asserted as a test, through Roslyn's own matcher,
because it is the fact that kills the whole family of designs.

An IDE formatting against a different configuration than the gate is the failure ADR-001 exists to
prevent. **So the canonical must physically be the repository's `.editorconfig`.** The only open
questions are who writes it, and when.

### What is built instead: a carrier, a command, and a check

| Piece | Job |
|---|---|
| `Rikarin.Skala.Canonical` | The versioned, restorable, hash-addressed carrier: `content/canonical.editorconfig`, `content/canonical.json`, and a **check-only** MSBuild target. Pinned in `Directory.Packages.props` like everything else. |
| `skala config sync [--apply]` | The only thing that writes. Explicit, offline, produces a reviewable git diff. |
| `skala config diff --canonical` | The gate condition. Exit `3` on drift ([09](09-quality-gates-and-reporting.md) § "Exit codes"). |

The payload is carried twice from one file on disk: embedded in `Rikarin.Skala.Core`, so `sync`
works offline behind the tool's own version pin — which [11](11-cli-and-integrations.md) already
calls the recommended form — and packed, so CI can obtain it without installing the tool. A test
asserts the two carriers are byte-identical.

`Rikarin.Skala.Canonical` is deliberately *not* `Rikarin.Skala.Sdk` and not `Rikarin.Skala.Rules`: a
canonical bump is a repository-wide reformatting commit and a rule bump is not, and one version
across both forces every repository to take the reformat to get a bug fix.

The MSBuild target answers only the half of the question that needs no tool — "is this repository on
the canonical this package carries?" — by comparing the marker's hash against the manifest's, which
is a string comparison. It cannot detect an *edit* to the block, because that needs a SHA-256 that
MSBuild has no way to compute, and a second implementation of the marker grammar in MSBuild would be
a second thing to keep in sync. ✅ Measured at **5 ms per project**, and off with
`SkalaCanonicalCheck=false`. Default severity is a message; `SkalaCanonicalCheckSeverity=error`
for a repository that wants the build to stop.

### The layering: one file, two blocks, editorconfig's own cascade

There is exactly one file, so the layering is inside it:

```ini
# skala:canonical begin version=0.1.0 sha256=461eabddabf1…
# … 4 261 lines: the Rider export, `root = true`, `max_line_length = 120` …
# skala:canonical end

# ------------------------------------------------------------------------------
# This repository's own configuration. Skala never writes below this line.
# ------------------------------------------------------------------------------
# skala:local begin

[{Core,Gameplay,Platform}/**/*.cs]
dotnet_diagnostic.IL2026.severity = error
…
```

The local block comes **after**, and editorconfig resolves later sections over earlier ones within a
file. That is the whole mechanism: a legitimate local override survives every canonical bump, and
Skala never has to know it exists. `sync` replaces the block between the markers and copies
everything below `skala:local begin` byte for byte; `diff --canonical` hashes the block between the
markers and ignores everything below it.

Vixen is the case this is tested against, because Vixen is what it has to survive: **56 path-scoped
sections**, `[{Core,Gameplay,Platform}/**/*.cs]` escalating trimming diagnostics to errors,
`[**/*.Tests/**/*.cs]` relaxing them, forty-odd single-file suppressions each with the paragraph of
reasoning that justifies it. ✅ After `sync`, all 56 sections and all of their comments are present
verbatim, and the effective resolution still gives `indent_size = 2` for `.props`,
`csharp_prefer_braces = when_multiline:suggestion` and `trim_trailing_whitespace = true` for `.cs` —
Vixen's values, not the canonical's — while `resharper_csharp_max_line_length = 120`, which Vixen
never set, comes from the canonical.

Overrides are **reported, never fought**: `SK9013`, info, one per option the local block takes back,
naming both values. That list is the review artefact — "here is what this repository does differently
from the canonical" is exactly the conversation a canonical is for. On Vixen, `sync` produces a
5 188-line file and the report is **7 lines**, every one of them a real disagreement between Vixen's
hand-written config and the Rider export:

```
[*] insert_final_newline: canonical false -> local true
[*] trim_trailing_whitespace: canonical false -> local true
[*.{json,yaml,yml,csproj,props,targets,slnx,xml,g4}] indent_size: canonical 4 -> local 2
[*.cs] csharp_using_directive_placement: canonical outside_namespace:silent -> local …:warning
[*.cs] csharp_style_namespace_declarations: canonical file_scoped:suggestion -> local …:warning
[*.cs] dotnet_sort_system_directives_first: canonical false -> local true
[*.cs] csharp_prefer_braces: canonical true:none -> local when_multiline:suggestion
```

⚠ Comparison is by **exact spelling within a section**, falling back to the canonical's `[*]`. The
tempting shortcut — "the canonical's last value for this `OptionId`" — is wrong twice over: it
conflates a key with its aliases, so `insert_final_newline = false` reads as an override of
`resharper_csharp_insert_final_newline = true`, which is the export's own contradiction and already
`SK9005`; and it conflates sections, so `[*.csv]` reads as overriding `[*]`. Both fired against
Skala's own configuration, which is the export, and which must report **zero** overrides. It does.

Only keys the registry owns are reported here. Vixen's forty-odd per-file `dotnet_diagnostic`
suppressions would bury the seven that matter, so they get their own report instead — the next
section, which exists because the omission stopped a build.

### ⚠ `SK9016`: what the canonical does to compiler severities

**Adopting the canonical took a repository from 0 build errors to 17, and nothing said so.** The
canonical is the Rider export and the export carries **253** `dotnet_diagnostic.*.severity` lines,
**213** of them `cs*`. Vixen carried 71 `dotnet_diagnostic` lines and not one `cs*`. One of the 213
is `dotnet_diagnostic.cs9209.severity = warning`, which raises `CS9209` above the compiler's own
default; Vixen builds with `TreatWarningsAsErrors`, which is not exotic. The `.editorconfig` commit
**alone** — no code touched — turned a tree that built clean into **17 errors in 15 files**,
isolated by rebuilding with only that file swapped.

`SK9013` said nothing, because `dotnet_diagnostic` keys are deliberately not in the option registry;
`config check` filed them under "keys the option registry does not own" and moved on. So the loudest
thing the canonical does to a repository was the one thing it did silently.

⚠ **The fix is a report, not a change to the payload.** The severities are what the canonical is
*for*. What was missing is that adopting one must state which diagnostics it moves, in which
direction, **before** it is applied. Both `sync` and `diff --canonical` now do:

```
Diagnostic severities the canonical moves, relative to the file it replaces:
  compiler diagnostics  236
  analyzer diagnostics  17

  ⚠ 236 compiler diagnostic(s) move up to warning or error.
    With `TreatWarningsAsErrors` these become build errors from a commit that touches no
    code. Measured on one repository: 0 errors before, 17 in 15 files after, from the
    .editorconfig alone.

  ⚠ [*] CS9209: (not set) -> warning
  …
```

| | |
|---|---|
| `SK9016` **warning** | a *compiler* diagnostic moves up to `warning` or `error`. The only warning in this file: drift is an error because somebody edited a managed block, being behind is info because eighteen repositories must not go red on a publication day — this one is neither wrong nor survivable |
| `SK9016` info | a compiler severity lowered or dropped (docs/plan/09 § "`--no-new-suppressions`": a severity turned down is the widest suppression there is), or an analyzer's changed — those add and remove findings rather than failing the build |

⚠ **Effective value against effective value, not block against block.** "Before" is the whole file as
it stands; "after" is the incoming canonical with the local block laid *over* it, because sync
preserves that block verbatim below the canonical one and editorconfig resolves later sections over
earlier ones. Comparing the two blocks would report a key the local block already pins as changing,
which is the one case where nothing changes at all.

⚠ **An introduction has no measurable direction, and the report says so rather than guessing.** When
the file sets nothing, the severity being moved away from is the compiler's own default — not
written down in any `.editorconfig`, and different by language version. A hand-maintained table of
Roslyn defaults would be a second copy of somebody else's data, wrong on the next compiler release.
So the report states what it knows — `(not set) -> warning` — and names `TreatWarningsAsErrors`,
which is the mechanism that turns that into a build failure.

⚠ `sync` prints the summary and `diff --canonical` prints **every** line, uncapped. A truncated list
in the command whose job is pricing the change is the same silence, just shorter. The list puts `cs*`
before `bc*`: sorting 253 ids alphabetically put 23 Visual Basic ids ahead of every C# one, and the
first capped version of this list showed a C# repository nothing but Visual Basic.

⚠ `skala.jsonc` gets one key, `canonical.drift` (`error` | `warning` | `off`), and deliberately no
`version`: the version a repository is on is recorded in the marker, beside the bytes it names, so
the question "is this file what it claims to be" is answerable from the file alone. A version in a
second file is a version that comes to disagree with itself, which is this feature's whole disease.
`canonical.version` in `skala.jsonc` is `SK9012` (error).

### The rollout: eighteen repositories, eighteen days

A canonical change must not require eighteen simultaneous reformatting commits. It does not, because
**drift and behindness are different questions**:

| | test | severity |
|---|---|---|
| **Drift** — `SK9008` | `sha256(block) ≠ the marker's own sha256` | error by default; fails the gate |
| **Behind** — `SK9009` | `the marker's sha256 ≠ the tool's canonical` | info; never fails |

Drift needs no canonical payload at all, only the file — so it is decidable offline, at any version,
by any version of the tool. Publishing 0.2.0 changes nothing about a repository on 0.1.0: its block
still hashes to its own marker, and its gate stays green. Bumping is a per-repository PR that is
always the same three steps — `skala config diff --canonical --options` to price it, `skala config
sync --apply`, then `skala format` in its own commit with its SHA in `.git-blame-ignore-revs`
([11](11-cli-and-integrations.md) § "Adoption path"). Eighteen repositories take it in whatever
order suits them.

Hashing is over LF-normalised UTF-8, so a clone with `core.autocrlf=true` verifies against the same
hash a Linux runner computes. A hash over raw bytes would make "this repository has drifted" mean
"this repository is on Windows".

### ⚠ ADR-001 survives intact

The canonical **is** the Rider export. `skala config canonical` composes it, and the composition is
two additions long — both of them what `skala config fix` already does:

```
canonical.editorconfig  =  advisory preamble
                        +  root = true                     ← hazard 1, above
                        +  editor_config_template verbatim  ← 4 226 assignments, untouched
                        +  max_line_length = 120            ← hazard 2, beside the ReSharper key
```

✅ Verified: the composed file carries all 4 226 of the export's assignments with their values
unchanged, plus exactly two. The maintainer loop is unchanged and is still mostly Rider — change a
setting in Rider, re-export over `editor_config_template`, `./build.sh Canonical`, commit, publish.
A test fails the build when the checked-in payload is not what the export would compose to, so a
re-export that skips the regeneration step is a red build rather than a silent divergence between
what is in the IDE and what eighteen repositories are given.

### Skala's own repository is deliberately unmanaged, for now

`skala config diff --canonical` on Skala itself reports `UNMANAGED`, `SK9014` (info), exit 0, and
zero overrides — because Skala's `.editorconfig` *is* the export ([02](02-repository-layout.md),
ADR-015) and is therefore the canonical's own source. A repository that has not opted in must not be
failed by a command it did not ask for. Adopting the markers here is a follow-up worth taking once
Milestone 1 lands, and it is the obvious dogfooding test.

### What was discarded

| Considered | Why not |
|---|---|
| Restore-time drop from a package | Does not exist. Measured, table above. |
| Build-time drop from a package target | Three builds to take effect, the first two green. Writes to the source tree from parallel builds. Fights Rider. |
| `EditorConfigFiles` / `.globalconfig` from a package | Invisible to Rider, `dotnet format`, and every other editor. Splits the IDE from the gate, which is the thing ADR-001 forbids. |
| Point the compiler at the packaged `.editorconfig` | Section globs resolve relative to the file's own directory, so it configures the NuGet cache. Asserted as a test. |
| A second file (`.editorconfig.local`) beside the canonical | editorconfig cascades by *directory*; two files cannot coexist in one, and Rider reads only the one called `.editorconfig`. |
| `skala.jsonc` declaring which sections are local | A second place to say what code should look like — `SK9003`, and ADR-001. The markers in the file itself carry no information the file does not already have. |
| A git submodule | Nobody enjoys one, and it still cannot put a file at the repository root. |
| Version pinned in `skala.jsonc` | Two records of one version. `SK9012`. |

## What lives in `skala.jsonc`

```jsonc
{
  "$schema": "https://…/skala.schema.json",     // generated, shipped in the package
  "include": ["**/*.cs"],
  "exclude": ["**/obj/**", "**/bin/**", "**/*.g.cs", "artifacts/**"],
  "generated": {                                 // how to recognise generated code
    "byPath": ["**/*.g.cs", "**/Generated/**"],
    "byHeader": true                             // <auto-generated> — Roslyn's own heuristic
  },
  "analysis": {
    "load": "binlog",                            // binlog | workspace | loose
    "binlog": "artifacts/build.binlog",
    // ⚠ `resharperSeverities` was here and has been removed — § "Severities", Q5
    "hostedAnalyzers": [                          // ADR-008 — opt-in, never bundled
      { "package": "Meziantou.Analyzer", "version": "2.0.*" }
    ]
  },
  "canonical": { "drift": "error" },              // error | warning | off. No `version` — SK9012
  "gates": {
    "ci":   { "maxSeverity": "warning", "newIssues": 0, "baseline": ".skala/baseline.sarif",
              "canonical": "clean" },              // ⇒ `skala config diff --canonical` must exit 0
    "local":{ "maxSeverity": "error" }
  },
  "duplication": { "minTokens": 100, "maxPercent": 3.0 }
}
```

Everything in it is about *where to look* and *what to do about what is found*. Nothing in it is
about what code should look like. That line is the whole point of ADR-001, and `SK9003` enforces it.

⚠ **What of that block is real, as of M10:** `canonical.drift`, `gates`, and `exclude`. The rest —
`include`, `generated`, `analysis`, `duplication` — is design and is read by nothing; a key from that
half is inert rather than diagnosed, which is a hole this document should stop hiding.

`exclude` is a list of `.editorconfig` section globs anchored at this file's directory, matched by
Roslyn's own matcher so there is no second glob dialect to learn (`SectionMatcher`). It answers "is
this `.cs` file source code this repository wants looked at", and **every whole-tree walk in the tool
reads the same answer** — `format`, `arrange`, `check`, `fix`, and the coverage denominator behind
doc 07's `--require-fresh-binlog`. ⚠ It had to exist because a repository can hold `.cs` files that
are deliberately in no compilation and nothing outside MSBuild can see that: Skala's own tree holds
1 924, the coverage ratio read 13 % against a complete binlog, and every push to `master` exited 4.
The built-in exclusions — `obj`, `bin`, `.git`, `.claude`, `artifacts`, `.skala` — are not
configurable and cannot be put back, because nothing in them is anybody's source.

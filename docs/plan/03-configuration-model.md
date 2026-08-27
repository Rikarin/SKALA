# 03 — Configuration Model

This is the first thing to build, because everything else is a consumer of it, and because the
input is a 4 238-line machine-generated file whose contents nobody has ever read end to end.

## What the Rider export actually contains

`editor_config_template`, exported from Rider's **Settings → Editor → Code Style → Export to
.editorconfig**, measured:

| Group | Count | Notes |
|---|---|---|
| Total assignments | 4 226 | across 3 sections |
| `resharper_*` non-severity (formatting + arrangement) | 648 | ≈ 380 apply to C# |
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

⚠ 380 formatting options cannot be maintained as hand-written C# properties, hand-written parsers,
hand-written docs and hand-written tests. There is exactly one source of truth:

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

From it, `Rikarin.Skala.Options.Generator` (an incremental source generator, so it participates in
the build rather than being a checked-in codegen step) emits:

- `FormattingOptions` — a readonly struct-of-arrays keyed by a generated `OptionId` enum, so that
  reading an option in the fitting hot loop is an array index, not a dictionary lookup. ✅ This
  matters: the fitting pass reads options millions of times over the corpus.
- The parser: key → `OptionId`, value → typed value, including ReSharper's value aliases
  (`true`/`always`, `false`/`never`).
- `docs/options/*.md`, one page per option, and the tier matrix.
- The `.editorconfig` completion list used by the LSP.
- A test stub per option asserting that the option is exercised by at least one corpus file — an
  option with no test is a build failure, which is the mechanism that keeps Tier A honest.

Adding support for an option is therefore: add the JSON entry, add the corpus file, implement the
switch arm the generator's `[MustHandle]` attribute now requires. Forgetting any of the three does
not compile.

## Four tiers

Every option in `options.json` has a tier, and `skala config explain` prints them for the current
file. This is non-negotiable #4 from [00](00-vision-and-principles.md) made concrete.

| Tier | Meaning | Behaviour |
|---|---|---|
| **A — implemented** | Skala reproduces Rider's behaviour, pinned by at least one oracle fixture | Applied |
| **B — approximated** | Implemented, with a documented divergence in stated edge cases | Applied; `skala config explain` shows the divergence text |
| **C — accepted, ignored** | Parsed, validated, and deliberately not implemented (C++ keys, VB keys, XAML keys, `resharper_old_engine`, autodetect toggles) | Ignored silently unless `--strict-config` |
| **D — unknown** | Not in `options.json` at all | `SK9001` info once per key per run, with a "did you mean" over the registry |

⚠ Tier D must be *info*, not warning, by default. The export contains ~2 000 keys Skala will never
implement (`resharper_cpp_*` alone is 1 896), and a tool that emits two thousand warnings on first
run gets uninstalled on first run. `--strict-config` promotes C and D to warnings for people who
want the audit.

**The tier matrix is published**, generated into `docs/options/`, and the README carries the
headline number: "Tier A: n of 380 C# options". That number going up is the project's progress bar
through Milestones 1–3.

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
   the config must produce what Rider produces with that key absent. ⚠ M3 makes that true for 126
   keys by deriving the value from the oracle rather than taking the export's; see "Deriving
   ReSharper's defaults" below for what the other 397 still record and why.

`skala.jsonc` never participates. It cannot set a style option; attempting to is `SK9003` (error).

## Severities

Three namespaces arrive in the same file and all three must work:

| Form | Example | Applies to |
|---|---|---|
| `dotnet_diagnostic.<id>.severity` | `dotnet_diagnostic.CA2252.severity = error` | Any Roslyn analyzer, incl. Skala's `SK####` and hosted third-party rules |
| `resharper_<inspection>_highlighting` | `resharper_convert_to_primary_constructor_highlighting = suggestion` | ReSharper inspections — mapped to the Skala rule that reimplements them |
| `<style_key>:<severity>` | `csharp_style_namespace_declarations = file_scoped:suggestion` | Microsoft style keys with an inline severity |

The middle row was written as a headline feature: **the 853 C#-relevant `resharper_*_highlighting`
keys already in the export configure Skala's rules**, via a mapping table in `rules.json`
(`resharperId: "ConvertToPrimaryConstructor"`), so that the author's existing Rider severity applies
with no new configuration and adoption is a copy rather than a project.

⚠ **Milestone 5 measured it against the real export, and it does not hold as stated.** It is
[16](16-risks-and-open-questions.md) § Q5, and the answer is below.

### Q5, resolved: the mapping is a recorded choice, and it is off by default

Four measurements, all on the export in this repository:

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

Nothing computes which of those governs `SK1010`. In the other direction, `SK1034` covers what
`use_collection_count_property`, `replace_with_single_call_to_any` and `replace_with_single_call_to_count`
split into three. The mapping is therefore **recorded in `rules.json` as a choice**, one key per
rule, with a `resharperNote` saying which alternatives were passed over and why. It is a *function
from Skala rule to at most one key*, never the reverse — which is the direction
[16](16-risks-and-open-questions.md) § Q5 guessed was the safe one, and it is.

**2. It is partial, and the gap is not exotic.** `SK1005` (file-scoped namespace) has **no**
ReSharper inspection id. Rider drives that conversion from the *Microsoft* key
`csharp_style_namespace_declarations = file_scoped:suggestion` and reports the result under
`resharper_arrange_namespace_body_highlighting = hint`. One concept, two mechanisms, two severities,
and neither of them is an inspection Skala can name. `resharperId` for that rule is `null` and the
docs page says so.

**3. ⚠ A derived key that looks right and does not exist is worse than no mapping.**
`ConvertToFileScopedNamespace` and `ConvertToThrowIfNull` both snake-case into plausible keys and
JetBrains emits neither. A mapping to a key nothing sets never applies, looks like a feature and
behaves like a comment. `RuleCatalogTests.EveryDeclaredReSharperKey_ExistsInTheExport` reads the real
export and fails the build for it.

**4. ⚠ Reading the keys as authoritative would switch a rule off in the repository the tool was
built for.** The export sets

```ini
resharper_use_throw_if_null_method_highlighting = none
```

so `SK1020` — the `ArgumentNullException.ThrowIfNull` rule — would be silently disabled by a value
nobody chose for it. That is the decisive measurement: the 912 `resharper_*_highlighting` values in
an export (462 `warning`, 232 `suggestion`, 110 `hint`, 93 `none`, 15 `error`) were chosen for
ReSharper's inspections, and a value that has never been looked at is not consent.

**The resolution.** The mechanism exists and is opt-in:

```bash
skala check --resharper-severities        # or "analysis": { "resharperSeverities": true }
```

with the precedence [16](16-risks-and-open-questions.md) § Q5 predicted:

| | wins over | because |
|---|---|---|
| `dotnet_diagnostic.SK1010.severity` | everything | it names the Skala rule, so it cannot mean anything else |
| `resharper_<inspection>_highlighting` | the rule's default | only under `--resharper-severities` |
| `rules.json` `defaultSeverity` | — | the fallback |

`skala explain <id>` prints the key a rule maps to, its value in the current configuration, and the
note about what was passed over. So the headline claim survives in a smaller and truer form: the
export *can* configure Skala's rules, one rule at a time, when someone asks it to.

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
skala config distill              # ← the important one
skala config fix                  # add root/max_line_length, resolve contradictions, in place
skala config check                # tier report + contradictions, exit non-zero under --strict
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
`fidelity defaults` is the command.

| Verdict | Count | Meaning |
|---|---:|---|
| `Derived` | 131 | exactly one value reproduced the baseline |
| `Insensitive` | 54 | every value did; the fixture cannot see the option |
| `Ambiguous` | 10 | several did, but not all |
| `Contradicted` | 2 | none did; something else moved the fixture |

⚠ Only `Derived` is written, and it is written as `defaultSource: "oracle-probe"` and never
`"resharper-docs"`, because it is derived and JetBrains still documents nothing. 110 of the 131
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
rule with severity). This is standard editorconfig and Roslyn already parses and enforces it —
`IDE1006` is the compiler's naming rule engine.

Skala does **not** reimplement naming. It runs Roslyn's own naming analyzer as part of the analysis
pass and reports `IDE1006` under its own ID, so `dotnet_naming_rule.private_instance_fields_rule.*`
means exactly what it means in the IDE. The one Skala addition is `SK0110`, which reports a naming
*configuration* problem — two rules matching the same symbol group with conflicting styles, a
`required_prefix` that no rule can satisfy — because the Microsoft engine silently applies the first
match and the export has 20 rules whose order nobody has checked.

The export's rules, for the record: interfaces `IPascal`, type parameters `TPascal`, everything
public `PascalCase`, locals/parameters `camelCase`, and a Unity serialized-field rule that will
never fire outside Unity.

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
    "resharperSeverities": false,                // ⚠ off by default — § "Severities", Q5
    "hostedAnalyzers": [                          // ADR-008 — opt-in, never bundled
      { "package": "Meziantou.Analyzer", "version": "2.0.*" }
    ]
  },
  "gates": {
    "ci":   { "maxSeverity": "warning", "newIssues": 0, "baseline": ".skala/baseline.sarif" },
    "local":{ "maxSeverity": "error" }
  },
  "duplication": { "minTokens": 100, "maxPercent": 3.0 }
}
```

Everything in it is about *where to look* and *what to do about what is found*. Nothing in it is
about what code should look like. That line is the whole point of ADR-001, and `SK9003` enforces it.

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

Only keys the registry owns are reported. Vixen's forty-odd per-file `dotnet_diagnostic`
suppressions are Milestone 5's business, and listing them would bury the seven that matter.

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
    "resharperSeverities": false,                // ⚠ off by default — § "Severities", Q5
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

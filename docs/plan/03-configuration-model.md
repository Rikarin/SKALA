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
  "default": "wrap_if_long",                       // ReSharper's default, not ours
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
   the config must produce what Rider produces with that key absent.

`skala.jsonc` never participates. It cannot set a style option; attempting to is `SK9003` (error).

## Severities

Three namespaces arrive in the same file and all three must work:

| Form | Example | Applies to |
|---|---|---|
| `dotnet_diagnostic.<id>.severity` | `dotnet_diagnostic.CA2252.severity = error` | Any Roslyn analyzer, incl. Skala's `SK####` and hosted third-party rules |
| `resharper_<inspection>_highlighting` | `resharper_convert_to_primary_constructor_highlighting = suggestion` | ReSharper inspections — mapped to the Skala rule that reimplements them |
| `<style_key>:<severity>` | `csharp_style_namespace_declarations = file_scoped:suggestion` | Microsoft style keys with an inline severity |

The middle row is the interesting one and it is a headline feature: **the 853 C#-relevant
`resharper_*_highlighting` keys already in the export configure Skala's rules**, via a mapping table
in `rules.json` (`resharperId: "ConvertToPrimaryConstructor"`). Where Skala has no rule for an
inspection, the key is Tier D and reported once. Where it does, the author's existing Rider severity
applies with no new configuration — which is what makes adoption a copy rather than a project.

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

⚠ **This does not work yet, and the reason is a mistake in this document.** The original text said
`options.json` "stores those defaults precisely for this purpose", and M0 established that it cannot:
JetBrains' EditorConfig property tables publish each property's name, language and possible values,
and **never its shipped default** — all 22 schema pages and the 5 200-row index were checked. So
every registry entry records the export's own value as its default, marked `defaultSource:
"template"` or `"unknown"`, and none is marked `"resharper-docs"`.

`distill` may only drop a key whose default is `resharper-docs`, so today it drops **0 of 4 226** and
says so, at length, on stdout. That is the correct answer, not a failure: dropping a key on a guessed
default silently changes formatting, which non-negotiable #4 forbids.

**What unblocks it** is a verified default table, and the only reliable source is a one-off human
action: export an `.editorconfig` from a *pristine* Rider profile with nothing customised, and diff
it against the author's export. The difference is, by construction, exactly the set of non-default
keys. Until that exists, `distill` is honest and useless, and no option may claim Tier A on the
grounds that its default is known.

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

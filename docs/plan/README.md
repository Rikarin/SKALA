# Skala — Implementation Plan

Skala is a formatter, linter and static-analysis gate for .NET, built so that one configuration —
the `.editorconfig` exported from Rider — produces identical results in the IDE, on the command
line, in CI, and in front of an AI agent. It replaces the three tools that job currently takes:
**ReSharper/Rider code cleanup** (formatting and arrangement), **Qodana** (the CI inspection run),
and **SonarQube** (the quality gate, the metrics, the baseline).

C# is the language it is built for. HTML and the CSS family — including Vixen's `.vxml` and `.vcss`
dialects — are a second front end behind the same engine, and they come later; document
[14](14-web-languages.md) says what "later" means and what must be true of the core before then.

This directory is the authoritative design record: what Skala is meant to be, and why each decision
was taken. Read 00–03 first; after that each file is the spec for one subsystem.

**These documents do not say what is built.** When there is an `overview.md` beside this directory,
it does, and it is checked against the code — so where it and a document here disagree, it wins.

⚠ Where a document *has* been measured against the code, it says so inline with a ⚠ and a number.
Milestones 0–3 and 5 have been; **4 has not**, and it is deferred behind 5 (see
[15](15-roadmap.md) § M4). `docs/rules/` and `docs/options/` are generated from `rules.json` and
`options.json` and are not hand-edited — `skala rules docs` regenerates the first, and a test fails
when a page is stale.

## The one-sentence problem

`dotnet format` cannot wrap a line, CSharpier will not be configured, ReSharper's own CLI is the
only thing that reads `resharper_*` keys and it is a 400 MB proprietary download with no
machine-readable diff output — so there is currently no way to make a build agent, a CI job and an
AI agent agree with Rider about what the code should look like.

## The index

| # | Document | Scope |
|---|---|---|
| 00 | [Vision and Principles](00-vision-and-principles.md) | What Skala is, what it refuses to be, the non-negotiables |
| 01 | [Technology Decisions](01-technology-decisions.md) | Dependencies, pinned versions, ADR register |
| 02 | [Repository Layout](02-repository-layout.md) | Folder tree, project graph, package boundaries, naming |
| 03 | [Configuration Model](03-configuration-model.md) | `.editorconfig` ingestion, the Rider export, the option registry, precedence |
| 04 | [Formatting Engine](04-formatting-engine.md) | The document IR, break decisions, the fitting algorithm, trivia, safety |
| 05 | [C# Formatting Rules](05-csharp-formatting-rules.md) | Every `resharper_*` whitespace key mapped to a construct and a tier |
| 06 | [Arrangement and Syntax Styles](06-arrangement-and-syntax-styles.md) | The semantic rewrites: bodies, `var`, qualifiers, redundancies, usings |
| 07 | [Analysis Host](07-analysis-host.md) | Project loading, compilation, analyzer execution, incremental cache |
| 08 | [Rule Catalogue](08-rule-catalogue.md) | The `SK####` rules, their ranges, the modernization set, ID stability |
| 09 | [Quality Gates and Reporting](09-quality-gates-and-reporting.md) | SARIF, baselines, gates, metrics, duplication, CI surfaces |
| 10 | [AI Agent Integration](10-ai-agent-integration.md) | The MCP server, agent-shaped output, hooks, the verify loop |
| 11 | [CLI and Integrations](11-cli-and-integrations.md) | Command surface, daemon, LSP, MSBuild, git hooks, NUKE |
| 12 | [Conformance and Testing](12-conformance-and-testing.md) | The Rider oracle, idempotency, the corpus, fuzzing, golden files |
| 13 | [Performance](13-performance.md) | Budgets, measurements, parallelism, the cache, what is allowed to be slow |
| 14 | [Web Languages](14-web-languages.md) | HTML, CSS, `.vxml`, `.vcss`, and the language-plugin contract |
| 15 | [Roadmap](15-roadmap.md) | Milestones, order of work, what "done" means at each stage |
| 16 | [Risks and Open Questions](16-risks-and-open-questions.md) | What could sink this, and what is still undecided |

## What the input actually is

`editor_config_template` in the repository root is a Rider **export**, not a hand-written file, and
its shape is the reason this project is hard rather than tedious:

| Measurement | Value |
|---|---|
| Lines / assignments | 4 238 / 4 226 |
| `resharper_*` formatting keys | 648, of which ≈ 380 apply to C# |
| `resharper_*_highlighting` severities | 3 021, of which 853 apply to C# (M5 recounts it at 912; [03](03-configuration-model.md) says why both) |
| `dotnet_diagnostic.*` severities | 253 |
| `dotnet_naming_*` keys | 215, forming 20 naming rules |
| Microsoft `csharp_*` / `dotnet_style_*` keys | 40 |
| Sections | three: `[*]`, `[*.csv]`, and one 47-extension glob |

Nearly all of it is Rider writing out its defaults. The distillation of that file into a reviewable
configuration — and the decision about which of the 4 226 keys Skala *implements*, which it
*accepts and approximates*, and which it *rejects loudly* — is document
[03](03-configuration-model.md), and it is the first thing to build.

## The two facts that decide the architecture

1. **Roslyn's formatter cannot wrap.** It adjusts whitespace between tokens and preserves the line
   breaks it is given; wrapping to a column has been requested since 2016 and repeatedly declined.
   `resharper_csharp_max_line_length = 120` with `chop_if_long` therefore cannot be implemented on
   top of `Formatter.Format` — Skala needs its own line-fitting engine. ([04](04-formatting-engine.md))
2. **`resharper_keep_user_linebreaks = true` and `resharper_keep_user_wrapping = true`.** The
   configuration explicitly asks the formatter to *keep* the author's line breaks and only intervene
   where a rule or the column limit demands it. That is the opposite of Prettier's model — and of
   CSharpier's, which is a Prettier port — where the original line breaks are discarded and the
   output is printed from the syntax tree. Skala is **preserve-and-repair**, not print-from-scratch,
   and that is ADR-002.

## Conventions in these documents

- ⚠ marks a decision that is easy to get wrong and expensive to reverse.
- ✅ marks a decision that has been validated against the real corpus rather than reasoned about.
- A "tier" always means the compatibility tier from [03](03-configuration-model.md) § "Four tiers".
- Measurements are from `~/Projects/Vixen` (4 708 C# files, 1 374 580 lines at M3; the tree grows,
  so a number quoted against 4 691 files is from an earlier milestone and says so) unless stated. That
  tree is the reference corpus: it is the largest C# body the tool must handle and the one whose
  formatting the author actually cares about.

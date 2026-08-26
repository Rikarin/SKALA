# 00 — Vision and Principles

## What Skala is

One tool, installed as a .NET global tool and as an MSBuild package, that answers three questions
about a repository and answers them the same way everywhere:

1. **Is this file formatted?** — and if not, rewrite it. Formatting is defined by the
   `.editorconfig` that Rider exports, so the answer in the IDE and the answer in CI are the same
   answer by construction rather than by luck.
2. **Is this code written the way we write code?** — expression bodies where the configuration
   asks for them, `var`, target-typed `new`, file-scoped namespaces, `is not null`, collection
   expressions, primary constructors where they fit. This is *arrangement*, and it is a rewrite,
   not a report.
3. **Is this code correct, safe and maintainable?** — the static analysis: bug patterns, async
   misuse, allocation traps, security sinks, complexity, duplication. This is a *report*, gated,
   baselined, and machine-readable.

Those are the three tools it replaces, in order: ReSharper/Rider code cleanup, Qodana, SonarQube.

**The audience order is decided and it matters** — where these compete for effort, earlier wins:

1. **The author's own repositories**, as a pre-commit and CI gate. Vixen first, because it is
   4 691 files and 1.35 M lines and everything that is wrong with the design will show up there
   before it shows up anywhere else.
2. **AI agents writing C#**, as a verification loop they can run themselves. An agent that can ask
   "is what I just wrote acceptable, and if not exactly what is wrong with it" is an agent whose
   output does not need a human formatting pass. This is not a bolt-on: it changes the output
   format, the exit codes, the message text, and whether a fix is machine-applicable.
3. **Other people's repositories**, eventually. A tool that only works on one config is a script.

## What Skala is not

- **Not a compiler front end.** Roslyn parses and binds C#. Skala never writes a C# parser, a
  symbol table or a type system. (ADR-003)
- **Not an opinionated formatter.** CSharpier already exists, is excellent, and is opinionated by
  design — five options and a print width. Skala's entire reason to exist is that the author's
  formatting is 380 decisions long and lives in Rider. A Skala that is "mostly right by default"
  has failed.
- **Not a fork of CSharpier or of `dotnet format`.** Both are structurally incompatible with
  `keep_user_linebreaks = true` (see ADR-002). Both are read carefully; neither is copied.
- **Not a ReSharper clone.** ReSharper has ~2 500 inspections. Skala reimplements the ones the
  configuration actually turns on above `hint`, and it says out loud which ones it does not have
  rather than pretending the set is complete.
- **Not a build system.** It does not compile for output, does not restore, does not publish. It
  will read a build to learn what a compilation is, and that is all.
- **Not a service.** No server to stand up, no database, no dashboard, no token. SonarQube's
  quality gate is replaced by a file in the repository and an exit code, not by a hosted instance.

## Non-negotiables

These are the things that, if traded away, mean the tool should not be built at all.

1. **The Rider export drops in unchanged.** `cp editor_config_template .editorconfig` and Skala
   works. If Skala needs its own dialect of a formatting option, Skala is wrong. Tool-level
   concerns that `.editorconfig` genuinely cannot express — which paths to scan, what the CI gate
   is, where the baseline lives — go in `skala.jsonc`, and nothing about *style* is ever allowed in
   there. ([03](03-configuration-model.md))
2. **A formatting run never changes what the code means.** Every write is preceded by a
   verification that the significant token stream of the output equals that of the input. A
   formatter that can silently break a file is worse than no formatter, because it is trusted.
   ([04](04-formatting-engine.md) § "The safety net")
3. **Formatting is idempotent.** `format(format(x)) == format(x)`, for every file in the corpus,
   enforced in CI on every commit. A formatter that oscillates makes every diff a lie.
4. **Unknown configuration is a diagnostic, never a silent default.** If the `.editorconfig` sets
   an option Skala does not implement, Skala says so, at the line, with a tier. The failure mode
   this prevents is the one that makes people distrust linters: the config says one thing, the tool
   does another, and nothing ever tells you.
5. **Every diagnostic has a stable ID, a documented rationale, and a said-out-loud severity
   source.** `SK1042` means the same thing in five years or it is retired, never redefined.
   ([08](08-rule-catalogue.md) § "ID stability")
6. **Machine-readable output is a first-class surface, not a `--json` afterthought.** SARIF 2.1.0
   is the canonical form; the human text renderer is a *view* of the SARIF, generated from it.
   ([09](09-quality-gates-and-reporting.md))
7. **No network.** Skala never phones home, never downloads rules, never uploads code. It runs on a
   machine with no internet and in a sandboxed agent container.
8. **Deterministic.** Same inputs, same bytes out, same exit code, on every OS and every run.
   Parallelism is never allowed to reorder a report.

## The quality bars

| Bar | Target | Measured how |
|---|---|---|
| Formatting fidelity | ≥ 99.9 % of lines identical to `jb cleanupcode` on the reference corpus for Tier-A options | [12](12-conformance-and-testing.md) differential harness |
| Idempotency | 100 %, no exceptions | Second-pass diff over the corpus |
| Semantic safety | 100 %, no exceptions | Token-stream equivalence on every write |
| Cold format, whole corpus | < 20 s on 4 691 files / 1.35 M lines, 10 cores | `13` benchmark |
| Warm format, one file | < 40 ms end to end from the daemon | `13` benchmark |
| Cold analysis, whole corpus | < 4 min | `13` benchmark |
| Warm analysis, changed files | < 5 s | `13` benchmark |
| False-positive rate, default rule set | < 1 % of reported diagnostics on the corpus | Manual triage of a 200-diagnostic sample per release |

The last one is the one that decides whether the tool gets used. A linter with a 5 % false-positive
rate on a million-line tree produces hundreds of wrong findings, and the response to that is a
blanket suppression, after which the tool is decoration.

## Design principles

**Preserve, then repair.** The formatter's default answer to "should there be a line break here"
is "whatever the author wrote". It intervenes when a rule says it must (blank lines around members,
braces on the same line) or when the column limit says it must. This is what the configuration
asks for, and it is also what makes the tool safe to run on a 1.35 M-line tree: the diff is small
enough to read.

**Whitespace needs no semantics; arrangement does.** Reformatting is a pure syntax operation and
runs without a build, in parallel, on any folder. Turning `public int Foo() { return x; }` into an
expression body needs to know that `x` is not a `ref` local and that no `#if` splits the body —
syntax is enough there, but converting to a primary constructor or a collection expression needs a
`SemanticModel`. So the pipeline splits at exactly that line: `skala format` never needs a project,
`skala arrange` and `skala check` do. ([06](06-arrangement-and-syntax-styles.md))

**A fix is worth ten reports.** Every rule ships with a code fix unless it is genuinely
undecidable. The measure of the analysis half is not how many problems it finds, it is how many it
removes without a human. This is also what makes it usable by an agent: "here is the patch" beats
"here is the complaint".

**Report at the altitude of the fix.** A diagnostic points at the smallest span that has to change,
carries the rule ID, one sentence of *why*, and either a fix or a concrete "do this instead". No
essays in the terminal; the essay is at `skala explain SK1042`.

**Configuration is data, and data can be generated.** ~380 formatting options and ~250 rules is too
many to hand-maintain as C# properties, docs and tests in three places. There is one machine-readable
option table, and the option struct, the parser, the docs, the completion list and the conformance
matrix are all generated from it. ([03](03-configuration-model.md) § "The option registry")

**Say what you cannot do.** Tier C and Tier D exist so that the tool can be honest about the 4 226
keys it is handed. `skala config explain` prints, for the current `.editorconfig`, exactly which
options are implemented, which are approximated and how, and which are ignored.

## What success looks like, concretely

A year from now, in every repository under `~/Projects`:

- `.editorconfig` is the Rider export, copied, unmodified.
- `./build.sh Lint` runs `skala check --gate ci` and is the only formatting/analysis step.
- A pre-commit hook runs `skala format --staged` in under a second.
- `CLAUDE.md` tells the agent to run `skala verify` before claiming work is finished, and the agent
  does, and the result is a patch it applies rather than prose it argues with.
- Nobody has run Rider's "Reformat and Cleanup" manually in months, because there is nothing left
  for it to do.

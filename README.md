# Skala

A formatter, linter and quality gate for C#, driven by one `.editorconfig`.

Skala formats your code, arranges it, runs 336 analysis rules over a real compilation, and fails
your build or your CI job when the result crosses a line you drew. The command line, your editor,
CI and any AI agent working in the repository all read the same configuration file, so they agree
about what the code should look like by construction rather than by convention.

```bash
dotnet tool install --global Rikarin.Skala.Cli
skala format .
skala check .
```

## Install

**As a .NET tool**, for the command line and CI:

```bash
dotnet tool install --global Rikarin.Skala.Cli
```

Prefer a local tool manifest so everyone on the repository runs the same version:

```bash
dotnet new tool-manifest
dotnet tool install Rikarin.Skala.Cli
```

**As a package**, to get the analyzers and the build integration in every `dotnet build`:

```xml
<PackageReference Include="Rikarin.Skala.Sdk" Version="[2.0.0]" PrivateAssets="all" />
```

The square brackets are deliberate. All five packages ship at one version, and an exact pin is what
keeps the analyzers and the tool agreeing about whether the tree is clean.

Requires the .NET 10 SDK.

## Usage

```bash
skala format .                  # rewrite: spacing, blank lines, braces, indentation, wrapping
skala format --check .          # report what would change, write nothing
skala arrange .                 # body styles, var, target-typed new, qualifiers, usings
skala check .                   # run the analyzers, print findings, apply the gate
skala fix .                     # apply the safe fixes, verify each one, re-format
skala explain SK1084            # what a rule means, why, and what it rewrites
skala explain skala_wrap_arguments_style   # what an option governs
```

`skala verify .` is the one an agent or a pre-commit hook should run: it is `format --check` plus
`arrange --check` plus `check --gate=local`, and **exit 0 means there is nothing left to do**.

### Commands

| | |
|---|---|
| `format` | Spacing, blank lines, braces, indentation, line breaks and wrapping |
| `arrange` | Body styles, `var`, target-typed `new`, qualifiers, using directives |
| `check` | Run the analyzers over a real compilation, report, and gate |
| `verify` | `format --check` + `arrange --check` + `check --gate=local` |
| `fix` | Apply fixes, verify each edit still compiles, re-format |
| `explain` | A rule's rationale and examples, or what an option does |
| `config` | Inspect, check, diff, distil and sync the `.editorconfig` |
| `baseline` | Accept the findings a repository has today, so the gate is about new ones |
| `report`, `trend` | Re-render a stored SARIF; show findings and metrics over time |
| `lsp`, `mcp` | Language Server Protocol and Model Context Protocol, over stdio |
| `hooks` | Install the pre-commit hook |

### Exit codes

| Code | Meaning |
|---:|---|
| 0 | Nothing to do |
| 1 | The gate failed |
| 2 | Formatting or arrangement is needed (`--check`) |
| 3 | The configuration is wrong |
| 4 | The project or compilation could not be loaded |
| 5 | Internal error — including the formatter's own safety net tripping |
| 130 | Cancelled |

## Configuration

Everything is configured from `.editorconfig`. Standard EditorConfig and Roslyn keys work as they
are; Skala's own 436 formatting options are `skala_*`:

```ini
root = true

[*.cs]
indent_style = space
indent_size = 4
max_line_length = 120

skala_wrap_arguments_style = chop_if_long
skala_keep_existing_declaration_block_arrangement = false
skala_place_simple_initializer_on_single_line = false

dotnet_diagnostic.SK1084.severity = warning
```

`skala config check .` reports which options are honoured, which contradict each other, and which
keys Skala does not know. `skala config explain <file>` shows the effective option set for one file
with the `file:line` each value came from.

Gates live in `skala.jsonc`:

```jsonc
{
  "gates": {
    "local": { "maxSeverity": "error" },
    "pr":    { "since": "origin/main", "newIssues": 0, "maxSeverity": "warning" },
    "ci":    { "baseline": ".skala/baseline.sarif", "newIssues": 0,
               "metrics": { "duplication": 10.0, "cognitiveComplexity": 20 } }
  }
}
```

Adopting Skala in a repository that already has findings does not mean fixing them all first —
`skala baseline create --apply` accepts what exists today, and the gate is then about what you add.

## What you get

- **A formatter that wraps.** Not just whitespace between tokens: a line-fitting engine, so
  `max_line_length` with a wrapping style is a thing you can actually configure.
- **336 rules** across correctness, modernization, async, performance, security, design and
  maintainability. 162 carry a fix; 107 of those are safe enough for `skala fix` to apply unasked.
- **Real analysis.** Rules run over a Roslyn compilation loaded from a binary log, an MSBuild
  workspace, or loose files, and each rule declares which of those it needs.
- **SARIF output**, so findings land in GitHub code scanning or anything else that reads it.
- **Baselines, `--since` scoping, duplication detection, cognitive complexity** and a recorded
  history you can plot.
- **Editor and agent integration** over LSP and MCP.

Two things Skala will not do, with no flag to turn them off:

- It never writes a file whose token stream differs from the input's. If a rewrite would change the
  program, the file is left alone and the run reports it.
- It never formats a file it could not parse.

## Contributing

```bash
git clone https://github.com/Rikarin/Skala.git
cd Skala
./build.sh            # restore, compile, test
./build.sh Lint       # Skala checks its own source with itself
```

Some checks need the [ReSharper command line tools][jb] installed, which Skala uses as the reference
formatter when regenerating its conformance corpus. Ordinary contributions do not.

Issues and pull requests are welcome at
[github.com/Rikarin/Skala](https://github.com/Rikarin/Skala/issues).

## Versioning

The version is measured, not declared. Each release builds the previous one alongside the candidate
and compares five surfaces — formatted output over a reference corpus, the rule catalogue, the exit
codes, the SARIF shape and the option registry — and the bump is whatever the largest difference
implies. A formatting change is a minor bump at minimum, because downstream it is a repository-wide
diff.

`2.0.0` is the first stable release. Every push to `master` publishes a `2.0.0-alpha.N` prerelease
beside it, where `N` counts commits — those are opt-in, found only with `--prerelease`.

## License

[Apache-2.0](LICENSE).

[jb]: https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html

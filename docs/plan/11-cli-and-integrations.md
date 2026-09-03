# 11 — CLI and Integrations

The CLI is the contract (ADR-010). Everything else — LSP, MSBuild, MCP, hooks — is a different way
to reach the same code, and none of them may have behaviour the CLI does not.

## Command surface

```
skala format   [paths…]  [--check] [--diff] [--range a:b] [--staged] [--since <ref>]
                         [--arrange[=syntactic|full]] [--reflow] [--no-xmldoc] [--quiet]
                         [--define A,B] [--load binlog|workspace|loose|none]      ← M5
skala arrange  [paths…]  [--check] [--include <ids>] [--exclude <ids>]
                         [--load auto|binlog|workspace|loose|none] [--project <file>]
skala check    [paths…]  [--gate <name>] [--since <ref>] [--baseline <file>]
                         [--load binlog|workspace|loose] [--binlog <file>] [--project <file>]
                         [--require-fresh-binlog] [--rules <ids>] [--include-hints]
                         [--show-suppressions] [--no-formatting] [--define A,B]
                         [--profile]
skala fix      [paths…]  [--safe] [--include <ids>] [--dry-run] [--load …] [--binlog <file>]
                         [--project <file>]
skala verify   [paths…]  [--fix] [--format agent|json|plain] [--load …] [--project <file>] [--define A,B]
                         [--since <ref>] [--baseline [<file>]]
skala explain  <ruleId | optionKey>
skala rules    list|docs                                                          ← M5
skala config   explain|diff|distill|fix|check
skala baseline create|update|prune
skala report   [--format …] [--input <sarif>]
skala trend
skala cache    stats|clear
skala mcp
skala lsp
skala hooks    install
```

Global: `--format`, `--output`, `--no-color`, `--verbose`, `--config <file>`, `--option k=v`,
`--jobs n`, `--no-cache`.

`IDE1006` is the one hosted Roslyn code-style diagnostic with a Skala fix path. It is deliberately
outside `--safe`: `skala fix --include IDE1006` infers workspace mode and opens the MSBuild solution,
while an explicitly incompatible `--load=loose|binlog` is rejected. The command
applies Roslyn's own rename action one symbol at a time, so references in other files and projects
move with the declaration. The pass refuses `loose` and `binlog`, refuses a partially failed
workspace, previews without writing under `--dry-run`, rebinds the changed solution, and formats
only the documents the rename touched. Every proposed rename is compiled before it joins the batch;
a suggestion that would introduce a binding error is reported and skipped while the remaining
IDE1006 findings continue. The written batch is refused if a declaration or reference change would
touch a formatter-off region. `format` and `arrange` never rename symbols.

`arrange` and `verify` default to `--load=auto`: one unambiguous `.slnx`, `.sln`, or `.csproj` is
loaded as a workspace, and a repository with no target stays on the loose fast path. Multiple
targets require `--project`. Once auto discovers a target, a workspace load failure is fatal rather
than a silent fallback to a semantics-free result. This shared default is part of the gate contract:
the `skala arrange <path>` instruction emitted by `verify` must run the same semantic rules that
produced the finding.

⚠ **`skala daemon`, `--no-daemon` and `SKALA_NO_DAEMON` are removed, not deprecated.** The daemon is
deleted (§ "The daemon, and why it is gone"), so the flag would have had nothing to turn off. A flag
accepted and ignored is a flag that lies to the next person who reads a script containing it; there
was no released version carrying it and nothing in this repository passed it, so it goes cleanly.

⚠ **What exists after M5 and M4**, because a command surface that lists intent as if it were
behaviour is a surface nobody can trust: `format`, `arrange`, `check`, `verify`, `fix`, `explain`,
`rules`, `config`, `cache`, `mcp`, `lsp` and `hooks`. Still intent: `baseline`, `report`, `trend`
and every `--since`/`--baseline` flag (M6). `--jobs` remains `format`-only; `--no-cache` is now on
`check`, `verify` and `format`.

⚠ **`arrange` is a separate verb from `format`, and is sequential where `format` is parallel.**
`format` changes whitespace, needs no project, and is reversible by reformatting; `arrange` changes
the tree, wants a `Compilation`, costs a re-bind per changed file, and is reversible by
`git revert` ([06](06-arrangement-and-syntax-styles.md) § "The line between `format` and `arrange`").
`skala format --arrange[=syntactic|full]` runs both; bare `--arrange` means `syntactic`, which is the
subset that works on a loose file an agent just wrote. `arrange` says out loud how many files it saw
no compilation for, because a syntactic run quietly doing a third of the catalogue looks exactly like
a full run that found little to do.

⚠ **`arrange --aggressive`** is parenthesis removal and nothing else. The oracle's own cleanup profile
performs it and Skala's default does not; the gate costs a measured 4.02 points of changed-span
agreement (SK-DIV-0014), which is the price of the caution rather than a hidden disagreement.

⚠ **`format --no-xmldoc`** switches the documentation-comment sub-formatter off, and it is the one
flag on this page whose polarity is a correction rather than a design. It shipped as `--xmldoc`, on
the reading that `jb cleanupcode` honours none of the `resharper_xmldoc_*` family the export
configures — which turned out to be a fact about the profile pinned in `OracleProfile.FormatOnly`
rather than about the tool. Rider formats doc comments; the profile does not; **not** formatting
them was the divergence, so the default flipped and the flag inverted (SK-DIV-0006).

It is a flag rather than `skala_xmldoc_wrap_lines = false` deliberately. That key means "do not
wrap long lines" — with it false the sub-formatter still re-indents, still collapses blank lines
between tags and still inserts the marker space, which is what Rider does with it false too — and
JetBrains' `.editorconfig` index does not document it at all. Overloading a ReSharper key with a
meaning ReSharper does not give it is the class of mistake this default is undoing.

⚠ It used to disqualify the daemon, in the other direction: the daemon protocol carried no xmldoc
switch and the daemon formatted with the default, so `--no-xmldoc` fell through to the CLI rather
than being served by a process that would have quietly done *more* than it was asked. With the daemon
gone there is one path and the asymmetry with it.

⚠ **`--verbose` is not implemented on `check`, and the way it fails is worse than not being there.**
`check` takes `<paths>` as a variadic argument, so an unrecognised flag is bound as a *path*:
`skala check --load loose --verbose` in a clean repository reports
`SK9023: no C# files were found under the requested paths` and exits 4, and
`skala check --load loose --verbose src` quietly analyses `src` while ignoring the flag. The same
holds for any typo'd option and for `skala check no-such-directory`, which is exit 4 rather than a
message naming the path that does not exist. Found by running the installed tool, not by reading the
parser. Two separate fixes are wanted — a global `--verbose` that exists, and an argument binder that
rejects a token beginning with `-` — and neither is in this milestone.

⚠ **What exists after M5**, because a command surface that lists intent as if it were behaviour is a
surface nobody can trust: `format`, `check`, `verify`, `fix`, `explain`, `rules`, `config`, `cache`,
`mcp`, `lsp` and `hooks`. Still intent: `arrange` (M4), `baseline`, `report`, `trend` and
every `--since`/`--baseline` flag (M6). `--jobs` remains `format`-only; `--no-cache`
is now on `check`, `verify` and `format`.

⚠ **`--define` is on `format`, not only on `check`.** SK-DIV-0004 is a *formatting* limitation —
without symbols Roslyn hands back every `#if DEBUG` body as disabled text and the formatter correctly
refuses to touch it — so the symbols have to reach the formatter, and `--load` on `format` is the
shorthand for "take them from what the build compiled". They are part of the LSP server's cache key
too: the same file formatted for Debug and for Release are two answers.

### Path resolution

No paths ⇒ the repository root (nearest `.git`), filtered by `skala.jsonc`'s `include`/`exclude`.
Paths may be files, directories, globs, `.sln`/`.slnx`/`.csproj`, or `-` for stdin (formatting only,
with `--stdin-path` supplying the name that decides the `.editorconfig` section). ⚠ `.gitignore` is
respected by default; `--no-ignore` overrides. A formatter that reformats `artifacts/` is a formatter
that is quietly very slow.

`--staged` reads the git index and formats only staged files, writing back to both the worktree and
the index — the correct behaviour for a pre-commit hook, and one that is easy to get wrong in a way
that loses uncommitted work. It refuses to run with unstaged changes to a staged file unless
`--staged=worktree` is given.

## The daemon, and why it is gone

⚠ **Deleted.** There was a per-repository format daemon here — started lazily, holding parsed trees
keyed by content hash, spoken to over a Unix domain socket by a NativeAOT thin client
(`Tools/Rikarin.Skala.Client`) across a private length-prefixed JSON protocol
(`Tools/Rikarin.Skala.Protocol`). It worked, and it was measured: a warm single-file
`format --check` went from 66.9 ms to 8.65 ms.

It existed for one budget — [13](13-performance.md)'s 40 ms warm single-file row — and that budget
existed for a post-edit agent hook that fired on every file write and had to be invisible. **There is
no such consumer.** Skala runs ahead of test suites that take about twenty minutes. The budget is
withdrawn (doc 13 § "Budgets"), and everything built to meet it went with it.

What it cost, stated once, because the trade is the useful part:

- **A second implementation of one command shape.** `DaemonUse.TryFormat` re-implemented the
  reporting, the file writing and the exit codes for a single named file with no `--staged`, no
  `--range` and no overrides. Two implementations of "what does `format --check` print" is one more
  than the number that can be right, and holding them in step needed a test that ran both binaries.
- **An asymmetry that had to be found before it could be fixed.** The protocol carried no xmldoc
  switch, so the daemon could not serve `--no-xmldoc` and had to refuse it. A correct refusal — but
  it is a shape the CLI accepted and the daemon did not, which is the kind of divergence that is
  invisible until somebody's comments get rewrapped.
- **A stale-build hazard, and then a guard for it.** The protocol version was a *wire* version; the
  wire shape almost never moved and the formatter moved constantly. Rebuild Skala, leave the daemon
  up, and every `skala format` kept answering with the old build's bytes — for ever, because the
  30-minute idle timer was refreshed by every request. Measured cost: two agents, about forty minutes
  each, on one day. The fix (`BuildIdentity`, fingerprinting module MVIDs behind a length-and-mtime
  stamp, refusing and stopping on a changed build) landed and is now deleted with what it guarded.
- **A kernel path limit.** `struct sockaddr_un` caps a socket path at 104 bytes on macOS and 108 on
  Linux; `<repo>/.skala/daemon.sock` exceeded that past about eighty-five characters and the daemon
  died there with **exit code 0** while every later format silently took the cold path.
- **Two binaries and a RID-specific package.** The client owned the name `skala`, so the tool became
  `skala-tool`; they had to ship in one directory because adjacency was how the client found the
  tool; and a native command cannot be `Runner="dotnet"`, so the `dotnet tool` package was
  RID-specific with a wrapper package naming its per-RID siblings.

Both of the correctness rules it was held to are now trivially true, which is the point: there is one
path, so there is nothing for a second one to disagree with.

`Tools/Rikarin.Skala.Server` survives, holding what never belonged to the daemon: the LSP server
below, the `FormatService` cache behind it, and `skala hooks install`.

## LSP

`skala lsp` — stdio, and deliberately four capabilities wide:

| Request | Behaviour |
|---|---|
| `textDocument/formatting` | full-file edits |
| `textDocument/rangeFormatting` | full-file fit, edits filtered to the range ([04](04-formatting-engine.md)) |
| `textDocument/diagnostic` (pull) | findings for the file, `loose` or the loaded compilation |
| `textDocument/codeAction` | the fixes, as `quickfix` actions with the SARIF's `artifactChanges` |

Enough for VS Code, Neovim, Helix, Zed and anything else with a generic LSP client to get Skala
formatting on save and Skala squiggles inline. ⚠ Range formatting is a *filter over a whole-file
fit* and never a fit of the range, and the test asserts that a range's edits are a strict subset of
the file's: the column a construct is measured against depends on the indentation stack above it, so
a fit that starts half way down a file has to guess, and the guess is what makes "format selection"
and "format document" disagree.

⚠ **Rider is not an LSP consumer for formatting, and does not need to be.** Rider already implements
this `.editorconfig` — it is where the file came from. The Rider integration is: nothing. That is the
point of ADR-001, and it is worth stating so that nobody builds a Rider plugin to solve a problem
that does not exist. If Rider's behaviour and Skala's diverge, the fix is in Skala
([12](12-conformance-and-testing.md)), not in a plugin.

For the *analysis* half in Rider, the answer is the analyzer package: `Rikarin.Skala.Rules` as a
`PackageReference` puts `SK` diagnostics in Rider's editor, in the build, and in CI, through the
mechanism the IDE already has (ADR-006).

## MSBuild

✅ **Built.** `Rikarin.Skala.MSBuild` adds one target that runs after `Build`:

```xml
<PropertyGroup>
  <SkalaEnabled Condition="'$(SkalaEnabled)' == ''">true</SkalaEnabled>
  <SkalaMode>check</SkalaMode>   <!-- check | format | off -->
  <SkalaGate>local</SkalaGate>
</PropertyGroup>
```

It is **off in the inner loop by default for `check`** and on for `format --check`. The reasoning:
analysis costs minutes, `dotnet build` is run constantly, and a build tool that triples build time
gets disabled by whoever is in a hurry — permanently. CI sets `SkalaMode=check`; developers get
formatting verification only, which is seconds.

The analyzer package (`Rikarin.Skala.Rules`) is the *other* half and it does run in every build,
because that is what analyzers are for and the compiler already pays for the tree walk.

### The knobs, and what each is for

| Property | Default | What it does |
|---|---|---|
| `SkalaEnabled` | `true` | The master switch. `false` and the target never runs. |
| `SkalaMode` | `off` | `off` runs `format --check`; `check` runs `skala check --gate`; `format` rewrites files. |
| `SkalaGate` | `ci` under CI, else `local` | Which gate `check` evaluates. |
| `SkalaTreatFindingsAsErrors` | `false` | Whether a finding fails the build. |
| `SkalaPaths` | `$(MSBuildProjectDirectory)` | What to run over. |
| `SkalaArguments` | — | Appended verbatim, so a new CLI flag needs no new property. |
| `SkalaToolPath` | — | An explicit path to `skala`, which is what a self-contained CI image sets. |
| `SkalaRequireTool` | `false` | Whether an absent tool is an error rather than a warning. |
| `SkalaSarifOutput` | — | `--output` for `check`. |

⚠ **Per project, not per repository, and it removes a problem rather than accepting one.** The target
runs over `$(MSBuildProjectDirectory)`. One repository-wide run coordinated to happen once per build
was considered: MSBuild has no per-build hook, so "once" has to be faked with a marker file or with a
task holding `RegisteredTaskObjectLifetime.Build` state, and both are wrong under a non-incremental
build, under multi-targeting, and under two solutions built in one process. Per-project needs no
coordination and puts the diagnostic on the project that owns the file. ⚠ Do not point `SkalaPaths`
at the `.csproj`: `FormatCommand.Collect` treats an existing file as a file to format and would hand
the formatter an XML document.

⚠ **There is no MSBuild task.** [02](02-repository-layout.md) § "Package boundaries" has the
reasoning; the short form is that everything a task would do is start `skala` and read an exit code,
which `Exec` already does, and the first line of this document says the CLI is the contract.

⚠ **No `SK` id is emitted from MSBuild.** The `SK9000` register is in [08](08-rule-catalogue.md) and
ADR-012 makes every entry permanent. The tool that finds the problem prints its own ids; a
second, MSBuild-side numbering of "the tool said no" would be an id with no rule behind it.

### Four things that had to be measured

The target reads exit codes, and three of the four ways of getting that wrong are silent.

1. ⚠ **`ContinueOnError` alone is not enough — `IgnoreExitCode` is what hands over the code.** With
   `ContinueOnError` the build continues, but the task has *failed*, and MSBuild does not gather the
   output parameters of a failed task. `_SkalaExit` came back empty, every comparison was an empty
   string against an empty string, and a tree that merely needed formatting was reported as
   "could not complete (exit )".
2. ⚠ **`CallTarget` cannot see properties the calling target set.** It builds the project again
   rather than continuing the current execution. Sharing one exit-code ladder between three modes
   that way printed "Skala: ." The modes are mutually exclusive, so it is one target now.
3. ⚠ **"The tool is missing" cannot be read from the exit code alone.** A missing executable is 127
   under `sh` and 9009 under `cmd`, but an *unrestored tool manifest* makes `dotnet tool run skala`
   exit **1** — the same code `format --check` uses for "this file needs formatting". Detection also
   reads the console output.
4. ⚠ **The tool manifest is not always in `.config/`.** Measured on SDK 10.0.400,
   `dotnet new tool-manifest` wrote `dotnet-tools.json` into the repository root and created no
   `.config/` at all. Resolution looks in both places, `.config/` first.

And one that is not about exit codes: an **absent tool is a warning, never an error**, unless
`SkalaRequireTool=true`. This package is imported by a `Directory.Build.props` that every project
inherits, including on a machine that has just cloned the repository and not run
`dotnet tool restore`. Failing there means the first thing Skala does to a new contributor is break
`dotnet build`, and the second is get removed.

## Git hooks

```bash
# .git/hooks/pre-commit (or via the repository's hook manager)
skala format --staged --quiet || exit 1
skala verify --format=plain  || exit 1
```

⚠ **The 500 ms budget for a typical commit is withdrawn** along with the rest of doc 13's table. A
pre-commit hook formatting a handful of files pays one cold process start, 120–200 ms before `Main`,
and then the formatting; that is a fraction of a second either way and nothing measures it. It is
also not the tight case it was written as — the commit that follows is going to wait on a test suite.

`skala hooks install` writes them, detecting an existing hook manager rather than clobbering it.
⚠ husky, lefthook, `pre-commit` and a `core.hooksPath` pointing elsewhere are all detected, and the
last matters most: git would never run `.git/hooks/pre-commit`, so writing one there looks installed
and is inert. It also refuses to overwrite a `pre-commit` it did not write, and says what to add
instead. Without `--apply` it only says what it would do.

## CI

```yaml
- run: skala config diff --canonical          # exits 3 if the managed block has been edited
- run: dotnet build -bl:artifacts/build.binlog
- run: skala check --load=binlog --binlog artifacts/build.binlog
                   --gate ci --since origin/${{ github.base_ref }}
                   --format github --output .skala/report.sarif
- uses: github/codeql-action/upload-sarif@v3
  if: always()
  with: { sarif_file: .skala/report.sarif }
```

Three properties this shape has: the build happens once and both compiles and feeds analysis; the
annotations land on the PR diff through the standard mechanism; and the SARIF is uploaded even on
failure, so the findings are visible in the Security tab rather than only in a log.

For NUKE, matching Vixen's build:

```csharp
Target Lint => _ => _
    .DependsOn(Compile)
    .Executes(() => SkalaTasks.Check(s => s
        .SetLoad("binlog").SetBinlog(BinlogFile)
        .SetGate(IsServerBuild ? "ci" : "local")
        .SetSarif(ArtifactsDirectory / "skala.sarif")));
```

## Adoption path for an existing repository

The order matters, because the wrong order produces a 40 000-line first diff and a reverted commit.

1. `skala config sync --apply` — writes the canonical block and preserves whatever `.editorconfig`
   was already there, verbatim, below the `skala:local begin` marker
   ([03](03-configuration-model.md) § "Canonical distribution across repositories"). On a repository
   with existing path-scoped sections this is the whole migration, and its git diff is reviewable.
2. `skala config check` — read the tier report and the contradictions. Fix the config, not the code.
3. `skala format --check --diff | head -200` — look at what it *would* do, on a branch.
4. `skala format` in one commit, alone, with a message saying so. Add its SHA to
   `.git-blame-ignore-revs`. ⚠ This commit will be enormous and must contain nothing else.
5. `skala verify` — formatting, arrangement, modernization, naming, and analysis findings in the
   agent-facing completion gate. It runs `format --check`, `arrange --check`, and
   `check --gate=local`; arrangement remains its own writing command because its structural rewrites
   require an explicit decision.
6. `skala baseline create --apply` — accept the current analysis findings. ⚠ Commit
   `.skala/baseline.sarif`. It is the one thing under `.skala/` that is not scratch, and the marker
   Skala writes there un-ignores it by name for exactly that reason; everything else in the
   directory — `cache/`, `crash/`, `report.sarif`, `history.jsonl` — stays ignored. Until M9 the
   marker was a bare `*` and this step needed `git add -f`.
7. Turn on the `pr` gate with `--since`. New code is clean; old code is a backlog, not a blocker.
8. ⚠ **Point the agents at the baseline too**: `skala verify --baseline --since=origin/master`.
   `verify` is the one command doc 10 tells an agent to run, and until M9 it had neither flag — so
   on the first repository to adopt Skala it reported **778 findings needing a decision**, every
   run, for ever, with no way to be told what step 6 had just accepted. Both scopings compose the
   same way they do on `check`: a finding is to do only if it is absent from the baseline *and* on
   a line the branch touched.
9. Burn the baseline down, rule by rule, with `skala fix --safe --include <id>`.

Steps 4 and 6 are the two that make adoption survivable on a 1.35 M-line tree, and both of them are
"accept the present, gate the future". ⚠ Step 8 is what extends that promise to the agent-facing
surface: a baseline the one command an agent runs cannot read has accepted nothing as far as the
agent is concerned.

## Distribution

| Channel | Artefact |
|---|---|
| `dotnet tool install -g Rikarin.Skala.Cli` | the tool |
| `dotnet tool install` in a local manifest | pinned per repository — ⚠ the recommended form, because a formatter whose version drifts between developers reformats the tree back and forth |
| `PackageReference Rikarin.Skala.Sdk` | ⚠ **the one-line adoption**: the three below, together |
| `PackageReference Rikarin.Skala.Rules` | analyzers in build and IDE |
| `PackageReference Rikarin.Skala.MSBuild` | the build target |
| `PackageReference Rikarin.Skala.Canonical` | the canonical `.editorconfig`, and a 5 ms build-time check that the repository is on it |
| GitHub Releases | standalone NativeAOT binaries per RID, for CI images and hooks — `./build.sh Native` |
| GitHub Action | a thin wrapper that installs the pinned version and runs it — ⚠ not built |

Version pinning is a correctness feature here, not a preference. Two Skala versions with different
formatting behaviour on one repository is a merge conflict generator.

### ⚠ Adopting the analyzers without a flag day

**"The one-line adoption" was, on the first repository to try it, the one line that could not be
taken.** The Sdk brings `Rikarin.Skala.Rules`; on Vixen that was **16 `SK3002`** and **58 further
`CS0246`/`CS0234`** from the projects downstream of the ones that failed. Not because `SK3002` ships
at `error` — it ships at `warning` — but because real repositories set `TreatWarningsAsErrors`.

Doc 09's mechanism for exactly this is "accept the present, gate the future", and it does not reach:
`.skala/baseline.sarif` is read by `skala check` and by nothing else, so an analyzer package has no
idea a baseline exists. The escape hatch did not work either. ⚠ **`ExcludeAssets="analyzers"` on the
metapackage is silently ineffective**: `ExcludeAssets` governs the assets of the package it is
written on, the Sdk has none, and the analyzer arrives from a transitive dependency whose nuspec
entry says `include="All"` — which is load-bearing, since `PrivateAssets="none"` is the only reason
any of the three dependencies delivers anything at all. Reproduced against the packed package:
`project.assets.json` records `Rikarin.Skala.Rules` with an **empty asset list** and `SK3002` still
fires three times.

So the Sdk honours two properties itself, rather than leaving it to NuGet:

| Property | Default | What it does |
|---|---|---|
| `SkalaRulesAsErrors` | **`false`** | ⚠ **The answer to the flag day.** Skala's own ids are added to `WarningsNotAsErrors`, so the diagnostics fire at their real severities, in the build log and in Rider, and do not turn a warning into a build error. Nothing is silenced and nothing is measured differently. Scoped to `SK` ids — it never touches `TreatWarningsAsErrors` itself, because a package that quietly made a repository's *other* warnings non-fatal would be doing the invisible thing this whole design objects to |
| `SkalaRulesEnabled` | `true` | The total opt-out: the `Analyzer` item is removed before `CoreCompile`, so the analyzer never reaches `csc`. This works where `ExcludeAssets` does not, and it works matched on the assembly name, so a repository that references `Rikarin.Skala.Rules` directly for its own reasons is unaffected |

The adoption path is then the one doc 09 designed, with no day on which the tree does not build:

1. reference the Sdk — the diagnostics appear, the build stays green
2. `skala baseline create --apply`, and commit `.skala/baseline.sarif`
3. `skala check --gate=ci` gates the future against that baseline
4. burn the backlog down with `skala fix --safe --include <id>`
5. `<SkalaRulesAsErrors>true</SkalaRulesAsErrors>` — one line, one commit, a decision somebody makes
   on purpose

Verified by packing the real packages into a local feed and building a real consumer with
`TreatWarningsAsErrors`: default → 3 `SK3002` **warnings**, exit 0; `SkalaRulesAsErrors=true` → the
same 3 as **errors**, exit 1; `SkalaRulesEnabled=false` → no `SK` diagnostic at all, exit 0.

### The tool package is one portable binary

⚠ **It used to be two, and RID-specific.** The command was the NativeAOT thin client, which .NET 10
packs as `tools/any/<rid>/` with `Runner="executable"`; the full tool shipped beside it as
`skala-tool` because adjacency was how `Fallback.Locate` found it, and packing more than one RID
also emitted a RID-agnostic wrapper package listing the per-RID package ids — publishing that wrapper
without every package it names is an install that fails on whichever platform is missing.

With the client deleted, the command is an ordinary managed entry point again:

```
tools/net10.0/any/
├── DotnetToolSettings.xml     <Command Name="skala" EntryPoint="skala.dll" Runner="dotnet" />
├── skala.dll                  the tool
└── (78 more files)            Roslyn, MSBuild, SARIF, the build hosts
```

One .nupkg, every platform, and `./build.sh Pack` takes no `--rids`.

⚠ **It is also less than half the size: 33.4 MB → 15.2 MB**, measured on the same machine before and
after, 89 files either way. That was not the goal and is worth stating, because it is the cost of the
old arrangement showing up somewhere it was never accounted: the RID-specific package carried
ReadyToRun-compiled native images of Roslyn *and* the 2.9 MB AOT binary, where the portable package
carries IL. Two other things that were in it and are not, both removed deliberately: the client's
13 MB `.dSYM` bundle, which `PackTool`'s publish glob collected and which nothing in a tool install
can read, and 6.6 MB of localised Roslyn resources, which `InvariantGlobalization` does not suppress
on its own — `SatelliteResourceLanguages` does.

⚠ One measurement from the two-binary arrangement is worth keeping, because it is the kind of thing
that is assumed and is load-bearing: **`Environment.ProcessPath` resolves the install symlink.**
`dotnet tool install` puts a symlink in `~/.dotnet/tools/`, and the process reported
`~/.dotnet/tools/.store/rikarin.skala.cli/1.0.0/…/tools/any/osx-arm64/skala`, not the symlink. Had it
reported the symlink, "beside my own executable" would have been `~/.dotnet/tools/`, where
`skala-tool` was not, and the fallback would have failed for every installed user while working
perfectly in the repository.

### Verified by installing it

The measurements above come from a run that installed the packages from a local feed into a fresh
`git init` and used them. Reproducing it is `./build.sh Pack`, then, against
`artifacts/packages` as a source: `dotnet tool install`, `skala config sync --apply`,
`skala format`, `skala check --load loose`, `skala verify`, `skala explain SK1010`,
`PackageReference Rikarin.Skala.Sdk` and `dotnet build`, and `dotnet tool uninstall`.

⚠ **Historical**, from the run that installed the two-binary package, reference machine, 50 runs in
a shell loop divided by N, spawn floor 4.9 ms measured the same way. The warm rows describe a daemon
that no longer exists; the disk figures do not:

| | measured |
|---|---:|
| ~~warm single-file `format --check`, installed tool, shallow repository~~ | ~~**8.04 ms**~~ |
| ~~the same, in a repository nested 127 characters deep~~ | ~~**13.05 ms**~~ |
| the tool package, installed on disk | 153 MB |
| left behind after `dotnet tool uninstall` | nothing |

The deep-path case is still exercised by the smoke test, without the socket assertion: a repository
nested 127 characters deep has other ways to go wrong and the case costs one `mkdir -p`.

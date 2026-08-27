# 11 — CLI and Integrations

The CLI is the contract (ADR-010). Everything else — daemon, LSP, MSBuild, MCP, hooks — is a
different way to reach the same code, and none of them may have behaviour the CLI does not.

## Command surface

```
skala format   [paths…]  [--check] [--diff] [--range a:b] [--staged] [--since <ref>]
                         [--arrange[=syntactic|full]] [--reflow] [--quiet]
                         [--define A,B] [--load binlog|workspace|loose|none]      ← M5
skala arrange  [paths…]  [--check] [--include <ids>] [--exclude <ids>]
skala check    [paths…]  [--gate <name>] [--since <ref>] [--baseline <file>]
                         [--load binlog|workspace|loose] [--binlog <file>] [--project <file>]
                         [--require-fresh-binlog] [--rules <ids>] [--include-hints]
                         [--show-suppressions] [--no-formatting] [--define A,B]
                         [--resharper-severities] [--profile]
skala fix      [paths…]  [--safe] [--include <ids>] [--dry-run] [--load …] [--binlog <file>]
skala verify   [paths…]  [--fix] [--format agent|json|plain] [--load …] [--define A,B]
skala explain  <ruleId | optionKey>
skala rules    list|docs                                                          ← M5
skala config   explain|diff|distill|fix|check
skala baseline create|update|prune
skala report   [--format …] [--input <sarif>]
skala trend
skala cache    stats|clear
skala mcp
skala lsp
skala daemon   status|stop
```

Global: `--format`, `--output`, `--no-color`, `--verbose`, `--config <file>`, `--option k=v`,
`--jobs n`, `--no-cache`, `--no-daemon`.

⚠ **What exists after M5**, because a command surface that lists intent as if it were behaviour is a
surface nobody can trust: `format`, `check`, `verify`, `fix`, `explain`, `rules`, `config`, `cache`,
`mcp`, `lsp`, `daemon` and `hooks`. Still intent: `arrange` (M4), `baseline`, `report`, `trend` and
every `--since`/`--baseline` flag (M6). `--jobs` and `--no-daemon` remain `format`-only; `--no-cache`
is now on `check`, `verify` and `format`.

⚠ **`--define` is on `format`, not only on `check`.** SK-DIV-0004 is a *formatting* limitation —
without symbols Roslyn hands back every `#if DEBUG` body as disabled text and the formatter correctly
refuses to touch it — so the symbols have to reach the formatter, and `--load` on `format` is the
shorthand for "take them from what the build compiled". The daemon protocol carries them too, and
they are part of its cache key: the same file formatted for Debug and for Release are two answers.

⚠ `skala daemon` has no `start`. The daemon is started lazily by whatever needs it and exits after
thirty minutes idle; a `start` verb invites a person to run one by hand and then wonder why their
editor is using a different one. `skala daemon run` is the foreground form, for a supervisor and for
the tests.

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

## The daemon

Per-repository, started lazily on the first command, exits after 30 minutes idle. It holds parsed
trees keyed by content hash, the option registry, resolved `.editorconfig` chains, and — for
`check` — loaded compilations and metadata references.

⚠ "Lazily" means the first single-file `skala format` in a repository finds no socket, **does the
work itself**, and leaves a daemon behind for the next one. It does not wait for the daemon it
starts: waiting would put the daemon's own start — process, JIT, first configuration resolution —
inside the very budget the daemon exists to meet, and lazy starting would then feel slower than no
daemon at all. Measured: the first `format --check` of a 615-line file is 310 ms and every one after
it is 70 ms. The start is skipped entirely unless the running executable is `skala` itself, so a
`dotnet run`, a test host, or anything using the formatter as a library never spawns one.

- Socket: a unix domain socket (named pipe on Windows) under `.skala/daemon.sock`, permissions
  0600.
- Protocol: private, length-prefixed JSON, versioned by exact match. A client that meets a daemon of
  a different version kills it and starts its own. No negotiation, no compatibility window.
- Correctness rule: **every command must work identically with `SKALA_NO_DAEMON=1`.** The daemon is
  only allowed to make things faster. It holds the results of the same `CSharpFormatter` the CLI
  calls and never a second implementation of anything, and the suite compares the two answers byte
  for byte.
- The daemon never watches the filesystem. It is asked; it does not observe. ⚠ The way that is made
  safe is the *cache key*: a file's content hash together with the identity of every
  `.editorconfig` above it, so there is no invalidation to get wrong and no window in which a stale
  answer is served. A config edited under a running daemon is picked up the same way — a reloaded
  document carries a new version stamp and the stamp is in the key.
- ⚠ Every failure on the client side is a fallback and never an error: absent, stale, or of another
  protocol version, and the CLI does the work itself. An optimisation that can fail a pre-commit hook
  is not one. A socket file left by a crashed daemon is probed before it is unlinked, so recovering
  is not the same as stealing a live daemon's socket.
- ⚠ The daemon serves exactly one shape — a single named file, no `--staged`, no `--range`, no
  overrides — because that is the shape the budget is about. A whole-corpus run is already parallel
  and is bounded by the formatter rather than by process start; serving more shapes would mean a
  second implementation of the reporting, the writing and the exit codes.

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

`Rikarin.Skala.MSBuild` adds a target that runs after `Build`:

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

## Git hooks

```bash
# .git/hooks/pre-commit (or via the repository's hook manager)
skala format --staged --quiet || exit 1
skala verify --format=plain  || exit 1
```

Budget: under 500 ms for a typical commit (a handful of files, warm daemon). [13](13-performance.md)
§ "Startup" is what makes that possible, and it is why NativeAOT for the CLI front end is on the
table. ⚠ M3 measures a warm single-file format at 60–70 ms against a 40 ms budget, of which
essentially all is the client's own process start — `skala daemon status`, which does no work at
all, is the same 60 ms. The daemon itself answers in single-digit milliseconds; nothing in the
budget is left for Skala to optimise, and the fix is NativeAOT for the client, which is not done.

`skala hooks install` writes them, detecting an existing hook manager rather than clobbering it.
⚠ husky, lefthook, `pre-commit` and a `core.hooksPath` pointing elsewhere are all detected, and the
last matters most: git would never run `.git/hooks/pre-commit`, so writing one there looks installed
and is inert. It also refuses to overwrite a `pre-commit` it did not write, and says what to add
instead. Without `--apply` it only says what it would do.

## CI

```yaml
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

1. `cp editor_config_template .editorconfig`, add `root = true`.
2. `skala config check` — read the tier report and the contradictions. Fix the config, not the code.
3. `skala format --check --diff | head -200` — look at what it *would* do, on a branch.
4. `skala format` in one commit, alone, with a message saying so. Add its SHA to
   `.git-blame-ignore-revs`. ⚠ This commit will be enormous and must contain nothing else.
5. `skala verify` — now the arrangement and modernization findings, which are a *different* commit
   or several.
6. `skala baseline create` — accept the current analysis findings.
7. Turn on the `pr` gate with `--since`. New code is clean; old code is a backlog, not a blocker.
8. Burn the baseline down, rule by rule, with `skala fix --safe --include <id>`.

Steps 4 and 6 are the two that make adoption survivable on a 1.35 M-line tree, and both of them are
"accept the present, gate the future".

## Distribution

| Channel | Artefact |
|---|---|
| `dotnet tool install -g Rikarin.Skala.Cli` | the tool |
| `dotnet tool install` in a local manifest | pinned per repository — ⚠ the recommended form, because a formatter whose version drifts between developers reformats the tree back and forth |
| `PackageReference Rikarin.Skala.Rules` | analyzers in build and IDE |
| `PackageReference Rikarin.Skala.MSBuild` | the build target |
| GitHub Releases | standalone NativeAOT binaries per RID, for CI images and hooks |
| GitHub Action | a thin wrapper that installs the pinned version and runs it |

Version pinning is a correctness feature here, not a preference. Two Skala versions with different
formatting behaviour on one repository is a merge conflict generator.

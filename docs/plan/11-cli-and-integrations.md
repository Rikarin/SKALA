# 11 — CLI and Integrations

The CLI is the contract (ADR-010). Everything else — daemon, LSP, MSBuild, MCP, hooks — is a
different way to reach the same code, and none of them may have behaviour the CLI does not.

## Command surface

```
skala format   [paths…]  [--check] [--diff] [--range a:b] [--staged] [--since <ref>]
                         [--arrange[=syntactic|full]] [--reflow] [--quiet]
skala arrange  [paths…]  [--check] [--include <ids>] [--exclude <ids>]
skala check    [paths…]  [--gate <name>] [--since <ref>] [--baseline <file>]
                         [--load binlog|workspace|loose] [--binlog <file>]
                         [--rules <ids>] [--include-hints] [--show-suppressions] [--profile]
skala fix      [paths…]  [--safe] [--include <ids>] [--dry-run]
skala verify   [paths…]  [--fix] [--format agent|json]
skala explain  <ruleId | optionKey>
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

- Socket: a unix domain socket (named pipe on Windows) under `.skala/daemon.sock`, permissions
  0600.
- Protocol: private, length-prefixed JSON, versioned by exact match. A client that meets a daemon of
  a different version kills it and starts its own. No negotiation, no compatibility window.
- Correctness rule: **every command must work identically with `SKALA_NO_DAEMON=1`.** The daemon is
  only allowed to make things faster. The conformance suite runs the entire corpus both ways and
  diffs, which is the only way this property survives contact with a cache.
- The daemon never watches the filesystem. It is asked; it does not observe. File watching is where
  daemons acquire their stale-state bugs, and the client already knows what changed.

## LSP

`skala lsp` — stdio, and deliberately four capabilities wide:

| Request | Behaviour |
|---|---|
| `textDocument/formatting` | full-file edits |
| `textDocument/rangeFormatting` | full-file fit, edits filtered to the range ([04](04-formatting-engine.md)) |
| `textDocument/diagnostic` (pull) | findings for the file, `loose` or the loaded compilation |
| `textDocument/codeAction` | the fixes, as `quickfix` actions with the SARIF's `artifactChanges` |

Enough for VS Code, Neovim, Helix, Zed and anything else with a generic LSP client to get Skala
formatting on save and Skala squiggles inline.

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
table.

`skala hooks install` writes them, detecting an existing hook manager rather than clobbering it.

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

# 10 — AI Agent Integration

## The problem, stated precisely

An agent writing C# in this ecosystem gets three things wrong, reliably, and none of them are
intelligence failures:

1. **It writes the dialect it was trained on.** The median line of C# on the internet is from 2018:
   `new List<string>()`, `x == null`, `String.Format`, block-bodied one-liners, `Task.Run` around
   synchronous work. The repository speaks C# 14. Nothing in the model's context makes the difference
   visible unless a tool says so.
2. **It cannot see its own formatting.** It emits text; whether that text matches 380 formatting
   options is unknowable to it. It will confidently claim a file is formatted.
3. **It optimises for the check passing.** Given a warning and the ability to edit, `#pragma warning
   disable` is a valid move. Given a failing gate, lowering the gate is a valid move. This is not
   malice; it is the objective. The tooling has to make the honest path the easy one and the
   dishonest path visible.

Skala's answer to each: a rule set that names the dialect gap (`SK1000`, doc
[08](08-rule-catalogue.md)), one command that answers "is this acceptable" with machine-applicable
fixes, and a gate that treats *new suppressions* as findings.

## `skala verify` — the one command

```bash
skala verify [<paths>] [--fix] [--format=agent|json]
```

It is `format --check` + `arrange --check` + `check --gate=local`, with output shaped for a model
rather than for a terminal. Arrangement remains a deliberately structural command and its rewrites
are not included in `fix --safe`: `verify` reports the exact `skala arrange <path>` command when
structural cleanup is needed. Auto-load supplies real project semantics when one workspace target
is unambiguous; loose mode runs the syntactic subset and lists the semantic arrangement rules as
skipped. It is the command that goes in `CLAUDE.md`, and its contract is deliberately narrow so it
can be memorised:

- Exit 0 means "nothing to do". Nothing else means that.
- Every finding either carries a fix or carries a one-sentence instruction. Never both, never
  neither.
- Output is bounded (see below) and ordered by actionability, not by file.
- It works with no project, no build and no network (`--load=loose`, doc [07](07-analysis-host.md)),
  so an agent that just wrote a file into a scratch directory can run it.

### Agent-shaped output

```
FORMAT  3 files need formatting — run: skala format Core/Foo.cs Core/Bar.cs Core/Baz.cs

FIXABLE 4 findings have safe automatic fixes — run: skala fix --safe
  SK1001  Core/Foo.cs:142  Use a collection expression instead of new List<int> { … }
  SK1010  Core/Foo.cs:88   Use `is not null` instead of `!= null`
  SK1030  Core/Bar.cs:31   Use `??=` instead of `x = x ?? y`
  SK1002  Core/Baz.cs:12   Use a primary constructor

ACTION  2 findings need a decision
  SK3002  Core/Bar.cs:57   Blocking on an async call (.Result) inside a lock.
          → Make the enclosing method async and await, or move the call outside the lock.
  SK2009  Core/Baz.cs:96   switch over TokenKind does not handle Kind.Raw and has no default.
          → Add the missing case, or a default that throws.

2 findings suppressed by #pragma in this change — see: skala check --show-suppressions
```

Design notes, each of which is a decision:

- **Three buckets, always in this order.** Formatting first because it is free and unconditional;
  fixable second because the next command is mechanical; decisions last because they are the only
  part that needs the model to think. An agent that reads top-down does the cheap work first and
  arrives at the hard part with a clean tree.
- **The command to run is printed, complete, with paths.** Not "run skala format" — the exact
  invocation. This removes a whole class of agent error (guessing flags) at the cost of a longer
  line.
- **Instructions are imperative and specific.** "→ Make the enclosing method async and await" is
  actionable; "consider avoiding blocking calls" is not.
- **Suppressions introduced by the current change are surfaced unprompted.** Point 3 above.
- **Bounded output.** Default cap 50 findings and 8 000 characters, truncated with an exact count of
  what was elided and the command to see the rest. An unbounded lint dump eats the context window
  that the agent needs to fix things with.

`--format=json` gives the SARIF for agents that would rather parse than read.

### The INCOMPLETE banner

A run that could not check every file it was asked to check says so **above** the three buckets,
because the buckets are a verdict and this line says how much of the tree the verdict covers
(#345). Exit 5 is the same fact as a number; before the banner, `agent` printed `OK  nothing to
do.` on exit 5 for weeks and nobody could tell from the output.

```
INCOMPLETE  1 of 754 file was not checked — could not be read: check permissions and that the path is still mounted. Everything below covers the rest.
  SK9015  Core/Locked.cs  Access to the path '…/Core/Locked.cs' is denied.
```

Decisions, each measured against a run that got it wrong:

- **The count is a fraction.** "1 file could not be checked" invites the reading that one file is
  the whole problem; "1 of 754" says the other 753 were covered.
- ⚠ **The cause is stated per run, not assumed (#355).** The first banner said "this is a Skala
  bug, not a finding in your code" for every blocking diagnostic, one line above a per-file
  `SK9015` that `ExitCodeContractTests` asserts is *not* reported as one. A mode-000 file owned by
  someone else is fixed with `chmod`; a banner that sends the reader to look for a defect in the
  tool spends its credibility on the wrong thing, and the reader is a model that will act on it.
  The cause is read off the per-file diagnostics: `SK9015` says the file could not be read and
  what to check; `SK9010` follows ADR-003; **everything else is Skala's fault and keeps the
  original sentence** — the default is the defect, so a new blocking id cannot ship quietly
  telling readers to check their permissions.
- ⚠ **A mixed run names every cause, each with its own file count, Skala's first, in one line.**
  `2 files were not checked — 1 a Skala bug, not a finding in your code; 1 could not be read
  (check permissions and that the path is still mounted).` A banner that names only the first
  cause is #355's defect one level down: a reader sent to `chmod` over a tree that also holds a
  token-stream failure is as misdirected as one sent to file a bug over a locked file. Skala's own
  fault leads because it is the one the reader cannot fix. A file carrying two blocking
  diagnostics is attributed once, to the stronger cause, so the per-cause counts sum to the
  fraction the line opens with.
- ⚠ **Only file-scoped diagnostics decide the cause.** `ArrangementFindings` appends a stage
  summary under `SK9015`, located at the repository root, after *every* kind of arrangement
  failure. Read by id alone, that summary turns every `SK9098` in the tree into a permissions
  problem. A run whose only blocking diagnostics are stage summaries has nothing per-file to read
  and falls back to the original sentence.
- **The `PARTIAL` trailer `verify` appends reads the same classification.** It said "Exit 5 is
  that Skala bug" unconditionally, and a fix to the banner alone would have left it standing; it
  now says "reports that" when nothing in the run was Skala's fault.
- ⚠ **`SK9015` exits 5, decided once (#357), and the premise that 5 means "Skala bug" was
  refuted.** #355 and #356 each deferred moving it to `LoadFailure` (4) so that all four verbs
  would move together; the survey the move was waiting on found nobody to move it for. Measured on
  2026-09-10, all four verbs — `arrange --check`, `format --check`, `check --load loose`,
  `verify --load loose` — exit 5 for the same mode-000 file, with and without a readable neighbour,
  and `ExitCodeContractTests.AnUnreadableFile_ExitsTheSameCodeFromEveryVerb` holds them there.
  - **No consumer outside the process splits 4 from 5.** The MSBuild target
    (`Rikarin.Skala.MSBuild.targets`, the "could not complete" `Warning`) reads 0, the finding code,
    and "anything else"; the MCP `skala_verify` verdict reads 0 and non-zero; the installed
    pre-commit hook is `|| exit 1`; the Claude Code hooks above are `[ $? -eq 0 ]`; CI's
    `skala.yml` forgives 0 and 1 and fails on the rest. Every one of them treats the two numbers
    identically, so the move would have changed no behaviour anywhere Skala is consumed.
  - **Every consumer inside the process that does split them reads 4 as "there is no report".**
    `VerifyCommand.Verdict` returns a 4 untouched because "no compilation was built, so the report
    is empty" — the `PARTIAL` trailer #345 and #356 built is appended only on 5. `FixCommand` stops
    on 4 and keeps fixing on 5; `BaselineCommand` likewise. An unreadable file is the opposite case:
    the run completed for every other file and has a report worth reading. Routing it to 4 would
    either delete the partial verdict for exactly the run it was written for, or force each of
    those branches to re-derive "full failure or partial" from the report — at which point the
    exit code has stopped carrying the distinction, which was the only thing the move was for.
  - **5 never meant "Skala bug".** `ExitCodes.InternalError` has read "including … an I/O failure
    that stopped a file being read or written" since `3578e170` (2026-08-27), two weeks before
    #353 — #353 honoured the table, it did not extend it. "This is a Skala bug" is the sentence
    `Program.cs` prints for an *unhandled exception*, and the one #355 removed from the banner; it
    was never a row in the table. `LoadFailure`, by contrast, is documented as "no compilation could
    be built", which is false for this case. The one thing that did contradict `SK9015` landing on
    5 was the README's row, which named only the safety net; it now names the I/O case too.
  - `SK9010` was never in question and is where it was.
- ⚠ **A gate input that could not be read is a separate sentence, outside the fraction (#360).**
  After #358 a baseline that exists and will not open — a merge-conflict marker in
  `.skala/baseline.sarif`, a file two branches both `baseline update` — fails the reliability gate
  at exit 1 under an error-severity `SK9028` located at the baseline. Measured on 2026-09-11 over a
  one-file tree with `check --gate=local --format=agent --baseline .skala/baseline.sarif`, the
  banner above that exit read `INCOMPLETE  1 of 1 file was not checked — this is a Skala bug, not a
  finding in your code.` Both halves were wrong at two different sites: `CauseOf` let `SK9028` fall
  through to the defect default, and `BlockedFiles` counted any non-root error diagnostic, so the
  baseline was the "1 file". The source file *was* checked; the fraction is `N of M source files`
  and a baseline is not one of the M. The decision:
  - The fraction stays about source files only. `SK9028` is a fourth cause, `GateInput`, that
    `BlockedFiles` and `Causes` exclude, so `Scale`'s numerator and the per-cause counts never
    include it.
  - The baseline gets its own sentence, after the fraction when there is one and opening the line
    when there is not, naming the path because the path is the thing to open and fix:

    ```
    INCOMPLETE  the baseline at .skala/baseline.sarif could not be read, so the gate compared against nothing. Every file was checked; everything below is shown as if there were nothing to compare against.
      SK9028  .skala/baseline.sarif  the baseline at …/.skala/baseline.sarif could not be read: … is not valid JSON: …
    ```

    Mixed with a genuine `SK9099` over a two-file tree: `1 of 2 file was not checked — this is a
    Skala bug, not a finding in your code. The baseline at .skala/baseline.sarif could not be read,
    so the gate compared against nothing. Everything below covers the rest, shown as if there were
    nothing to compare against.` The `agent` surface prints no gate verdict, so this line is the
    only thing on it that explains the exit code; "every file was checked" is stated outright
    rather than left to be inferred from a missing fraction. "Shown as if there were nothing to
    compare against" is literal: `HasBaseline` stays false when the read fails, so `IsNew` is true
    for everything and the buckets are the unscoped report.
  - The root-located variants of the same id — `--since` that will not resolve,
    `--no-new-suppressions` that could not compare — fail the same gate clause and used to print
    `this run did not finish — this is a Skala bug` above it. They take the same sentence with a
    generic subject, `an input the gate scopes by could not be read (SK9028 below)`, because the
    `SK9028` line under the banner already carries the detail.
  - ⚠ The same id at **warning** — the gate names a baseline and there is no such file yet — was
    deliberately left non-blocking by #358 and never reaches the banner;
    `AgentBanner_IsSilentForABaselineThatDoesNotExistYet` pins it beside
    `MissingGateInput_DoesNotFailTheReliabilityGate`.
  - ⚠ **`verify` was exiting 0 over the same tree.** `VerifyCommand.Verdict` recomputed its exit
    from `report.New` alone for every `check` exit but 4 and 5, so the gate `check` had just failed
    at exit 1 was overruled and the banner sat above exit 0 — #358's defect, closed for `check`,
    open one verb over — and the same line took a crashed analyzer, a cancelled unit and the exit-3
    refusals to 0. `verify` now keeps any non-zero exit `check` reached and adds only the stricter
    direction over a clean one. ⚠ This is a behaviour change for a repository whose `skala.jsonc`
    defines a `local` gate with conditions `verify` cannot evaluate (a `metrics` threshold: `verify`
    never measures metrics, and the gate fails "not measured" rather than passing without it):
    `verify` now exits 1 there, which is what `verify` is `check --gate=local` + `format --check` +
    `arrange --check` has always meant.
  - ⚠ **#356's `Scale` guard survives, on a different way in.** `SK9028` at the baseline over a
    generated-only tree was the pinned "only way" into `FileCount < blocked`, and a gate input is no
    longer a blocked file, so that way is closed. The enumeration of every error-severity tool id
    that can sit at a non-root path (`rules.json`, all thirteen) found exactly one other:
    `ProjectLoader`'s binlog ladder keeps a failed *middle* rung's diagnostics when it falls through
    to loose, so `SK9024`/`SK9029` at the `.csproj` arrive in a report whose `FileCount` is the
    loose rung's. Measured on 2026-09-11 with a `.csproj` naming a nonexistent SDK and no binlog:
    `1 of 1 file was not checked — this is a Skala bug` above **exit 0**, the "1 file" being the
    project. That is #361, and the guard is re-pinned on that shape
    (`AgentBanner_OmitsTheFractionOnlyForABlockingDiagnosticThatIsNotASourceFile`, now `SK9024`
    at a `.csproj` with `FileCount` 0) so that the branch and the test go together when #361 lands.
    Everything else sits where the loaders count it (`SK9015`, `SK9010`, `SK9096`–`SK9099`), never
    enters a `RunReport` (`SK9003`, `SK9007`, `SK9008`, `SK9012` are `config check`'s), or is
    refused at exit 4 before a renderer runs (`SK9020`/`SK9021` under `--require-fresh-binlog`,
    `SK9024`/`SK9029` on the rung the caller named).
  - ⚠ **#361 closed that way in too, and the guard is deleted.** `SK9024`/`SK9029` at error severity
    are `IncompleteCause.LoadRung`, the sibling of `GateInput`: outside the fraction, their own
    sentence (`Broken.csproj could not be loaded, so the run fell back to loose and the rules that
    need a compilation did not run. Every file was checked by the rules that could run; the SKIPPED
    line names the rules that did not run.`), and — the half that is not a rendering change — a
    reliability failure at exit 1, because the run that printed `SKIPPED 260 rule(s)` over a tree
    with a project had been passing. The decision, its five measured rows and the per-id
    re-enumeration are in [07](07-analysis-host.md) § "The ladder's contract under fallback"; the
    invariant that replaced the guard is
    `IncompleteBannerTests.EveryBlockingToolId_IsEitherACountedSourceFileOrOutsideTheFraction`.
- The sentence is asserted over the **whole** output — banner, per-file line, trailer — on every
  text format, with a positive control that the genuine-defect case still says "Skala bug", so the
  suite cannot pass by deleting the sentence: `IncompleteBannerTests`, `PartialVerdictTests`, and
  `ExitCodeContractTests.Verify_*` against the real binary over a mode-000 file.

## Fixes

Two classes, declared per rule in `rules.json` (`fixIsSafe`):

- **Safe** — provably behaviour-preserving under the checks in [06](06-arrangement-and-syntax-styles.md)
  § "Safety": collection expressions, `is not null` where no user `==` exists, `ThrowIfNull`,
  expression bodies, `??=`. `skala fix --safe` applies all of them, then re-verifies, then reports
  what changed. This is the loop that removes the entire modernization category from the agent's
  workload.
- **Unsafe** — a fix exists but changes shape enough to want eyes: primary constructors (field
  ordering, attribute placement), `TimeProvider` (needs injection), `SearchValues` (needs a static
  field). `skala fix` without `--safe` requires `--include SK1002,SK1024` explicitly. An agent may
  do this; it must name the rules, which makes the choice visible in its transcript.

Every applied fix is verified: re-parse, re-bind, diagnostic delta, revert on regression. A fixing
tool that can break the build is a tool an agent will use to break the build.

⚠ **This sentence was true of the plan and false of the tool for two milestones, and it is what the
README repeated** (#344). `FixCommand.Diagnostics` was `CSharpSyntaxTree.ParseText(text).GetDiagnostics()`
under a comment claiming it caught "a parse or bind error" — but `SyntaxTree.GetDiagnostics` is
syntactic only, and there was no compilation, no reference set and no semantic model anywhere on that
path, so it could not return a bind error for any fix, ever. One `skala fix --safe` run over a
~750-file green tree applied 26 fixes, produced 12 CS1620 and 4 CS0234, reverted nothing and exited 0.
The re-bind now lives in `FixSafety` and works the way `ArrangementSafety` does.

**The cost, measured, because "a re-bind per file is unaffordable" is what deferred it.** It is
unaffordable only if each file rebuilds a compilation. `Compilation.ReplaceSyntaxTree` on the
compilation `check` already built is one document bind, and `CheckRequest.ObserveLoad` hands `fix`
that compilation so nothing is loaded twice. On `skala fix Rules --include SK6034` over Skala itself —
54 files rewritten, far past what `--safe` ever touches — **user CPU was 115 s and 118 s before the
re-bind and 107–114 s over four runs after it**: re-binding 54 documents does not clear the noise
floor of the analysis run that found the findings. That run also reverted **4 files whose fixes broke
the build** and which the parse check had waved through.

⚠ **Wall clock could not be used as the instrument and is why the figures above are CPU.** The
measurements were taken on a machine at load average ~300 from concurrent work, and the same binary
on the same command ranged 56–144 s. ⚠ **Parallelising the per-file loop the way `FormatCommand`
parallelises its own was tried and refuted**: two runs at ten jobs took 189 s and 255 s of wall clock
for the same user CPU, at 46–62 % CPU. Every `after` is a *different* derived compilation carrying
its own declaration table over every tree in the project, so ten alive at once cost more in GC than
the parallelism wins. The loop stays serial.

⚠ **`--load=loose` is the one case that still gets the parse check**, because a loose compilation
references the running framework and nothing else — binding against it answers a question about a
program that does not exist, which is why `ArrangementFindings` refuses it too. `fix` says so in its
output ("checked for parse errors only … a build is still owed") rather than letting it pass for the
check the other files got.

## The MCP server (ADR-014)

`skala mcp` — stdio, one process per repository, started by the agent host.

| Tool | Input | Output |
|---|---|---|
| `skala_verify` | paths, fix?, since? | the three-bucket report, structured |
| `skala_format` | paths or content | edits, or the formatted text for unsaved content |
| `skala_check` | paths, gate, since, rules | findings, structured, bounded |
| `skala_fix` | paths, rules, safeOnly | what changed, as a diff |
| `skala_explain` | ruleId | the rule's docs page: rationale, bad/good, false positives |
| `skala_config_explain` | path, keys? | effective options with source and tier |

Two of these matter more than they look:

**`skala_format` accepting *content*** rather than a path lets an agent format a file it has not
written yet — draft the code, format it, then write the formatted text. That turns formatting from a
correction into a step, and it is the single highest-leverage integration in this document.

**`skala_explain`** is what stops an agent from arguing with a rule or suppressing it. A model that
can read *why* `SK3002` exists will restructure the code; one that only sees "SK3002: blocking call"
will add a pragma.

The MCP server exposes no tool that can disable a rule, edit `.editorconfig`, or update a baseline.
Those are human operations and their absence from the tool list is the enforcement.

## Hooks

For Claude Code specifically, and by analogy for anything else with a post-edit hook:

⚠ **The example this section used to carry did not work, in three ways**, and M5 installed the
working one. Recorded because each is a trap:

1. **`$CLAUDE_FILE_PATH` does not exist.** A `PostToolUse` hook receives the tool call as JSON on
   *stdin*; the path is `.tool_input.file_path`. A hook using the environment variable formats
   nothing, silently, forever.
2. **`.claude/settings.json` is strict JSON.** No comments, so the reasoning for a hook cannot live
   beside it and has to live here.
3. **`skala format --quiet` prints nothing at all** when a file needs formatting — `--quiet` means
   "nothing but diagnostics" and "this file is not formatted" is not a diagnostic. A hook that uses
   it reports success in every case.

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Write|Edit",
        "hooks": [
          {
            "type": "command",
            "timeout": 30,
            "command": "f=$(python3 -c 'import sys,json;print(json.load(sys.stdin).get(\"tool_input\",{}).get(\"file_path\",\"\"))' 2>/dev/null); case \"$f\" in *.cs) skala format --check \"$f\" 2>&1 | head -20 ;; esac; exit 0"
          }
        ]
      }
    ],
    "Stop": [
      {
        "hooks": [
          {
            "type": "command",
            "timeout": 120,
            "command": "… stop_hook_active guard …; files=$(git diff --name-only --diff-filter=ACMR HEAD -- '*.cs' | head -50); [ -n \"$files\" ] || exit 0; out=$(skala verify --format=agent $files 2>&1); [ $? -eq 0 ] && exit 0; echo \"$out\" | head -60 >&2; exit 2"
          }
        ]
      }
    ]
  }
}
```

Three decisions in that `Stop` hook:

- ⚠ **Exit 2, with the report on stderr.** That is the only exit code a `Stop` hook can use to hand
  the agent something to read; 0 lets the turn end and the findings go nowhere. It is what makes this
  the honest-work check rather than a log line.
- ⚠ **The `stop_hook_active` guard.** Without it, an agent that cannot fix a finding loops forever.
  The field exists for exactly this and the hook exits 0 the second time.
- ⚠ **Scoped to `git diff --name-only HEAD`, not the whole tree.** `skala verify` over 4 688 files is
  ten seconds and two thousand findings truncated at the character cap, which is not a check, it is a
  wall. Scoped to the change it is the question the hook is asking.

⚠ **In Vixen the `PostToolUse` hook is `--check` and not a write, deliberately.** doc 10's design is
that the file is formatted the moment it is written; [15](15-roadmap.md) § M3 records that Vixen's
reformat — 2 717 files, 83 241 lines — is **deferred** until the fidelity tail closes, because at
98.86 % Rider reformats about one line in a hundred back. A writing `PostToolUse` hook performs that
deferred commit one file at a time, unreviewed, which is the same diff with none of the
reviewability. It becomes a write by deleting one word, and the word to delete is `--check`.

`PostToolUse` formatting is the right shape: the agent never sees a formatting finding because the
file is formatted the moment it is written, which is both fewer tokens and fewer opportunities to
argue. It must be fast — the warm-path budget in [13](13-performance.md) exists for this hook — and it
must never fail the edit: a file that does not parse is left alone, silently, because an agent
mid-refactor writes files that do not parse.

`Stop` verification is the honest-work check: the agent cannot end a turn claiming success while the
tree is failing its own gate.

## The `CLAUDE.md` contract

What a repository tells its agents, and what Skala is designed to make true:

```markdown
## Before you claim work is finished
Run `skala verify`. Exit 0 or it is not finished.
- Formatting: run the command it prints. Never format by hand.
- Fixable findings: run `skala fix --safe`, then re-verify.
- Findings needing a decision: fix the code. Do not add `#pragma warning disable`,
  do not lower a severity in `.editorconfig`, do not add to the baseline —
  all three are visible in review and all three are reverted.
- If you believe a rule is wrong, run `skala explain <id>` and say so in your message.
  Do not act on that belief unilaterally.
```

That last line matters: the escape hatch is *saying so*, not *doing something*. An agent with a
sanctioned way to disagree does not need an unsanctioned one.

## Suppression pressure

Because the failure mode is predictable, it is measured — ⚠ **by one of these three, not three.**

- ✅ `skala check --since=<ref> --no-new-suppressions` — a gate condition that fails when a change
  adds a `#pragma`, a `SuppressMessage`, a severity downgrade in `.editorconfig`, or a baseline
  entry. ⚠ Including the `.editorconfig` and baseline cases, which is what makes it a real
  constraint rather than a grep for `#pragma`. This one is built and is the load-bearing half.
- ❌ `SK7050`/`SK7051` — suppressions without justification, as ordinary findings. **Neither rule
  exists**: no analyzer, no `rules.json` entry, no allocation. [08](08-rule-catalogue.md) allocates
  the ids and records them as not started; this list stated them as shipping.
- ❌ `skala report --suppressions` — **the flag does not exist.** `report` takes `--format`,
  `--no-color`, `--include-hints` and `--summary`. The periodic-review artefact it describes has no
  producer. The nearest thing today is `skala check --show-suppressions`, which includes suppressed
  findings in the report but does not list justifications.

⚠ The gap matters more here than the arithmetic suggests. `--no-new-suppressions` catches a
suppression being *added*; the two missing pieces are what would catch the ones already there. A
section headed "it is measured" that lists three mechanisms and has one is the failure mode this
document is about, in this document.

The point is not that suppressions are wrong. It is that a suppression should be a decision someone
made on purpose, and the tool's job is to make sure it looks like one.

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
  SK1020  Core/Bar.cs:31   Use ArgumentNullException.ThrowIfNull(source)
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

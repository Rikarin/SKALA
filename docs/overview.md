# Skala — Implementation Overview

**What is actually built, measured against the code and the tests rather than against the plan.**

[`plan/`](plan/) is the design record: what Skala is meant to be and why each decision was taken.
It says what was intended. **This file says what exists, and where the two disagree, this file
wins** — that is the arrangement [`plan/README.md`](plan/README.md) has described since milestone 0
and this is the first time the file it points at has existed.

⚠ **Every number below was produced by running something at `8cbd66d`** (*Merge M7: hardening —
startup, rules, CI, and the budgets asserted*, the head of `master`), and the command is recorded
beside it. Nothing is carried over from a plan document or from a milestone report. Where a figure
recorded elsewhere could **not** be reproduced, it is named as such and left out rather than
repeated — § "Numbers this file does not carry" is the list.

⚠ **Work in flight is not in here.** `master` at `8cbd66d` had no arrangement engine and no taint
table; both were on unmerged branches when this file was measured. **Both have since merged** —
`arrange` is a top-level command with seventeen `SK02xx` rules, and `taint.json` ships beside
`rules.json` — so the command table below is right about them and this paragraph was not.

⚠ **This file is pinned to `8cbd66d` and `master` is a long way past it.** That is the arrangement:
every number here was produced by running something at that commit, and a number is not updated
without re-running the thing that produced it. But it means a *claim* here can be stale in the
direction of understating what exists, and the two above were. When this file and the code disagree,
the code wins — the same way this file wins over `docs/plan/`.

## Legend

| | Meaning |
|---|---|
| ✅ | Built, tested, and measured |
| 🟡 | Partially built — the working half is named, the rest is under *Owed* |
| ⬜ | Not started |
| ⛔ | Blocked on something that does not exist |
| ✂️ | Deliberately cut, with a reason |

---

# Part 1 — The command surface

## 1.1 Commands

`skala --help`, run against `artifacts/native/osx-arm64/skala`. Sixteen top-level commands, all of
which exist and do something. ⚠ There were seventeen: `daemon` is deleted, along with the format
daemon behind it ([`plan/11`](plan/11-cli-and-integrations.md) § "The daemon, and why it is gone").
⚠ `arrange` was missing from this table and is not new.

| Command | What it does today | Status |
|---|---|---|
| `format <paths>` | Spaces, blank lines, braces, indentation, break presence and position, and wrapping. Writes, checks, diffs, or formats the git index | ✅ |
| `arrange <paths>` | Rewrites the tree: body styles, `var`, target-typed `new`, qualifiers, usings. Needs a project for the semantic half | ✅ |
| `check <paths>` | Loads a compilation three ways, runs Skala's analyzers plus Roslyn's IDE1006 naming analyzer, writes SARIF, evaluates a named gate | ✅ |
| `verify <paths>` | `format --check` + `arrange --check` + `check --gate=local`, shaped for an agent. Exit 0 means nothing to do | ✅ |
| `fix <paths>` | Applies finding edits; explicit `IDE1006` uses Roslyn's solution-wide rename action; verifies and re-formats what it touched | ✅ |
| `explain <rule>` | A rule's rationale, examples and known false positives | ✅ |
| `rules list` · `rules docs` | The catalogue as a table; regenerates `docs/rules/` from `rules.json` | ✅ |
| `docs site` | Regenerates `docs/site/` from `rules.json` **and** `options.json` — **143 files**: 108 option pages, 32 rule pages, two indexes and a stylesheet | ✅ |
| `baseline create` · `update` · `prune` · `show` | The accepted-findings file, `.skala/baseline.sarif` | ✅ |
| `report <sarif>` | Re-renders a stored SARIF in any output format. Runs no analysis | ✅ |
| `trend <path>` | The recorded history from `.skala/history.jsonl` | ✅ |
| `cache clear` · `stats` | The incremental analysis cache | ✅ |
| `config explain` · `check` · `diff` · `distill` · `fix` · `sync` · `canonical` | Everything that reads or reshapes the `.editorconfig` | ✅ |
| `lsp` | LSP over stdio: formatting, range formatting, pull diagnostics, code actions | ✅ |
| `mcp <path>` | MCP over stdio, six tools | ✅ |
| `hooks install` | Writes `.git/hooks/pre-commit` unless a hook manager owns it | ✅ |

`verify` is `format --check` + `arrange --check` + `check`, and its own `--help` text says so.
Auto-load on both commands gives arrangement a real semantic model when one workspace target is unambiguous;
without a project, the syntactic arrangement subset runs and the semantic rules are reported as
skipped.

## 1.2 Flags, per command

⚠ **This is the table [`plan/11`](plan/11-cli-and-integrations.md) § "Command surface" approximates
and this one is the parser's.** Taken from `skala <command> --help` for all sixteen.

| Flag | `format` | `check` | `verify` | `fix` | `report` | Notes |
|---|:-:|:-:|:-:|:-:|:-:|---|
| `--check` | ✅ | | | | | Writes nothing, exits **2** when there is anything (doc 09's table; it exited 1 until the reconciliation pass) |
| `--diff` | ✅ | | | | | Unified diff over the edits |
| `--range a:b` | ✅ | | | | | Character offsets, filtered after a full-file fit |
| `--staged[=worktree]` | ✅ | | | | | Formats the index and writes back to both |
| `--quiet` | ✅ | | | | | |
| `--option k=v` | ✅ | | | | | Repeatable. For debugging and the conformance harness |
| `-j, --jobs <n>` | ✅ | | | | | ⚠ **`format` only.** Default `min(cores, 10)` |
| `--no-cache` | ✅ | ✅ | ✅ | | | ⚠ **Two different caches.** On `format` it is the `.editorconfig` memo; on `check`/`verify` it is the incremental analysis cache |
| `-d, --define` | ✅ | ✅ | ✅ | ✅ | | Preprocessor symbols, repeatable and comma-separated |
| `--load` | ✅ | ✅ | ✅ | ✅ | | `none` on `format`, `binlog` on `check`, `auto` on `verify` and `fix` |
| `--binlog` | | ✅ | | ✅ | | |
| `--project` | | ✅ | ✅ | ✅ | | Selects the workspace target when discovery is ambiguous |
| `--require-fresh-binlog` | | ✅ | | | | CI sets it |
| `--gate <name>` | | ✅ | | | | Default `local` |
| `--format` | | ✅ | ✅ | | ✅ | `check`/`report`: seven renderers. `verify`: three |
| `-o, --output` | | ✅ | | | | Default `.skala/report.sarif` |
| `--include-hints` | | ✅ | | | ✅ | |
| `--no-color` | | ✅ | | | ✅ | |
| `--summary` | | ✅ | | | ✅ | |
| `--show-suppressions` | | ✅ | | | | |
| `--rules <ids>` | | ✅ | | | | |
| `--no-formatting` | | ✅ | | | | Leaves `SK0001` out |
| `--resharper-severities` | | ✅ | | | | Opt-in, [`plan/16`](plan/16-risks-and-open-questions.md) § Q5 |
| `--since <ref>` | | ✅ | | | | |
| `--baseline <file>` | | ✅ | | | | |
| `--no-new-suppressions` | | ✅ | | | | Four mechanisms: `#pragma`, `[SuppressMessage]`, an `.editorconfig` severity, a baseline entry |
| `--record` | | ✅ | | | | Appends to `.skala/history.jsonl` |
| `--duplication` | | ✅ | | | | ⚠ Off by default: a whole-repository pass |
| `--fix` | | | ✅ | | | |
| `--safe` | | | | ✅ | | The default and the only unqualified mode |
| `--include <ids>` | | | | ✅ | | ⚠ Required without `--safe`; `IDE1006` automatically selects workspace mode |
| `--dry-run` | | | | ✅ | | |
| `-n, --limit` | | | | | | `trend` only |

⚠ **Flags the plan documents describe and the parser does not accept:** `--strict-config`
([`plan/03`](plan/03-configuration-model.md), [`plan/09`](plan/09-quality-gates-and-reporting.md)),
`--profile` ([`plan/07`](plan/07-analysis-host.md), [`plan/13`](plan/13-performance.md)), `--reflow`
and `--verbose` ([`plan/04`](plan/04-formatting-engine.md)), `--path`
([`plan/09`](plan/09-quality-gates-and-reporting.md)), `report --suppressions`
([`plan/10`](plan/10-ai-agent-integration.md)), and every `--arrange` form
([`plan/06`](plan/06-arrangement-and-syntax-styles.md)). `config check --strict` exists and is a
different flag on a different command.

---

# Part 2 — The formatter

## 2.1 Fidelity against the oracle

```
dotnet run --project Testing/Rikarin.Skala.Testing -c Release -- fidelity
```

⚠ **Two numbers per corpus, because both are true of a real invocation.** `skala format` on a loose
file has no preprocessor symbols; `skala format --load=binlog` has whatever the build compiled. The
harness runs both by default and the pair is the honest answer.

| Corpus | Files | Lines | Line (no symbols) | Line (with) | File (no) | File (with) |
|---|---:|---:|---:|---:|---:|---:|
| `corpus/real/` | 380 | 76 375 | 99.63 % | **99.70 %** | 85.26 % | **85.79 %** |
| `corpus/constructs/` | 273 | 2 053 | 96.64 % | 96.64 % | 91.21 % | 91.21 % |
| `corpus/pathological/` | 52 | 455 | 95.60 % | 95.60 % | 86.54 % | 86.54 % |

⚠ **The two lower numbers are lower on purpose.** `constructs/` and `pathological/` are built out of
the shapes that are hard — one fixture per option, and a file whose whole content is a raw
interpolated string with nested braces. A number near 100 % on those would mean the fixtures had
stopped being adversarial. `corpus/real/` is the one the bar is read against.

**`corpus/real/`, split** — `… -- preprocessor`, and per-subset arithmetic over
`dump real <dir> defined`:

| Subset | Files | Line | File | Divergent lines |
|---|---:|---:|---:|---:|
| all | 380 | **99.70 %** | **85.79 %** | 231 |
| no `#if` | 289 | **99.79 %** | 89.97 % | 125 |
| containing a `#if` | 91 | 99.36 % | 72.53 % | 106 |
| containing a raw literal | 11 | 99.68 % | 90.91 % | 12 |

| Origin | Files | Line | File | What it measures |
|---|---:|---:|---:|---|
| `vixen/` | 200 | 99.81 % | 90.00 % | Does Skala leave code that already conforms alone |
| `newtonsoft/` | 110 | 99.41 % | 80.91 % | Does Skala move Allman-braced, differently-spaced code where Rider would |
| `serilog/` | 70 | 99.57 % | 81.43 % | The same, a second house style |

⚠ **`vixen/` is the flattering third.** Those 200 files were already formatted by Rider under this
`.editorconfig`, so their 99.81 % is a different measurement from the other two's — and Vixen is a
test subject rather than a specification
([`plan/16`](plan/16-risks-and-open-questions.md) § "The reference trees are a test subject").

**Against the bars:** the revised milestone-3 bar of **≥ 99.5 % on files with no `#if` is met** at
99.79 %. The **≥ 99.9 % overall bar is not met** at 99.70 %, and has now been missed with a
measurement at two milestones running. 231 divergent line slots across 54 files; to reach 99.9 %
they would have to fall to 76. [`divergences.md`](divergences.md) is where they are: twelve entries,
three resolved, two half closed, seven open, and two of the open ones — SK-DIV-0005's fitted margin
and SK-DIV-0011's unknown lambda discriminator — are most of the residue and neither is reachable by
more of the same work.

## 2.2 Both symbol sets

⚠ **The differential runs under both symbol sets by default, and the reason is a real bug that hid
for four milestones.** With `#if` bodies live, `count > (n)` came back as `count >(n)` — `IsCallSite`
read every `>` as a type-argument close — and every corpus line that shows it is inside a conditional
body. A single-symbol-set run cannot see that class at all.

| | Measured at `8cbd66d` |
|---|---|
| Symbols supplied | **18**, read out of `artifacts/skala.binlog` through the loader `skala check` uses, not from a typed list |
| Divergences under **one** symbol set only | **0 with-symbols-only, 65 without** |
| Where the 65 are | All in one file, `newtonsoft/Newtonsoft.Json.Tests/Issues/Issue2504.cs` |

## 2.3 The option registry

`Core/Rikarin.Skala.Options/options.json`, counted directly.

| Tier | Count | Meaning |
|---|---:|---|
| **A** — implemented, pinned by an oracle fixture | **284** | |
| **B** — approximated | **0** | ⚠ see below |
| **C** — accepted and deliberately ignored | **6** | `apply_auto_detected_rules`, `autodetect_indent_settings`, `csharp_old_engine`, `show_autodetect_configure_formatting_tip`, `use_indent_from_vs`, `use_old_engine` |
| **D** — known to the registry, not implemented | **230** | ⚠ **"not implemented" is not what this tier means** — see below |
| **Total** | **520** | |

⚠ This table said **201 / 313** until it was re-counted against `options.json`, which is what the row
above claims to be doing. Count it again before quoting it.

⚠ **Tier D does not mean "remaining work", and reading it that way overstates the gap by a factor of
three.** [`tier-d-split.md`](tier-d-split.md) splits all 230 with the evidence per key: **58 already
agree with the oracle at the value the export sets** — they are Tier D because they diverge at a
value the export never uses — and 104 more are duplicate spellings, masked keys, unreachable keys or
another subsystem's. **63 are real, reachable, unimplemented behaviour**, and 5 are unresolved. So
**12 % of the export is genuinely unenforced, not 45 %**. ⚠ None of that is a Tier A claim and the
split changes no tier: Tier A is a statement about an option across its domain, and agreement at one
value is not one.

⚠ **Tier B has no members, and it is not a tier.** [`plan/03`](plan/03-configuration-model.md)
documents it as live with its own behaviour row; the conformance suite contains a test
(`OptionCoverageTests.NoOptionClaimsTierB_WithoutADocumentedDivergence`) whose entire job is to guard
the empty set. **The four-tier model is a three-tier model in practice.** An option that would have
been B becomes a Tier D key plus an `SK-DIV` entry instead, which is a better answer — an option
Skala honours and Rider ignores is a divergence wearing a tier badge — and doc 03 has not been told.

⚠ **`defaultSource` — where each default comes from**, which is the M3 repair that mattered most:

| Source | Count |
|---|---:|
| `template` — the Rider export's own value | 297 |
| `oracle-probe` — derived by asking `jb cleanupcode` under a configuration carrying only `root = true` | **123** |
| `unknown` | 100 |

⚠ [`plan/03`](plan/03-configuration-model.md) § "Deriving ReSharper's defaults" says 131 derived, of
which four are recorded `unknown` — 127. The registry holds **123**. The document is four out.

⚠ **Two more registry facts the documents get wrong.** The registry holds **520** keys, not the
"~380 C# options" [`plan/00`](plan/00-vision-and-principles.md) and
[`plan/03`](plan/03-configuration-model.md) still quote — M3 extended it past the C# whitespace keys
it was scoped to. And `docs/options/` **does not exist**: doc 03 lists it as a generator output and
the generator emits four sources, none of them Markdown. The option documentation that does exist is
`docs/site/options/`, **108 pages**, written by `skala docs site`.

## 2.4 The properties

`dotnet test Skala.slnx -c Release` — `Rikarin.Skala.Conformance.Tests`.

| | |
|---|---|
| Conformance cases | **8 981, all green** |
| What they assert | Idempotency, token equivalence, parse stability, determinism, range consistency, whitespace absorption |
| Coverage | All three corpora, under both symbol sets |

⚠ **The nightly "fuzzing" job runs exactly this suite and there is no fuzzer.** 8 981 cases over a
fixed corpus is a property suite, not a seeded mutation driver; there is no weighted grammar and no
delta-debugging minimiser into `corpus/pathological/`. The workflow says so in its own header, which
is the right way to ship it, and the number above is the whole of what it runs.

## 2.5 What the formatter does not do

| | |
|---|---|
| Documentation comments | ✅ **Formatted, by default** — Rider formats them and the oracle profile Skala pins does not ([`divergences.md`](divergences.md) § SK-DIV-0006). `XmlDocComments.cs` detects malformed XML and reports `SK0003`, always, and such a comment is left byte-identical. The sub-formatter (`XmlDocFormatter`) honours 17 of the 27 `resharper_xmldoc_*` keys, refuses ten with a reason each, and writes no comment whose content does not survive a round trip. `--no-xmldoc` turns it off. ⚠ The 3.59 points it costs against the committed fixtures are the fixtures' profile, not a fidelity cost — the differential's basis is the lines outside doc comments until they are regenerated |
| Interpolated raw string literals | ⛔ Emitted verbatim (SK-DIV-0003) |
| Disabled `#if` text | ⛔ Never touched, byte for byte (SK-DIV-0001) |
| Arrangement | ⬜ M4 |
| Column alignment | 🟡 Statement conditions only (SK-DIV-0008); four other `align_multiline_*` keys are Tier D |

---

# Part 3 — The rules

## 3.1 Every shipped rule, and what it finds

**32 ids are allocated** (`skala rules list`) — 24 analyzers, metrics and formatter findings, plus 8
tool diagnostics.

⚠ **Two finding counts per rule, because the two invocations answer different questions**, and
quoting one without the other is how a rule's zero comes to mean the wrong thing:

- **product** — `skala check --load=loose --no-cache --include-hints --duplication` over
  `corpus/real/`'s 380 files. This is the nightly job's exact command. A loose compilation has no
  semantic model worth trusting, so `AnalyzerHost` **drops the semantic rules**, and their 0 here is
  "not run", not "did not fire".
- **audit** — `Testing/Rikarin.Skala.Testing -- audit`, which tells the host the run is a binlog one
  so the semantic rules survive a loose compilation. Deliberately more aggressive than the product:
  every finding it produces is one to check and the ones it misses are misses. **Its counts are a
  floor**, and the compiler-error count is printed beside them so the floor is visible.

| Id | Rule | Default | Fix | Loose | `corpus/real` product | `corpus/real` audit | Vixen audit | Vixen audit + global usings |
|---|---|---|---|:-:|---:|---:|---:|---:|
| `SK0001` | The file is not formatted | suggestion | safe | ✅ | **276** | — | — | — |
| `SK0002` | Line over the width, nothing can break | hint | — | ✅ | **211** | — | — | — |
| `SK0003` | Doc comment is not well-formed XML | hint | — | ✅ | **2** | — | — | — |
| `SK1005` | Use a file-scoped namespace | suggestion | safe | ✅ | **27** | **27** | 0 | 0 |
| `SK1010` | `is null` / `is not null` | suggestion | safe | — | 0 † | **114** | **38** | **41** |
| `SK1020` | `ArgumentNullException.ThrowIfNull` | suggestion | safe | — | 0 † | **2** | 0 | 0 |
| `SK1030` | Use `??=` | suggestion | safe | ✅ | **0** | 0 | 0 | 0 |
| `SK1034` | `Count` over `Count()`/`Any()` | suggestion | safe | — | 0 † | 0 | 0 | 0 |
| `SK1035` | `Enum.GetValues<T>()` | suggestion | safe | — | 0 † | 0 | 0 | 0 |
| `SK2013` | Exception constructed and discarded | warning | safe | — | 0 † | 0 | 0 | 0 |
| `SK2015` | `throw ex;` resets the stack trace | warning | safe | ✅ | **0** | 0 | 0 | 0 |
| `SK3001` | `async void` outside an event handler | **none** | review | — | 0 ‡ | 0 | 0 | 0 |
| `SK3002` | Blocking on an async call | warning | review | — | 0 † | 0 | **7** | **44** |
| `SK4010` | A `Where` the next operator could take | suggestion | safe | — | 0 † | 0 | 0 | 0 |
| `SK6003` | Abstract type with a public constructor | suggestion | safe | ✅ | **1** | **1** | 0 | 0 |
| `SK7001` | Cyclomatic complexity over threshold | hint | — | — | 0 † | **2** | **85** | **98** |
| `SK7002` | Cognitive complexity over threshold | suggestion | — | ✅ | **45** | **45** | **768** | **768** |
| `SK7003` | Member over the statement-count threshold | hint | — | ✅ | **0** | 0 | **5** | **5** |
| `SK7004` | Type over the member-count threshold | hint | — | ✅ | **0** | 0 | **13** | **13** |
| `SK7005` | Member over the parameter-count threshold | hint | — | ✅ | **0** | 0 | **197** | **197** |
| `SK7006` | Member nests deeper than the threshold | hint | — | ✅ | **0** | 0 | **2** | **2** |
| `SK7010` | Public API without a doc comment | **none** | — | ✅ | 0 ‡ | — | — | — |
| `SK7020` | Duplicated block | warning | — | — | **13** | — | — | — |
| `SK8005` | `Thread.Sleep` in a test | suggestion | — | — | 0 † | 0 | **0** | **25** |
| `SK9001` | Unknown configuration key | suggestion | — | — | 0 | — | — | — |
| `SK9010` | The file does not parse | warning | — | ✅ | 0 | — | — | — |
| `SK9011` | Braces split across a preprocessor branch | suggestion | — | ✅ | 0 | — | — | — |
| `SK9020` | The binlog is stale for this file | suggestion | — | — | 0 | — | — | — |
| `SK9021` | The binlog names a file that does not exist | warning | — | — | 0 | — | — | — |
| `SK9030` | An analyzer threw | warning | — | — | 0 | — | — | — |
| `SK9031` | An analyzer package failed to load | warning | — | — | 0 | — | — | — |
| `SK9099` | Output was not token-equivalent | **error** | — | ✅ | 0 | — | — | — |

† not run in loose mode — the rule needs semantics.  ‡ ships disabled (`none`).

**Totals.** The product run reports **577 findings across 291 files from 33 rules** — 32 Skala ids
plus `SYSLIB0051` ×2 from the hosted compiler. That reproduces the committed
`.skala/rule-counts.json` baseline exactly, count for count.

⚠ **`SK8005` is the reason both columns exist.** Vixen builds with `<ImplicitUsings>enable</…>`, a
loose compilation has no generated `GlobalUsings.g.cs`, and so `Thread`, `Task` and `List<T>` are
unresolved in every file that never writes the `using`. **The tree has 195 253 compiler errors that
way, and `SK8005` reports zero.** Handing the audit a stand-in global-usings file takes it to
**128 490** errors, and `SK8005` goes 0 → **25** and `SK3002` 7 → **44**. A semantic rule's zero
under `--load=loose` is not evidence of anything until the errors around it have been looked at.

⚠ **That stand-in was never committed.** M7's numbers rested on it and were not reproducible from
the repository; the ones above were re-derived with a fresh one (the `Microsoft.NET.Sdk` default
implicit-using set), which is why the error count is 128 490 here and 128 833 there.

⚠ **False positives: still zero, and "zero findings" is not the same claim.** The rules with corpus
mass were each read at the milestone that shipped them, and this pass re-ran the counts rather than
re-reading the findings. What it can say is that **the counts are stable**: every M5 and M6 figure
reproduces exactly, and the fix verification does too — 171 fixes across 65 files over
`corpus/real/`, **15 743 compiler errors before and 15 731 after, 0 `(file, id)` pairs worse than
before**.

⚠ **A low finding count on a reference tree is not evidence that a rule is good.** It is a fact about
the tree. **Seven of the twenty-one shipped analyzers and metrics fire zero times on both trees** —
`SK1030`, `SK1034`, `SK1035`, `SK2013`, `SK2015`, `SK3001`, `SK4010` — and an eighth, `SK7010`, ships
disabled so its zero is a configuration fact rather than a measurement. Their evidence is their
fixtures and nothing else: a rule with no corpus occurrences has a false-positive rate *measured* at
zero and *tested* at nothing. That is a reason to be careful and never a reason to cut, demote or
disable. **Where Vixen does not follow a rule, Vixen changes**
([`plan/16`](plan/16-risks-and-open-questions.md) § "The reference trees are a test subject").

## 3.2 The catalogue, against the code

⚠ **Three artefacts agree with each other and are test-enforced — `rules.json`, `allocated-ids.txt`
and `docs/rules/`, 32 entries each — and nothing compares any of them to the catalogue.** Derived by
intersecting the ids [`plan/08`](plan/08-rule-catalogue.md) names with `rules.json`:

| | Count | |
|---|---:|---|
| Rules the catalogue named **before this pass** | 106 | |
| …shipped | 21 | 19.8 % |
| Shipped but **not named in the catalogue at all** | **3** | `SK7003`, `SK7004`, `SK7005` — real, shipping, documented in doc 07's metrics table instead. **Reconciled into doc 08 by this pass** |
| **Rules the catalogue names now** | **109** | |
| **Shipped** | **24** | **22.0 %** |
| **Cut**, with a reason that survives review | **10** | duplicates a compiler/analyzer diagnostic · costs the warm cache for no gain · cannot be implemented correctly |
| **Outstanding** | **75** | of which **12** were declared cut in M7's retrospective with **no reason recorded anywhere** |

Per range, after the reconciliation:

| Range | Named | Shipped |
|---|---:|---:|
| `SK0xxx` Formatting | 3 | **3** |
| `SK1xxx` Modernization | 32 | 6 |
| `SK2xxx` Correctness | 16 | 2 |
| `SK3xxx` Async, disposal | 12 | 2 |
| `SK4xxx` Performance | 9 | 1 |
| `SK5xxx` Security | 9 | **0** |
| `SK6xxx` Design | 8 | 1 |
| `SK7xxx` Maintainability and metrics | 13 | **8** |
| `SK8xxx` Tests | 7 | 1 |

[`plan/08`](plan/08-rule-catalogue.md) § "Rule status" has the per-rule breakdown, what each
outstanding rule is waiting on, and the decisions that rest on a reference-tree count and are
awaiting revisit.

## 3.3 ⚠ Two live id collisions

| Id | Problem |
|---|---|
| `SK9007` | Emitted by `ToolConfiguration` as a **bare string literal** for "`skala.jsonc` is not valid JSON". It was in no register and no constant class until this pass added it to [`plan/08`](plan/08-rule-catalogue.md) |
| `SK9012` | **Two meanings.** `ConfigDiagnosticIds.CanonicalVersionInToolConfig` (a canonical version pinned in `skala.jsonc`) and `FormatCommand.cs` (an `IOException` while formatting). ADR-012 forbids exactly this, and doc 08's own note says this class of collision was caught before it merged |

⚠ **Both hid the same way.** `ToolDiagnosticIdTests.ToolDiagnosticIds_AreDeclaredOnce` matches
`public const string … = "SK….";` — it reads **declarations, not uses** — and both of these are bare
literals passed to a constructor. The guard is the mechanism ADR-012 rests on, and it is enforced
against only the half of the code that declares a constant. `SK9012`'s collision is owed a renumber
before anyone holds a baseline containing either finding.

⚠ **`SK6001` and `SK7010` are one rule under two ids.** The catalogue allocates `SK6001` as "public
API without doc comments"; `SK7010` is that rule and is the one that shipped. ADR-012 makes both
permanent, so `SK6001` is retired before it was ever built.

---

# Part 4 — Performance

## 4.1 Measured at `8cbd66d`

⚠ **Apple M-series, 10 cores, 32 GB, macOS. Release build, `./build.sh Native` layout.** Each row is
a loop of N invocations divided by N, which is the shape [`plan/13`](plan/13-performance.md)
§ "Startup" requires: a .NET `Process.Start` harness costs 10–22 ms per spawn on its own, so a 40 ms
budget cannot be asserted without measuring the spawn floor with the same spawner.

> ⚠ **The `Budget` column is withdrawn.** Every budget in [`plan/13`](plan/13-performance.md) was
> written for a post-edit agent hook firing on every file write; there is no such consumer, Skala runs
> ahead of test suites that take about twenty minutes, and the tests that asserted these rows are
> deleted with the daemon. The measurements are kept as measurements. Nothing checks them.

| Operation | ~~Budget~~ | Measured | N | |
|---|---:|---:|---:|---|
| `/usr/bin/true` — process-start floor | — | **2.65–3.31 ms** | 150–200 | What any process costs here |
| ~~`format --check`, one 456-line file, warm, AOT client~~ | ~~< 40 ms~~ | ~~**12.38 ms**~~ | 150 | withdrawn — there is no AOT client and no daemon |
| `format --check`, same file, **cold** | ~~< 40 ms~~ | **69.48 ms** | 40 | this is what one single-file format costs now, warm page cache |
| full tool, bare (no work) | — | **43.10 ms** | 40 | process start before `Main`, because the tool references Roslyn |
| `format --check`, one file, cold | ~~250 ms~~ | **202.22 ms** | 20 | cold page cache |
| `format --check`, whole Vixen source tree (4 717 files) | ~~< 20 s~~ | **12.36–13.80 s** | 2 | the row that still matters |
| `verify`, 5 files, `--load=loose`, cold cache | ~~900 ms~~ | **0.651 s** median | 7 | |
| `verify`, 5 files, `--load=loose`, warm cache | ~~< 300 ms~~ | **0.406 s** median | 7 | |
| ~~Daemon RSS after a single-file session~~ | ~~< 1.5 GB~~ | ~~**166 MB**~~ | — | withdrawn — there is no daemon |

⚠ **The 43.10 ms bare start is what the whole two-binary arrangement existed to remove**, and it is
now simply paid. `skala --version` does no work at all and costs that much before `Main` runs,
because the framework-dependent tool references Roslyn. Against a twenty-minute test suite it does
not signify.

## 4.2 ⚠ Windows is unverified

**Every number in § 4.1 was taken on macOS/arm64.** The cross-platform CI matrix names macOS, Linux
and Windows with `fail-fast: false` and runs the whole suite on each, so *correctness* is exercised
on Windows — and two real Windows bugs were found that way, a cache key that hashed the path's raw
bytes and a named-pipe transport that did not exist. **Performance on Windows is unmeasured**, and
now unmeasured everywhere: see § 4.3.

## 4.3 ⚠ Nothing asserts a performance budget

`PerformanceBudgetTests` and `ClientAgreesWithToolTests` are **deleted**, along with the
`performance` CI job that ran them behind `SKALA_PERF=1` and a published native layout.

They asserted the budgets in [`plan/13`](plan/13-performance.md), and those budgets were written for
a format-on-save workflow Skala does not have. Rather than leave a table asserting numbers nothing
measures, the tests and the budgets were withdrawn together — doc 13 § "Budgets" carries the reason.

What is left is measurement by running it: the § 4.1 table is a record of a run, not a gate.

---

# Part 5 — What is not built

## 5.1 Named absences

| | Status | |
|---|---|---|
| **A fuzzer** | ⬜ | The nightly job runs the 8 981-case property suite. There is no seeded mutation driver, no weighted grammar, and no delta-debugging minimiser into `corpus/pathological/`. The workflow's own header says so |
| **Adoption beyond Vixen** | ⬜ | Zero repositories. ⚠ **Including Vixen** — it has been measured read-only at every milestone and never written to. `git status` in `/Users/jiu/Projects/Vixen` is unchanged by this pass. The formatting commit is prepared and deferred ([`plan/15`](plan/15-roadmap.md) § M3.1) |
| **`.vxml` / `.vcss`** | ⛔ | M9, and postponed to last. See below |
| **HTML and the CSS family** | ⛔ | Same |
| **`ISkalaLanguage`** | ⛔ | ⚠ **The interface does not exist in any C# source.** It is described in [`plan/01`](plan/01-technology-decisions.md), [`plan/14`](plan/14-web-languages.md) and [`plan/15`](plan/15-roadmap.md), and M9 is gated on it "having been exercised" by lifting the xmldoc sub-formatter out. ✅ That sub-formatter now exists — four files in `Formatting.CSharp` sharing no state with the document builder — so one of the two missing pieces is gone; the interface is still the other |
| **`skala arrange`** | ⬜ | M4. The command, the second oracle profile, the fixed point across format-and-arrange, and the edit-to-span map for `--range` are all outstanding |
| **`SK5xxx` security** | ⬜ | M8. Nine ids, none in `rules.json`; no `taint.json` in the tree |
| **`SK9098`** | ⬜ | Allocated in the register for "arrangement reverted, new diagnostics". Arrangement does not exist, so neither does it |
| **`SK7050` / `SK7051`** | ⬜ | ⚠ [`plan/07`](plan/07-analysis-host.md) and [`plan/10`](plan/10-ai-agent-integration.md) describe both as **working mechanisms** — "a `#pragma warning disable` with no justification comment is `SK7050`". Neither is in `rules.json`; neither has a `docs/rules/` page |
| **`docs/options/`** | ⬜ | Listed as a generator output in doc 03. `docs/site/options/` is what exists — **108 pages**, from `skala docs site` |
| **Tier B** | ✂️ | Zero members. In practice an option that would have been B becomes Tier D plus an `SK-DIV` entry, which is the better answer |

## 5.2 The `SK9xxx` gaps

| Id | State |
|---|---|
| `SK9007` | ✅ Live, but was in no register. Added to doc 08 by this pass |
| `SK9012` | ⚠ **Allocated twice.** See § 3.3 |
| `SK9098` | ⬜ Allocated, unbuilt — arrangement is M4 |
| `SK9002`–`SK9006`, `SK9008`, `SK9009`, `SK9013`, `SK9014` | ✅ Live as configuration diagnostics, and deliberately **not** in `rules.json` — they are not analyzer rules and have no `docs/rules/` page. `rules.json` carries the eight `SK9xxx` ids that *are* findings |

## 5.3 The deliberately-cut rules

Ten, and none of the reasons is about a reference tree.

| Kind | Rules |
|---|---|
| **Duplicates a diagnostic the user already sees** | `SK3006` (`CS1998`) · `SK8003` (`xUnit1001`) · `SK8004` (`xUnit1049`) |
| **The fix cannot be made behaviour-preserving** | `SK8002` — see below · `SK4005` (needs a dataflow proof, not an edit) · `SK6006` (inserts a member into a public API) · `SK6007` (generates an implementation) |
| **No mechanical fix and a large false-positive surface** | `SK6002` · `SK6005` · `SK8001` (an assertion inside a helper is indistinguishable from none without following the call) |

⚠ **`SK8002` is the interesting one and its reason splits in two.** The half that survives is about
the rewrite and holds in any repository: `Assert.Equal` has no overload taking a custom failure
message; `Assert.NotEqual(0, flags & Member)` over a `[Flags]` enum does not compile because the `0`
was an implicit constant conversion and the rewrite drops it; and `Assert.Equal` calls `Equals`,
which is a *different predicate* from `operator ==`. The half that does not survive is the conclusion
drawn from it — "fires zero times on a tree with twelve thousand candidates" — which is a Vixen count
and is struck. A narrower or fixless `SK8002` is not disposed of by the cut.

⚠ **Twelve more were declared cut in M7's retrospective with no reason recorded**, and are counted
as outstanding rather than cut: `SK4001`–`SK4004`, `SK4006`–`SK4008`, `SK6001`, `SK6004`, `SK6008`,
`SK8006`, `SK8007`. Two of them look cheap and obviously right, and their absence is a gap rather
than a decision.

## 5.4 Bars that were set and missed

| Milestone | Bar | Outcome |
|---|---|---|
| M3 | Line fidelity ≥ 99.9 % | ❌ 98.86 %. Re-stated as two bars |
| M3.1 | ≥ 99.5 % without `#if`; ≥ 99.9 % overall | 🟡 **99.79 % met; 99.70 % not** |
| M3.1 | [`plan/16`](plan/16-risks-and-open-questions.md) § R1 | ❌ 37 of 56 under the old rule. ⚠ **The rule itself was the defect** — it selected on occurrences and graded on lines owned, two unrelated populations, and was equivalent to 100 % line fidelity. Re-stated; under the re-statement three constructs fail and all three name an open `SK-DIV` entry |
| M5 | A week of agent work with no hand-formatting | ❌ The hooks are installed in Vixen; the week has not been observed |
| M6 | A `ci` gate on Vixen's CI in place of everything else | ❌ Vixen was analysed read-only; the gate ran against a git-initialised copy |
| M6 | False-positive triage of a 200-finding sample under 1 % | ❌ Not measurable as stated — the four new rules produce 7 findings on Vixen, which is the shipping bar working |
| M7 | Every budget in doc 13 met and asserted | 🟡 Met on macOS. ⚠ Windows unmeasured; the assertions are opt-in |
| M7 | Adopted by three repositories beyond Vixen | ❌ Zero. ⚠ A milestone whose exit criterion is a change to somebody else's repository fails on paper whatever it builds; adoption is its own step now |

---

# Appendix — headline numbers

Every row was produced by running the named command at `8cbd66d`.

| | Value | Produced by |
|---|---|---|
| Line fidelity, `corpus/real/`, with symbols | **99.70 %** (76 144 / 76 375) | `… -- fidelity` |
| …without symbols | 99.63 % (76 095 / 76 379) | same |
| File fidelity | **85.79 %** (326 / 380) | same |
| Files diverging | **54** of 380 | same |
| Divergent line slots | **231** | same |
| Files with no `#if` | **99.79 %** / 89.97 % (289) | `… -- preprocessor` |
| Preprocessor symbols supplied | 18, from `artifacts/skala.binlog` | same |
| Divergences under one symbol set only | 0 with, **65** without | `… -- fidelity` |
| R1, old rule | 56 constructs over 50 occurrences, **37 at 100 %** | `… -- constructs real` |
| R1, re-stated rule | **3 constructs fail** — `ParameterList`, `EqualsValueClause`, `Parameter` | same table |
| Documented divergences | **12** — 3 resolved, 2 half closed, 7 open | [`divergences.md`](divergences.md) |
| Options in the registry | **520** — A 201 · B **0** · C 6 · D 313 | `options.json` |
| Defaults derived from the oracle | **123** `oracle-probe` (297 `template`, 100 `unknown`) | same |
| Rule ids allocated | **32** | `skala rules list` |
| Catalogue coverage | **24 of 109** (22.0 %); 10 cut, 75 outstanding. It was 21 of 106 before three shipped metrics were reconciled in | `rules.json` ∩ [`plan/08`](plan/08-rule-catalogue.md) |
| Generated documentation | `docs/rules/` **32 pages** + README; `docs/site/` **143 files** (108 options, 32 rules, 2 indexes, 1 stylesheet) | `find docs/rules docs/site -type f` |
| Findings, `corpus/real/`, product path | **577** across 291 files from 33 rules | `skala check --load=loose --no-cache --include-hints --duplication` |
| Duplication, `corpus/real/` | **13** `SK7020` groups | same |
| Fix verification, `corpus/real/` | 171 fixes / 65 files; 15 743 → **15 731** errors; **0** pairs worse | `… -- audit` |
| Vixen source files analysed | **4 660** (4 717 `.cs` on disk) | `… -- audit <11 source dirs>` |
| Vixen compiler errors, loose | **195 253**; **128 490** with a global-usings stand-in | same |
| Tests | **9 795 passed · 0 failed · 7 skipped · 9 802 total** | `dotnet test Skala.slnx -c Release` |
| Conformance cases | **8 981** green | same |
| Single-file format | **202 ms** cold, 69 ms warm page cache | N = 20; ⚠ no budget — see § 4.3 |
| `format --check`, whole Vixen tree | **12.4–13.8 s** | 4 717 files |
| …files it would reformat | **2 346** reformatted, 2 371 left alone | same, under Vixen's own `.editorconfig` |
| Vixen `.editorconfig` | 916 lines, 56 path-scoped sections | `wc -l` |

## ⚠ Numbers this file does not carry

Recorded elsewhere, **not reproducible in this pass**, and therefore deliberately absent above rather
than repeated:

| Number | Where it is recorded | Why it is not here |
|---|---|---|
| `skala check --load=binlog` over Vixen, 58–134 s | [`plan/13`](plan/13-performance.md), [`plan/15`](plan/15-roadmap.md) § M5 | Needs `dotnet build -bl` **inside Vixen**, which writes to it. This pass was read-only against Vixen and the number stands unrefreshed |
| Duplication over Vixen, 4.8 % / 514 clone groups | [`plan/15`](plan/15-roadmap.md) § M6 | Same — the run that produced it wrote a cache into Vixen and was reverted |
| `ci` gate end-to-end timings (18.9 s / 7.2 s / 8.3 s) | [`plan/15`](plan/15-roadmap.md) § M6 | Ran against a git-initialised copy of Vixen's `Core/` that no longer exists |
| M1/M2/M3/M5 fidelity figures | [`divergences.md`](divergences.md) trajectory table | Historical. The formatter they describe no longer exists; they are a trajectory, not a claim about today |
| The 600-file whole-tree sample (99.47 % / 87.33 %) | [`plan/15`](plan/15-roadmap.md) § M3.1 | `tree <dir> [n]` takes tens of minutes and was not re-run |
| M7's 128 833 Vixen error count | [`plan/15`](plan/15-roadmap.md) § M7 | The global-usings stand-in behind it was never committed. Re-derived here at **128 490** with a fresh one |

---

*Measured against the repository at `8cbd66d` on 2026-08-27. Where this file and a document under
[`plan/`](plan/) disagree, this file is the one that was checked.*

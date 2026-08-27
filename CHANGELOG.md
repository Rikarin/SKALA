# Changelog

Skala's versions follow [docs/plan/02](docs/plan/02-repository-layout.md) § "Repository policy":
semver, where a **formatting output change is a minor bump at minimum** and is listed here with the
corpus effect. That rule is stricter than semver needs because downstream a formatting change is a
repository-wide commit, and a tool that changes what a file looks like in a patch release makes that
commit happen by accident.

⚠ **Every number below is the one the milestone reached, not the one it aimed at.** Where a bar was
missed it says so and by how much; three of them were, and one of those is still open at 1.0.

---

## Unreleased

### Added — five `SK1xxx` modernization rules

The range doc 08 calls "the reason the tool exists in an AI-heavy workflow", which was a quarter
built. Eight were attempted and **five** ship, all at `suggestion` — the range's default, unchanged.

| Id | Rule | Floor | corpus/real | Vixen |
|---|---|---:|---:|---:|
| `SK1001` | Collection expression where the target type is written | 12 | 12 | 1 |
| `SK1006` | `using` declaration where the block runs to the end of the scope | 8 | 5 | 9 |
| `SK1015` | `is T t` instead of `is T` and a cast | 7 | 1 | 0 |
| `SK1031` | Null-conditional assignment | 14 | 0 | 13 |
| `SK1033` | `TryGetValue` / `TryAdd` instead of `ContainsKey` and a second lookup | 7 | 0 | 2 |

Every one of the 43 findings was read; none is a false positive, and applying every fix over both
trees introduces **0 `(file, id)` pairs worse than before**.

⚠ **`SK1033` was measured wrong before it shipped.** `if (!d.ContainsKey(k)) d[k] = Build();` calls
`Build()` only when the key is absent; `d.TryAdd(k, Build())` calls it every time, because C#
evaluates arguments before the call. Two of the Vixen findings mattered — one mutated a mesh, one
built one — so the written value must now be a name or a literal. A fixture set cannot find that;
only a tree can.

⚠ **Three of the five move a declaration one scope outwards**, because C# scopes an `out var` or a
pattern variable declared in an `if` condition to the *enclosing block*. A name in a neighbouring
scope is invisible to a lookup at the destination and is still `CS0136`, so the guard scans the whole
member. It over-bails, which costs a finding where the alternative costs a build.

### Changed — the fix round-trip goes through the binder

`EveryFix_ProducesTextThatStillParses` checks that edited text parses, which misses every fix that is
wrong at *binding*: a pattern inside an expression tree is `CS8122` and a declaration lifted into a
taken scope is `CS0136`, and both parse. `FixRoundTripTests` re-compiles the edited text, compares
error counts per diagnostic id, and asserts the rule no longer fires on its own output — which
catches a fix that is correct but is not a fix. It covers every rule in the catalogue that declares
one and finds its analyzers by reflection.

### Fixed — two defects in how the reference trees were measured

- `fidelity audit --implicit-usings` supplies the global-usings file the SDK writes into `obj/`,
  which the loader skips. Without it a tree that sets `ImplicitUsings` binds `Dictionary<,>` to an
  error type and most of the semantic rule set goes quiet for the wrong reason: over Vixen, 195 724
  errors against 128 833, and `SK1033` 0 findings against 5. Doc 15 § M7 records this stand-in being
  used and never committed, which is why M7's figures were not reproducible from the repository; it
  is a constant in the harness now.
- Auditing a repository that has agent worktrees nested inside it counted every file once per
  worktree — 13 743 files for Vixen's 4 681, and 1 585 971 errors for its 128 833. The measurements
  above name the source directories explicitly. ⚠ `EnumerateSources` itself still has no
  `.claude/worktrees` exclusion beside its `obj/`, `bin/`, `.git/` and `artifacts/` ones.

### Added — the documentation-comment sub-formatter, behind `skala format --xmldoc`

⚠ **Off by default, and off is the setting that agrees with Rider.** `jb cleanupcode` 2025.2.6 does
not format documentation comments at all — SK-DIV-0006, re-verified at this release by a committed
oracle fixture — so nothing in the `resharper_xmldoc_*` family can be pinned the way every other
option in Skala is pinned, and turning it on by default would be a divergence from Rider on every
doc comment in every repository.

- **Seventeen of the twenty-seven `resharper_xmldoc_*` keys** are honoured under the flag, plus
  `resharper_space_after_triple_slash`. **Ten are refused with a reason each**, six of them under one
  rule: a tag header is emitted byte-for-byte and never broken open, so it has no attribute style,
  no attribute indent, and no spaces around its `=`.
- ⚠ **None of them is Tier A and none of them can become Tier A.** They are read through the
  registry's inert path. What pins them instead is hand-written fixtures for the documented
  semantics, a round trip checked on every comment of every run, and four corpus-wide properties.
- ⚠ **The output effect, measured over `corpus/real/`** (380 files, 3 032 doc comments): line
  fidelity 99.63 % → 96.04 % with the flag on, and **99.53 % → 99.53 % with every `///` line
  excluded from both sides** — nothing the flag is not allowed to touch moved. 3 030 comments
  re-wrap and round-trip clean; the 2 left are the 2 that are not well-formed XML.
- Malformed doc comments stay byte-identical and reported at `hint` (`SK0003`) under every setting,
  now with a corpus fixture that the real oracle produced.

### Changed

- The safety net gained the allowance for the xmldoc rewrap that
  [04](docs/plan/04-formatting-engine.md) § "The safety net" had described for four milestones
  without it existing. It applies only under `--xmldoc`, only to `///` trivia, and it is the
  sub-formatter's own signature — which compares a `<code>` body **byte-for-byte**, so the net is
  stricter there than it was before, not looser.
- `format --xmldoc` does not use the daemon. The daemon protocol carries no such flag, and serving
  the request would silently format without the sub-formatter.

---

## 1.0.0 — 2026-08-27

The version at which four surfaces become compatibility promises (ADR-012). What is *not* frozen is
the longer and more useful list, and it is in the [README](README.md) § "What 1.0 means".

### Frozen at 1.0

| Surface | What the promise is |
|---|---|
| **Rule ids** | `SK` + four digits, allocated once and never re-purposed. A baseline fingerprint carries the id, so one number with two meanings silently un-suppresses one finding and wrongly suppresses another in every repository holding a baseline. Held by `RuleCatalogTests` and `ToolDiagnosticIdTests`, both now reading the tree they are run against. |
| **Option behaviour** | An `.editorconfig` key that Skala honours keeps meaning what it means. |
| **Exit codes** | `0` nothing to do · `1` gate failed · `2` formatting needed · `3` configuration error · `4` load failure · `5` internal error · `130` cancelled. |
| **The SARIF shape** | Fields present at 1.0 stay present and keep their meaning; paths repository-relative with forward slashes on every platform. |

### Not frozen, and expected to change

The formatter's output (fidelity is 99.70 % of lines; closing the gap reformats files), which rules
exist and their default severities, the daemon protocol (exact-match, no negotiation), and every
developer instrument — `--profile`, the fidelity harness, `Rikarin.Skala.Testing`'s subcommands.

### Packaging

Five packages, all built by `./build.sh Pack`
([02](docs/plan/02-repository-layout.md) § "Package boundaries"):

| Package | `.nupkg` | What it is |
|---|---:|---|
| `Rikarin.Skala.Cli` | 32.9 MB | The `skala` tool. RID-specific: the command is the NativeAOT client, with the full `skala-tool` shipped beside it. |
| `Rikarin.Skala.Rules` | 74 kB | The `SK` analyzers, for the build and the IDE. |
| `Rikarin.Skala.MSBuild` | 9.8 kB | The build integration. `format --check` after `Build`; `check` when `SkalaMode=check`. |
| `Rikarin.Skala.Canonical` | 43 kB | The canonical `.editorconfig` payload (`0.1.0`) and a check-only build target. |
| `Rikarin.Skala.Sdk` | 5.2 kB | The meta package. One `PackageReference` adopts Skala. |

⚠ **`Rikarin.Skala.MSBuild` and `Rikarin.Skala.Sdk` did not exist before this release**, though doc
02 had named them since M0. Verified by installing all five from a local feed into a fresh
`git init` and using them — see [11](docs/plan/11-cli-and-integrations.md) § "Verified by installing
it" for the run and its numbers. That verification found four faults invisible from inside the
repository, the worst being that **`Rikarin.Skala.Rules` had never been restorable by anybody**: it
declared a dependency on `Rikarin.Skala.Rules.Metadata`, an id nobody publishes.

⚠ **The tool package ships both binaries.** M7 split the CLI into a NativeAOT `skala` and a
framework-dependent `skala-tool`; a package with only the second throws away the 8.65 ms warm number
for everyone who installs from NuGet, and a package with only the first exits 5 on every command
that is not a warm single-file format. `Environment.ProcessPath` resolving the install symlink is
what makes the adjacency work, and it was measured on a probe package before anything was built on
it.

### Known gaps at 1.0, stated rather than implied away

- **Line fidelity is 99.70 % against a 99.9 % bar** — about 230 divergent line slots where 99.9 %
  needs 76. Twelve documented `SK-DIV-*` entries; the two largest classes are ones where ReSharper's
  actual rule was swept for and not found.
- **`arrange` is unfinished** (M4 is deferred), `SK5xxx` security does not exist (M8), and web
  languages do not (M9).
- **32 rule ids are allocated; far fewer analyzers ship than doc 08 lists.** Each milestone's
  "Rules shipped" row says which were cut and why.
- **Windows is in the CI matrix and unverified on real hardware.**
- **The nightly fuzzing job runs the property suite; there is no fuzzer** — no seeded mutation
  driver, no weighted grammar, no delta-debugging minimiser.
- **No repository beyond Vixen has adopted the tool**, and Vixen is read-only in every milestone so
  far. `skala format` over Vixen is a 2 527-file, 73 014-line diff that has never been committed.
- **`--verbose` is not implemented on `check`**, and an unrecognised flag there binds as a path
  rather than erroring.

---

## The road to 1.0

Nine merges, 2026-08-26 to 2026-08-27. Each row is what the branch was worth, measured at the merge.

### M7 — Hardening · `8cbd66d` · 24 commits

The CLI splits into a NativeAOT thin client over a dependency-free protocol assembly, with the full
tool behind it.

- ✅ **Warm single-file format: 8.65 ms against a 40 ms budget**, from 66.9 ms. `skala daemon status`,
  doing no work at all, cost 79.5 ms before; the AOT client starts in 4.85 ms against a 1.9 ms
  process floor.
- ✅ Three budgets asserted in CI with doc 12's 20 % band: cold 170 ms/250, warm 48 ms less a 10 ms
  harness floor/40, daemon RSS 160 MB/1.5 GB.
- ⚠ **Three rules** — `SK4010`, `SK6003`, `SK8005` — of the twenty-three the `SK4xxx`/`SK6xxx`/`SK8xxx`
  sets name. Zero false positives across 26 findings, every one read.
- ✅ Cross-platform matrix (macOS, Linux, Windows, `fail-fast: false`), plus `lint` and `performance`
  jobs CI was running nowhere.
- ✅ Vulnerabilities 8 → 0. `NuGetAudit` at `low`, NU1901–NU1904 as errors, including `_build`.
- ✅ **9 787 tests green**, up from 5 402.

⚠ Four bugs it found that were not on its list, two silent: **the daemon could not start in any
repository nested deeper than about eighty-five characters** — a Unix socket path caps at 104 bytes
and the exception was unhandled, so it died with **exit code 0** and every later format took the cold
path without saying so; the **named-pipe transport had never existed** despite a comment describing
it; and `ToolDiagnosticIdTests`, the ADR-012 guard added one merge earlier, **was passing without
reading the tree under test**, because `.git` is a file rather than a directory inside a worktree.
Confirmed fixed by mutation.

### M6 — Correctness rules, metrics, duplication, baselines, gates · `dd39851` · 1 commit

- ⚠ **Four analyzers** — `SK2013`, `SK2015`, `SK3002`, and `SK3001` off by default — of the
  twenty-nine `SK2xxx`/`SK3xxx` lists, plus seven metrics and duplication.
- ✅ Zero false positives. `SK3002` is the only rule with corpus occurrences: 7 on Vixen, all seven
  read and all seven true.
- ✅ Duplication over Vixen: **4.8 % production, 514 clone groups**, 4 660 files, 37 s inside a full
  `check`.
- ✅ `ci` gate end to end: baseline 18.9 s → clean PASS 7.2 s exit 0 → finding introduced FAIL 8.3 s
  exit 1 → `--since` scoping it away PASS 7.3 s exit 0.
- ✅ `--no-new-suppressions` across all four mechanisms; **3 m 19 s → under a second** once the audit
  stopped spawning one `git show` per file.
- ✅ **5 402 tests green.**

⚠ The incremental cache did not carry the fingerprint's terms, so a baseline expired on the first
warm run: 686 accepted, 686 "new" and 686 "fixed" on a tree where nothing had changed.

### Q4 — Canonical `.editorconfig` distribution · `c179146` · 1 commit

The hypothesis in doc 16 — a package that drops the canonical at restore time — **disproved by probe
rather than by argument**. `content/` and `contentFiles/` do not copy to the project directory; a
`BeforeTargets="Restore"` target never runs, because package targets arrive via
`obj/*.nuget.g.targets` which restore is generating at the time; and the build-time drop is worse
still — on a repository where the canonical makes a violation an error, **the first two builds passed
and only the third, non-incremental one failed**. A gate whose first two runs pass is not a gate.
`.editorconfig` globs also resolve relative to the file's own directory, so a canonical in the NuGet
cache has a `[*]` matching the NuGet cache.

What shipped instead: the package carries the payload and a **check-only target at 5 ms per
project**, one `.editorconfig` with a canonical block and a local block after it so editorconfig's
own later-wins rule does the layering, and drift decidable offline from the file alone.

⚠ `SK9010` and `SK9011` were renumbered to `SK9013`/`SK9014` before the merge — both were already
live in the formatter, and ADR-012 makes an id permanent. It was caught by eye during review, which
is not a mechanism; M6 added the test.

### M3.1 — The fidelity tail · `4da2a70` · 13 commits

- ✅ **99.79 %** line fidelity on the 289 files with no `#if` — the ≥ 99.5 % bar, **met**.
- ⚠ **99.70 %** overall with symbols — the ≥ 99.9 % bar, **not met**, and the measurement argues it is
  unreachable by more of the same work: 230 divergent line slots where 99.9 % needs 76, and the two
  largest classes are ones where the oracle was swept and the rule not found.
- **99.63 %** overall without symbols (M5 left it at 98.86 %).
- ⚠ R1: **37 of the 56** constructs occurring more than 50 times are at 100 %, up from 27 of 54.
- ✅ All six properties at 100 % on all three corpora **under both symbol sets** — 8 981 conformance
  tests green.

⚠ Two of the fitter's four measures had been returning zero since M3 and no property caught it. What
moved the number was **not** the preprocessor: symbols are worth 0.07 points and the milestone gained
0.77.

### M5 — Analysis host, rules, SARIF, the agent surface · `330a2ad` · 3 commits

- ✅ `skala verify`, five files, `--load=loose`, cold process: **0.39–0.54 s** clean, 0.50–1.02 s when
  all five have findings, against a 1 s budget.
- ✅ `skala check --load=binlog` over Vixen: **58–134 s** against a 4-minute budget; 4 688 files, 60
  compilations.
- ⚠ **Six analyzers** plus three formatter findings, not the thirty-six doc 08 names.
- ✅ Zero false positives: 143 findings on `corpus/real/`, 12 on Vixen, every one reviewed.
- ✅ SK-DIV-0004 closed — `--define`, and symbols from a loaded compilation. 98.60 % → **98.92 %** on
  the 91 `#if` files.

⚠ The incremental cache buys **12 %, not an order of magnitude**, on a small solution: the analyzer
pass is not the cost there, reading the binlog and re-running generators is.

### M3 — Wrapping, the fitting engine, daemon, LSP · `d74779e` · 17 commits

- ⚠ **98.90 %** line fidelity against a 99.9 % bar — the merge's own independent re-measurement;
  [15](docs/plan/15-roadmap.md) § M3 records 98.86 % from the branch harness. Merged short, with the
  roadmap revised by the measurement that explains it rather than by lowering the number.
- ✅ Whole-corpus format **34.2 s → 11.9 s**.
- ✅ ReSharper defaults derived from the oracle rather than guessed: 123 keys oracle-probed, `distill`
  drops 108 where it dropped none.
- ✅ 4 928 tests green.

⚠ The warm single-file budget was **missed at 60–70 ms against 40 ms**, essentially all of it the
client's own process start. `skala daemon status`, doing no work, was the same 60 ms. NativeAOT for
the client was named as the fix here and delivered in M7.

### M2 — Break presence and position · `5b7b1a1` · 2 commits

- ✅ **97.48 %** line fidelity against a ≥ 93 % bar (the merge's independent re-measurement;
  [15](docs/plan/15-roadmap.md) § M2 records 97.47 %).
- ⚠ The Vixen diff is **not** "small enough to read in one sitting": 2 374 files of 4 703, against
  M1's 999. Roughly half was a configuration artefact — `options.json`'s `default` was the export's
  value rather than ReSharper's — and repairing it took the diff to 2 506 and agreement under Vixen's
  own configuration from 97.00 % to 97.84 %.
- ✅ 4 775 tests green. 172 of 483 options at Tier A.

⚠ A `format --diff` write bug found and fixed in this milestone had rewritten five Vixen trees before
it was caught; all five were restored, verified as zero `.cs` files modified in the main tree or any
of the four worktrees.

### M1 — Spaces, blanks, braces, indentation · `e62ad85` · 3 commits

- ✅ **94.44 %** line fidelity against an ≥ 85 % bar, reproduced with an independent LCS diff. (M2
  re-states it as 94.26 % on its own basis; the two populations are not identical and the difference
  was never reconciled.)
- ✅ A second format pass produces no edits; all 380 corpus files identical to input modulo
  whitespace; no crash artefacts.
- ✅ 4 372 tests green. 130 of 483 options at Tier A, each pinned by a committed fixture.

### M0 — Configuration model and repository skeleton · `3fc1935` · 7 commits

- ✅ The option registry: **483 options, tiered**, with an incremental generator over it.
- ✅ `.editorconfig` ingestion with provenance and ReSharper language specialisation.
- ✅ `skala config explain|check|diff|distill|fix` with `SK9001`–`SK9006`.
- ✅ 71 tests. All three definition-of-done criteria reproduced independently.

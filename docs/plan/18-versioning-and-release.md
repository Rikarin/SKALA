# 18 — Versioning and Release

The version is **measured**, not declared. A release job builds the previous release's tool beside
this one's, runs five detectors over the pair, and the number falls out of the highest verdict.

## Why this document exists

[02](02-repository-layout.md) § "Repository policy" says semver, with a formatting output change as
a minor bump at minimum. It does not say who decides that a formatting output change happened, and
until M10 the answer was "whoever writes the commit message". This document replaces that with a
measurement, and describes the pipeline that performs it.

⚠ **Nothing here publishes.** The pipeline computes the version, creates the tag, packs, writes the
notes, uploads, and prints exactly what a publish would push. The publish step exists, is complete,
and is gated on a repository variable nobody has set. See § "Armed, not firing".

## The decision: what a version number of Skala is a statement about

Skala's compatibility surface is **not an API**. Nobody calls its assemblies — the project graph
forbids it ([02](02-repository-layout.md) § "The project graph", asserted by `ProjectGraphTests`).
What consumers depend on is **output**: formatted bytes, exit codes, rule ids, the SARIF shape,
option behaviour.

That inverts the usual semver instinct. A patch-level fix in the fitting engine changes formatted
output; downstream that is a repository-wide diff and, per [11](11-cli-and-integrations.md)
§ "Distribution", a merge-conflict generator between two developers on different versions. It is
breaking in effect while looking like a patch, which is why doc 02's policy is stricter than semver
needs and why the detector for it is the one this document spends most of its length on.

### ⚠ Why not conventional commits

Deriving the version from commit-message prefixes would derive it from **the least reliable artefact
in this pipeline**. The evidence is in this repository's own record, four times over:

| What a summary claimed | What a measurement found |
|---|---|
| the property suite was the fuzzing job | the fuzzer found **seven** defects no green suite saw |
| the Tier A option set was honoured | the sweep found **69** Tier A options with nothing substantiating them |
| the catalogue was ~27 % of ReSharper's coverage | the parity map measured **5 %** |
| the exit-code table was right and tested | `ExitCodeContractTests` found it **inverted**, from M1 to M9 |

A release process for a tool whose entire premise is "measure it rather than assert it" cannot rest
on a prefix somebody typed. The rule of the repository is that a claim without a denominator is not
a claim, and `feat:` has no denominator.

⚠ It is worth being precise about what conventional commits would have got *wrong* here, rather than
merely unreliable. The first real run of this pipeline (§ "What the first run found") classified the
tree as **major** on two surfaces, and neither would have been announced by any plausible commit
prefix. The exit code for "this file needs formatting" changed from 1 to 2 — a `fix:`, and correct,
and breaking. Six `.editorconfig` defaults changed on keys that were already honoured — three of them
`keep_existing_*` going `false → true`, which is the arrangement work landing, a `feat:` by any
honest reading, and it changes formatting for every repository that never wrote those keys down.

## The detectors

Five, all built on machinery that already exists. Each reports one of `major`, `minor`, `patch`, or
**unmeasured**; the release takes the highest, with `patch` as the floor.

| Detector | Built from | Bump |
|---|---|---|
| Formatted output differs over the corpus | the differential harness, pointed at two *Skala* builds | ≥ **minor**, with the diff summary in the notes |
| Rule removed, retired, or default severity raised | `rules.json`, `allocated-ids.txt` | **major** |
| Rule added firing at `warning` or above | same | **minor** |
| Exit-code table or observed exit code changed | doc 09's table, and both binaries run over five scenarios | **major** |
| SARIF shape changed | a real report from each binary | **major** |
| Option A→D, or a default or type changes **on a key that was honoured** | `options.json` | **major**; D→A is **minor** |
| None of the above | | **patch** |

⚠ **`unmeasured` is not `patch`.** A detector with no baseline contributes *nothing* to the verdict
rather than contributing a floor it did not measure, and the notes say "unmeasured" where they would
otherwise say "unchanged". The two produce the same version number and must not produce the same
sentence — which is the failure mode three of this repository's four previous guard mechanisms had.

### The output detector, which is the one that matters

It is `Fidelity.Compare` from `Rikarin.Skala.Testing` — the same line diff and the same divergence
classifier the fidelity report ranks its work queue with — with the oracle taken out of it and a
second Skala build put in its place.

Four things make it a measurement rather than a tautology, and each corresponds to a way it was
observed to break while being built:

1. **Two binaries, and they are checked for being two.** Both sides are separate processes. If the
   two paths resolve to bytes with the same SHA-256, the run **throws**. A detector comparing a
   build against itself reports "no change" forever and is indistinguishable from a green one. Both
   fingerprints go into the release notes so the claim is checkable by a reader.

2. **The comparison set is the two corpora's intersection.** The corpus only grows
   ([12](12-conformance-and-testing.md) § "Corpus expansion"), and a file added since the previous
   release has no "before". ⚠ This is not bookkeeping: on the first real run, 60 of 765 files were
   new and **the previous release's tool crashed on one of them** — with an unhandled
   `IndexOutOfRangeException` in `EditEmitter`, taking the whole measurement down. Those files exist
   *because* they broke the formatter; measuring against them is measuring the corpus, not the tool.

3. **A tool that refuses a file is recorded, not fatal.** Formatting runs in chunks of 128, and a
   failed chunk is retried one file at a time from a pristine copy. A comparable file that one side
   can format and the other cannot **is** an output change — the strongest kind — and is counted as
   one, named in the notes in the direction it moved.

4. **Nothing is formatted in this process.** Both sides are read back off disk after an external
   `skala format` wrote them. `Rikarin.Skala.Release` links the formatter, because `Fidelity` lives
   beside it, so this is a rule the code has to keep rather than a property of its dependencies.

The inputs and the configuration are held constant and both come from the candidate: the corpus, the
repository's `.editorconfig` and `skala.jsonc` are staged at their **real relative paths**, so
`.editorconfig` discovery walks the directories it walks in the repository rather than resolving
against a flat scratch folder. The only variable in the experiment is which binary ran.
⚠ `SKALA_NO_DAEMON=1` used to be set on every invocation, because two tool versions racing for one
per-repository daemon would have measured the daemon. The daemon is deleted; there is one path, and
it is the one being measured.

### The exit-code detector reads the document *and* runs the binaries

`ExitCodeContractTests` records that the contract was wrong from M1 to M9 and that every test in the
tree agreed with it: the constants matched each other and neither matched the command. So this
detector does both halves. It parses the table out of [09](09-quality-gates-and-reporting.md)
§ "Exit codes" — the thing hooks, CI and agents are written against — and separately runs **both
binaries** over five scenarios that need no compilation. A row that moves in the document is a change
to what people were told; a code that moves in the binary is a change to what happens. Either is
major, and on the first run both had.

The probes deliberately avoid anything requiring a project load. A detector whose verdict depends on
whether the runner had an SDK is measuring the environment.

### The SARIF detector compares shape, never values

The shape is read out of a **real report from each binary**, not out of the writer's source: a report
model can be refactored without changing a byte of output, and a serializer setting can change every
byte without touching the model. What is compared is the set of JSON paths and their value kinds,
with array indices collapsed and unioned across elements. Values are discarded — the report carries
the tool's own version, two timestamps and a fingerprint per finding, and a value comparison would
report "the SARIF changed" on every release and mean nothing.

⚠ The probe input is misformatted on purpose so that `SK0001` fires. An empty `results[]` exercises
none of `locations`, `partialFingerprints` or `fixes`, and `partialFingerprints` is what every
baseline in every repository is keyed on. The run asserts at least 20 paths for the same reason: two
near-empty reports compare equal.

### The option detector reads the registry the sweep wrote

⚠ It does **not** re-run the conformance sweep. The sweep needs JetBrains installed and takes minutes
(ADR-011), so it is a nightly job and what the fast path reads is the committed result table, which
is `options.json`. The release measures the registry the sweep last wrote. Stating that plainly
matters: the tier numbers in a release's notes are as fresh as the last sweep, not as fresh as the
release.

## Rejected alternatives

| Alternative | Why not |
|---|---|
| **Conventional commits** | § "Why not conventional commits". Derives the version from the artefact this repository has the most evidence against. |
| **A `[breaking]` label on the PR** | Same failure with a nicer interface: a human classifies a change whose effect nobody has measured. |
| **Compare the formatter's *source* between releases** | Source diff has no denominator, and the two runs in this document are the demonstration: **125 commits** of formatter work between 1.0.0 and `master` moved **3 lines** of the corpus, and **one inverted boolean** moved **7 326**. A source diff would have called the first one large and the second one small. |
| **Compare against the *oracle* rather than the previous release** | That is the fidelity measurement and it answers a different question. Fidelity going *up* still reformats every repository, and a release whose fidelity is unchanged can still have moved 400 files. |
| **Run the detector against the candidate's own fixtures** | The committed `.expected.cs` fixtures are regenerated by `./build.sh Oracle` in the same commit as a formatting change ([02](02-repository-layout.md) § "Repository policy"), so they agree with the candidate by construction. The measurement has to come from a binary that predates the change. |
| **Diff the packed `.nupkg` bytes** | Deterministic builds make this fire on the version string alone, and it says nothing about what changed. |
| **Manual version bumps in `Directory.Build.props`** | This is the status quo, and it produced `since: 1.2` in `rules.json` describing two releases that never happened. |

## One source of truth

**`Directory.Build.props` carries the version, and nothing else does.** `VersionPrefix` and
`VersionSuffix`; MSBuild composes them into `Version`. The release workflow overrides `Version` for
the duration of one `dotnet pack` and never edits the file.

`VersionSourcesTests` in `Rikarin.Skala.Core.Tests` is what keeps a second one from appearing. It
asserts, against the real tree:

- `<VersionPrefix>` appears exactly once, and **no `.csproj` declares its own** `<Version>`,
  `<VersionPrefix>` or `<VersionSuffix>` — the five packages ship as a set, which is what makes one
  number sufficient to pin the whole surface;
- the version parses, and a suffix is a well-formed pre-release identifier (NuGet orders `alpha.9`
  before `alpha.10` only if both halves are);
- ⚠ **no `since` in `rules.json` or `options.json` is ahead of it.** `since` is a version, in two
  registries, and both reach a consumer — through `rules[].properties.since` in the SARIF and
  through `docs/rules/`. This test failed the moment it was written: `rules.json` carried `1.1` and
  `1.2` against a declared `1.0.0` and zero tags;
- the `CHANGELOG.md` top released section is not ahead of it;
- and the canonical payload version is **not coupled** to it — see below.

### The canonical payload keeps its own version, and nothing may join them

Confirmed, not changed. `Distribution/Rikarin.Skala.Canonical/canonical.json` carries `0.1.0`, set
only by `./build.sh Canonical` with its own parameter. A canonical bump is a repository-wide
reformatting commit and a tool bump is not; a repository must be able to take a bug fix without
taking the reformat.

`VersionSourcesTests.TheCanonicalPayloadVersion_IsNotCoupledToTheToolVersion` asserts the
**mechanism** rather than the current values — that `Build.CanonicalVersion` is a literal and not
derived from `VersionPrefix`, and that no source in `build/Rikarin.Skala.Release` reads
`canonical.json`. Two numbers that happen to differ today are not two numbers that cannot be joined
tomorrow. The release pipeline computes no canonical version and never will.

## The number

1. The **current** version is the highest `v*` tag; with no tags, what `Directory.Build.props`
   declares.
2. The **verdict** is the highest contribution across the five detectors, floor `patch`.
3. If the current version is a **pre-release**, the verdict is *recorded* and the counter advances;
   the major does not. `2.0.0-alpha.7` means 2.0.0 has not happened, so nothing inside the series is
   a compatibility promise and advancing the major would publish 3.0.0 before 2.0.0 existed.
4. Otherwise the verdict is applied: major → `X+1.0.0`, minor → `X.Y+1.0`, patch → `X.Y.Z+1`.

### Pre-release identifiers, and which pushes tag

⚠ **A `master` push does not tag and does not publish.** It measures, computes, packs, uploads and
prints. A tag is a permanent public identity, and doc 11 makes pinning a correctness feature — a pin
to a version nobody can rebuild is worse than no pin. Tagging every push also means publishing every
push, which is the thing § "Armed, not firing" exists to prevent.

A `master` build is stamped `X.Y.Z-alpha.N`, where `N` is **the baseline's counter plus the commit
count since the baseline tag**.

⚠ The "plus the baseline's counter" is not decoration. With a bare height, a baseline tag of
`v2.0.0-alpha.126` one commit ago produces `2.0.0-alpha.1`, which sorts *below* the tag it was
measured against; NuGet would resolve the older package for `--prerelease`. A version that goes
backwards is worse than no version. This was found by the mutant run in § "Proving the detector
fires", which produced exactly that.

A **release** is a `workflow_dispatch` with `release: true`. It drops the `-alpha.N` and produces the
computed number.

### Why the first published artefact is a pre-release

⚠ **This is the recommendation to "start at `0.x`", evaluated and rejected — and replaced with
something that does what it was actually asking for.**

The recommendation's premise is right and the pipeline's first run substantiates it beyond what the
recommendation claimed. [02](02-repository-layout.md) § "Repository policy" records that 1.0 was
declared at M7 because ADR-012 froze four surfaces: rule ids, option behaviour, exit codes and the
SARIF shape. Run against the tree that declared it, the detectors report that **two of the four moved
within 125 commits** — the exit code for "this file needs formatting" went from 1 to 2, and six
`.editorconfig` defaults changed on keys that were already honoured. A 1.0 that broke its own freeze
inside a week, before publishing anything, was declared early. The README's "a version number is not
a claim of completeness" is a true sentence that NuGet does not read.

**But `0.x` is the wrong instrument, and it is wrong for a reason the design supplies.** At `0.x`
the convention is that *minor* carries breaking changes, which leaves exactly two meaningful
positions: "something changed that may break you" and "nothing did". This design has **three**, and
the distinction that gets destroyed is the load-bearing one — between *"your repository will show a
repository-wide diff"* (minor) and *"your baselines and your CI will break"* (major). Collapsing
those is the specific failure doc 02's stricter-than-semver policy exists to prevent. Retreating to
`0.x` to express "not finished yet" would cost the vocabulary in order to say something semver
already has a word for.

**What is done instead:** the version stays on the measured line, and the first published artefact is
a **pre-release** — `2.0.0-alpha.N`.

- Semver's pre-release identifier means precisely "this is not a compatibility promise", which is the
  whole content of the recommendation.
- NuGet makes it **opt-in**: `dotnet tool install -g Rikarin.Skala.Cli` finds nothing; the
  `--prerelease` flag finds it. For a tool with six open formatter defects in
  `Testing/corpus/pathological/open/register.md`, 236 of 520 options at Tier D, line fidelity at
  99.70 % against a 99.9 % bar, `arrange` unfinished and no adopting repository outside this one,
  that is the correct default for a bare install.
- The three-position vocabulary survives, and every alpha carries its own measurement.

**The gate for promoting `2.0.0-alpha.N` to `2.0.0` is a decision a person makes, and these are the
conditions it should be made against** — none of them a workflow's to check:

1. `pathological/open/register.md` is empty.
2. Line fidelity is at the 99.9 % bar doc 12 sets, or the shortfall is a documented `SK-DIV-*`.
3. At least one repository other than this one has adopted the tool and taken a version bump.
4. The last three alpha releases measured `patch` on the output detector — the surfaces have stopped
   moving on their own.

⚠ The jump from an unpublished 1.0.0 to 2.0.0 costs nobody anything, because nothing was ever
published under 1.0.0 — zero tags, zero packages on any feed. It is also the honest record: the
number says the surfaces 1.0 froze did not stay frozen, which is what happened.

### What a consumer pins

All five packages ship at one version, so one number pins the whole surface. The recommended form is
a **local tool manifest** ([11](11-cli-and-integrations.md) § "Distribution"), because a manifest
pins exactly and is committed:

```jsonc
// .config/dotnet-tools.json
{ "tools": { "rikarin.skala.cli": { "version": "2.0.0-alpha.127", "commands": ["skala"] } } }
```

For the analyzers and the build integration, an exact-version `PackageReference`:

```xml
<PackageReference Include="Rikarin.Skala.Sdk" Version="[2.0.0-alpha.127]" PrivateAssets="all" />
```

⚠ The bracket is the point. A floating `2.0.0-alpha.*` puts a different formatter on each developer's
machine, and doc 11 § "Distribution" calls that a merge-conflict generator rather than an
inconvenience. The two pins must also name the **same** version: the tool formats and the analyzer
reports `SK0001` when a file is not formatted, so a tool and an analyzer at different versions
disagree about whether the tree is clean.

## The notes are the deliverable, not a byproduct

Generated from the measurements, never from the commit log. `release-notes.md`, `changelog-entry.md`
and `version.json` are written on every run, and the notes go into the job summary whole — a reader
should not have to download an artefact to find out that this release moves 400 files.

Doc 02 § "Repository policy" already requires a formatting change to be listed with a corpus diff
summary, because downstream that change *is* a commit in someone's repository. So the notes say
"formatting output changed on N of M corpus files, in these divergence classes", "3 rules added at
warning, 1 retired", "12 options moved D→A" — every line a number a detector produced. Where a list
is truncated it prints the count of what was dropped: a list that says "12 of 340" is a measurement,
and a list of 12 with no denominator is a sample.

`changelog-entry.md` keeps the format `CHANGELOG.md` already has — `## <version> — <date>` with
`### Added/Changed/Fixed` beneath — because that file was written by hand from the merge history and
a generator that reformatted it would make the whole record unreadable in one commit.

## ⚠ Armed, not firing

The pipeline is complete up to and not including the push.

| Step | Runs |
|---|---|
| measure the version | every `master` push and dispatch |
| pack every artefact, per RID | ditto |
| install smoke test | ditto |
| write the notes, upload them | ditto |
| **create the tag** | on a dispatch with `release: true` — **created in the job, not pushed** |
| print exactly what would be published | ditto |
| **push the tag, push to NuGet, open a GitHub Release** | **only when `vars.SKALA_PUBLISH == 'armed'`** |

The `publish` job is written out in full rather than left as a TODO, and that is deliberate: a
publish step that does not exist gets written in a hurry on the day somebody wants to publish, which
is the worst day to write it. What it must not become is a step that runs on a push to `master`.

`SKALA_PUBLISH` is a repository variable nobody has set, so the job's condition is false on every
run today — including a dispatch with `release: true` and including a tag push. The job also targets
a GitHub `environment`, which can require a human approval that a variable cannot. Arming it is one
deliberate act, by a person who has read the notes the measure job wrote.

## What the first run found

`./build.sh ReleasePlan --baseline-ref d8e9d34 --baseline-version 1.0.0` — `d8e9d34` being the commit
that wrote `## 1.0.0` into `CHANGELOG.md`, 125 commits back. Measured in **13 s** after the baseline
tool was built.

| Surface | Verdict | Measurement |
|---|---|---|
| formatted output | minor | 1 of 705 comparable files, 3 lines, class `indentation (-4 columns)`; 60 files added to the corpus |
| rule catalogue | minor | 15 rules added, **10 of them at `warning` or `error`** (`SK2007`, `SK3004`, `SK3007`, `SK3501`, `SK3503`, `SK5001`, `SK5002`, `SK5005`, `SK5007`, `SK5009`) |
| exit codes | **major** | 4 changes — see below |
| SARIF shape | — | unchanged, 53 paths |
| option registry | **major** | 84 changes: **6 defaults changed on Tier A keys**, 77 moved `D → A`, 1 type changed while inert |

**Verdict: major. Version: `2.0.0-alpha.<height>`** for the `master` build — `alpha.126` at the
commit it was first run on — and `2.0.0` for a release.

The exit-code findings are the ones worth reading, because they are what a hand-written changelog
would have missed:

- `format --check` on an unformatted file exited **1** at 1.0.0 and exits **2** now.
- `format --diff` on an unformatted file: the same inversion.
- `format --check` on a path that does not exist exited **0** at 1.0.0 and exits **3** now.
- doc 09's row for exit 3 was reworded.

⚠ Those are the *fixes* `ExitCodeContractTests` was written for, and they are correct. They are also
breaking: a hook told to auto-format on 2 and stop on 1 did the opposite of both at 1.0.0, and a
CI job that treated "nothing to format" as success now fails on a typo'd path. The detector's job is
to make sure that lands in the version number and in the notes rather than in a bug report.

⚠ **77 of the 84 option changes are in the safe direction** — those keys moved `D → A`, meaning they
are honoured now and were inert before, which is `minor`. The `major` is **six defaults that changed
on keys that were already Tier A**, three `keep_existing_*` and three wrapping keys:

```
resharper_csharp_keep_existing_expr_member_arrangement          false → true
resharper_csharp_keep_existing_property_patterns_arrangement    false → true
resharper_csharp_keep_existing_switch_expression_arrangement    false → true
resharper_csharp_wrap_after_declaration_lpar                    true  → false
resharper_csharp_wrap_before_declaration_rpar                   true  → false
resharper_csharp_wrap_extends_list_style           chop_if_long → wrap_if_long
```

A default is what applies to every repository that has not written the key down, which is most of
them for most keys, so those six change formatting for everybody who never opted in. **This is the
second of the four surfaces 1.0 froze that moved**, and unlike the exit codes it is not visible in
the output detector's 3 lines — the corpus does not exercise those constructs at those widths.

⚠ **The detector was wrong about this on its first pass and was corrected.** It reported
`dotnet_style_require_accessibility_modifiers` changing type from `string` to an enum as major. That
key was **Tier D at the baseline** — inert, doing nothing — and its type changed as part of
*implementing* it in the same release. A default or a type only breaks a promise on a key that was
honoured, so the detector now requires that, and reports the rest as "changed while inert". Left
uncorrected it would have cried major on the ordinary work of implementing an option, which is the
fastest way to make a version number stop meaning anything.

## Proving the detector fires

⚠ **A detector nobody has seen fire is a guard that has not been tested.** The output detector was
therefore run against a deliberately broken build.

A scratch copy of the tree at `HEAD`, with one change in
`Formatting/Rikarin.Skala.Formatting.CSharp/SpaceRules.cs`:

```diff
  return prev.Parent is not TypeArgumentListSyntax { Arguments: [OmittedTypeArgumentSyntax, ..] }
-     && o.SpaceAfterComma;
+     && !o.SpaceAfterComma;
```

One inverted boolean: the space after every comma, everywhere. Built, and measured against the
unmodified build.

| Surface | Verdict | Measurement |
|---|---|---|
| **formatted output** | **minor** | **393 of 765 corpus files, 7 326 lines** |
| rule catalogue | — | unchanged — 47 rules |
| exit codes | — | unchanged — 7 codes, 5 probes agree |
| SARIF shape | — | unchanged — 53 paths |
| option registry | — | unchanged — 520 options |

Divergence classes, as the notes rendered them:

```
7098 lines across 391 files — inter-token spacing
 158 lines across  45 files — wrapping: the oracle broke a line Skala left long (phase 3)
  41 lines across  36 files — wrapping: one side continues where the other broke (phase 3)
  28 lines across  16 files — other
   1 lines across   1 files — brace placement
```

Three things this establishes and one it does not:

- the detector **fires**, at a magnitude proportional to the change;
- it **classifies** correctly — `inter-token spacing` is 97 % of the lines, which is what a comma
  spacing change is, and the wrapping classes are the second-order effect of every line getting one
  character shorter;
- it is **discriminating** — the other four surfaces reported unchanged, because nothing else moved.
  A detector that fired on everything would be as useless as one that fired on nothing;
- it does **not** establish that the detector catches a change smaller than one option. The first
  real run did that: 1 file and 3 lines out of 705, from a change nobody flagged.

The self-comparison guard was checked the same way, by pointing both sides at the same binary:

```
skala-release: The baseline and candidate tools are the same bytes (b58d8329456b). A differential
against itself reports 'no change' unconditionally; build the baseline release's tool separately,
or pass --baseline-tool.
```

## Running it

```sh
# The reliable form, and the one the workflow uses: build the baseline yourself, then measure.
mkdir -p /tmp/skala-baseline
git archive --format=tar v1.2.3 | tar -x -C /tmp/skala-baseline
( cd /tmp/skala-baseline && dotnet build Skala.slnx --configuration Release )
./build.sh ReleasePlan --baseline-directory /tmp/skala-baseline --baseline-version 1.2.3

# The convenience form, which materialises and builds the baseline itself. See the warning below.
./build.sh ReleasePlan --baseline-ref v1.2.3
./build.sh ReleasePlan                       # baseline = the highest v* tag
./build.sh ReleaseDryRun --baseline-directory /tmp/skala-baseline --baseline-version 1.2.3
```

⚠ **`--baseline-ref` builds the previous release from inside the NUKE process, and on at least one
machine that does not work.** It fails with `CS0234: 'Options' does not exist in the namespace
'Rikarin.Skala'` on exactly the two projects the CLI reaches *transitively* —
`Rikarin.Skala.Options` through Core and `Rikarin.Skala.Rules` through Analysis — after reporting all
twelve referenced projects as built, from a tree that is **byte-for-byte identical** to one that
builds cleanly from a shell. Ruled out by measurement: the environment (the child's differs from a
working shell's only in `DOTNET_HOST_PATH`, `DOTNET_ROOT_ARM64` and `_MSBUILDTLENABLED`, and adding
those three to a shell changes nothing), MSBuild node reuse, `--no-restore`, the working directory,
and the extraction. Building the **solution** instead of the CLI project makes it succeed more often
but not always.

NUKE's own in-process MSBuild is visibly unhealthy in this repository — evaluating `Skala.slnx`
throws `Could not load file or assembly 'NuGet.Frameworks, Version=7.9.0.0'` at every startup, which
NUKE logs as suppressed, because `_build` pins `NuGet.Packaging` forward for its advisories
(`Directory.Packages.props`). That is the most likely root and **it is not proven**. So the release
job does not depend on it: `.github/workflows/release.yml` builds the baseline in a bash step and
passes `--baseline-directory`, which is the path that is exercised.

`ReleasePlan` materialises the baseline with `git archive` rather than a second worktree — a worktree
mutates the repository's worktree list, and this runs on developer machines that already have
several — and builds the baseline tool in **Release** whatever the current configuration is, because
a Debug build of it would measure the configuration. The target refuses to run in Debug for the same
reason.

⚠ **The baseline is materialised into the temp directory, never into `artifacts/`.** It was in
`artifacts/release/baseline/` first, and three `ProjectGraphTests` failed at once: a whole second
checkout inside the tree means `ProjectFile.LoadAll` finds two `Rikarin.Skala.Core`, and every
`Assert.Single` in that class breaks. A copy of the repository inside the repository is a trap for
every tree-walking tool this project has — the graph tests, `skala config check`, `rules docs`, the
docs-site check — and the fix is not to teach each of them a new exclusion. The scratch path is keyed
by the repository root's path so that the several agent worktrees this repository usually carries do
not share one and measure each other's baselines. `artifacts/` was added to `IsScratch` as well, so
that the next thing to publish there does not rediscover this.

⚠ **The baseline is built by one `dotnet build` that does its own restore, and `--no-restore` is
what broke it.** Through NUKE's `DotNetRestore`/`DotNetBuild`, and again through an explicit
`dotnet restore` followed by `dotnet build --no-restore`, the baseline failed every run with
`CS0234: 'Options' does not exist in the namespace 'Rikarin.Skala'` — after four seconds, which is
less time than the build takes, so the reference closure was never built. A `dotnet restore` of the
CLI alone leaves a freshly extracted tree in a state a subsequent `--no-restore` build cannot resolve
its `ProjectReference`s from; the same tree builds clean the moment the flag comes off. The saving
was two seconds against a measurement nobody could run.

The invocation is written out as a command line and logged rather than driven through the task, so
that what runs is what is printed and the printed line is one a person can paste when this next goes
wrong. The extraction also asserts it found more than ten projects, because a partial checkout would
otherwise be measured as a release that deleted most of the tool.

## Known gaps

- **The output detector measures the corpus, not a repository.** 765 files against Vixen's 4 681.
  A release that moves nothing in the corpus can still move a real tree; doc 12's corpus is a sample
  chosen for construct coverage, not for representativeness of any one repository.
- **The option detector reads the registry rather than re-deriving it**, so its tier numbers are as
  fresh as the last nightly sweep. § "The option detector".
- **The SARIF detector's shape is the union across array elements**, so a rule that introduces an
  optional property nothing else carries registers as a shape change. That is arguably correct and is
  certainly noisy; it has not fired yet.
- **The exit-code probes cover five scenarios**, reaching rows 0, 2 and 3. Rows 1, 4, 5 and 130 need
  a compilation, a gate or a signal, and are covered by `ExitCodeContractTests` rather than here.
- **The first release of any line cannot be measured.** There is nothing to measure against, and the
  pipeline reports every surface as unmeasured and says so in its own notes.
- **`git archive` needs the baseline ref to be in the clone.** The workflow checks out with
  `fetch-depth: 0` and `fetch-tags: true` for exactly this; a shallow clone would silently degrade
  every run to "first release".
- **`./build.sh ReleasePlan --baseline-ref` is not reliable** — § "Running it". The workflow does not
  use it, and it should either be fixed or deleted rather than left as a second way to do the one
  thing this pipeline exists for.

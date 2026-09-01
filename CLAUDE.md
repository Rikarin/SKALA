# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

`README.md` covers what Skala is, what it replaces, the CLI surface and the 1.0 contract — read it
first. This file is what the README does not say: how to run things when the ordinary way is broken,
how this codebase decides a claim is proved, and the traps that only show up after they have cost
somebody a day.

## The working agreement

Standing, for every session, unless the current session says otherwise.

1. **Audit before fixing.** When pointed at a subsystem, survey it first and report findings with
   `file:line` before changing anything.

2. **Dispatch parallel agents in isolated worktrees for anything decomposable**, and do it unprompted.
   Write briefs naming the specific failure classes to look for. ⚠ A brief saying *"fix the parity
   map"* finds one entry; one saying *"the lookup is keyed on an id that is null for 81 rows"* finds
   the defect and two more underneath it.

3. **Verify before merging — never on an agent's claim.** Reproduce the number yourself in your own
   tree. Claims in plan documents and in code comments get the same treatment: confirm or refute each,
   and say which turned out wrong. **A refuted claim is worth as much as a fix.**

4. **Merge each task as it lands.** Merging a landed branch to `master` is pre-authorised. Do not batch.

5. ⚠ **Do not stop to ask when the recommended answer is knowable.** Revising a bar, reordering
   milestones, deferring sub-scope and taking implementation decisions are all pre-authorised, provided
   the change is written into `docs/plan/` with the measurement that justifies it and reported in the
   next message. Wanting to label an option *"(Recommended)"* is itself the signal not to ask. The only
   hard stop is pushing to a remote.

6. **Commit before any optional verification step.** ⚠ This is a heading because it is ignored when it
   is a sentence: agents have repeatedly reported work "done and verified" with everything uncommitted,
   which reads as having done nothing because `git diff master...HEAD` is empty — and every constraint
   check against an empty diff passes vacuously.

7. **Report honestly**: what landed versus where you stopped, what stayed owed, what you could not
   verify. *"I skipped the self-gate and here is why"* beats a half-finished claim of done.

8. **Check memory first** for conventions and traps; write new ones when something durable is learned.

9. ⚠ **Never calibrate a rule to what Vixen already does.** Vixen and the vendored Serilog and
   Newtonsoft.Json are test subjects, not a specification. Where they do not follow a rule, they change.
   A low finding count on one of them was never evidence that a rule is good.

## Build and test

`./build.sh <target>` is the entry point; targets are in `build/Build.cs`. `Clean Restore Compile Test
Native Lint Conformance Oracle Sweep Freeze Pairwise Fidelity Unformat Canonical Docs Pack ReleasePlan
ReleaseDryRun`.

⚠ **`./build.sh` does not currently run on this machine, at any commit, including a clean `master`.**
`build/_build.csproj` fails with a flood of `CS0246` for `Parameter`, `Solution` and `AbsolutePath`.
`global.json` pins SDK `10.0.301` with `rollForward: latestFeature` and only `10.0.400` is installed,
so local builds resolve to 400; NUKE 10.1.0 supplies its global usings from `Nuke.SourceGenerators.dll`,
which `csc` is handed and which emits nothing under that Roslyn. Clearing `build/obj` does not help.
**CI is unaffected** — every workflow uses `actions/setup-dotnet` with `global-json-file: global.json`,
which installs 10.0.301 exactly. Installing that SDK locally would restore the target.

Until then, go around it — `dotnet test` bypasses the NUKE bootstrap entirely:

```bash
dotnet test Rules/Rikarin.Skala.Rules.Tests/Rikarin.Skala.Rules.Tests.csproj --nologo   # ~3 100 tests, 15 s
dotnet test Formatting/Rikarin.Skala.Formatting.CSharp.Tests --filter "FullyQualifiedName~TheNameOfIt"
dotnet build Tools/Rikarin.Skala.Cli -c Release      # then drive `skala` directly for the Lint checks
```

Test projects are siblings of what they test (`Rikarin.Skala.Rules` / `Rikarin.Skala.Rules.Tests`), so
the project path is derivable from the assembly name in any failure.

⚠ **Never pipe a gate through `head` or `tail`.** The exit code becomes the pager's, so a failing run
reports success. This has cost real time twice. Redirect to a file and read it.

⚠ **Run gates one at a time.** Two `./build.sh` invocations collide on `bin`/`obj`.

⚠ **`./build.sh Oracle --only <name>` — with a space, never `--only=<name>`.** NUKE's parser binds
`--only value` and *silently drops* `--only=value`: no error, the parameter is null, and the target
regenerates the entire corpus. Documented on the `Oracle` target in `build/Build.cs`; the `=` spelling
went into every brief for a day before an agent caught it and reverted 1 344 files.

## A green `./build.sh` is not a green CI

`./build.sh` runs **Restore, Compile, Test**. It does not run the two checks that measure Skala against
itself, and both have gone red while the full local gate was green. Before saying CI will pass, run all
three:

1. `./build.sh` — compile and test.
2. `./build.sh Lint` — `skala config check` over the root, then `skala format --check` over nine
   directories: `Analysis build Core Distribution Formatting Reporting Rules Testing Tools`. ⚠ Any
   formatter change can drift Skala's own source out of Skala's own format (ADR-015). It was red for
   eight consecutive pushes before anyone noticed. `Distribution` and `build` were each missing from
   that list at one point, found a directory apart.
3. The self-gate — build Release with `-bl:artifacts/skala.binlog --no-incremental`, then
   `check --load=binlog --binlog artifacts/skala.binlog --require-fresh-binlog --gate=ci --duplication`,
   and `baseline update` / `prune --apply` if it fails. Any code change moves the complexity and
   duplication metrics, so the baseline settles after the *last* merge, not the first.

## How this codebase decides something is proved

- ⚠ **Do not quote a coverage, fidelity or rule-count number from a document or from memory.** Every
  figure written down here has gone stale at least once. Read the artefact: the conformance sweeps under
  `Testing/Rikarin.Skala.Conformance.Sweep/`, `options.json` for the tier split, `rules.json` for what
  ships, `Testing/parity-analysis/` for the parity numbers.
- ⚠ **Verify the instrument before trusting what it printed.** Ask what a measurement prints on the day
  it does not run. Real examples from this repository: a parity pipeline whose map lookup was keyed on
  an id that was `null` for 81 of 888 rows, so those rows silently fell through to "uncovered" and
  inflated the headline; a published figure that was never reproducible because it depended on an
  uncommitted cache file; and *three* wrong entries in one hand-written map, one of them hidden
  underneath another so that only fixing the first made the second reachable.
- ⚠ **A zero from a disabled check and a zero from clean code are the same zero.** Raise every severity
  first, then measure. The parity measurement found 44 inspections sitting at `none` that fired
  immediately once enabled.
- **The oracle is the definition of correct, and it is not Rider.** ADR-011 makes `jb cleanupcode` the
  reference, but the CLI does not format documentation comments at all and Rider does (SK-DIV-0006). ⚠
  **Where the two conflict, Rider's behaviour is the requirement** — and in any area where they differ
  there is no differential safety net at all, which matters more the closer ReSharper gets to being
  retired.
- **A rule ships with a fix, zero false positives on both reference trees, and a "should not fire"
  fixture set at least as large as its positive one.** Shipping a fraction of the catalogue is that bar
  working, not the plan falling short. ⚠ The failure mode to avoid is "a hundred rules that are usually
  right".
- **Sabotage-test where you can.** A test that stays green when you break the thing it covers is the
  defect. A completeness claim with nothing asserting it is worth nothing — `verify_ledger.py` exists
  because "every rule is accounted for" was otherwise just a sentence.

## Architecture worth knowing before editing

**Two registries drive nearly everything generated.**
`Rules/Rikarin.Skala.Rules.Metadata/rules.json` is the single source for each rule's
`DiagnosticDescriptor`, its `docs/rules/` page, its `skala explain` text, its SARIF `rules[]` entry and
its ReSharper severity mapping. `Core/Rikarin.Skala.Options/options.json` is the same for options and
`docs/site/`. Editing generated output instead of its registry is always wrong.

⚠ **A rule id is permanent (ADR-012).** `allocated-ids.txt` is append-only and `RuleCatalogTests`
asserts it. A withdrawn rule is marked `retired`, never deleted, and an id is never re-purposed —
baselines carry the id in their fingerprint, so one number with two meanings silently un-suppresses one
finding and wrongly suppresses another in every repository holding a baseline. **Do not allocate an id
for a concept nobody has specified yet.**

⚠ **Tier D means "known to the registry and not implemented".** An option parsed, reported by
`skala config check`, and then ignored. Crediting Skala with arrangement it declares and does not
perform is the double-count that is easiest to miss, because the key looks handled.

**The two promises are unconditional.** Skala never writes a file whose token stream differs from the
input's (`SK9099`, writes nothing, drops a reproduction under `.skala/crash/`), and never formats a
file it could not parse (`SK9010`, left byte-identical). There is no flag that turns either off.

**`verify` is `format --check` + `arrange --check` + `check --gate=local`.** Analysis loads a
compilation three ways — binlog, workspace, loose — and rules declare `scope` (`Syntax` / `Semantic` /
`Compilation`), which decides both caching and what still runs without a project.

**Skala exists for AI-generated code, not for editors.** That is why the `SK1xxx` modernization range
matters disproportionately: a model writes an older dialect of C# because that is what most of its
training data is. It is also why nothing formats on save here, and why the wall-clock budgets that once
gated the build were deleted rather than met.

**`Testing/parity-analysis/` measures what ReSharper and SonarQube catch and Skala does not.** It is
plain Python, deliberately outside the solution. `LEDGER.md` and the two `ledger-*.json` files record
what was then *done* with that measurement — every rule either proposed as a GitHub issue or excluded
with a written reason — and `verify_ledger.py` asserts neither can lose one. ⚠ `catalogued.json`,
`gov.json` and the hosted map inside `classify.py` are hand-written judgement, not measurement; every
entry missing from them inflates the gap and every wrong entry hides one.

## Conventions

- **`docs/overview.md` is the state and wins** where a `docs/plan/` document disagrees; plan documents
  record intent, not what exists. ⚠ `README.md` drifts too — its 1.0 status table still lists `skala
  arrange` as not existing, and it does exist. When the two disagree, check the code.
- **GitHub issues on `Rikarin/SKALA` are the task tracker.** ⚠ **Always pass `--repo`** to `gh`; it
  otherwise infers the repository from the working directory, which is wrong inside an agent's worktree.
  Rule proposals carry `rule-proposal`, a parity label and a range label. Search the open issues before
  filing so a finding two agents hit independently lands once, and close an issue only when the work is
  **merged to master** — with the real outcome, including "closed as hosted by a `CA*` analyzer" and
  "refuted, not fixed".
- **Commit messages are declarative and explain the insight, not the diff**, and mark anything
  surprising or previously believed false with ⚠. Read `git log` before writing one.
- **Reserve disjoint `SK-DIV-*` ranges per agent.** Two concurrent agents told to "take the next number
  from `docs/divergences.md`" read the same base and collide; one such batch had to be renumbered across
  42 references.

## Parallel worktrees: the four ways agents actually collide

Eight agents have run concurrently here successfully. What breaks is specific and cheap to prevent, and
every brief should say so:

- ⚠ **`git stash` is not worktree-local.** The stack is shared across every worktree in the repository.
  One agent's `stash pop` took another's uncommitted work. **Forbid the stash**; use a temporary commit.
- ⚠ **Agent worktrees are created *behind* `master`, not at it.** Tell each agent to check
  `git log --oneline -1` against local `master` and reset onto it, and to say that it did.
- ⚠ **The session scratchpad is one directory shared by every agent**, not one per agent. Two agents
  wrote the same filename and one measured the other's tree while reporting the number as its own.
  Prefix scratch filenames uniquely or write inside the worktree.
- **Generated files conflict; source usually does not.** `docs/site/` and `options.json` tier edits
  collide constantly. Resolve with either side and regenerate once from the merged tree.

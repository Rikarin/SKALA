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

### Fixed — the tier system rested on a measurement nothing could connect to the code

⚠ **Between the sweep at `2a14dee` and the one at `603fbd3` the tree moved 88 commits, and every tier
decision in that window rested on a measurement of a formatter no instrument could identify.**
`OptionCoverageTests` reads `conformance-sweep.json` to decide which tier an option is entitled to;
`ProvenanceTests` checked that the sweep measured the *configuration* in force and nothing checked
that it measured the *formatter* in force. The `.editorconfig` had not changed, so the suite was
green throughout.

- **The archive now records Skala's own output digest at every configuration the sweep measured**, and
  `TheCommittedSweep_MeasuredTheFormatterInForce` re-asks it on every commit. The oracle half needs
  JetBrains and minutes; Skala's half is under half a second, so this runs on the fast path.
  ⚠ Changing how a swept construct formats now turns the suite red until `./build.sh Sweep` is re-run.
  That is the bargain the corpus provenance test already strikes, and there is deliberately no button
  that re-stamps the digests: unlike a configuration, a formatter whose output changed has by
  construction changed its output.

- **Tier A is checked in three directions, not two.** `overclaimed` is Tier A minus implemented,
  `underclaimed` is implemented minus Tier A minus the sweep's demotions — and an option that is
  claimed *and* implemented *and* unsubstantiated fell through both. The sweep's report has printed
  "each is a dilution of the tier system and must be demoted" since it was written, and the demoting
  was a person reading a markdown table. The `603fbd3` run produced the first two options ever to sit
  in that state and both assertions passed on them.

- **A fired canary is written into the report.** It was a line on the console, so it reached whoever
  ran the sweep and nobody else, while the committed table looked exactly as confident as a healthy
  one.

- **The broken-measurement canary was asking the wrong question.** It counted whether anything
  *moved* when the question is whether `cleanupcode` *answered*. Rounds past the widest option's arity
  hold one option, and there "nothing moved" and "this value reproduces its own fixture" are the same
  observation — it cried wolf on a healthy round at `603fbd3`. Split into `IsBrokenMeasurement`
  (answered at all) and `IsUnvaryingRound` (answered with the input, population > 1); both historical
  catches still fire.

### Changed — four tiers moved, on the first measurement 25 options had ever had

| option | | |
|---|---|---|
| `indent_size` | D → **A** | the formatter improved; the old demotion was stale |
| `resharper_csharp_indent_size` | D → **A** | the same |
| `resharper_csharp_wrap_lines` | A → **D** | `DIVERGENT` at its first measurement |
| `resharper_place_attribute_on_same_line` | A → **D** | `DIVERGENT` at its first measurement |

The sweep now covers **283 options at 636 configurations** against 258 at 567. The 25 newly covered
had been Tier A on a fixture alone; 23 were right. ⚠ The unsubstantiated count is 70 before and 70
after and that is a coincidence — two left the set and two joined it. Both demotions keep their
`oracle` fixture so a later sweep can reverse them.

### Added — `./build.sh Pairwise`, and the interaction question answered for `keep_existing_*`

⚠ **The key-flip sweep measures each key on that key's own fixture with every other key at the
export's value, so of a two-key grid it sees one line.** docs/plan/05's `keep_existing_*` section is a
four-way table across two keys; three of its corners had never been measured by any committed run.

`pairwise [--family=keep,wrap,align]` sweeps the whole grid of two keys' values against the oracle and
writes `conformance-pairwise.md` and its `.json`. **58 pairs, 342 corners, 1.5 minutes.**

**No interaction was found in any of the three families**, and `keep_existing_*` — the family the
design named — is 10 `CONFORMANT` and 3 `BASELINE`. Its interior agrees with the oracle.

⚠ **Three verdicts exist because the pass reported false findings without them**, each caught by its
own output rather than by review:

- `ReachedBySingleSweep` first read "either key at the export's value". The column of the grid is
  measured on the *secondary's* fixture and says nothing about the primary's, so **58 never-measured
  corners were filed as duplicates of existing rows and the pass reported zero interactions as an
  artefact of that line.**
- `BASELINE`: 17 corners disagreed at the base configuration itself, blaming pairs for divergences
  their fixtures already had.
- `INHERITED`: the corrected run then reported **17 interactions across `wrap_*`, every one
  disagreeing only at `max_line_length = 0` and `= 1`** — the two values where the single sweep
  records `max_line_length` diverging *alone, on its own fixture*. Seventeen findings, one cause,
  none about a pair. The pass now reads the single sweep's per-value agreement and excuses such a
  corner; one unattributable corner still keeps the row a finding.

⚠ **`wrap_* × max_line_length` is blocked rather than clean.** 28 of its 37 pairs are `INHERITED`
because the int probe set offers `120`, `0` and `1`, and two of those are degenerate margins the
engines already disagree about. Answering it needs three legal margins — the same defect as the flags
probe below, one type over, and not fixed here because the int probe set is shared with the single
sweep.

### Fixed — a flags option's probe set tested no combination at all

The probe set was every member singly, then the join of them all. For
`csharp_new_line_before_open_brace` that join contains `all`, which dominates every other member — so
the probe was a second copy of the `all` singleton wearing fourteen names, and on a fixture already in
that layout it scored as an agreement.

⚠ **The gap it left is the defect: the probe set never tested a combination**, which is what a flags
option is for. A formatter honouring `methods` and `types` and mishandling `methods, types` passed
every probe. It now ends with a genuine two-member value, and on the `603fbd3` run that value
**disagrees** where the join it replaced agreed. `EveryLegalValue` still offers the join, because
accepting it is a real requirement of the parser.

### Fixed — a nested ternary chain wraps the way ReSharper wraps it

⚠ **A formatting output change.** `a ? x : b ? y : z` is a different construct from `a ? x : y`, and
Skala had only the second. Asked with a chain too wide for the margin it broke the outermost
conditional at its `?` and `:` and left the tail flat; asked with the oracle's own layout as input it
rewrote it into that same shape, so `keep_user_linebreaks` did not protect it either.

`BreakPlan.PlanTernaryChain` gives the chain a group of its own whose break points are the gaps
**after** each `:`, one member per line. Measured against `jb cleanupcode` 2025.2.6: neither
`wrap_ternary_expr_style` nor `wrap_before_ternary_opsigns` moves a chain — flipping either returns
every chain in the new fixture byte-identical while it moves the single conditional beside them — so
the two layouts are separate rules and not two settings of one. The chain runs through `WhenFalse`
only and does not see through parentheses; the innermost member is never broken; a break the author
put before the final else is kept, which a single ternary's is not.

**Corpus effect: none, in either direction.** `corpus/real/` holds one nested chain and it fits on
its line, so `real` stays at 99.53 % / 85.26 % and `constructs` rises only by the two new fixtures.
The absence is the finding — the shape was unreachable from the committed corpus, which is why it
survived to M11.

- **`resharper_csharp_int_align_nested_ternary` is Tier A**, pinned by
  `constructs/alignment/int-align-ternary.cs`. Its column pass was written and correct for nine
  milestones with no document to pad.
- **`resharper_csharp_int_align_binary_expressions` stays Tier D and its reason changed.** With the
  chain layout written the per-option unit reaches it, and it disagrees on a second shape: the
  oracle also pads adjacent local variable *declarations* whose initializers are binary, at every
  operator. Pinned unimplemented in the same fixture.
- **`resharper_align_ternary` and `resharper_indent_aligned_ternary` stay Tier D**, re-measured on
  the chain — the shape most likely to overturn them — and both still return it byte-identical.
  `resharper_outdent_ternary_ops` is read by the C# formatter and moves only the at-the-signs layout.
- **`resharper_csharp_nested_ternary_style` stays Tier D and is now measured rather than unknown:**
  all four values are distinct layouts, recorded in the registry.

### Fixed — `skala.jsonc` can say which files are not source code, and CI can go green

⚠ **Every push to `master` had failed CI for eleven consecutive commits, and three faults were
stacked on top of each other.**

- **The coverage denominator counted files no compilation could ever contain.**
  `--require-fresh-binlog` refuses a binlog covering under 90 % of the selected source files, and
  the selection was a filesystem walk. This repository holds **1 924** `.cs` files that are
  deliberately in no compilation — `Testing/corpus/`, `Rules/Rikarin.Skala.Rules.Tests/fixtures/`
  and that project's `corpus/` — each declared as data by a `<Compile Remove>` nothing outside
  MSBuild can see. The ratio read **294 of 2 220 (13 %)** against a complete binlog and `check`
  exited 4 before an analyzer ran. Neither the floor nor the exit code was wrong.

  `skala.jsonc` now honours the `"exclude"` key docs/plan/03 has always specified, and **every walk
  in the tool reads one predicate** (`SourceExclusions`) rather than the four hard-coded lists that
  had already stopped agreeing with one another — `FormatCommand` knew about `.claude/` and not
  `.skala/`, `BinlogLoader` the reverse, and it tested the *absolute* path, which is the shape of the
  bug that once rewrote 2 796 files inside another agent's worktree. The globs are Roslyn's
  `.editorconfig` matcher, not a dialect of our own.

  The payoff beyond CI: `./build.sh Lint` named projects one at a time under `Testing/` and `Rules/`
  to work around this, which is why a new project under either was invisible to the format check
  until somebody remembered to add it — exactly how `Distribution` went unchecked until M8 and
  `build/` until M10. It now names nine top-level directories and nothing else.

- **A merge had reverted `build/Build.cs` to a stale copy.** `ReleasePlan` and `ReleaseDryRun`
  (docs/plan/18), their six parameters, and `build` in the `Lint` area list were dropped by the
  merge resolution in `12922b14`; `.nuke/build.schema.json` lost the same. The release workflow had
  been failing on `Target with name 'ReleasePlan' does not exist` ever since. Restored, keeping the
  `Only` parameter the same merge added.

- **One crashed step failed three times.** The SARIF upload and the report render carried
  `if: always()` and so failed *on the absence of a file* whenever the check step died before
  writing one, naming the report rather than the load. They are now `always() && hashFiles(…) != ''`.
  Writing a well-formed empty SARIF instead was rejected: code scanning reads an empty result set
  under a category as "this analysis found nothing" and resolves every alert the last good run
  raised, so a crash would silently clear the surface it crashed before measuring.

- Node 20 is deprecated on the runners: `actions/checkout` v4 → v7, `actions/setup-dotnet` v4 → v6,
  `actions/upload-artifact` and `actions/download-artifact` v4 → v7,
  `github/codeql-action/upload-sarif` v3 → v4. All are Node-24 and ESM moves; the two behaviour
  changes in the range are `download-artifact` v5's path fix for downloads *by ID* (not used here)
  and `checkout` v6 persisting credentials to a separate file, which only the gated publish job
  relies on and which cannot be exercised without arming it.

### Added — `SK9017`, and the 83 options that validated nothing

⚠ **An out-of-domain value was a silent default**, which is docs/plan/00's non-negotiable #4 satisfied
to the letter and missed in substance. `OptionResolver` had always detected these and appended a
string to `ResolutionResult.ValueErrors`; grepping the tree, nothing outside the tests and the
key-flip sweep ever read that field — not `config check`, not `config explain`, not the format path.

The measurement, on an `.editorconfig` carrying seven deliberately wrong lines: **before, exit 0 with
zero diagnostics; after, six `SK9017` and exit 3.** The seventh line, `indent_size = tab`, is legal
EditorConfig and is now accepted.

- **`SK9017` is a warning**, where `SK9001` (unknown key) is info. `SK9001` is info because the
  export carries ~2 000 keys Skala will never implement and the user wrote nothing wrong. Here the
  key *is* in the registry, the configured value was discarded, and the code is formatted against a
  value nobody chose. It is also the only configuration diagnostic that fails `config check` without
  `--strict`, at exit 3 — every other warning there describes a configuration that means something
  and might mean the wrong thing; this one describes a line that means nothing at all.
- The message names the key, the value, the domain, and **what is in force instead** — the last one
  measured from the built options rather than assumed from the registry, because a generalized key
  can have moved it. `config explain` no longer prints `(default)` beside a key the file visibly
  sets; the row carries the effective value, `SK9017`, and the line that was refused.
- **17 of the 27 `string` options were enums or flag lists** typed as strings during distillation and
  therefore accepting anything — `resharper_align_ternary = sideways` and
  `csharp_using_directive_placement = nowhere` among them. ⚠ The worst was
  `resharper_csharp_keep_existing_declaration_block_arrangement`: a discarded bool there means the
  arranger rearranges the file on the strength of a setting it threw away. The remaining **10 stay
  free-form and now say why**, in a `freeFormBecause` the generator requires; two of them
  (`resharper_labeled_statement_style`, `resharper_prefer_wrap_around_eq`) are recorded as gaps
  rather than open domains, because JetBrains publishes no property page for either.
- **All 56 `int` options gained a `min`**, with the reason on the entry. ⚠ None gained a `max`: an
  upper bound nobody can justify refuses values for no reason. Widths and indents floor at 1;
  counters and blank-line settings at 0, because `max_line_length = 0` and `max_*_on_line = 0` are
  values the formatter deliberately supports.
- **`indent_size = tab` is accepted**, resolving to `tab_width` and propagating through expansion.
  The EditorConfig specification defines it — "If this equals `tab`, the `indent_size` shall be set
  to the tab size, which should be `tab_width` (if specified)" — and both reference cores implement
  it. ⚠ Only on `indent_size`: JetBrains documents `resharper_csharp_indent_size` as "an integer",
  so accepting it there would be an invention.
- `OptionValueValidationTests` sweeps the registry in both directions: every value in every declared
  domain accepted, every closed domain refusing a value outside it with exactly one `SK9017`, and the
  free-form set asserted against a reviewed list. ⚠ The suite had **positive coverage only** before
  this; nothing anywhere fed an illegal value.
- The five hand-kept copies of `LegalValues` are one `OptionDomain`. One of them carried a comment
  saying it was "kept deliberately identical" to another; giving `int` options a floor invalidated
  four of the five at once.

### Changed — ⚠ documentation comments are formatted by default; `--xmldoc` is now `--no-xmldoc`

**This changes formatting output on almost every file with a documentation comment in it**, which by
the rule at the top of this file is a minor bump at minimum.

⚠ **The flag existed because of a measurement that was read wrongly, and the correction is worth
more than the change.** M3 asked `jb cleanupcode` whether it formats documentation comments, got
"no" under every shape of the `resharper_xmldoc_*` family, and recorded it as a property of the
tool. It is a property of the **profile**: `CSharpFormatDocComments` is a real cleanup task,
ReSharper's `Full Cleanup` enables it, `Built-in: Reformat Code` does not, and
`OracleProfile.FormatOnly` is `<CSReformatCode>True</CSReformatCode>` — `Built-in: Reformat Code`,
exactly. JetBrains documents the same thing in prose: "the Built-in: Reformat Code profile does not
reformat XML doc comments". Rider formats them. **Not formatting them was the divergence.**

The consequence had stood for six milestones: a whole sub-formatter behind an opt-in flag, its
seventeen keys held at Tier D, and `resharper_space_after_triple_slash` actively *demoted* from
Tier A for doing the right thing. See `docs/divergences.md` § SK-DIV-0006 and
`docs/oracle-cleanup-profile.md`, which carries the probe with its negative control.

- The escape hatch is `skala format --no-xmldoc`, not `resharper_xmldoc_wrap_lines = false`. That
  key means "do not wrap long lines" — with it false the sub-formatter still re-indents, still
  collapses blank lines between tags and still inserts the marker space, which is what Rider does
  with it false too — and JetBrains' `.editorconfig` index does not document it at all. Attaching a
  meaning to a ReSharper key that ReSharper does not give it is the class of mistake being undone.
- ⚠ The daemon's exclusion inverted with the default. It carries no xmldoc switch and formats with
  the default, so `--no-xmldoc` falls through to the CLI. A daemon that does *more* than it was
  asked makes one command mean two things, the same rule read the other way round.
- The seventeen keys move from `Ids.OfInert` to a new `Ids.OfUnoracled`: honoured, observable, and
  unable to claim Tier A because every committed fixture was generated under the profile that
  leaves doc comments alone. `AnInertKey_StillCannotBeObserved` would have failed on seven of them
  the moment the default flipped, correctly — they are not inert any more.
  `AnUnoracledKey_IsObservable` is its mirror and asserts all seventeen do change output.

### Changed — ⚠ the fidelity number's basis is now `outside doc comments`, and says so

The differential compares against fixtures that do not format doc comments, so with the
sub-formatter on, `///` lines measure a profile gap rather than a formatter defect. The basis is
named in `FidelityBasis`, in every message the ratchet prints, and in `fidelity.json`'s own `Basis`
field, which `FidelityBaseline.Read()` refuses to compare across.

`corpus/real/`, 380 files, no symbols:

| basis | line | file |
|---|---:|---:|
| outside doc comments (the ratchet) | **99.53 %** | 85.26 % |
| every line | 96.04 % | 47.89 % |
| every line, `--no-xmldoc` (the old population) | 99.63 % | 85.26 % |

⚠ The 0.10 between 99.63 and 99.53 is the `///` lines leaving the denominator, where they were
counted as agreeing because neither side touched them. It is not a regression and not an
improvement. The 3.59 to 96.04 is the fixtures' profile.

An excluded category that nobody looks at again can grow unwatched, so the every-line number is
asserted alongside the ratchet by `TheEveryLineNumber_IsStillReported`, and
`TheCodeAroundTheComments_IsUntouched` asserts over all 716 corpus files that nothing outside a
`///` line moves. ⚠ The exclusion has an expiry: enabling `CSharpFormatDocComments` in
`OracleProfile.FormatOnly` and running `./build.sh Oracle` returns the basis to every line and makes
these keys promotable to Tier A.

### Changed — `docs/plan/16` § Q1 reopened

Q1 — "does `jb cleanupcode` reproduce Rider's editor formatting exactly?" — was recorded as
**narrowed** on the strength of the autodetect keys, with cleanup-profile parity dismissed as
"handled by pinning the profile explicitly". Pinning a profile is a choice of answer, not a handling
of the question. Autodetect was the IDE having a setting the CLI lacks; this is the CLI and the IDE
agreeing and Skala asking the wrong profile — which is worse, because nothing about it looks like a
difference. ⚠ It matters most at the end of the roadmap: once ReSharper is removed the fixtures
*are* the specification, and a profile they were generated under wrongly can no longer be re-asked.

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

### Added — the version is measured, and the release line becomes `2.0.0-alpha`

[18](docs/plan/18-versioning-and-release.md). `./build.sh ReleasePlan` builds the previous release's
tool beside this one's and runs five detectors over the pair — the corpus formatted by both binaries,
the rule catalogue, the exit-code table *and* the codes both binaries produce, the SARIF each writes,
and the option registry. The number is the highest verdict. **Nothing reads a commit message**, for
the reason everything else here is measured: four times in this project a summary and a measurement
disagreed and the measurement was right.

⚠ **Run against the tree that declared 1.0 (`d8e9d34`, 125 commits back), the verdict is `major`,
and two of the four surfaces ADR-012 froze at 1.0 are what made it one.**

| Surface | Verdict | Measured |
|---|---|---|
| formatted output | minor | 1 of 705 comparable corpus files, 3 lines — `indentation (-4 columns)`. 60 files added to the corpus and not comparable |
| rule catalogue | minor | 15 rules added, **10 at `warning` or `error`** |
| exit codes | **major** | `format --check` on an unformatted file exited **1** at 1.0.0 and exits **2** now; `--diff` the same; a path that does not exist went from **0** to **3** |
| SARIF shape | — | unchanged, 53 paths |
| option registry | **major** | 84 changes — 77 keys `D → A`, and **6 defaults changed on keys that were already Tier A**: three `keep_existing_*` going `false → true`, `wrap_after_declaration_lpar` and `wrap_before_declaration_rpar` going `true → false`, and `wrap_extends_list_style` `chop_if_long → wrap_if_long` |

The exit-code moves are the fixes `ExitCodeContractTests` was written for and they are correct. They
are also breaking, and no plausible commit prefix would have said so: both are `fix:`. The six option
defaults are the arrangement work landing — a `feat:` — and a default is what applies to every
repository that never wrote the key down.

⚠ **The option detector was wrong on its first pass and was corrected before the number was taken.**
It called `dotnet_style_require_accessibility_modifiers` changing type major; that key was **Tier D
at the baseline**, doing nothing, and its type changed as part of implementing it. A default or a
type only breaks a promise on a key that was honoured, so the detector requires that now and reports
the rest as "changed while inert". The verdict is still `major`, for the six that are real.

⚠ **1.0.0 was never published** — zero tags, zero packages on any feed — so the jump costs nobody
anything. The first *published* artefact is `2.0.0-alpha.N` rather than `2.0.0`: a pre-release is
opt-in on NuGet, which is the right default for a tool with six open formatter defects, 236 Tier D
options and no adopting repository outside this one. Doc 18 records the promotion gate, and why
retreating to `0.x` was rejected — at `0.x` the "your repository will show a diff" and "your
baselines will break" verdicts collapse into one, and that distinction is the whole point.

⚠ **The output detector was proved to fire before it was trusted.** A scratch copy with
`o.SpaceAfterComma` inverted — one boolean — measured **393 of 765 corpus files and 7 326 lines**,
97 % of them classified `inter-token spacing`, with the other four surfaces reporting unchanged. The
detector also refuses two tools with the same SHA-256, because a differential against itself reports
"no change" forever and looks green doing it.

`.github/workflows/release.yml` now measures, packs, writes the notes from the measurements, creates
the tag **in the job**, and prints exactly what a publish would push. The `publish` job is written
out in full and gated on a repository variable nobody has set.

### Fixed — two holes the release work fell into

- **`build/` was never format-checked by the `Lint` target it defines.** The area list named seven
  directories and not the one it lives in; `Build.cs` had drifted out of formatting. Same class as
  the `Distribution` hole M8 closed (`7c56c8f`), one directory later.
- **`rules.json` claimed `since: 1.1` and `since: 1.2`** against a declared `1.0.0` and zero tags —
  two releases that never existed. `since` reaches a consumer through `rules[].properties.since` in
  the SARIF and through `docs/rules/`. `VersionSourcesTests` refuses it now.
- ⚠ **Not fixed, found in passing and filed:** `skala check --output report.sarif` with a bare
  filename crashes with an unhandled `ArgumentException` and exit 5 (`SkalaDirectory.EnsureForFile`
  calls `Directory.CreateDirectory("")`). An absolute or directory-qualified path works.

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

# 16 — Risks and Open Questions

## The risks that could sink this

### R1 — ⚠ Rider fidelity is asymptotic, and the last 0.1 % is most of the work

ReSharper's formatter is twenty years of accumulated special cases, many undocumented, several
version-dependent. The differential harness will get to 99 % quickly and then produce a long tail of
one-line divergences in constructs nobody thinks about — a lambda inside a collection initializer
inside an attribute argument, a conditional access chain broken across a ternary.

**Why it matters:** the whole value proposition is "the IDE and the gate agree". At 99 % they
disagree on roughly one line in a hundred, which on a 1.35 M-line tree is 13 000 lines that Rider
will reformat back the moment someone opens the file. Formatting ping-pong between two tools is
worse than either tool alone.

**Mitigation:** the divergence register (`SK-DIV-*`) plus a hard rule — any construct that appears in
the corpus more than 50 times must be at 100 %, and the tail is only allowed in constructs that are
genuinely rare. Plus `resharper_formatter_tags_enabled` as the human escape hatch for the handful of
places where the tools cannot agree. Plus, honestly: if a divergence is small and Skala's answer is
better, change the Rider setting to match Skala rather than the reverse — the settings are the
author's, and they can move.

⚠ **M3 measured it, and the shape is exactly as predicted.** `fidelity constructs` attributes every
divergent line to the innermost node that owns it and puts that beside how often the construct
occurs: of the 54 constructs occurring more than 50 times in `corpus/real/`, **27 are at 100 %** at
98.86 % overall. The rule is not met. What it is short of is characterised rather than mysterious —
the largest attributed shares are `IdentifierName`, `StringLiteralExpression` and `ArgumentList`,
which are where a wrap decision *lands* rather than constructs mishandled in themselves, and the
eight `SK-DIV-*` entries name the decisions.

⚠ The paragraph above under-states one thing and over-states another. The tail is *not* mostly
exotic constructs: two of the eight entries (SK-DIV-0001, SK-DIV-0004) are about preprocessor
conditionals, which are ordinary, and they are limitations of parsing without a project rather than
of the formatter. And "99 % means 13 000 lines Rider will reformat back" is the wrong test for those
two, because Skala does not *touch* what it cannot see — the disagreement is code Skala left alone,
not code it moved.

**Residual risk: high.** This is the risk that decides whether the project succeeds.

### R2 — The fitting engine is the only novel code, and novel code is where the bugs are

Everything else in Skala is assembly of well-understood parts. The three-state group model (ADR-002)
does not exist in any published formatter; Prettier-lineage tools have two states and ReSharper's
implementation is closed.

**Mitigation:** the property suite ([12](12-conformance-and-testing.md)) is designed for exactly
this — idempotency, token equivalence, width monotonicity and preservation are all violated by the
plausible bugs in a three-state resolver. Fuzzing runs nightly from M1, not from M7.

⚠ **M3 is where the novel code arrived, and the property suite earned its place immediately.** The
three-state model needed a fourth measure and a fifth: `pointWidth`, which stops at the first
*optional* break, and the trailing context, because a group is not the line it lands on. Two of the
three worst bugs in the milestone were found by properties rather than by fidelity — a
non-idempotency that no corpus file contains and that took a 4 708-file tree to surface, and a
blank-line rule that disagreed with itself between the first pass and the second. Neither moved the
fidelity number at all before it was fixed.

**Residual risk: medium.** Contained by testing, not by cleverness.

### R3 — The false-positive budget is not met, and the analysis half is switched off

A 1 % false-positive rate on a corpus that produces 5 000 findings is 50 wrong findings, which is
survivable. A 5 % rate is 250, which is not. The rules most likely to over-fire are exactly the ones
with the most value: modernization rules that do not understand why the old form was chosen, and
async rules that do not understand the call graph.

**Mitigation:** the shipping bar in [08](08-rule-catalogue.md) — zero false positives on the
reference corpus, a documented false-positive story, and a "should not fire" fixture set at least as
large as the positive one. Plus the nightly rule-count job, which catches drift. Plus shipping
uncertain rules at `hint`, where they are invisible until asked for.

**Residual risk: medium.**

### R4 — Scope. This is three products

A formatter, a linter with 200 rules, and a quality-gate platform. Any one of them is a serious
project; CSharpier is a formatter alone and has years in it. The failure mode is not abandonment, it
is three half-finished halves — a formatter at 97 %, forty rules, and a gate nobody trusts.

**Mitigation:** the milestone order is strict and each milestone is adoptable on its own. M3 alone —
a formatter that matches Rider — is a complete, useful product, and the plan says so. If the project
stops there it has still replaced the thing that has no alternative.

**Residual risk: medium-high**, and it is the risk that is fully under the author's control.

### R5 — Roslyn moves

C# 15 will add syntax. A formatter that meets unknown syntax must not corrupt it.

**Mitigation:** the document builder has a total-function requirement — every `SyntaxNode` kind maps
to a handler, and the fallback handler emits the node `Verbatim` from its original span rather than
throwing or guessing. A generated exhaustiveness test over `SyntaxKind` fails the build when Roslyn
adds a kind Skala does not name, which turns "silently mangles new syntax" into "fails to compile
after a package bump". Plus token equivalence, which catches it at runtime anyway.

**Residual risk: low.**

### R6 — The binlog dependency is awkward in practice

`skala check` needing a build is a real ergonomic cost, and the staleness cases
([07](07-analysis-host.md)) are a class of confusing failure.

**Mitigation:** three load modes, clear reporting of which one ran, and `loose` mode being genuinely
useful for the agent path (which is the highest-frequency use). Also: CI builds anyway, so the cost
lands where it is already paid.

**Residual risk: low-medium.**

### R7 — Single maintainer

One person plus AI agents, across a game engine, an engine editor, an MMO framework and now a
static-analysis tool.

**Mitigation:** none available. What the plan does instead is minimise the *maintenance* surface:
generated code over hand-written (options, rules, docs), no bundled third-party rules to track, no
server, no telemetry, no plugin ecosystem, and a strict "no unowned features" policy. And the tool is
built to be used by agents, which means agents can also maintain it — the conformance harness is
precisely the artefact that makes agent-written formatter changes reviewable.

**Residual risk: structural.** Acknowledged rather than mitigated.

## Open questions

### Q1 — Does `jb cleanupcode` reproduce Rider's *editor* formatting exactly? — ✅ **narrowed**

The oracle assumption is that CLI cleanup and the IDE's "Reformat Code" produce identical output for
the same `.editorconfig`. Mostly true; ReSharper has settings that exist only in the IDE
(`resharper_use_indent_from_vs`) and cleanup profiles that differ from the format action.

The sharp edge here was indentation autodetection — Rider detecting a file's actual indentation and
formatting against *that* rather than the configured value, which would make the IDE and the oracle
disagree with each other and leave Skala unable to match both.

**Resolved by decision, not by experiment:** the template now sets

```ini
resharper_autodetect_indent_settings = false
resharper_apply_auto_detected_rules  = false
```

so the configured indentation is the only indentation, in the IDE and on the command line. Both keys
are Tier C (accepted, ignored) for Skala — it has no autodetection to switch off — and `skala config
check` reports `SK9006` if either is ever set back to `true`, because that reintroduces a
disagreement Skala cannot resolve.

What remains of Q1 is the smaller question of cleanup-profile parity, which the oracle harness
handles by pinning the profile explicitly ([12](12-conformance-and-testing.md)).

### Q2 — How aggressive should the first run be?

The adoption path ([11](11-cli-and-integrations.md)) assumes one enormous formatting commit. An
alternative is a `--conservative` mode that only fixes what is unambiguously wrong (spaces, blank
lines) and leaves all line breaks alone, so the first diff is a tenth the size and the second diff
comes later.

Undecided. It costs a mode and it may be the difference between adoption and revert on a repository
that is not the author's.

### Q3 — Should arrangement ever run in the pre-commit hook?

It needs a compilation, which needs a build, which a pre-commit hook cannot afford. The syntactic
subset can run, and probably should. But a commit where half the arrangement rules ran and half did
not is a commit with an arbitrary boundary in it.

Leaning: syntactic subset in the hook, full arrangement as a deliberate command and in CI as a
*check* (not a fix).

### Q4 — What is the story for multi-repository consistency?

The stated goal is "used across all my projects so everything is consistent". Today that means
copying `.editorconfig` into each repository, which drifts. Options: a `Rikarin.Skala.Sdk` package
that carries the canonical `.editorconfig` and drops it at restore time (drift becomes a version
bump); an `.editorconfig` that `import`s — which editorconfig does not support; or a git submodule,
which nobody enjoys.

Leaning toward the SDK package, with `skala config diff` against the packaged canonical file as a
gate condition so drift is a finding rather than a surprise. Not designed yet.

### Q5 — Does the ReSharper severity mapping survive?

[03](03-configuration-model.md) claims the 853 `resharper_*_highlighting` keys can configure Skala's
rules through a mapping table. That works where a Skala rule corresponds one-to-one with a ReSharper
inspection. Many do not: one ReSharper inspection may cover what Skala splits into three rules, or
vice versa.

**Resolution needed by M5.** The likely answer is that the mapping is many-to-one in the direction
that is safe (a ReSharper key sets the severity of every Skala rule it maps to) and that
`dotnet_diagnostic.SK…` overrides it, with `skala config explain` showing which mechanism won.

### Q6 — Duplication across repositories?

Sonar can detect a clone between two projects. Skala's index is per-repository. Cross-repository
clone detection would be genuinely useful in a tree of eighteen related projects — and is a research
project with a storage story attached.

Out of scope, noted because the question will come back.

### Q7 — What happens when Rider changes its formatter?

A ReSharper update changes output; the fixtures become wrong; fidelity appears to regress. This is a
recurring maintenance cost with no clean answer.

**Current plan:** the fixture header records the ReSharper version, regeneration is a reviewed commit
([12](12-conformance-and-testing.md)), and the divergence register absorbs deliberate
non-following. Skala is allowed to *not* follow a ReSharper change — it is a compatibility target,
not a master.

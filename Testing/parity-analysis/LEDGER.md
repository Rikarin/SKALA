# The curation ledger

[`docs/plan/17`](../../docs/plan/17-inspection-parity.md) measures the gap between Skala and the two
tools it replaces. It stops at a count. **These two files are what was then done with that count**:
the judgement that turned 586 uncovered ReSharper inspections and 481 published SonarQube rules into
247 rule proposals, recorded so the next revision can **re-run it rather than re-argue it** — the
standard [`README.md`](README.md) sets for the rest of this directory.

| File | What it records |
|---|---|
| `ledger-resharper.json` | 136 proposed concepts covering 493 inspections, and 97 inspections excluded with a written reason each |
| `ledger-sonar.json` | 110 proposed concepts covering 156 rules, and 325 rules resolved as shipped, tracked, hosted, decided or out of scope |
| `ledger-sonar.json` § `ideas` | 28 proposed concepts covering 32 of upstream's **open, unimplemented** rule ideas, and 79 resolved |
| `verify_ledger.py` | Asserts that neither ledger can lose a rule, and that no shipped rule can go unrecorded |

⚠ The three counts above are what `verify_ledger.py` prints today, not what this file said before.
The previous row read *137 concepts / 494 inspections / 94 excluded* and every one of the three was
stale. Re-read them from a run rather than from here.

⚠ **The `ideas` section has a different source and a different half-life.** Its 111 entries are open
`Rule Idea` issues on `SonarSource/sonar-dotnet` — proposals upstream has *not* specified or
implemented, so they appear in no published rule list and the audit of `analyzers/rspec/cs` could not
see them. That also means there is no reference implementation to compare against and no
false-positive experience to inherit: the idea is the contribution and the specification is entirely
ours. It is a **snapshot**, dated in `auditedAgainst`, and nothing re-checks it; the verifier says so
on every run rather than letting the date go quietly stale. Refresh it by re-running the `gh issue
list` recorded in `auditedAgainst.query`.

Each concept carries the GitHub issue that tracks it, so the queue is navigable in both directions:
from an inspection id to the issue arguing for it, and from an issue back to every rule it retires.

## Why a ledger and not just the issues

The issues carry the argument. They do **not** carry the *negative* half, and the negative half is
most of the work: 419 of the 1 067 decisions were "no, and here is why". A rule excluded with a
reason is settled; a rule that simply never appears is indistinguishable from one nobody looked at,
and the next audit pays for it again. ⚠ The first version of this analysis had exactly that shape —
doc 17 § "Three corrections during the pass" records 78 inspections that moved bucket once somebody
read them rather than their names, a 12 % error all in one direction.

## The completeness claim, and what checks it

Every rule appears **exactly once** — in a concept, or in the exclusions. `verify_ledger.py` asserts
it against the pipeline's own output, and fails on a rule that appears in neither:

```bash
python3 universe.py && python3 classify.py   # -> classified.json
python3 fetch_sonar.py                       # -> sonar.json
python3 verify_ledger.py
```

Both generated inputs are optional; the structural checks (unique slugs, unique issue numbers, no
rule claimed twice, no rule both assigned and excluded, vocabularies for range/severity/scope/fix)
run without them, and the cross-checks announce loudly that they were skipped rather than passing
quietly. A check that is silently not running is the failure mode this whole directory exists to
avoid.

⚠ **Reconcile items are warnings, not failures.** When the pipeline is corrected or ReSharper ships a
release, a rule can move out of `Uncovered` while the ledger still assigns it. That is a prompt to
narrow or close a concept, not a broken ledger — the verifier prints it and exits 0. The one
exception is deliberate: a concept may name an inspection the pipeline buckets `Catalogued`, because
`docs/plan/08` allocates ids for concepts it has never shipped (`SK1002`, `SK1004`), and a proposal
to *implement* one of those is exactly what the ledger should hold.

⚠ **Read `Uncovered` from a run, never from here.** It is the last line of
`python3 universe.py && python3 classify.py`, and it fell 563 → 508 in the two hours this section
took to write, entirely because other agents were shipping rules. What follows is a delta, which
survives; the absolutes are dated examples of why not to quote one.

The four map entries added in this pass are worth **−3**:
`PublicConstructorInAbstractClass`, `ChangeFieldTypeToSystemThreadingLock` and
`RedundantDictionaryContainsKeyBeforeAdding` leave the residue, and `ArrangeNullCheckingPattern`
moves `Option` Tier D → `Catalogued` without touching it.

⚠ **The absolute moves under you and the delta does not.** Those same four entries measured
563 → 560 at `8d0d8fb3` and 525 → 522 an hour later — same change, same −3, two different
headlines — because 39 more rules and ~38 more map entries landed in between. **Quote the delta,
re-run for the absolute.** The 586 in `curatedAgainst` is two pipeline corrections and a
catalogue's worth of rules out of date.

⚠ **`universe.py` prints a complete-looking universe with zero metadata when `types-2026.xml` is
absent** — 953 rows, 888 C#-proper, every `id` null — and every downstream map then misses. The file
is `.gitignore`d, so a fresh worktree has none and the run looks fine. Check the
`with tool metadata:` line reads **872**, not 0, before trusting anything below it.

⚠ **`uncoveredCount: 586` in `ledger-resharper.json` was measured before the id-keyed lookup defect
in `classify.py` was found**, and is therefore inflated. A re-run after that fix moves several
inspections into `Catalogued`/`Hosted`; expect reconcile warnings on the next run and use them to
narrow the affected concepts. The number is kept rather than quietly updated because the curation was
performed against it.

## ⚠ The Sonar half and the licence

`sonar-dotnet` is under the **SONAR Source-Available License v1.0**, which is not open source, and
whose definition of "Competing" describes what Skala is. Doc 17 § "The licence" sets the boundary and
this file stays inside it:

- `ledger-sonar.json` carries **rule ids and this repository's own judgement about them**. It carries
  none of SonarSource's rule prose.
- The audit read `analyzers/rspec/cs/*.json` — the published metadata, the same facts served at
  `rules.sonarsource.com`. `analyzers/src/**`, their implementation, was never fetched: the working
  copy was a `git sparse-checkout` limited to the metadata directory, so the boundary was enforced by
  the checkout rather than by intention. `fetch_sonar.py` enforces the same thing by assertion.
- **Work from the problem, never from their solution.** Every summary and rationale in the issues is
  written from scratch against the rule title alone.

## The second completeness claim: what has *shipped*

Everything above tracks the **proposal queue** — that no inspection or Sonar rule can be dropped
without a word. Nothing tracked the other direction, and the cost was measured: 84 rules landed in
`rules.json` while `catalogued.json` went almost entirely un-updated. Joining the map against the
ledgers reached **13 concepts** for all 84, and every one of the 13 had already been handled by hand.

Two things now record it, and `verify_ledger.py` asserts both.

**Concepts carry `coverage` and `coveredBy`.** `coverage` is `complete` (every inspection or `S####`
listed on the concept is covered) or `partial`; `coveredBy` names the shipped `SK` ids. Today:
**13 complete / 6 partial** on ReSharper, **10 / 1** on Sonar, **1 / 1** on the idea list. A
`complete` claim is what closes a GitHub issue, so the verifier requires it to name at least one id
that is actually in `rules.json`.

⚠ **Every `complete` concept turned out to be an issue that was already closed.** The reconciliation
produced **zero** new closures. The expectation going in was that proposals would duplicate shipped
rules; instead the issue bodies consistently name the neighbouring Skala rule and carve around it
(#67↔SK4002, #100↔SK4010, #146↔SK5002, #164↔SK5001, #208↔SK6003, #256↔SK3004, #266↔SK2014). The
curation was careful. What was stale was the *map*, not the queue.

**The map's keys are checked, not just its values.** `RuleCatalogTests` asserts every *value* is an
id doc 08 knows. Nothing asserted the *keys*, and 16 of them name an inspection that does not
exist — `MemberCanBeInternal.Global` has no `.Global` suffix in ReSharper, `CyclomaticComplexity` and
`CognitiveComplexity` have no ReSharper counterpart at all. They matched no universe row, credited
nothing, and were counted as part of the map's size.

## What the map actually decides

An entry only decides a bucket if its key matches a universe row *and* no higher-precedence bucket
claims that row first. Three states, and only one of them does anything:

| | effect |
|---|---|
| **Load-bearing** — the entry is what puts the row in `Catalogued` | real |
| **Shadowed** — `hosted()` runs before `catalogued()`, so a `CA*`/`IDE*` claim wins | none |
| **Inert** — the key matches no universe row | none |

⚠ **No count is written here on purpose.** `verify_ledger.py` prints the split on every run
(`parity map: N of M entries match a universe row`), and the numbers moved three times in the hour
this section was being written, as twelve agents shipped rules and appended to the map. When it was
first measured the map was 142 entries of which **107** were load-bearing, **18** shadowed and **17**
inert — a quarter of it doing nothing. Read the current split from a run.

⚠ **The 18 shadowed entries were the interesting ones, and they have since been adjudicated.**
`SK1006`, `SK1010`, `SK1012`, `SK1020`, `SK1030` and `SK1034` each shipped a rule for a concept the
hosted map said Roslyn already covers (IDE0063, IDE0078, IDE0066, CA1510, IDE0074, CA1860). ADR-008
is *host, never rebuild*, so either those hosted entries were wrong or six shipped rules were
duplicates. **The answer turned out to be neither, and the distinction that resolved it is the state
the host is in** — recorded now in `classify.py`'s `host_state`:

- `IDE0063`, `IDE0078`, `IDE0066`, `IDE0074` are **code-style**: off unless somebody sets
  `EnforceCodeStyleInBuild` and an `.editorconfig` key. A host nobody has turned on shadows nothing,
  and `SK1006`, `SK1010`, `SK1012` and `SK1030` are the ones actually reporting the concept in a
  build with nothing configured. They stay.
- `CA1510` (for `SK1020`) and `CA1860`/`CA1829` (for `SK1034`) are **on at stock** — enabled by
  default at `Info`, no configuration at all. Those two rules were genuine duplicates and are
  **retired** (#281). ⚠ Measured, not reasoned: a probe outside the repository with an empty
  `Directory.Build.props`/`.targets` and a `root = true` `.editorconfig` above it, read from a SARIF
  2.1 error log because all of it fires at `note` and prints nothing on the console. 3/3 and 4/4 of
  the two rules' positive fixtures respectively.

⚠ `classify.py` prints an alert for any remaining row in the second state. It reads zero now, and
`shipped_ids` excludes `retired` entries so that acting on the alert can actually clear it.

## Entries that over-claim

⚠ `catalogued.json` used to map `UseThrowIfNullMethod` and `UseArgumentExceptionThrowIfMethod` →
`SK1020` and `UseCollectionCountProperty` → `SK1034`. All three are **removed**: those rules are
retired, and a map entry crediting a withdrawn rule claims Skala answers an inspection it does not.
The rows bucket `Hosted` now, which is where they always belonged. The bucket totals did not move,
because `hosted()` runs before `catalogued()` and was already claiming them.

⚠ This section previously also named `ReplaceWithOfType` → `SK4010` as a deliberate over-claim.
**That was moot and nobody had checked**: `ReplaceWithOfType` is not an inspection id — only the
dotted variants (`ReplaceWithOfType.Any.1`, `.Count.1`, `.Where`, …) exist, and those are already
members of concept #100. The entry suppressed nothing. A bare `grep -c` returns 18 hits and every one
is a longer id, which is how it read as real.

Larger over-claims found in the same pass and **reported rather than fixed**, because each needs a
`jb inspectcode` run or an owner's decision: all eight `SK2001` entries (mapped against doc 08's
older wording, not the shipped rule — see #8); nine of fifteen `SK2010` entries, which is why #51
lists three search inspections instead of nine; `StructuredMessageTemplateProblem` → `SK2016`, an
umbrella id covering the whole of #20; `RedundantSuppressNullableWarningExpression` → `SK7050`, a
`warning`-severity inspection in neither `concepts[]` nor `excluded[]`; and
`OutParameterValueIsAlwaysDiscarded.Global`/`.Local` → `SK2006`, an id that has never existed.

## Regenerating

The ledgers are **hand-written judgement**, in the same class as `catalogued.json`, `gov.json` and
`sonar_hand.json`, and belong on the same list of files in this directory that are not measurements.
They are not regenerated by a script; they are edited when a concept is specified, split, narrowed or
closed. `verify_ledger.py` is what keeps an edit from losing a rule.

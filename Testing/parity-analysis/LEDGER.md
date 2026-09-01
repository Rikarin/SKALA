# The curation ledger

[`docs/plan/17`](../../docs/plan/17-inspection-parity.md) measures the gap between Skala and the two
tools it replaces. It stops at a count. **These two files are what was then done with that count**:
the judgement that turned 586 uncovered ReSharper inspections and 481 published SonarQube rules into
247 rule proposals, recorded so the next revision can **re-run it rather than re-argue it** — the
standard [`README.md`](README.md) sets for the rest of this directory.

| File | What it records |
|---|---|
| `ledger-resharper.json` | 137 proposed concepts covering 494 inspections, and 94 inspections excluded with a written reason each |
| `ledger-sonar.json` | 110 proposed concepts covering 156 rules, and 325 rules resolved as shipped, tracked, hosted, decided or out of scope |
| `verify_ledger.py` | Asserts that neither ledger can lose a rule |

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

## Two entries that over-claim, deliberately left alone

`catalogued.json` maps `UseArgumentExceptionThrowIfMethod` → `SK1020` and `ReplaceWithOfType` →
`SK4010`. The shipped `SK1020` covers `ArgumentNullException.ThrowIfNull` only, and the shipped
`SK4010` covers a `Where` fused into a following operator, not `OfType`. Both concepts are broader
than the rule credited with them, so the ledger proposes them anyway. Narrowing the map is a decision
for whoever specifies those rules, and it is recorded here rather than taken silently.

## Regenerating

The ledgers are **hand-written judgement**, in the same class as `catalogued.json`, `gov.json` and
`sonar_hand.json`, and belong on the same list of files in this directory that are not measurements.
They are not regenerated by a script; they are edited when a concept is specified, split, narrowed or
closed. `verify_ledger.py` is what keeps an edit from losing a rule.

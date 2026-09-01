# `parity-analysis/` — the scripts behind [docs/plan/17](../../docs/plan/17-inspection-parity.md)

Doc 17 replaces a rule-catalogue number nobody had audited. These are the scripts that produced its
numbers, committed so that the next revision can **re-run them rather than re-argue them**.

Plain Python 3, no dependencies, no project file — deliberately not an MSBuild project, so nothing
here is part of the solution, the build or the test run.

## Order

⚠ The issue-type dump is read from **this directory**, not from the scratch dir — writing it
to `$W` left `universe.py` with no metadata to join and silently produced a universe with 888
entries and 0 of them described. `$W` holds only the SARIF reports.

```bash
W=/tmp/parity && mkdir -p $W
P=$(git rev-parse --show-toplevel)/Testing/parity-analysis

# 1. ReSharper's own catalogue, from whichever jb is being used to measure.
jb inspectcode --dumpIssuesTypes -o=$P/types-2026.xml -f=Xml

# 2. The universe: the C#-relevant inspections in the author's export, plus metadata.
python3 universe.py                 # -> universe.json

# 3. One bucket per inspection.
python3 classify.py                 # -> classified.json
python3 concepts.py                 # ids -> distinct concepts

# 4. Firing counts. See "the two ways a zero can lie" in doc 17 before changing anything here.
python3 graft.py <export> <scratch-copy>/.editorconfig     # raise every inspection to `warning`
jb inspectcode -e=INFO --no-build <project> -o=$W/reports/<name>.sarif -f=Sarif
python3 aggregate.py                # joins fire counts onto classified.json
python3 report.py                   # emits doc 17's tables

# 5. SonarQube. Read the licence boundary in doc 17 first.
python3 fetch_sonar.py              # metadata only, never analyzers/src/**
python3 sonar_sample.py 60          # the reproducible hand-classification sample
python3 sonar_sens.py               # why the mechanical title join is not usable
```

`review.py <Category>` and `find.py <regex>…` are for reading the classification, not for producing
numbers.

## ⚠ The tree's build state changes the answer

The fire-count runs are `--no-build` over a `git archive`, where a great many types do not resolve —
and an inspection that depends on a type it cannot resolve stays quiet. Re-run with the same ten
projects restored and built, the same sources report **7 756** `Option`-bucket findings rather than
3 718.

⚠ **Neither number is wrong and they are not comparable.** A before/after pair must be measured on
two trees in the *same* build state or the difference is mostly MSBuild. Per-inspection counts are
much more robust than bucket totals: `ArrangeRedundantParentheses` reproduces at 1 231 unbuilt and
1 226 built.

## ⚠ Two files here are judgement, not measurement

`gov.json` (inspection → governing option key) and `catalogued.json` (inspection → `SK` id) were
written by hand. They are the soft edge of the whole analysis: **every entry missing from them
inflates the uncovered count.** `sonar_hand.json` is the 60-rule sample classified by hand, kept so
the projection in doc 17 can be checked rather than taken on trust.

⚠ **`catalogued.json` is now pinned in the one direction a test can hold it.**
`RuleCatalogTests.TheParityMap_CreditsEveryShippedReSharperMappingToItsOwnRule` asserts
rules.json ⊆ `catalogued.json`, matched on the `SK` id: an inspection that a *shipped* rule declares
as its `resharperId` must be credited to that rule. Four shipped rules were missing from the map when
that test was written. **The reverse is deliberately not asserted** — `Catalogued` means an id in
doc 08 names the concept, allocated is enough and shipped is not required, so entries pointing at
ids nothing implements yet are the map working correctly. What is checked instead is that every value
is well formed and is a number the register knows. `gov.json` has no equivalent pin.

⚠ **The maps used to be looked up by inspection id, and that was a silent failure mode.**
`universe.py` can only attach an id by joining the export key against the issue-type dump, and that
join misses every inspection newer than the dumped release — 81 of the 888 rows carry `id: null`.
Keyed on id alone, all 81 bypassed both maps without a word and landed in `Uncovered`, which is the
residue and therefore the work queue. `classify.py` now reads both maps through a key-indexed view
built with the same `snake()` transform, and **the two copies of `snake()` must be kept in step.**
This inflated doc 17's published figure; the correction is recorded there.

⚠ **`OracleProfile.Cleanup` belongs on this list too**, and doc 17's first run is the proof. Five
inspections were classified as arrangement Skala "declares and does not perform" when the oracle
itself was not performing them either — because the profile was missing two real cleanup tasks that
nobody had probed for. A gap measured against an under-configured oracle is a gap in the oracle.
See `../../docs/oracle-cleanup-profile.md` § "Two tasks the first sweep missed".

## ⚠ What these scripts must never do

`fetch_sonar.py` touches only `analyzers/rspec/cs/*.json` — published rule *metadata* — and asserts
that it never fetches `analyzers/src/**`. `sonar-dotnet` is under the SONAR Source-Available License
v1.0, which is not open source and whose definition of "Competing" describes what Skala is.
Doc 17 § "The licence" states the boundary; the assertion in the script is there so that a future
edit trips over it.

# `parity-analysis/` — the scripts behind [docs/plan/17](../../docs/plan/17-inspection-parity.md)

Doc 17 replaces a rule-catalogue number nobody had audited. These are the scripts that produced its
numbers, committed so that the next revision can **re-run them rather than re-argue them**.

Plain Python 3, no dependencies, no project file — deliberately not an MSBuild project, so nothing
here is part of the solution, the build or the test run.

## Order

⚠ The issue-type dump is read from **this directory**, not from the scratch dir — writing it
to `$W` left `universe.py` with no metadata to join and silently produced a universe with 888
entries and 0 of them described. `$W` holds only the SARIF reports.

⚠ **Step 1 is committed now and you should not re-run it casually.** `types-2026.xml` is in git:
it is a dumped snapshot of an external tool's catalogue, ReSharper is being retired, and re-dumping
from a later release would silently move every parity number measured against it. While it was
gitignored, `verify_ledger.py`'s strongest assertion did not run in any fresh clone and the run
still printed `0 failures` (#311).

```bash
W=/tmp/parity && mkdir -p $W
P=$(git rev-parse --show-toplevel)/Testing/parity-analysis

# 1. ReSharper's own catalogue. COMMITTED — re-dump only deliberately, and say that you did.
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

⚠ **`catalogued.json`'s keys are pinned by nothing at all, and neither are `gov.json`'s.**
There used to be a cross-check, in C# and again in `verify_ledger.py`: rules.json ⊆
`catalogued.json`, matched on the `SK` id, so an inspection that a *shipped* rule declared as its
`resharperId` had to be credited to that rule. It caught 17 phantom keys, ~25 wrong credits, 14
under-credits and 7 entries crediting rules that do not exist. **It is gone**, because `resharperId`
is gone — the field went out with the `resharper_*_highlighting` severity bridge, since one field
could name one inspection while this map credits 295 inspections to 162 rules, 49 of them covering
more than one. ⚠ **Nothing replaces it and nothing is meant to**; see doc 17. Treat every inspection
name in this file as unverified.

What is still checked is only the map's **values**:
`RuleCatalogTests.TheParityMap_CreditsOnlyIdsTheRegisterHasAllocated` and `verify_ledger.py` (1b)
assert every `SK` id it credits is one `allocated-ids.txt` holds. **Map ⊆ rules.json is deliberately
not asserted** — `Catalogued` means an id in doc 08 names the concept, allocated is enough and
shipped is not required, so entries pointing at ids nothing implements yet are the map working
correctly.

⚠ **`editor_config_template` is the universe; `types-2026.xml` is only metadata joined onto it.**
Getting that backwards produces a specific wrong answer, and #318 produced it: it measured
`catalogued.json`'s keys against the XML and reported 26 of them as fabrications. **The real count
against that reference is 29**, and the three the issue missed —`CognitiveComplexity`,
`SelfAssignment`, `ReplaceWithOfType` — are missing because the measurement matched *substrings*:
all three occur in the file only inside longer ids (`CppClangTidyReadabilityFunctionCognitiveComplexity`,
`cplusplus.SelfAssignment`, `ReplaceWithOfType.1`). ⚠ **But 29 is the wrong question.** Fourteen of
them are real, live, correctly mapped inspections that merely post-date the dump —
`ConvertToExtensionBlock`, `MoveToExtensionBlock`, the three `NUnit*`, `ShortLivedHttpClient` and
the rest all carry a `resharper_*_highlighting` key in the export. The dump is **known-incomplete by
construction**, which is exactly why 81 of the 888 C#-proper rows carry `id: null`. Checking keys
against the XML would fail on correct entries. `verify_ledger.py` checks them against the universe,
which is the reference that answers the question actually being asked.

⚠ **A claim that used to stand in `universe.py` and is false**: the comment on the `types-2026.xml`
load said it "covers plugin + newer-C# inspections that the 2025.2.6 base dump omits (NUnit, EF,
logging templates, `ConvertToExtensionBlock`, ...)". `grep -c 'Id="NUnit'` is **0**, and no id in
the file contains `ExtensionBlock`. Every example the comment gave is one the file does not have.

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

# `parity-analysis/` — the scripts behind [docs/plan/17](../../docs/plan/17-inspection-parity.md)

Doc 17 replaces a rule-catalogue number nobody had audited. These are the scripts that produced its
numbers, committed so that the next revision can **re-run them rather than re-argue them**.

Plain Python 3, no dependencies, no project file — deliberately not an MSBuild project, so nothing
here is part of the solution, the build or the test run.

## Order

```bash
W=/tmp/parity && mkdir -p $W

# 1. ReSharper's own catalogue, from whichever jb is being used to measure.
jb inspectcode --dumpIssuesTypes -o=$W/types-2026.xml -f=Xml

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

## ⚠ Two files here are judgement, not measurement

`gov.json` (inspection → governing option key) and `catalogued.json` (inspection → `SK` id) were
written by hand. They are the soft edge of the whole analysis: **every entry missing from them
inflates the uncovered count.** `sonar_hand.json` is the 60-rule sample classified by hand, kept so
the projection in doc 17 can be checked rather than taken on trust.

## ⚠ What these scripts must never do

`fetch_sonar.py` touches only `analyzers/rspec/cs/*.json` — published rule *metadata* — and asserts
that it never fetches `analyzers/src/**`. `sonar-dotnet` is under the SONAR Source-Available License
v1.0, which is not open source and whose definition of "Competing" describes what Skala is.
Doc 17 § "The licence" states the boundary; the assertion in the script is there so that a future
edit trips over it.

#!/usr/bin/env python3
"""
doc 12 § "Testing the rules", addition 2: every rule is run over the whole reference corpus in a
nightly job, its finding count is recorded in `.skala/rule-counts.json`, and a rule whose count
changes by more than 10 % between commits without an intentional change is flagged.

⚠ There is no new C# here, and that is the point. `skala check` already emits SARIF, SARIF already
carries `ruleId` on every result, and a counter written in the analysis layer would be a second
implementation of "what did the run find" that can disagree with the report the run wrote. This
reads the artefact the tool actually produced.

Two subcommands, deliberately not one:

    record <report.sarif> <out.json> [--corpus NAME] [--mode NAME] [--commit SHA]
    compare <baseline.json> <current.json> [--threshold 10] [--summary FILE]

`record` never compares and `compare` never runs the tool, so regenerating the baseline after an
intentional rule change is `record` alone and cannot be confused with accepting a drift.
"""

from __future__ import annotations

import argparse
import datetime
import json
import subprocess
import sys
from collections import Counter
from pathlib import Path

# ⚠ The floor exists because a percentage over a small count is noise. A rule that goes from 2
# findings to 3 has "risen 50 %" and means nothing; a rule that goes from 400 to 600 is the
# over-firing regression doc 12 wants caught before a release. Below this, only appearing and
# disappearing entirely are reported.
NOISE_FLOOR = 10


def load_sarif(path: Path) -> dict:
    with path.open(encoding="utf-8-sig") as handle:
        return json.load(handle)


def count(document: dict) -> dict[str, int]:
    """Every result in every run, by rule id."""
    counter: Counter[str] = Counter()
    for run in document.get("runs", []):
        for result in run.get("results", []):
            rule = result.get("ruleId")
            if rule:
                counter[rule] += 1

    return dict(sorted(counter.items()))


def located(document: dict) -> set[str]:
    """
    The distinct files something was reported in.

    ⚠ Not <c>run.artifacts</c>: `skala check` does not emit an artifacts table, so reading one gives
    a confident zero. Counting the locations the results actually carry gives a number that moves
    when the corpus does, which is what makes a count comparable between two commits.
    """
    files: set[str] = set()
    for run in document.get("runs", []):
        for result in run.get("results", []):
            for location in result.get("locations", []):
                uri = location.get("physicalLocation", {}).get("artifactLocation", {}).get("uri")
                if uri:
                    files.add(uri)

    return files


def declared_rules(document: dict) -> list[str]:
    """
    Every rule the run's tool declared, whether or not it fired.

    ⚠ Zero is a finding count and the interesting one: a rule that stops firing entirely is the
    regression that a dictionary of only-what-fired cannot express, because the id simply vanishes
    from the file and a diff of two such files shows a deletion that reads like a removed rule.
    """
    ids: set[str] = set()
    for run in document.get("runs", []):
        driver = run.get("tool", {}).get("driver", {})
        for rule in driver.get("rules", []):
            if rule.get("id"):
                ids.add(rule["id"])

    return sorted(ids)


def record(arguments: argparse.Namespace) -> int:
    document = load_sarif(arguments.sarif)
    fired = count(document)
    counts = {rule: fired.get(rule, 0) for rule in sorted(set(declared_rules(document)) | set(fired))}

    payload = {
        "corpus": arguments.corpus,
        "mode": arguments.mode,
        "commit": arguments.commit or git_head(),
        "recorded": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "files": len(located(document)),
        "total": sum(counts.values()),
        "counts": counts,
    }

    arguments.out.parent.mkdir(parents=True, exist_ok=True)
    with arguments.out.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")

    print(f"{len(counts)} rules, {payload['total']} findings -> {arguments.out}")
    return 0


def git_head() -> str:
    try:
        return subprocess.run(
            ["git", "rev-parse", "HEAD"], capture_output=True, text=True, check=True
        ).stdout.strip()
    except (subprocess.CalledProcessError, FileNotFoundError):
        return "unknown"


def compare(arguments: argparse.Namespace) -> int:
    with arguments.baseline.open(encoding="utf-8") as handle:
        baseline = json.load(handle)
    with arguments.current.open(encoding="utf-8") as handle:
        current = json.load(handle)

    if baseline.get("corpus") != current.get("corpus") or baseline.get("mode") != current.get("mode"):
        # ⚠ Not a drift and not ignorable. Two runs over different corpora or under different load
        # modes produce different counts for reasons that have nothing to do with the rules, and
        # reporting that as a 40 % regression in nine rules is how a nightly job gets muted.
        print(
            f"::error::the baseline was recorded over {baseline.get('corpus')}/{baseline.get('mode')} "
            f"and this run over {current.get('corpus')}/{current.get('mode')}. Not comparable.",
        )
        return 2

    before: dict[str, int] = baseline.get("counts", {})
    after: dict[str, int] = current.get("counts", {})

    flagged: list[str] = []
    lines: list[str] = []

    for rule in sorted(set(before) | set(after)):
        was = before.get(rule)
        now = after.get(rule)

        if was is None:
            # A rule the baseline had never heard of: new, and its first count is its baseline.
            lines.append(f"| `{rule}` | — | {now} | new rule |")
            continue

        if now is None:
            flagged.append(rule)
            lines.append(f"| `{rule}` | {was} | — | **gone from the catalogue** |")
            continue

        if was == now:
            continue

        if was == 0:
            flagged.append(rule)
            lines.append(f"| `{rule}` | 0 | {now} | **started firing** |")
            continue

        if now == 0:
            flagged.append(rule)
            lines.append(f"| `{rule}` | {was} | 0 | **stopped firing** |")
            continue

        change = (now - was) / was * 100
        if max(was, now) < NOISE_FLOOR:
            lines.append(f"| `{rule}` | {was} | {now} | {change:+.0f} %, under the noise floor |")
            continue

        if abs(change) > arguments.threshold:
            flagged.append(rule)
            lines.append(f"| `{rule}` | {was} | {now} | **{change:+.1f} %** |")
        else:
            lines.append(f"| `{rule}` | {was} | {now} | {change:+.1f} % |")

    report = [
        "## Rule counts over " + str(current.get("corpus")),
        "",
        f"{current.get('total')} findings from {len(after)} rules, "
        f"against {baseline.get('total')} at `{str(baseline.get('commit'))[:12]}`.",
        "",
    ]

    if lines:
        report += ["| Rule | Was | Now | Change |", "|---|---:|---:|---|", *lines, ""]
    else:
        report += ["No rule's count moved.", ""]

    if flagged:
        report += [
            f"⚠ **{len(flagged)} rule(s) flagged** — moved by more than {arguments.threshold} %, "
            f"started firing, stopped firing, or left the catalogue: "
            + ", ".join(f"`{rule}`" for rule in flagged)
            + ".",
            "",
            "If the change was intended, regenerate the baseline in the same commit that caused it:",
            "",
            "```",
            "dotnet run --project Tools/Rikarin.Skala.Cli -c Release -- check \\",
            "  --load=loose --no-cache --include-hints --duplication \\",
            "  --format=plain --output .skala/rule-counts.sarif \\",
            "  $(find Testing/corpus/real -name '*.cs' ! -name '*.expected.cs' | sort)",
            "python3 .github/scripts/rule-counts.py record \\",
            "  .skala/rule-counts.sarif .skala/rule-counts.json --corpus corpus/real --mode loose",
            "git add -f .skala/rule-counts.json   # .skala/.gitignore is `*`",
            "```",
            "",
        ]

    text = "\n".join(report)
    print(text)
    if arguments.summary:
        with arguments.summary.open("a", encoding="utf-8") as handle:
            handle.write(text + "\n")

    return 1 if flagged else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subcommands = parser.add_subparsers(dest="subcommand", required=True)

    recorder = subcommands.add_parser("record", help="SARIF in, rule-counts.json out.")
    recorder.add_argument("sarif", type=Path)
    recorder.add_argument("out", type=Path)
    recorder.add_argument("--corpus", default="corpus/real")
    recorder.add_argument("--mode", default="loose")
    recorder.add_argument("--commit", default=None)
    recorder.set_defaults(handler=record)

    comparer = subcommands.add_parser("compare", help="Two rule-counts.json files, and a verdict.")
    comparer.add_argument("baseline", type=Path)
    comparer.add_argument("current", type=Path)
    comparer.add_argument("--threshold", type=float, default=10.0)
    comparer.add_argument("--summary", type=Path, default=None)
    comparer.set_defaults(handler=compare)

    arguments = parser.parse_args()
    return arguments.handler(arguments)


if __name__ == "__main__":
    sys.exit(main())

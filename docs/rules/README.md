# Rules

<!-- Generated from Rules/Rikarin.Skala.Rules.Metadata/rules.json. Do not edit. -->

`SK` + four digits, allocated once and never re-purposed (ADR-012). 17 ids are allocated.

## Formatting

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK0001](SK0001.md) | The file is not formatted | suggestion | safe | yes |
| [SK0002](SK0002.md) | The line is over the width and nothing in it can break | hint | — | yes |
| [SK0003](SK0003.md) | The documentation comment is not well-formed XML | hint | — | yes |

## Modernization

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK1005](SK1005.md) | Use a file-scoped namespace | suggestion | safe | yes |
| [SK1010](SK1010.md) | Use `is null` / `is not null` instead of `==` / `!=` | suggestion | safe | no |
| [SK1020](SK1020.md) | Use `ArgumentNullException.ThrowIfNull` | suggestion | safe | no |
| [SK1030](SK1030.md) | Use `??=` | suggestion | safe | yes |
| [SK1034](SK1034.md) | Use the `Count` property, not `Count()` or `Any()` | suggestion | safe | no |
| [SK1035](SK1035.md) | Use `Enum.GetValues<T>()` | suggestion | safe | no |

## Tool

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK9001](SK9001.md) | Unknown configuration key | suggestion | — | no |
| [SK9010](SK9010.md) | The file does not parse | warning | — | yes |
| [SK9011](SK9011.md) | A member's braces are split across a preprocessor branch | suggestion | — | yes |
| [SK9020](SK9020.md) | The binlog is stale for this file | suggestion | — | no |
| [SK9021](SK9021.md) | The binlog does not name a file that exists | warning | — | no |
| [SK9030](SK9030.md) | An analyzer threw | warning | — | no |
| [SK9031](SK9031.md) | An analyzer package failed to load | warning | — | no |
| [SK9099](SK9099.md) | The formatter's output was not token-equivalent | error | — | yes |


# Rules

<!-- Generated from Rules/Rikarin.Skala.Rules.Metadata/rules.json. Do not edit. -->

`SK` + four digits, allocated once and never re-purposed (ADR-012). 29 ids are allocated.

## Async

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK3001](SK3001.md) | `async void` outside an event handler | none | review | no |
| [SK3002](SK3002.md) | Blocking on an async call | warning | review | no |

## Correctness

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK2013](SK2013.md) | An exception is constructed and then discarded | warning | safe | no |
| [SK2015](SK2015.md) | `throw ex;` resets the stack trace | warning | safe | yes |

## Formatting

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK0001](SK0001.md) | The file is not formatted | suggestion | safe | yes |
| [SK0002](SK0002.md) | The line is over the width and nothing in it can break | hint | — | yes |
| [SK0003](SK0003.md) | The documentation comment is not well-formed XML | hint | — | yes |

## Maintainability

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK7001](SK7001.md) | Cyclomatic complexity is over the threshold | hint | — | no |
| [SK7002](SK7002.md) | Cognitive complexity is over the threshold | suggestion | — | yes |
| [SK7003](SK7003.md) | The member is over the statement-count threshold | hint | — | yes |
| [SK7004](SK7004.md) | The type is over the member-count threshold | hint | — | yes |
| [SK7005](SK7005.md) | The member takes more parameters than the threshold | hint | — | yes |
| [SK7006](SK7006.md) | The member nests deeper than the threshold | hint | — | yes |
| [SK7010](SK7010.md) | Public API without a documentation comment | none | — | yes |
| [SK7020](SK7020.md) | Duplicated block | warning | — | no |

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


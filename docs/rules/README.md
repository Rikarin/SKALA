# Rules

<!-- Generated from Rules/Rikarin.Skala.Rules.Metadata/rules.json. Do not edit. -->

`SK` + four digits, allocated once and never re-purposed (ADR-012). 74 ids are allocated.

## Async

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK3001](SK3001.md) | `async void` outside an event handler | none | review | no |
| [SK3002](SK3002.md) | Blocking on an async call | warning | review | no |
| [SK3004](SK3004.md) | A `CancellationToken` is accepted and not passed on | warning | review | no |
| [SK3005](SK3005.md) | A task is discarded in synchronous code | warning | — | no |
| [SK3007](SK3007.md) | A `Task` that uses a `using` resource is returned instead of awaited | warning | review | no |

## Correctness

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK2007](SK2007.md) | The collection being enumerated is modified inside the loop | warning | review | no |
| [SK2009](SK2009.md) | An enum switch omits declared members | warning | — | no |
| [SK2013](SK2013.md) | An exception is constructed and then discarded | warning | safe | no |
| [SK2014](SK2014.md) | An empty catch silently discards an exception | warning | — | yes |
| [SK2015](SK2015.md) | `throw ex;` resets the stack trace | warning | safe | yes |
| [SK2016](SK2016.md) | A logger message is interpolated before it is logged | suggestion | — | no |

## Design

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK6003](SK6003.md) | An abstract type has a public constructor | suggestion | safe | yes |
| [SK6008](SK6008.md) | An extension method extends `object` | suggestion | — | no |

## Formatting

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK0001](SK0001.md) | The file is not formatted | suggestion | safe | yes |
| [SK0002](SK0002.md) | The line is over the width and nothing in it can break | hint | — | yes |
| [SK0003](SK0003.md) | The documentation comment is not well-formed XML | hint | — | yes |
| [SK0201](SK0201.md) | The body style is not arranged | suggestion | — | yes |
| [SK0202](SK0202.md) | The local variable type is not arranged | suggestion | — | no |
| [SK0203](SK0203.md) | The object creation is not arranged | suggestion | — | no |
| [SK0204](SK0204.md) | The default value expression is not arranged | suggestion | — | no |
| [SK0205](SK0205.md) | The null check is not arranged | suggestion | — | no |
| [SK0206](SK0206.md) | The empty string expression is not arranged | suggestion | — | no |
| [SK0207](SK0207.md) | The instance-member qualifier is not arranged | suggestion | — | no |
| [SK0208](SK0208.md) | The control-statement braces are not arranged | suggestion | — | yes |
| [SK0209](SK0209.md) | The expression parentheses are not arranged | suggestion | — | yes |
| [SK0210](SK0210.md) | The using directives are not arranged | suggestion | — | yes |
| [SK0211](SK0211.md) | The predefined type spelling is not arranged | suggestion | — | no |
| [SK0212](SK0212.md) | The accessibility modifier is not arranged | suggestion | — | yes |
| [SK0213](SK0213.md) | The namespace declaration is not arranged | suggestion | — | yes |
| [SK0214](SK0214.md) | The trailing comma is not arranged | suggestion | — | yes |
| [SK0215](SK0215.md) | The static-member qualifier is not arranged | suggestion | — | no |
| [SK0216](SK0216.md) | The argument naming style is not arranged | suggestion | — | no |
| [SK0217](SK0217.md) | The discard declaration is not arranged | suggestion | — | yes |

## Lifetime

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK3501](SK3501.md) | A disposable is created in a local and never disposed | warning | safe | no |
| [SK3502](SK3502.md) | A type owns a disposable field but is not disposable | warning | — | no |
| [SK3503](SK3503.md) | An `IAsyncDisposable` is disposed synchronously | warning | review | no |

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
| [SK7040](SK7040.md) | TODO or FIXME has no issue reference | suggestion | — | yes |
| [SK7050](SK7050.md) | A warning-disable pragma has no justification | warning | — | yes |
| [SK7051](SK7051.md) | A suppression attribute has no justification | warning | — | no |

## Modernization

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK1001](SK1001.md) | Use a collection expression | suggestion | safe | no |
| [SK1005](SK1005.md) | Use a file-scoped namespace | suggestion | safe | yes |
| [SK1006](SK1006.md) | Use a `using` declaration | suggestion | safe | yes |
| [SK1010](SK1010.md) | Use `is null` / `is not null` instead of `==` / `!=` | suggestion | safe | no |
| [SK1015](SK1015.md) | Use `is T t` instead of `is T` and a cast | suggestion | safe | no |
| [SK1020](SK1020.md) | Use `ArgumentNullException.ThrowIfNull` | suggestion | safe | no |
| [SK1030](SK1030.md) | Use `??=` | suggestion | safe | yes |
| [SK1031](SK1031.md) | Use a null-conditional assignment | suggestion | safe | no |
| [SK1033](SK1033.md) | Use `TryGetValue` or `TryAdd` instead of `ContainsKey` and a second lookup | suggestion | safe | no |
| [SK1034](SK1034.md) | Use the `Count` property, not `Count()` or `Any()` | suggestion | safe | no |
| [SK1035](SK1035.md) | Use `Enum.GetValues<T>()` | suggestion | safe | no |

## Performance

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK4010](SK4010.md) | A `Where` the next operator could have taken as its predicate | suggestion | safe | no |

## Security

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK5001](SK5001.md) | Request data is concatenated into SQL | error | — | no |
| [SK5002](SK5002.md) | Request data reaches a process start | error | — | no |
| [SK5005](SK5005.md) | A broken cipher, or a mode that leaks structure | error | — | no |
| [SK5007](SK5007.md) | A certificate callback that accepts everything | error | — | no |
| [SK5009](SK5009.md) | An XML reader that parses a DTD and fetches what it names | error | — | no |

## Tests

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK8005](SK8005.md) | `Thread.Sleep` in a test | suggestion | — | no |
| [SK8006](SK8006.md) | A skipped test has no reason | warning | — | no |

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


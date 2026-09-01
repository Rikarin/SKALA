# Rules

<!-- Generated from Rules/Rikarin.Skala.Rules.Metadata/rules.json. Do not edit. -->

`SK` + four digits, allocated once and never re-purposed (ADR-012). 136 ids are allocated.

## Async

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK3001](SK3001.md) | `async void` outside an event handler | none | review | no |
| [SK3002](SK3002.md) | Blocking on an async call | warning | review | no |
| [SK3003](SK3003.md) | Configure awaited tasks in library code | warning | — | no |
| [SK3004](SK3004.md) | A `CancellationToken` is accepted and not passed on | warning | review | no |
| [SK3005](SK3005.md) | A task is discarded in synchronous code | warning | — | no |
| [SK3007](SK3007.md) | A `Task` that uses a `using` resource is returned instead of awaited | warning | review | no |
| [SK3009](SK3009.md) | Review explicitly unsynchronized Lazy in static state | warning | — | no |
| [SK3020](SK3020.md) | The non-`async` `Task` method returns null | warning | review | no |
| [SK3021](SK3021.md) | A `SpinLock` is stored in a `readonly` field | warning | review | no |

## Correctness

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK2001](SK2001.md) | Comparison is fixed by the integral type's range | warning | — | no |
| [SK2002](SK2002.md) | A pure method's result is discarded | warning | — | no |
| [SK2003](SK2003.md) | Review exact equality of floating-point arithmetic | warning | — | no |
| [SK2004](SK2004.md) | Typed equality has no matching object equality | warning | — | no |
| [SK2005](SK2005.md) | Do not mutate a copy of a readonly struct field | warning | — | no |
| [SK2007](SK2007.md) | The collection being enumerated is modified inside the loop | warning | review | no |
| [SK2008](SK2008.md) | A stored delegate captures a changing loop variable | warning | — | no |
| [SK2009](SK2009.md) | An enum switch omits declared members | warning | — | no |
| [SK2010](SK2010.md) | A string comparison has an implicit culture policy | warning | — | no |
| [SK2011](SK2011.md) | A struct uses inherited ValueType.Equals | warning | — | no |
| [SK2012](SK2012.md) | Review a self-operation on an automatic property | warning | — | no |
| [SK2013](SK2013.md) | An exception is constructed and then discarded | warning | safe | no |
| [SK2014](SK2014.md) | An empty catch silently discards an exception | warning | — | yes |
| [SK2015](SK2015.md) | `throw ex;` resets the stack trace | warning | safe | yes |
| [SK2016](SK2016.md) | A logger message is interpolated before it is logged | suggestion | — | no |
| [SK2017](SK2017.md) | The exception names a parameter that does not exist | warning | safe | no |
| [SK2030](SK2030.md) | Detect NaN with IsNaN rather than equality | warning | safe | no |
| [SK2031](SK2031.md) | Do not discard a setter's value parameter | warning | — | yes |
| [SK2032](SK2032.md) | Remove GC.SuppressFinalize from a type with no finalizer | suggestion | safe | no |
| [SK2033](SK2033.md) | Do not stackalloc inside a loop | warning | — | yes |
| [SK2034](SK2034.md) | Do not name a declaration after a reserved keyword | suggestion | — | yes |

## Design

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK6003](SK6003.md) | An abstract type has a public constructor | suggestion | safe | yes |
| [SK6008](SK6008.md) | An extension method extends `object` | suggestion | — | no |
| [SK6020](SK6020.md) | An `Enum` constraint has no `struct` beside it | warning | review | no |
| [SK6021](SK6021.md) | A type is named like an exception and is not one | warning | — | no |
| [SK6022](SK6022.md) | A type name repeats the keyword the declaration already carries | suggestion | — | yes |
| [SK6023](SK6023.md) | A type has no members, no base and no attributes | suggestion | — | yes |
| [SK6040](SK6040.md) | An `out` variable is declared and never read | warning | safe | no |
| [SK6041](SK6041.md) | A `foreach` variable is declared wider than the element it receives | suggestion | review | no |

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
| [SK3510](SK3510.md) | A variable already owned by `using` is disposed again | warning | safe | no |
| [SK3511](SK3511.md) | The `using` resource is built with an object initializer | warning | safe | no |
| [SK3512](SK3512.md) | A variable captured by `using` is returned | warning | — | no |

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
| [SK7030](SK7030.md) | File length exceeds the configured threshold | hint | — | yes |
| [SK7040](SK7040.md) | TODO or FIXME has no issue reference | suggestion | — | yes |
| [SK7050](SK7050.md) | A warning-disable pragma has no justification | warning | — | yes |
| [SK7051](SK7051.md) | A suppression attribute has no justification | warning | — | no |
| [SK7060](SK7060.md) | Review commented-out statements | hint | — | yes |
| [SK7070](SK7070.md) | An obsolete marker has no message | warning | — | no |
| [SK7071](SK7071.md) | A coverage exclusion has no justification | warning | — | no |
| [SK7072](SK7072.md) | A warning suppression covers nothing | warning | safe | yes |
| [SK7073](SK7073.md) | A region is empty | suggestion | safe | yes |
| [SK7074](SK7074.md) | Review a goto to a label | warning | — | yes |

## Modernization

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK1001](SK1001.md) | Use a collection expression | suggestion | safe | no |
| [SK1003](SK1003.md) | Use a field-backed property | suggestion | safe | no |
| [SK1005](SK1005.md) | Use a file-scoped namespace | suggestion | safe | yes |
| [SK1006](SK1006.md) | Use a `using` declaration | suggestion | safe | yes |
| [SK1010](SK1010.md) | Use `is null` / `is not null` instead of `==` / `!=` | suggestion | safe | no |
| [SK1011](SK1011.md) | Use a property pattern | suggestion | safe | no |
| [SK1012](SK1012.md) | Use a switch expression for a returning equality chain | suggestion | safe | no |
| [SK1013](SK1013.md) | Use a list pattern for guarded element checks | suggestion | safe | no |
| [SK1014](SK1014.md) | Use relational and logical patterns | suggestion | safe | no |
| [SK1015](SK1015.md) | Use `is T t` instead of `is T` and a cast | suggestion | safe | no |
| [SK1020](SK1020.md) | Use `ArgumentNullException.ThrowIfNull` | suggestion | safe | no |
| [SK1022](SK1022.md) | Precompute a constant character search set | hint | safe | no |
| [SK1023](SK1023.md) | Use a dedicated System.Threading.Lock | suggestion | safe | no |
| [SK1025](SK1025.md) | Freeze a private lookup-only dictionary | hint | safe | no |
| [SK1026](SK1026.md) | Use a UTF-8 literal for constant ASCII bytes | suggestion | safe | no |
| [SK1028](SK1028.md) | Decode a byte span without an array copy | suggestion | safe | no |
| [SK1030](SK1030.md) | Use `??=` | suggestion | safe | yes |
| [SK1031](SK1031.md) | Use a null-conditional assignment | suggestion | safe | no |
| [SK1033](SK1033.md) | Use `TryGetValue` or `TryAdd` instead of `ContainsKey` and a second lookup | suggestion | safe | no |
| [SK1034](SK1034.md) | Use the `Count` property, not `Count()` or `Any()` | suggestion | safe | no |
| [SK1035](SK1035.md) | Use `Enum.GetValues<T>()` | suggestion | safe | no |
| [SK1040](SK1040.md) | Use `T?` instead of `Nullable<T>` | suggestion | safe | no |
| [SK1041](SK1041.md) | Use a compound assignment | suggestion | safe | yes |
| [SK1042](SK1042.md) | The nested `if` statements can be combined | suggestion | safe | yes |
| [SK1043](SK1043.md) | The `for` loop is a `while` | suggestion | safe | yes |
| [SK1044](SK1044.md) | Use `string.IsNullOrEmpty` | suggestion | safe | no |

## Performance

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK4001](SK4001.md) | Review LINQ in a configured hot path | none | — | no |
| [SK4002](SK4002.md) | Review iteration-local delegate captures | hint | — | no |
| [SK4003](SK4003.md) | Review a temporary params array with a span overload | hint | — | no |
| [SK4004](SK4004.md) | Review boxing despite an existing generic constraint | hint | — | no |
| [SK4006](SK4006.md) | Review a materialization used only by foreach | hint | — | no |
| [SK4007](SK4007.md) | Review large struct arguments in loops | hint | — | no |
| [SK4010](SK4010.md) | A `Where` the next operator could have taken as its predicate | suggestion | safe | no |
| [SK4020](SK4020.md) | The lambda captures nothing and is not `static` | suggestion | safe | no |
| [SK4021](SK4021.md) | The private method does not use instance state | hint | safe | no |
| [SK4022](SK4022.md) | The struct is never mutated and is not `readonly` | suggestion | safe | no |
| [SK4023](SK4023.md) | The capacity argument matches the default | warning | safe | no |
| [SK4024](SK4024.md) | `GC.Collect` is called from application code | warning | — | no |

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
| [SK8007](SK8007.md) | Use controlled assertion input | suggestion | — | no |

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


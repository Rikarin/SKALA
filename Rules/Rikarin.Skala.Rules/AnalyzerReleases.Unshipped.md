; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------------
SK1005  | Skala.Modernization | Info | Use a file-scoped namespace.
SK1010  | Skala.Modernization | Info | Use `is null` / `is not null` instead of `==` / `!=`.
SK1020  | Skala.Modernization | Info | Use `ArgumentNullException.ThrowIfNull`.
SK1030  | Skala.Modernization | Info | Use `??=`.
SK1034  | Skala.Modernization | Info | Use the `Count` property, not `Count()` or `Any()`.
SK1035  | Skala.Modernization | Info | Use `Enum.GetValues<T>()`.
SK2013  | Skala.Correctness | Warning | An exception is constructed and then discarded.
SK2015  | Skala.Correctness | Warning | `throw ex;` resets the stack trace.
SK3001  | Skala.Async | Info | `async void` outside an event handler. Ships disabled; see rules.json.
SK3002  | Skala.Async | Warning | Blocking on an async call.
SK4010  | Skala.Performance | Info | A `Where` the next operator could have taken as its predicate.
SK6003  | Skala.Design | Info | An abstract type has a public constructor.
SK7001  | Skala.Maintainability | Hidden | Cyclomatic complexity over the threshold.
SK7002  | Skala.Maintainability | Info | Cognitive complexity over the threshold.
SK7003  | Skala.Maintainability | Hidden | Statement count over the threshold.
SK7004  | Skala.Maintainability | Hidden | Member count over the threshold.
SK7005  | Skala.Maintainability | Hidden | Parameter count over the threshold.
SK7006  | Skala.Maintainability | Hidden | Nesting depth over the threshold.
SK7010  | Skala.Maintainability | Disabled | Public API with no documentation comment.
SK8005  | Skala.Tests | Info | `Thread.Sleep` in a test.
SK5001  | Skala.Security | Error | Request data is concatenated into SQL.
SK5002  | Skala.Security | Error | Request data reaches a process start.
SK5005  | Skala.Security | Error | A broken cipher (`DES`, `TripleDES`, `RC2`) or ECB mode.
SK5007  | Skala.Security | Error | A certificate callback that accepts everything.
SK5009  | Skala.Security | Error | An XML reader that parses a DTD and resolves what it names.
SK1001  | Skala.Modernization | Info | Use a collection expression where the type is already written.
SK1006  | Skala.Modernization | Info | Use a `using` declaration where the block runs to the end of the scope.
SK1015  | Skala.Modernization | Info | Use `is T t` instead of `is T` and a cast.
SK1031  | Skala.Modernization | Info | Use a null-conditional assignment.
SK1033  | Skala.Modernization | Info | Use `TryGetValue` / `TryAdd` instead of `ContainsKey` and a second lookup.
SK2007  | Skala.Correctness | Warning | The collection being enumerated is modified inside the loop.
SK2002  | Skala.Correctness | Warning | A pure method's result is discarded.
SK2004  | Skala.Correctness | Warning | Typed equality has no matching object equality.
SK2008  | Skala.Correctness | Warning | A stored delegate captures a changing loop variable.
SK2010  | Skala.Correctness | Warning | A string comparison has an implicit culture policy.
SK2011  | Skala.Correctness | Warning | A struct uses inherited ValueType.Equals.
SK2009  | Skala.Correctness | Warning | A non-flags enum switch omits declared values and has no catch-all.
SK2014  | Skala.Correctness | Warning | An empty catch silently discards an exception.
SK2016  | Skala.Correctness | Info | A logger message is interpolated before it is logged.
SK3004  | Skala.Async | Warning | A `CancellationToken` is accepted and not passed on.
SK3005  | Skala.Async | Warning | A task is discarded in synchronous code.
SK3007  | Skala.Async | Warning | A `Task` built from a `using` resource is returned instead of awaited.
SK3501  | Skala.Lifetime | Warning | A disposable is created in a local and never disposed.
SK3502  | Skala.Lifetime | Warning | A type owns a disposable field but is not disposable.
SK3503  | Skala.Lifetime | Warning | An `IAsyncDisposable` is disposed synchronously.
SK6008  | Skala.Design | Info | An extension method extends `object`.
SK7040  | Skala.Maintainability | Info | A TODO or FIXME has no issue reference.
SK7050  | Skala.Maintainability | Warning | A warning-disable pragma has no justification.
SK7051  | Skala.Maintainability | Warning | A suppression attribute has no justification.
SK8006  | Skala.Tests | Warning | A skipped xUnit test has no reason.
SK1011 | Skala.Modernization | Info | Use a property pattern.
SK1014 | Skala.Modernization | Info | Use relational and logical patterns.
SK1028 | Skala.Modernization | Info | Decode a byte span without an array copy.
SK3003 | Skala.Async | Warning | Configure awaited tasks in library code.
SK7030 | Skala.Maintainability | Hidden | File length exceeds the configured threshold.
SK1012 | Skala.Modernization | Info | Use a switch expression for a returning equality chain.
SK1013 | Skala.Modernization | Info | Use a list pattern for guarded element checks.
SK1026 | Skala.Modernization | Info | Use a UTF-8 literal for constant ASCII bytes.
SK2001 | Skala.Correctness | Warning | Comparison is fixed by the integral type's range.
SK2012 | Skala.Correctness | Warning | Review a self-operation on an automatic property.
SK3009 | Skala.Async | Warning | Review explicitly unsynchronized Lazy in static state.
SK4001 | Skala.Performance | Disabled | Review LINQ in a configured hot path.
SK4002 | Skala.Performance | Hidden | Review iteration-local delegate captures.
SK4006 | Skala.Performance | Hidden | Review a materialization used only by foreach.
SK8007 | Skala.Tests | Info | Use controlled assertion input.
SK1023 | Skala.Modernization | Info | Use a dedicated System.Threading.Lock.
SK2003 | Skala.Correctness | Warning | Review exact equality of floating-point arithmetic.
SK4004 | Skala.Performance | Hidden | Review boxing despite an existing generic constraint.
SK4007 | Skala.Performance | Hidden | Review large struct arguments in loops.
SK7060 | Skala.Maintainability | Hidden | Review commented-out statements.
SK1003 | Skala.Modernization | Info | Use a field-backed property.
SK1022 | Skala.Modernization | Hidden | Precompute a constant character search set.
SK1025 | Skala.Modernization | Hidden | Freeze a private lookup-only dictionary.
SK2005 | Skala.Correctness | Warning | Do not mutate a copy of a readonly struct field.
SK4003 | Skala.Performance | Hidden | Review a temporary params array with a span overload.
SK2017 | Skala.Correctness | Warning | An exception's `paramName` names no parameter in scope.
SK7070 | Skala.Maintainability | Warning | An obsolete marker has no message.
SK7071 | Skala.Maintainability | Warning | A coverage exclusion has no justification.
SK7072 | Skala.Maintainability | Warning | A warning suppression covers nothing.
SK7073 | Skala.Maintainability | Info | A region is empty.
SK7074 | Skala.Maintainability | Warning | Review a goto to a label.
SK2030 | Skala.Correctness | Warning | Detect NaN with IsNaN rather than equality.
SK2031 | Skala.Correctness | Warning | Do not discard a setter's value parameter.
SK2032 | Skala.Correctness | Info | Remove GC.SuppressFinalize from a type with no finalizer.
SK2033 | Skala.Correctness | Warning | Do not stackalloc inside a loop.
SK2034 | Skala.Correctness | Info | Do not name a declaration after a reserved keyword.
SK6020 | Skala.Design | Warning | An `Enum` constraint has no `struct` beside it.
SK6021 | Skala.Design | Warning | A type is named like an exception and is not one.
SK6022 | Skala.Design | Info | A type name repeats the keyword the declaration already carries.
SK6023 | Skala.Design | Info | A type has no members, no base and no attributes.
SK1040 | Skala.Modernization | Info | Use `T?` instead of `Nullable<T>`.
SK1041 | Skala.Modernization | Info | Use a compound assignment.
SK1042 | Skala.Modernization | Info | The nested `if` statements can be combined.
SK1043 | Skala.Modernization | Info | The `for` loop is a `while`.
SK1044 | Skala.Modernization | Info | Use `string.IsNullOrEmpty`.
SK3510 | Skala.Lifetime | Warning | A variable already owned by `using` is disposed again.
SK3511 | Skala.Lifetime | Warning | The `using` resource is built with an object initializer.
SK3512 | Skala.Lifetime | Warning | A variable captured by `using` is returned.
SK3020 | Skala.Async | Warning | The non-`async` `Task` method returns null.
SK3021 | Skala.Async | Warning | A `SpinLock` is stored in a `readonly` field.
SK7080 | Skala.Maintainability | Hidden | The inheritance chain is deeper than the threshold.
SK7081 | Skala.Maintainability | Hidden | The type depends on more other types than the threshold.
SK7082 | Skala.Maintainability | Info | The conditional expressions are nested.

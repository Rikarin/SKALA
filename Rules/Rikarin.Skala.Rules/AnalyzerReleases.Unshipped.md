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
SK6040 | Skala.Design | Warning | An `out` variable is declared and never read.
SK6041 | Skala.Design | Info | A `foreach` variable is declared wider than the element it receives.
SK4020 | Skala.Performance | Info | The lambda captures nothing and is not `static`.
SK4021 | Skala.Performance | Hidden | The private method does not use instance state.
SK4022 | Skala.Performance | Info | The struct is never mutated and is not `readonly`.
SK4023 | Skala.Performance | Warning | The capacity argument matches the default.
SK4024 | Skala.Performance | Warning | `GC.Collect` is called from application code.
SK7090 | Skala.Maintainability | Warning | A thrown `NotImplementedException` has no issue reference.
SK7091 | Skala.Maintainability | Warning | The process is terminated outside the entry point.
SK7092 | Skala.Maintainability | Warning | The exception is both logged and rethrown.
SK7093 | Skala.Maintainability | Warning | The console is written to where a logger was meant.
SK4030 | Skala.Performance | Info | Use the collection's own `Find`/`Exists`/`TrueForAll`/`Contains`.
SK4031 | Skala.Performance | Warning | The loop looks up a key it is already holding.
SK4032 | Skala.Performance | Info | Pass the start index instead of calling `Substring`.
SK4033 | Skala.Performance | Warning | Take the cheap `ConcurrentDictionary` member.
SK4034 | Skala.Performance | Info | Filter before sorting.
SK6030 | Skala.Design | Warning | A type is declared in the global namespace.
SK6031 | Skala.Design | Warning | `readonly` does not protect a mutable field's contents.
SK6032 | Skala.Design | Info | An abstract type declares nothing to implement.
SK6033 | Skala.Design | Warning | A type has only private constructors and is not static.
SK6034 | Skala.Design | Info | A public constant is baked into every caller.
SK3530 | Skala.Lifetime | Warning | The disposable field is not disposed by `Dispose`.
SK3531 | Skala.Lifetime | Warning | The `DisposeAsync` override never calls the base implementation.
SK3532 | Skala.Lifetime | Warning | The `ref struct` owns a disposable and cannot declare it.
SK3030 | Skala.Async | Warning | The async iterator is enumerated without `await foreach`.
SK3031 | Skala.Async | Info | The method is `async` only to return an awaited task.
SK0230 | Skala.Cleanup | Warning | An initializer or `with` expression is empty.
SK0231 | Skala.Cleanup | Warning | A call on a string returns the string it was given.
SK0232 | Skala.Cleanup | Warning | An argument or signature element restates the declaration.
SK0233 | Skala.Cleanup | Info | Nine token-level redundant syntax deletions.
SK0234 | Skala.Cleanup | Warning | A conversion that converts nothing.
SK8020 | Skala.Tests | Warning | A class with `[TestMethod]` members carries no `[TestClass]`.
SK8021 | Skala.Tests | Warning | The test class declares no test.
SK8022 | Skala.Tests | Warning | The assertion's expected and actual arguments are swapped.
SK7100 | Skala.Maintainability | Info | The documentation duplicates the base member's.
SK7101 | Skala.Maintainability | Disabled | A non-public member has no documentation comment.
SK0240 | Skala.Cleanup | Warning | The control flow does nothing.
SK0241 | Skala.Cleanup | Warning | The modifier has no effect.
SK0242 | Skala.Cleanup | Warning | The `#nullable` directive changes nothing.
SK0243 | Skala.Cleanup | Warning | The qualifier is redundant.
SK0244 | Skala.Cleanup | Warning | The declaration adds nothing.
SK1050 | Skala.Modernization | Info | Use pattern matching instead of a test-and-cast.
SK1051 | Skala.Modernization | Info | Simplify the pattern.
SK1052 | Skala.Modernization | Info | Merge the `?:` into a conditional access.
SK1053 | Skala.Modernization | Info | Use a discard.
SK1054 | Skala.Modernization | Info | Inline the `out` variable declaration.
SK3040  | Skala.Async | Warning | The `lock` is taken over a synchronization primitive.
SK3041  | Skala.Async | Warning | The compound operation on a `volatile` field is not atomic.
SK3042  | Skala.Async | Warning | The double-checked locking is not correct.
SK3043  | Skala.Async | Warning | Locks are taken in inconsistent orders.
SK3044  | Skala.Async | Warning | The field is guarded on some paths and not others.
SK7080 | Skala.Maintainability | Hidden | The inheritance chain is deeper than the threshold.
SK7081 | Skala.Maintainability | Hidden | The type depends on more other types than the threshold.
SK7082 | Skala.Maintainability | Info | The conditional expressions are nested.
SK7083 | Skala.Maintainability | Hidden | The string literal is repeated.
SK6050 | Skala.Design | Info | The method ignores its inputs and returns a constant.
SK6051 | Skala.Design | Info | The base type tests `this` against a derived type.

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
SK2007  | Skala.Correctness | Warning | The collection being enumerated is modified inside the loop.
SK3004  | Skala.Async | Warning | A `CancellationToken` is accepted and not passed on.
SK3007  | Skala.Async | Warning | A `Task` built from a `using` resource is returned instead of awaited.
SK3501  | Skala.Lifetime | Warning | A disposable is created in a local and never disposed.
SK3503  | Skala.Lifetime | Warning | An `IAsyncDisposable` is disposed synchronously.

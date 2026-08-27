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

; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------------
SKG001  | Skala.Options | Error | The option registry is missing.
SKG002  | Skala.Options | Error | The option registry could not be read.
SKG003  | Skala.Options | Error | An option's default is not one of its values.
SK9004  | Skala.Options | Error | Duplicate option alias. docs/plan/02-repository-layout.md § "Naming".

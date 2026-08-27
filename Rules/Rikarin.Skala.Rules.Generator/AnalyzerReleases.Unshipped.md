; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------------
SKR001  | Skala.Rules | Error | The rule catalogue is missing.
SKR002  | Skala.Rules | Error | The rule catalogue could not be read.
SKR003  | Skala.Rules | Error | A rule id is not in the SK#### shape.
SKR004  | Skala.Rules | Error | A rule id is allocated twice. ADR-012.
SKR010  | Skala.Rules | Error | The taint table is missing.
SKR011  | Skala.Rules | Error | The taint table could not be read.
SKR012  | Skala.Rules | Error | A taint sink does not name a rule id.

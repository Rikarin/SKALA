// The analyzer profile targets netstandard2.0 (docs/plan/01 § ADR-006): analyzers load into the
// compiler and the IDE, both of which may be older than the tool. The language version is current,
// so the few attributes C# 14 expects from the runtime are declared here instead.

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;

// The analyzer profile targets netstandard2.0 (docs/plan/01 § ADR-006): analyzers load into the
// compiler and the IDE, both of which may be older than the tool. The language version is current,
// so the few attributes C# 14 expects from the runtime are declared here instead.
//
// ⚠ The same file exists in Rikarin.Skala.Rules.Metadata and cannot be shared: the type has to be
// *accessible*, and an `internal` shim in another assembly is not. Two copies of a marker type with
// no members is the cheapest of the available wrongs.

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;

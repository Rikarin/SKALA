using System;
using System.Collections.Immutable;

public static class Reporting {
    // ⚠ `StringComparer` implements `IEqualityComparer<string?>`, so inference reaches `T = string?`
    // and the builder becomes `ImmutableHashSet<string?>.Builder`. Deleting `<string>` is a CS8619 on
    // the return. `SymbolEqualityComparer.Default` compares the two constructed methods equal, which
    // is what shipped four of these into Skala's own Analysis/ (#320).
    public static ImmutableHashSet<string> Ordinal(string value) {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        builder.Add(value);
        return builder.ToImmutable();
    }
}

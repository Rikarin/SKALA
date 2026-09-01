using System; class C { void Use(params int[] x) { } void Use(ReadOnlySpan<int> x) { } void M() => Use(new[] {1}); }

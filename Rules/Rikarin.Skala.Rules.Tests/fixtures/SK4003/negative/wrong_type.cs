using System; class C { static void Use(params int[] x) { } static void Use(ReadOnlySpan<long> x) { } void M() => Use(new[] {1,2}); }

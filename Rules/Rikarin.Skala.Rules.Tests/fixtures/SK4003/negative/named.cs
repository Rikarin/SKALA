using System; class C { static void Use(params int[] x) { } static void Use(ReadOnlySpan<int> x) { } void M() => Use(x: new[] {1,2}); }

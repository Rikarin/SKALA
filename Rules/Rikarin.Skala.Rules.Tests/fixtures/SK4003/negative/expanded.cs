using System; class C { static void Use(params int[] x) { } static void Use(ReadOnlySpan<int> x) { } void M() => Use(1,2); }

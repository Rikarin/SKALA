using System; class C { static void Use(int[] x) { } static void Use(ReadOnlySpan<int> x) { } void M() => Use(new[] {1,2}); }

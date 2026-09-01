using System; class C { static void Use(int count, params int[] x) { } static void Use(long count, ReadOnlySpan<int> x) { } void M() => Use(2,new[] {1,2}); }

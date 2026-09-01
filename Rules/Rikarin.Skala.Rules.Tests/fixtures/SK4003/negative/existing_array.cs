using System; class C { static void Use(params int[] x) { } static void Use(ReadOnlySpan<int> x) { } void M(int[] values) => Use(values); }

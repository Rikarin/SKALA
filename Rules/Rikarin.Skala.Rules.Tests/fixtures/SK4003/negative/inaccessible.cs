using System; class Api { public static void Use(params int[] x) { } private static void Use(ReadOnlySpan<int> x) { } } class C { void M() => Api.Use(new[] {1,2}); }

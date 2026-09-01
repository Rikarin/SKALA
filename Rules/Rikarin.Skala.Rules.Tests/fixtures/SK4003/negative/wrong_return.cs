using System; class C { static int Use(params int[] x) => 0; static string Use(ReadOnlySpan<int> x) => ""; int M() => Use(new[] {1,2}); }

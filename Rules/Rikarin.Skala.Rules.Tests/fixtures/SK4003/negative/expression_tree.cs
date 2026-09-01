using System; using System.Linq.Expressions; class C { static void Use(params int[] x) { } static void Use(ReadOnlySpan<int> x) { } Expression<Action> M() => () => Use(new[] {1,2}); }

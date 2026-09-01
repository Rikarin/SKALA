using System; class C { static void Use(params string[] x) { } static void Use(ReadOnlySpan<string> x) { } void M() => Use(new[] {"a","b"}); }

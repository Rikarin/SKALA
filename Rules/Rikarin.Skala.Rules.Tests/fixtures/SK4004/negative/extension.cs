interface I { } static class Extensions { public static void M(this I value) { } } class C { void M<T>(T value) where T : struct, I { ((I)value).M(); } }

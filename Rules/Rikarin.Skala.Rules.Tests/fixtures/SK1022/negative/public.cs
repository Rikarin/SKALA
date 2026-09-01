using System; class C { public static readonly char[] chars = "aeiou".ToCharArray(); int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars); }

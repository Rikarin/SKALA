using System; class C { static int count = 1; static readonly char[] chars = "aeiou".ToCharArray(); int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars); }

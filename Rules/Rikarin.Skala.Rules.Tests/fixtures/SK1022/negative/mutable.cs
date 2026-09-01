using System; class C { static char[] chars = "aeiou".ToCharArray(); int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars); }

using System; class C { static readonly char[] chars = "aeiou".ToCharArray(); int M(ReadOnlySpan<char> text) { chars[0] = 'x'; return text.IndexOfAny(chars); } }

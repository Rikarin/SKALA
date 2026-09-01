using System; class C { static readonly char[] chars = "aeiou".ToCharArray(); int Size => chars.Length; int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars); }

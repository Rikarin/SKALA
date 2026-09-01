using System; class C { static readonly char[] chars = "aeiou".ToCharArray(); char[] Get() => chars; int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars); }

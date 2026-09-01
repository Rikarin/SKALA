using System; partial class C { static readonly char[] chars = "aeiou".ToCharArray(); int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars); }

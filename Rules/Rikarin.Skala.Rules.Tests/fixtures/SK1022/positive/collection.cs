using System; class C { static readonly char[] chars = ['a', 'e', 'i', 'o', 'u']; int M(ReadOnlySpan<char> text) => text.IndexOfAnyExcept(chars); }

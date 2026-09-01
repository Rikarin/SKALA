using System; class C { static readonly char[] chars = new[] { 'a', 'e', 'i', 'o', 'u' }; int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars); }

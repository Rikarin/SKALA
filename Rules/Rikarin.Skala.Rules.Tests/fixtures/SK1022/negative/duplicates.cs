using System; class C { static readonly char[] chars = new[] { 'a', 'a', 'e', 'e' }; int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars); }

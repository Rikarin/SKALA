using System; class C { static readonly char[] chars = new[] { 'a', 'e', 'i' }; int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars); }

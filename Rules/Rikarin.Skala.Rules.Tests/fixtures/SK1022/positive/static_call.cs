using System; class C { static readonly char[] chars = new char[] { 'a', 'e', 'i', 'o', 'u' }; bool M(ReadOnlySpan<char> text) => MemoryExtensions.ContainsAnyExcept(text, chars); }

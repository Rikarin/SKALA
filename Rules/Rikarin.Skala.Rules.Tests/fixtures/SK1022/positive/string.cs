using System; class C { static readonly char[] chars = "aeiou".ToCharArray(); bool M(ReadOnlySpan<char> text) => text.ContainsAny(chars); }

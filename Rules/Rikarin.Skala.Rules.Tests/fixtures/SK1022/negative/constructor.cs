using System; class C { static readonly char[] chars = "aeiou".ToCharArray(); static C() { } int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars); }

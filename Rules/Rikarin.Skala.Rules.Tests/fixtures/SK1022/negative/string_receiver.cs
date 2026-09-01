class C { static readonly char[] chars = "aeiou".ToCharArray(); int M(string text) => text.IndexOfAny(chars); }

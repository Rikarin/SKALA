using System; class C { static readonly char[] chars = Environment.NewLine.ToCharArray(); int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars); }

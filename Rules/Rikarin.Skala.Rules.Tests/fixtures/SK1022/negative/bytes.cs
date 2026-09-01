using System; class C { static readonly byte[] bytes = [1,2,3,4,5]; int M(ReadOnlySpan<byte> text) => text.IndexOfAny(bytes); }

using System; using System.Text; class C { string M(Encoding encoding, ReadOnlySpan<byte> bytes) => encoding.GetString(bytes.ToArray()); }

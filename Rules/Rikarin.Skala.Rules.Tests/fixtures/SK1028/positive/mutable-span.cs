using System; using System.Text; class C { string M(Span<byte> bytes) => Encoding.UTF8.GetString(bytes.ToArray()); }

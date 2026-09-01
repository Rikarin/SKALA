using System; using System.Text; class C { string M(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes); }

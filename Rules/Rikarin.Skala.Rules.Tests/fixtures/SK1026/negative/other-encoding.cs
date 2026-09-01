using System; using System.Text; class C { void Consume(ReadOnlySpan<byte> data) { } void M(Encoding encoding) => Consume(encoding.GetBytes("OK")); }

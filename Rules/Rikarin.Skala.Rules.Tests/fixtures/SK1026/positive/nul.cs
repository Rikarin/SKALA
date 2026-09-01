using System; using System.Text; class C { int Consume(ReadOnlySpan<byte> data) => data.Length; int M() => Consume(Encoding.UTF8.GetBytes("a\0b")); }

using System; using System.Text; class C { void Consume(ReadOnlySpan<byte> data) { } void M() => Consume(Encoding.UTF8.GetBytes("OK\r\n")); }

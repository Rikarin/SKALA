using System; using System.Text; class C { void Consume(Span<byte> data) { } void M() => Consume(Encoding.UTF8.GetBytes("OK")); }

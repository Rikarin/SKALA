using System; using System.Text; class C { void Consume(ReadOnlySpan<byte> data) { } void M(string text) => Consume(Encoding.UTF8.GetBytes(text)); }

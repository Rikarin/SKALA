using System.Text; class C { void Consume(byte[] data) { } void M() => Consume(Encoding.UTF8.GetBytes("OK")); }

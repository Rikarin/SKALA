using System; using System.Text; class C { const string Data = "ASCII"; int Consume(ReadOnlySpan<byte> data) => data.Length; int M() => Consume(Encoding.UTF8.GetBytes(Data)); }

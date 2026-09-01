using System; using System.Text; class C { string M(byte[] bytes, int start, int length) => Encoding.UTF8.GetString(bytes.AsSpan(start, length).ToArray()); }

using System.Linq; using System.Text; using System.Collections.Generic; class C { string M(IEnumerable<byte> bytes) => Encoding.UTF8.GetString(bytes.ToArray()); }

using System; using System.Linq; using System.Collections.Generic; class C { void M(IEnumerable<int> values) { foreach (var value in values.ToList()) Console.WriteLine(value); } }

using System; using System.Collections.Generic; class C { void M(List<int> values) { foreach (var value in values.ToArray()) Console.WriteLine(value); } }

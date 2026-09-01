using System; using System.Linq; class C { void M(int[] values) { foreach (var value in values.Where(x => x > 0).ToArray()) Console.WriteLine(value); } }

using System; using System.Linq; class C { void M(int[] values) { foreach (var value in (Enumerable.ToArray(values))) Console.WriteLine(value); } }

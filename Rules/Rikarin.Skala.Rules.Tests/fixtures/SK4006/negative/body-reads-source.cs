using System; using System.Linq; class C { void M(int[] values) { foreach (var value in values.ToArray()) Console.WriteLine(values.Length); } }

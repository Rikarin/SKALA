using System; using System.Linq; class C { void M((int, int)[] values) { foreach (var (a, b) in values.ToArray()) Console.WriteLine(a + b); } }

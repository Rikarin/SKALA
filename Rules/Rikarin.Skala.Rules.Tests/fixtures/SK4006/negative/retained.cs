using System; using System.Linq; class C { void M(int[] values) { var snapshot = values.ToArray(); foreach (var value in snapshot) Console.WriteLine(value); } }

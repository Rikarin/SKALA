using System; class C { void M(int[] values) { foreach (var item in values) { Action Factory() { int value = 1; return () => Console.WriteLine(value); } _ = Factory(); } } }

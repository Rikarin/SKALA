using System; using System.Collections.Generic; class C { int value; void M(int[] values, List<Action> callbacks) { foreach (var item in values) callbacks.Add(() => Console.WriteLine(value)); } }

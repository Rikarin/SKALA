using System; using System.Collections.Generic; class C { void M(int[] values, List<Action> callbacks) { foreach (var value in values) callbacks.Add(() => Console.WriteLine(value)); } }

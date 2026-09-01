using System; using System.Collections.Generic; class C { void M(int n, List<Action> callbacks) { while (n > 0) { int copy = n--; callbacks.Add(() => Console.WriteLine(copy)); } } }

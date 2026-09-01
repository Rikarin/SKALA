using System; using System.Collections.Generic; class C { void M(int n, List<Action> callbacks) { int shared = 1; for (int i = 0; i < n; i++) callbacks.Add(() => Console.WriteLine(shared)); } }

using System; using System.Collections.Generic; class C { void M(List<Action> callbacks) { for (int i = 0; i < 3; i++) { int copy = i; callbacks.Add(() => Console.WriteLine(copy)); } } }

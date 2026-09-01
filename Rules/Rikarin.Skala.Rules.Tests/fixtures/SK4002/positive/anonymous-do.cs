using System; using System.Collections.Generic; class C { void M(int n, List<Action> callbacks) { do { int copy = n--; callbacks.Add(delegate { Console.WriteLine(copy); }); } while (n > 0); } }

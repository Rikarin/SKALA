using System; class C { void M(int[] values) { foreach (var item in values) { Func<Action> factory = () => { int value = 1; return () => Console.WriteLine(value); }; _ = factory(); } } }

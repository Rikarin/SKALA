struct Counter { public int Value; public readonly void Read() { System.Console.WriteLine(Value); } } class C { readonly Counter counter; void M() => counter.Read(); }

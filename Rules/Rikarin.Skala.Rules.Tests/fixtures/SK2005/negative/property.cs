struct Counter { public int Value; public void Increment() => Value++; } class C { Counter Counter => default; void M() => Counter.Increment(); }

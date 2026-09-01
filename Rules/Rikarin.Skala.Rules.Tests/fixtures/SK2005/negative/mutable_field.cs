struct Counter { public int Value; public void Increment() => Value++; } class C { Counter counter; void M() => counter.Increment(); }

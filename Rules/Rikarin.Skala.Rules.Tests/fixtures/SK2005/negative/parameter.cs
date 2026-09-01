struct Counter { public int Value; public void Increment() => Value++; } class C { void M(in Counter counter) => counter.Increment(); }

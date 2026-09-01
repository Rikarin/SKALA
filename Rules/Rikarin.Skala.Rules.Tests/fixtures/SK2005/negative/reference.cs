class Counter { public int Value; public void Increment() => Value++; } class C { readonly Counter counter = new(); void M() => counter.Increment(); }

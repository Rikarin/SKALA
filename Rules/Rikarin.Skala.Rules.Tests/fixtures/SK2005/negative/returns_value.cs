struct Counter { public int Value; public int Increment() => ++Value; } class C { readonly Counter counter; int M() => counter.Increment(); }

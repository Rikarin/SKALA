struct Counter { public int Value; public void Reset() { Value = 0; } } class C { readonly Counter counter; void M() => counter.Reset(); }

struct Counter { public int[] Values; public void Increment() => Values[0]++; } class C { readonly Counter counter; void M() => counter.Increment(); }

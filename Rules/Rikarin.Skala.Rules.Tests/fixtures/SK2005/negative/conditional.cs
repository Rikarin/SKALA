struct Counter { public int Value; public void Increment(bool yes) { if (yes) Value++; } } class C { readonly Counter counter; void M() => counter.Increment(true); }

struct Counter { public int Value; public void Increment() => Value++; } class C { readonly Counter counter; void M() { var copy = counter; copy.Increment(); } }

struct Counter { public static int Value; public void Increment() => Value++; } class C { readonly Counter counter; void M() => counter.Increment(); }

struct Counter { public int Value; [System.Diagnostics.Conditional("NEVER")] public void Increment() => Value++; }
class C { readonly Counter counter; void M() => counter.Increment(); }

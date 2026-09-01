struct Counter { public int Value; public void Increment() => Value++; }
class C { readonly Counter counter; public int Start { init { counter.Increment(); } } }

using System;
using System.Linq.Expressions;
struct Counter { public int Value; public void Increment() => Value++; }
class C { readonly Counter counter; Expression<Action> M() => () => counter.Increment(); }

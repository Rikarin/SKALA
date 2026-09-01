struct Counter { public int Value; public void Increment() => Value++; } class C { static readonly Counter counter; static C() { counter.Increment(); } }

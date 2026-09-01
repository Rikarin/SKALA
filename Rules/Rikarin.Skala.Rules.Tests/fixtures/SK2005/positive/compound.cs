struct Counter { public int Value; public void Add(int x) { this.Value += x; } } class C { static readonly Counter counter; void M() => counter.Add(3); }

struct Counter { public int Value; public void Step() { --Value; } } class C { readonly Counter counter; void M() => this.counter.Step(); }

class C { public readonly object gate = new(); void M() { lock (gate) { } } }

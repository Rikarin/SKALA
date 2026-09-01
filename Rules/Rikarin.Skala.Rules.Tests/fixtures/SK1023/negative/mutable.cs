class C { object gate = new(); void M() { lock (gate) { } } }

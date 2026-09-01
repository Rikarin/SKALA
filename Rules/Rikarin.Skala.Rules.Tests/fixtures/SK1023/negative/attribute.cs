class C { [System.NonSerialized] readonly object gate = new(); void M() { lock (gate) { } } }

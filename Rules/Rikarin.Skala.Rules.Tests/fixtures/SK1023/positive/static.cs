class C { static readonly object gate = new(); static void M() { lock (C.gate) { } } }

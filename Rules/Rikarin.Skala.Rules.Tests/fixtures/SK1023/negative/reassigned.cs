class C { readonly object gate = new(); public C() { gate = new(); } void M() { lock (gate) { } } }

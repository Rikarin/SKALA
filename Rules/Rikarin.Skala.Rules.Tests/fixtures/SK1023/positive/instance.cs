class C { readonly object gate = new object(); void M() { lock (gate) { Work(); } } void Work() { } }

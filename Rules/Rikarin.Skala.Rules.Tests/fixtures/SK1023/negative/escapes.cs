class C { readonly object gate = new(); object Gate => gate; void M() { lock (gate) { } } }

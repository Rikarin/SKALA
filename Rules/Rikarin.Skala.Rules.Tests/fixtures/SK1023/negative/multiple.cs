class C { readonly object a = new(), b = new(); void M() { lock (a) { lock (b) { } } } }

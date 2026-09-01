class C { readonly object gate = new(); void M() { lock ((object)gate) { } } }

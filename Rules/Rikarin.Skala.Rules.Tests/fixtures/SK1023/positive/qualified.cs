class C { readonly object gate = new(); void M() { lock ((this.gate)) { lock (gate) { } } } }

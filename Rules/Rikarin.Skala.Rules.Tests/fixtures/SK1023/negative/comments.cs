class C { readonly object gate = new object(/* retain */); void M() { lock (gate) { } } }

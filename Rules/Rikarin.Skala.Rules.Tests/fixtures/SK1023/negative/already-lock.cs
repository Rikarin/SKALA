class C { readonly System.Threading.Lock gate = new(); void M() { lock (gate) { } } }

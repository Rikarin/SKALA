partial class Outer { class C { readonly object gate = new(); void M() { lock (gate) { } } } }

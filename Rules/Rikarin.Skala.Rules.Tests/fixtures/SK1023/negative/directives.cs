class C { readonly object gate = new(); void M() { lock (gate) { } }
#if OTHER
object Gate => gate;
#endif
}

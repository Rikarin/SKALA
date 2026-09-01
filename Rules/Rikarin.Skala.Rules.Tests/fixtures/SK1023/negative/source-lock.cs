namespace System.Threading { public class Lock { } } class C { readonly object gate = new(); void M() { lock (gate) { } } }

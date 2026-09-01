using System.Threading; class C { readonly object gate = new(); void M() { lock (gate) { Monitor.Pulse(gate); } } }

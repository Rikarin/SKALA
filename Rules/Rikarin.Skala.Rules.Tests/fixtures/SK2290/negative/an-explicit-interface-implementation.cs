using System;

// ⚠ The one shape Roslyn reports as `Accessibility.Private` that is not private in the language.
// It is declined by a stated gate, and the gate is *not* load-bearing: every caller reaches it
// through the interface, so the invocation below binds to `ILookup.TryGet` and the implementation
// has zero visible call sites — which the zero-call-site guard refuses on its own.
interface ILookup {
    bool TryGet(string key, out int value);
}

class Store : ILookup {
    bool ILookup.TryGet(string key, out int value) {
        value = key.Length;
        return key.Length > 0;
    }

    public void Probe(string key) {
        if (((ILookup)this).TryGet(key, out _)) {
            Console.WriteLine("found");
        }
    }
}

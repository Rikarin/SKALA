using System;

// ⚠ `d.TryGet(key, out value)` on a `dynamic` receiver compiles — verified by probe, not assumed —
// and it is an IDynamicInvocationOperation rather than an IInvocationOperation, so the argument it
// passes is invisible to the call-site collection. The member name is recorded from the dynamic
// operation instead, which withdraws the finding the static call below would otherwise support.
class Store {
    bool TryGet(string key, out int value) {
        value = key.Length;
        return key.Length > 0;
    }

    public void Run(string key) {
        if (TryGet(key, out _)) {
            Console.WriteLine("static");
        }

        dynamic self = this;
        self.TryGet(key, out int value);
        Console.WriteLine(value);
    }
}

public sealed class Worker {
    int done;

    public void Run(object gate) {
        // A parameter is a local-looking name whose value came from the caller, so the object is
        // the caller's and is shared by construction. It binds to an `IParameterSymbol` and never
        // reaches shape 1's declarator test at all.
        lock (gate) {
            done++;
        }
    }
}

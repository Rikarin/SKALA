public sealed class Bootstrap {
    readonly object gate = new();

    bool initialized;

    int value;

    public int Value {
        get {
            if (!initialized) {
                lock (gate) {
                    if (!initialized) {
                        value = 42;
                        initialized = true;
                    }
                }
            }

            return value;
        }
    }
}

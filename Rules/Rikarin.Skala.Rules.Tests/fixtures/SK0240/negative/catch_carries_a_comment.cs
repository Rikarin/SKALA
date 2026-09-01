using System;

class C {
    public static void Save() {
        try {
            Run();
        } catch (InvalidOperationException) {
            // The stack trace is what the caller reads; do not wrap this one.
            throw;
        }
    }

    static void Run() { }
}

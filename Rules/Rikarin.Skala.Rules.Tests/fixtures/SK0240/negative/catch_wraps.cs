using System;

class C {
    public static void Save() {
        try {
            Run();
        } catch (InvalidOperationException error) {
            throw new InvalidOperationException("save failed", error);
        }
    }

    static void Run() { }
}

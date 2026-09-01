using System;

class C {
    public static void Save() {
        try {
            Run();
        } catch (InvalidOperationException error) {
            throw error;
        }
    }

    static void Run() { }
}

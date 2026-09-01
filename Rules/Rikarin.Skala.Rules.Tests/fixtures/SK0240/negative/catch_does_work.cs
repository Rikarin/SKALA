using System;

class C {
    static void Log(Exception error) { }

    public static void Save() {
        try {
            Run();
        } catch (InvalidOperationException error) {
            Log(error);
            throw;
        }
    }

    static void Run() { }
}

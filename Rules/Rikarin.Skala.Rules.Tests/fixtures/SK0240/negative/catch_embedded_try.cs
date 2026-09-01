using System;

class C {
    public static void Save(bool ready) {
        if (ready)
            try {
                Run();
                Flush();
            } catch (InvalidOperationException) {
                throw;
            }
    }

    static void Run() { }

    static void Flush() { }
}

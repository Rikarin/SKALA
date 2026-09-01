using System;
using System.IO;

class C {
    static void Log(Exception error) { }

    public static void Save() {
        try {
            Run();
        } catch (IOException error) {
            Log(error);
        } catch (InvalidOperationException) {
            throw;
        }
    }

    static void Run() { }
}

using System;
using System.IO;

class C {
    public static void Save() {
        try {
            Run();
        } catch (IOException error) when (error.HResult != 0) {
            throw;
        }
    }

    static void Run() { }
}

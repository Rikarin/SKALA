using System.Runtime.CompilerServices;

public static class Simple {
    public static void Log(string message, [CallerMemberName] string caller = "") { }

    public static void LogLocal() {
        void Inner(int level, [CallerMemberName] string caller = "") { }

        Inner(1);
    }
}

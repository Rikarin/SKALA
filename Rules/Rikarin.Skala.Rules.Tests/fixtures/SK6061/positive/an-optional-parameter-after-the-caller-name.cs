using System.Runtime.CompilerServices;

public static class Logging {
    public static void Log(string message, [CallerMemberName] string caller = "", int level = 0) { }
}

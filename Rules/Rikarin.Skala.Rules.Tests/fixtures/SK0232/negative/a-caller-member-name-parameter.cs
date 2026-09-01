using System.Runtime.CompilerServices;

public static class Tracing {
    static int Log(string message, [CallerMemberName] string? caller = null) =>
        message.Length + (caller?.Length ?? 0);

    // Omitting the argument lets the compiler substitute the caller's name, so the explicit null
    // is the only thing keeping the value null. It restates the default and is not redundant.
    public static int Anonymous(string message) => Log(message, null);
}

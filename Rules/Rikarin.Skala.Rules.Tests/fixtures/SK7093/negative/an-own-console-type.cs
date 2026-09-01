namespace Terminal;

public interface ILogger { }

// The type is resolved by the semantic model and never matched on the written name.
public static class Console {
    public static void WriteLine(string message) { }
}

public sealed class Shell {
    readonly ILogger logger = null!;

    public void Banner() => Console.WriteLine("ready");
}

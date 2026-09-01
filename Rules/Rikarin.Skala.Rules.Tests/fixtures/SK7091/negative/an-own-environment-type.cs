namespace Hosting;

// The type is resolved by the semantic model and never matched on the written name.
public static class Environment {
    public static void Exit(int code) { }
}

public sealed class Sandbox {
    public void Teardown() => Environment.Exit(0);
}

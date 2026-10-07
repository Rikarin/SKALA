// A static auto-property's getter reads its backing field and nothing else, so deleting the
// initializer that reads it is unobservable; the fix's result is pinned by Probe (#423).
public static class Defaults {
    public static int Retries { get; set; } = 5;
}

public sealed class Session {
    int retries = Defaults.Retries;

    public Session(int given) {
        retries = given;
    }

    public int Retries => retries;
}

public static class Probe {
    public static int Run() => new Session(2).Retries;
}

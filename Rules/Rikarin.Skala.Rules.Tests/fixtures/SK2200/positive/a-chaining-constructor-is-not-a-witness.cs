// Only constructors that run field initializers count, and a `this(…)` chain is not one of them.
// The delegating constructor is skipped rather than treated as a counterexample.
public sealed class Endpoint {
    readonly string host = "localhost";

    public Endpoint() : this("example.test") {
    }

    public Endpoint(string given) {
        host = given;
    }

    public string Host => host;
}

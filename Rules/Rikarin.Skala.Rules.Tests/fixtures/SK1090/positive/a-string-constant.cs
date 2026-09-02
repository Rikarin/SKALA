public sealed class Endpoint {
    public string Scheme { get; } = "https";

    public string Describe() => Scheme + "://";
}

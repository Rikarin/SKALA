// `init` is a setter the object initializer and the deserializer can both reach.
public sealed class Initialised {
    public string Scheme { get; init; } = "https";
}

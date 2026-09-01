public sealed class Options {
    public string Name { get; set; } = "";
}

public sealed class Consumer {
    // Nothing to leak: an initializer that throws drops an object with no resource behind it.
    public Options Build() => new() { Name = "main" };
}

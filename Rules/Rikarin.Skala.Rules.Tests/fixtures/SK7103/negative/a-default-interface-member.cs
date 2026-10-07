/// <summary>A greeter.</summary>
public interface IGreeter {
    /// <summary>Greets.</summary>
    /// <returns>The greeting.</returns>
    string Greet() => "hello";
}

/// <summary>A polite greeter.</summary>
public interface IPoliteGreeter : IGreeter {
    /// <inheritdoc />
    string IGreeter.Greet() => "good day";
}

/// <summary>A loud greeter.</summary>
public sealed class Loud : IGreeter {
    /// <inheritdoc />
    public string Greet() => "HELLO";
}

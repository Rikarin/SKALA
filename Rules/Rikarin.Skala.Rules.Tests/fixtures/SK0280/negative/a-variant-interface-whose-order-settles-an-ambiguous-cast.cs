using System;

// ⚠ #412's audit: `IOut<object>` is reachable through both `IOut<string>` and `IOut<Uri>`, and the
// runtime picks by declaration order. The restated `IOut<string>` is what puts it first; deleted, the
// cast reaches the `Uri` implementation.
public interface IOut<out T> {
    string Who();
}

public interface IStr : IOut<string> { }

public sealed class C : IOut<string>, IOut<Uri>, IStr {
    string IOut<string>.Who() => "string";

    string IOut<Uri>.Who() => "uri";
}

public static class Probe {
    public static string Run() {
        IOut<object> both = new C();
        return both.Who();
    }
}

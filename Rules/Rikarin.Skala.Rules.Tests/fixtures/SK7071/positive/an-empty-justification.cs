using System.Diagnostics.CodeAnalysis;

public sealed class Shim {
    [ExcludeFromCodeCoverage(Justification = "")]
    public void Call() { }

    [ExcludeFromCodeCoverage(Justification = "   ")]
    public void Retry() { }
}

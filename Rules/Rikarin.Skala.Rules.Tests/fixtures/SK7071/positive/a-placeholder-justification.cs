using System.Diagnostics.CodeAnalysis;

public sealed class Shim {
    [ExcludeFromCodeCoverage(Justification = "TODO")]
    public void Call() { }

    [ExcludeFromCodeCoverage(Justification = "N/A")]
    public void Retry() { }

    [ExcludeFromCodeCoverageAttribute(Justification = "none")]
    public void Close() { }
}

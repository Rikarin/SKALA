using System.Diagnostics.CodeAnalysis;

// ⚠ Present and not constant-foldable counts as written. The rule proves that the field is blank;
// it never claims to have read what is in it.
public sealed class Shim {
    const string Reason = "Thin P/Invoke wrapper, exercised by the integration suite.";

    [ExcludeFromCodeCoverage(Justification = Reason)]
    public void Call() { }

    [ExcludeFromCodeCoverage(Justification = nameof(Call) + " is a shim over the platform.")]
    public void Retry() { }
}

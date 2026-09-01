using System.Diagnostics;

// ⚠ The boundary fixture. `DebuggerDisplay` is AllowMultiple, so two applications are legal.
// SK2103 declines because the arguments differ; SK2102 reports the one string it can prove wrong.
// Total across SK2100-SK2103: one finding.
[DebuggerDisplay("{Label}")]
[DebuggerDisplay("{Missing}")]
sealed class Node {
    public string Label => "node";
}

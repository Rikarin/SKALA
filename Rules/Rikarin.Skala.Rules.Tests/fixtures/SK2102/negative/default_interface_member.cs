using System.Diagnostics;

interface ILabelled {
    string Label => "unlabelled";
}

// ⚠ The fixture the interface walk exists for, and the only one it alone can save. `Widget`
// declares no `Label` of its own — the member lives on the interface as a default implementation,
// so a lookup that stopped at the base chain would report correct code.
[DebuggerDisplay("{Label}")]
sealed class Widget : ILabelled { }

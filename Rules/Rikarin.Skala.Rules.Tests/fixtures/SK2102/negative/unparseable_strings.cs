using System.Diagnostics;

// Unbalanced, nested and stray braces, and a specifier this parser cannot account for, all
// withdraw the whole string rather than the one hole: a string it only half understands is one it
// has no standing to report on. Every identifier below is genuinely absent.
[DebuggerDisplay("{Missing")]
sealed class One { }

[DebuggerDisplay("{{Missing}")]
sealed class Two { }

[DebuggerDisplay("Missing}")]
sealed class Three { }

[DebuggerDisplay("{Missing,not a specifier}")]
sealed class Four { }

using System.Diagnostics;

// `Title` was renamed to `Name` and the display string was not. The debugger binds it at
// inspection time, so nothing said so.
[DebuggerDisplay("{Title,nq}")]
sealed class Book {
    public string Name { get; init; } = "";
}

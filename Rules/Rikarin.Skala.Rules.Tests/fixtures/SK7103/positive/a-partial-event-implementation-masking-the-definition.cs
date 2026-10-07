using System;

// ⚠ #397: `a-partial-implementation-masking-the-definition.cs` for a C# 14 partial event. The finding
// is the same and so is its reason — the implementation's comment replaces the definition's — but the
// event case was missing, and it was reported as overriding nothing instead.
/// <summary>A loader.</summary>
public sealed partial class Loader {
    EventHandler? loaded;

    /// <summary>Raised after everything is loaded.</summary>
    public partial event EventHandler? Loaded;

    /// <inheritdoc />
    public partial event EventHandler? Loaded {
        add => loaded += value;
        remove => loaded -= value;
    }

    /// <summary>Loads everything.</summary>
    public void Load() => loaded?.Invoke(this, EventArgs.Empty);
}

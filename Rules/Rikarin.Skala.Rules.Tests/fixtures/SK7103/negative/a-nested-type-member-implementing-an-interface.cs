using System;

/// <summary>The outer type.</summary>
public sealed class Outer {
    /// <summary>The inner type.</summary>
    public sealed class Inner : IDisposable {
        /// <inheritdoc />
        public void Dispose() { }
    }
}

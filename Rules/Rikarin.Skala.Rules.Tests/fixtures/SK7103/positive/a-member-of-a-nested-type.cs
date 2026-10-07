// The outer type is documented and that is not an inheritance source: measured, the nested
// member's `<inheritdoc/>` expands to nothing.
/// <summary>The outer type.</summary>
public sealed class Outer {
    /// <summary>The inner type.</summary>
    public sealed class Inner {
        /// <inheritdoc />
        public void Run() { }
    }
}

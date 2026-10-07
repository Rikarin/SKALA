// System.Enum implements IComparable, IFormattable and IConvertible; the declaration chose none of
// them, and Roslyn's expansion answers nothing for an enum.
/// <inheritdoc />
public enum Colour {
    /// <summary>Red.</summary>
    Red
}

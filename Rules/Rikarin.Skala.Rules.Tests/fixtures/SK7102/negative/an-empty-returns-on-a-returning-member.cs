// An empty returns section on a member that does return is a different defect, the empty-element
// family #390 declined, and not this rule's.
public static class Arithmetic {
    /// <summary>Adds one.</summary>
    /// <param name="value">The value.</param>
    /// <returns />
    public static int Next(int value) => value + 1;
}

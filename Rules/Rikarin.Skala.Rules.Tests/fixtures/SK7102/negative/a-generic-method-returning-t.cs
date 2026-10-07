// `T` is a value whatever it is instantiated with; `void` cannot be a type argument.
public static class Pick {
    /// <summary>Returns the value unchanged.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The value.</returns>
    public static T Same<T>(T value) => value;
}

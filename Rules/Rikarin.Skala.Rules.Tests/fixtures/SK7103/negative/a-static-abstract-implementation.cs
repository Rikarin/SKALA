/// <summary>Something that adds.</summary>
/// <typeparam name="T">The type.</typeparam>
public interface IAdds<T> where T : IAdds<T> {
    /// <summary>Adds two values.</summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The sum.</returns>
    static abstract T operator +(T left, T right);
}

/// <summary>A meter count.</summary>
public readonly record struct Meters(int Value) : IAdds<Meters> {
    /// <inheritdoc />
    public static Meters operator +(Meters left, Meters right) => new(left.Value + right.Value);
}

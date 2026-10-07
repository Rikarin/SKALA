public readonly struct Meters {
    readonly double value;

    /// <summary>Wraps a value.</summary>
    /// <param name="value">The length.</param>
    public Meters(double value) => this.value = value;

    /// <summary>Unwraps the length.</summary>
    /// <param name="meters">The wrapped length.</param>
    /// <returns>The length in meters.</returns>
    public static implicit operator double(Meters meters) => meters.value;
}

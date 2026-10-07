/// <summary>An amount.</summary>
/// <param name="Value">The value.</param>
public record Amount(decimal Value) {
    /// <inheritdoc />
    public virtual bool Equals(Amount? other) => other is not null && other.Value == Value;

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}

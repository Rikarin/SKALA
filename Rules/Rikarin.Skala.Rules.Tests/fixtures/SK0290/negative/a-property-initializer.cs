public sealed class PropertyInitializer {
    // A property initializer is an `EqualsValueClause` under the property declaration, not under a
    // variable declarator, so it is outside the whitelist even though the type beside it is written.
    public int? Limit { get; } = new int?(10);
}

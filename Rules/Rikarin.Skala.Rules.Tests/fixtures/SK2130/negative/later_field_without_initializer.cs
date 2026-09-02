// ⚠ A field with no initializer reads as `default` from above it *and* from below it, so the
// declaration order is not what makes the value wrong — and a finding blaming the order would be
// pointing at the wrong thing.
static class Config {
    public static readonly int Value = Later;

    public static readonly int Later;
}

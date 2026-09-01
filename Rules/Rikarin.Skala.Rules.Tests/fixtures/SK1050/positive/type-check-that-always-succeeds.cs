public sealed class Inspector {
    // `value` is already a `string`, so the only thing this test can discover is whether it is null.
    public bool Present(string? value) => value is object;
}

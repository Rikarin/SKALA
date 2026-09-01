// `object` does not convert to `string`, so the test discovers a type rather than a null.
public sealed class Inspector {
    public bool IsText(object value) => value is string;
}

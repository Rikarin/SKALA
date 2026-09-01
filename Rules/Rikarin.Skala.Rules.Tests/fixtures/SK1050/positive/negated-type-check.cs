public sealed class Inspector {
    public bool NotAString(object value) => !(value is string);
}

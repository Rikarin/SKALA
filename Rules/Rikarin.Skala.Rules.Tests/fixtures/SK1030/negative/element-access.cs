public sealed class Holder {
    readonly string?[] _values = new string?[4];

    public void Ensure(int index) {
        _values[index] = _values[index] ?? "fallback";
    }
}

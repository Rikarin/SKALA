public sealed class Holder {
    string? _first;
    string? _second;

    public void Ensure() {
        _first = _second ?? "fallback";
    }
}

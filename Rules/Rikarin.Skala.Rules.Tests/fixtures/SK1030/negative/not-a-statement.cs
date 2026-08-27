public sealed class Holder {
    string? _name;

    public string Ensure() => _name = _name ?? "anonymous";
}

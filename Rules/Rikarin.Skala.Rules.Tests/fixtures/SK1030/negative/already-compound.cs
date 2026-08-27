public sealed class Holder {
    string? _name;

    public void Ensure() {
        _name ??= "anonymous";
    }
}

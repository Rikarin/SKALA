public sealed class Holder {
    string? _name;

    public void Ensure() {
        this._name = this._name ?? "anonymous";
    }
}

public sealed class Holder {
    string? _name;

    public void Ensure() {
        _name = /* keep the long form, it is being debugged */ _name ?? "anonymous";
    }
}

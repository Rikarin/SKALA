sealed class Store {
    string? name = null;

    public void Rename(string value) => name = value;

    public string? Name => name;
}

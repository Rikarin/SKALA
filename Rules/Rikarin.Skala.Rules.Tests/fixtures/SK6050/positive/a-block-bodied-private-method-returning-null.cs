namespace Contoso.Design;

public sealed class Registry {
    public string? Resolve(string key) => Lookup(key);

    string? Lookup(string key) {
        return null;
    }
}

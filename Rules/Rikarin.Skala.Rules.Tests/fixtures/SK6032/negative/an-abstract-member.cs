namespace Contoso.Design;

public abstract class Importer {
    public string Name { get; init; } = string.Empty;

    public abstract int Import(string path);
}

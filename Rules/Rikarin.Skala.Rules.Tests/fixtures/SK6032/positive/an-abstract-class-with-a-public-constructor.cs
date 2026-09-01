namespace Contoso.Design;

public abstract class Ledger {
    public Ledger(string name) => Name = name;

    public string Name { get; }
}

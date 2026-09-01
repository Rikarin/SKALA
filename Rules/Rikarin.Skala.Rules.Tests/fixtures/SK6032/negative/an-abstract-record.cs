namespace Contoso.Design;

// A record's generated members are the derivation surface a class has to declare for itself, so
// there is no shape here the rule could read as an omission.
public abstract record Event(string Name);

public abstract record Command {
    public string Name { get; init; } = string.Empty;
}

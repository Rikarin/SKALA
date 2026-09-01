namespace Contoso.Design;

// The most ordinary base class in C#: state shared through a constructor only a derived type can
// call. It is arranged for derivation without declaring anything abstract.
public abstract class Shape {
    protected Shape(string name) => Name = name;

    public string Name { get; }
}
